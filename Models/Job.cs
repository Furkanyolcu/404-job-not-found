namespace _404JobNotFound.Models;

public enum WorkMode
{
    Unknown,
    Remote,
    Hybrid,
    OnSite
}

public enum JobStatus
{
    New,
    Scored,
    Rejected,
    Blacklisted,
    Applied
}

public enum JobSourceType
{
    Manual,
    Api
}

public class Job
{
    public int Id { get; set; }

    public string Title { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string? CompanyWebsite { get; set; }
    public string Description { get; set; } = "";
    public string? Location { get; set; }
    public WorkMode WorkMode { get; set; } = WorkMode.Unknown;
    public string? ApplyUrl { get; set; }
    public string? ContactEmail { get; set; }
    /// <summary>Where the contact email came from, e.g. "İlanda doğrudan yazılıydı", "Kariyer sayfası", "Manuel girildi".</summary>
    public string? ContactSource { get; set; }

    public JobSourceType SourceType { get; set; } = JobSourceType.Manual;
    public string? ExternalId { get; set; }
    public string? SourceName { get; set; }

    /// <summary>Hash of Title+CompanyName+Description, used for duplicate detection across sources.</summary>
    public string ContentHash { get; set; } = "";

    public JobStatus Status { get; set; } = JobStatus.New;
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
    public DateTime? PostedAt { get; set; }

    public JobScore? Score { get; set; }
    public Application? Application { get; set; }
}
