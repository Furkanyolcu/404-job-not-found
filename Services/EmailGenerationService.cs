using _404JobNotFound.Data;
using _404JobNotFound.Models;
using _404JobNotFound.Services.Llm;
using Microsoft.EntityFrameworkCore;

namespace _404JobNotFound.Services;

public class EmailGenerationService
{
    private readonly AppDbContext _db;
    private readonly ILlmClient _llm;

    public EmailGenerationService(AppDbContext db, ILlmClient llm)
    {
        _db = db;
        _llm = llm;
    }

    public async Task<Application> GenerateAsync(Job job, CancellationToken ct = default)
    {
        var profile = await _db.UserProfiles
            .Include(p => p.Skills)
            .Include(p => p.ExperienceBullets)
            .Include(p => p.Resumes)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Önce profilini oluşturmalısın (Profil sekmesi).");

        var descLower = job.Description.ToLowerInvariant();
        var matchedBullets = profile.ExperienceBullets
            .Where(b => b.RelatedSkills.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(skill => descLower.Contains(skill.ToLowerInvariant())))
            .Select(b => b.Text)
            .ToList();

        // Hiç eşleşme yoksa yine de bütün bullet'ları ver — AI en azından genel/dürüst bir metin kurabilsin.
        if (matchedBullets.Count == 0)
            matchedBullets = profile.ExperienceBullets.Select(b => b.Text).ToList();

        string subject, body;
        bool aiAssisted;
        string? factCheckIssues = null;

        if (_llm.IsConfigured)
        {
            var draft = await _llm.GenerateEmailAsync(new EmailGenerationContext
            {
                CandidateName = profile.FullName,
                ProfileSummary = profile.Summary,
                JobTitle = job.Title,
                CompanyName = job.CompanyName,
                JobDescription = job.Description,
                GroundingBullets = matchedBullets
            }, ct);

            subject = draft.Subject;
            body = draft.Body;
            aiAssisted = true;

            var factCheck = await _llm.FactCheckEmailAsync(body, matchedBullets, ct);
            if (!factCheck.Passed)
                factCheckIssues = string.Join(" | ", factCheck.Issues);
        }
        else
        {
            (subject, body) = BuildFallbackTemplate(profile, job, matchedBullets);
            aiAssisted = false;
        }

        var defaultResume = profile.Resumes.FirstOrDefault(r => r.IsDefault) ?? profile.Resumes.FirstOrDefault();

        var application = await _db.Applications.FirstOrDefaultAsync(a => a.JobId == job.Id, ct);
        if (application is null)
        {
            application = new Application { JobId = job.Id };
            _db.Applications.Add(application);
        }

        application.Subject = subject;
        application.Body = body;
        application.AiAssisted = aiAssisted;
        application.FactCheckIssues = factCheckIssues;
        application.ResumeId = defaultResume?.Id;
        application.Status = factCheckIssues is null ? ApplicationStatus.Draft : ApplicationStatus.NeedsReview;
        application.CreatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return application;
    }

    /// <summary>Used only when no LLM key is configured — plain template merge, no AI phrasing, no risk of invented facts.</summary>
    private static (string Subject, string Body) BuildFallbackTemplate(UserProfile profile, Job job, List<string> bullets)
    {
        var subject = $"Başvuru: {job.Title} — {profile.FullName}";
        var bulletLines = string.Join("\n", bullets.Take(3).Select(b => $"- {b}"));
        var body = $"""
            Merhaba,

            {job.CompanyName} bünyesinde yayınladığınız "{job.Title}" pozisyonuna başvurmak istiyorum.

            İlgili deneyimlerimden bazıları:
            {bulletLines}

            Özgeçmişimi ekte bulabilirsiniz. Değerlendirmeniz için şimdiden teşekkür ederim.

            Saygılarımla,
            {profile.FullName}
            {profile.Email}
            """;
        return (subject, body);
    }
}
