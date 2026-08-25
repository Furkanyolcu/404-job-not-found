using System.Security.Cryptography;
using System.Text;
using _404JobNotFound.Data;
using _404JobNotFound.Models;
using _404JobNotFound.Services.Llm;
using Microsoft.EntityFrameworkCore;

namespace _404JobNotFound.Services;

public class DuplicateJobException : Exception
{
    public int ExistingJobId { get; }
    public DuplicateJobException(int existingJobId) : base("Bu ilan (aynı başlık/şirket/açıklama ile) zaten kayıtlı.")
        => ExistingJobId = existingJobId;
}

public class JobIntakeService
{
    private readonly AppDbContext _db;
    private readonly ILlmClient _llm;

    public JobIntakeService(AppDbContext db, ILlmClient llm)
    {
        _db = db;
        _llm = llm;
    }

    /// <summary>Paste raw job posting text — if an LLM key is configured it extracts structured fields,
    /// otherwise the whole text is stored as the description and title/company must be filled in manually afterwards.</summary>
    public async Task<Job> IntakeFromRawTextAsync(string rawText, CancellationToken ct = default)
    {
        string title, companyName, description;
        string? location = null, applyUrl = null, contactEmail = null;
        var workMode = WorkMode.Unknown;

        if (_llm.IsConfigured)
        {
            var extracted = await _llm.ExtractJobAsync(rawText, ct);
            title = extracted.Title;
            companyName = extracted.CompanyName;
            description = extracted.Description;
            location = extracted.Location;
            applyUrl = extracted.ApplyUrl;
            contactEmail = extracted.ContactEmail;
            Enum.TryParse(extracted.WorkMode, true, out workMode);
        }
        else
        {
            title = "(Başlık girilmedi — düzenle)";
            companyName = "(Şirket girilmedi — düzenle)";
            description = rawText;
        }

        return await CreateAsync(title, companyName, description, location, workMode, applyUrl, contactEmail,
            JobSourceType.Manual, null, null, ct);
    }

    public async Task<Job> CreateManualAsync(
        string title, string companyName, string description, string? location,
        WorkMode workMode, string? applyUrl, string? contactEmail, CancellationToken ct = default)
        => await CreateAsync(title, companyName, description, location, workMode, applyUrl, contactEmail,
            JobSourceType.Manual, null, null, ct);

    private async Task<Job> CreateAsync(
        string title, string companyName, string description, string? location, WorkMode workMode,
        string? applyUrl, string? contactEmail, JobSourceType sourceType, string? sourceName, string? externalId,
        CancellationToken ct)
    {
        var hash = ComputeHash(title, companyName, description);
        var duplicate = await _db.Jobs.FirstOrDefaultAsync(j => j.ContentHash == hash, ct);
        if (duplicate is not null)
            throw new DuplicateJobException(duplicate.Id);

        var job = new Job
        {
            Title = title,
            CompanyName = companyName,
            Description = description,
            Location = location,
            WorkMode = workMode,
            ApplyUrl = applyUrl,
            ContactEmail = contactEmail,
            ContactSource = contactEmail is not null ? "İlan metninde doğrudan yazılıydı" : null,
            ContentHash = hash,
            SourceType = sourceType,
            SourceName = sourceName,
            ExternalId = externalId,
            Status = JobStatus.New,
            DiscoveredAt = DateTime.UtcNow
        };

        _db.Jobs.Add(job);
        await _db.SaveChangesAsync(ct);
        return job;
    }

    private static string ComputeHash(string title, string companyName, string description)
    {
        var normalized = $"{title.Trim().ToLowerInvariant()}|{companyName.Trim().ToLowerInvariant()}|{description.Trim().ToLowerInvariant()}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }
}
