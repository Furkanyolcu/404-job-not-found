namespace _404JobNotFound.Services.Llm;

public class ProfileContext
{
    public string FullName { get; set; } = "";
    public string Summary { get; set; } = "";
    public int YearsOfExperience { get; set; }
    public List<string> Skills { get; set; } = new();
}

public class JobExtractResult
{
    public string Title { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string? Location { get; set; }
    public string? WorkMode { get; set; } // "Remote" | "Hybrid" | "OnSite" | "Unknown"
    public string Description { get; set; } = "";
    public string? ApplyUrl { get; set; }
    public string? ContactEmail { get; set; }
}

public class ScoreBreakdownItem
{
    public string Requirement { get; set; } = "";
    public bool Met { get; set; }
    public string Note { get; set; } = "";
}

public class JobScoreResult
{
    public int Score { get; set; }
    public List<ScoreBreakdownItem> Breakdown { get; set; } = new();
}

public class EmailGenerationContext
{
    public string CandidateName { get; set; } = "";
    public string ProfileSummary { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string JobDescription { get; set; } = "";
    /// <summary>Only these bullets may be referenced as candidate facts — nothing outside this list.</summary>
    public List<string> GroundingBullets { get; set; } = new();
}

public class EmailDraftResult
{
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
}

public class FactCheckResult
{
    public bool Passed { get; set; }
    public List<string> Issues { get; set; } = new();
}
