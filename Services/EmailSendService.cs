using _404JobNotFound.Data;
using _404JobNotFound.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace _404JobNotFound.Services;

/// <summary>
/// Sends an already-approved Application by email. Every send is triggered manually by the user
/// (no scheduler, no auto-send tier) and passes through blacklist / duplicate / daily-limit checks first.
/// </summary>
public class EmailSendService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public EmailSendService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<Application> SendAsync(int applicationId, CancellationToken ct = default)
    {
        var app = await _db.Applications
            .Include(a => a.Job)
            .Include(a => a.Resume)
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct)
            ?? throw new InvalidOperationException("Başvuru bulunamadı.");

        if (app.Status != ApplicationStatus.Approved)
            throw new InvalidOperationException("Sadece 'Approved' durumundaki başvurular gönderilebilir.");

        var job = app.Job ?? throw new InvalidOperationException("İlan bulunamadı.");
        if (string.IsNullOrWhiteSpace(job.ContactEmail))
            throw new InvalidOperationException("Bu ilan için iletişim e-postası girilmemiş — önce ilan detayından ekle.");

        await EnsureNotBlacklistedAsync(job, ct);
        await EnsureNoDuplicateRecentSendAsync(job.ContactEmail, ct);
        await EnsureUnderDailyLimitAsync(ct);

        var smtpUser = _config["GMAIL_ADDRESS"];
        var smtpPass = _config["GMAIL_APP_PASSWORD"];
        if (string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPass))
            throw new InvalidOperationException("GMAIL_ADDRESS / GMAIL_APP_PASSWORD .env içinde tanımlı değil.");

        try
        {
            using var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(smtpUser));
            message.To.Add(MailboxAddress.Parse(job.ContactEmail));
            message.Subject = app.Subject;

            var builder = new BodyBuilder { TextBody = app.Body };
            if (app.Resume is not null && File.Exists(app.Resume.StoragePath))
                await builder.Attachments.AddAsync(app.Resume.StoragePath, ct);

            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls, ct);
            await client.AuthenticateAsync(smtpUser, smtpPass, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            app.Status = ApplicationStatus.Sent;
            app.SentAt = DateTime.UtcNow;
            app.ErrorMessage = null;
            job.Status = JobStatus.Applied;
        }
        catch (Exception ex)
        {
            app.Status = ApplicationStatus.Failed;
            app.ErrorMessage = ex.Message;
            await _db.SaveChangesAsync(ct);
            throw;
        }

        await _db.SaveChangesAsync(ct);
        return app;
    }

    private async Task EnsureNotBlacklistedAsync(Job job, CancellationToken ct)
    {
        var blacklist = await _db.BlacklistEntries.ToListAsync(ct);

        if (blacklist.Any(b => b.Type == BlacklistType.CompanyName
                && string.Equals(b.Value, job.CompanyName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Şirket kara listede: {job.CompanyName}");

        if (blacklist.Any(b => b.Type == BlacklistType.Email
                && string.Equals(b.Value, job.ContactEmail, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"E-posta adresi kara listede: {job.ContactEmail}");

        var domain = job.ContactEmail?.Split('@').Skip(1).FirstOrDefault();
        if (domain is not null && blacklist.Any(b => b.Type == BlacklistType.Domain
                && string.Equals(b.Value, domain, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Domain kara listede: {domain}");
    }

    private async Task EnsureNoDuplicateRecentSendAsync(string contactEmail, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-60);
        var alreadySent = await _db.Applications
            .Include(a => a.Job)
            .AnyAsync(a => a.Status == ApplicationStatus.Sent && a.SentAt > cutoff
                           && a.Job!.ContactEmail == contactEmail, ct);
        if (alreadySent)
            throw new InvalidOperationException($"Bu e-posta adresine ({contactEmail}) son 60 gün içinde zaten başvuru gönderilmiş.");
    }

    private async Task EnsureUnderDailyLimitAsync(CancellationToken ct)
    {
        var maxDaily = int.TryParse(_config["MAX_DAILY_SENDS"], out var v) ? v : 15;
        var todayStart = DateTime.UtcNow.Date;
        var sentToday = await _db.Applications.CountAsync(a => a.Status == ApplicationStatus.Sent && a.SentAt >= todayStart, ct);
        if (sentToday >= maxDaily)
            throw new InvalidOperationException($"Günlük gönderim limitine ulaşıldı ({sentToday}/{maxDaily}). Yarın tekrar dene ya da MAX_DAILY_SENDS'i .env'de artır.");
    }

    public async Task<int> GetSentTodayCountAsync(CancellationToken ct = default)
    {
        var todayStart = DateTime.UtcNow.Date;
        return await _db.Applications.CountAsync(a => a.Status == ApplicationStatus.Sent && a.SentAt >= todayStart, ct);
    }
}
