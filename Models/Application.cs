namespace _404JobNotFound.Models;

public enum ApplicationStatus
{
    Draft,
    NeedsReview,
    Approved,
    Sent,
    Failed,
    Rejected
}

/// <summary>
/// One row per Job — the generated (and eventually sent) application email plus its lifecycle state.
/// A Job can have at most one Application (enforced by a unique index on JobId).
/// </summary>
public class Application
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public Job? Job { get; set; }

    public int? ResumeId { get; set; }
    public Resume? Resume { get; set; }

    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public bool AiAssisted { get; set; }
    /// <summary>Issues raised by the post-generation fact-check pass, if any (only set when AI-assisted).</summary>
    public string? FactCheckIssues { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? ErrorMessage { get; set; }
}
