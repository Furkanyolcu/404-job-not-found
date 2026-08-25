namespace _404JobNotFound.Models;

public class JobScore
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public Job? Job { get; set; }

    public int Score { get; set; }
    /// <summary>True when the LLM produced this score; false means deterministic keyword-only fallback (no API key configured).</summary>
    public bool AiAssisted { get; set; }
    /// <summary>JSON array of {requirement, met, note} — the human-readable "+ C# gerekli -> mevcut" breakdown.</summary>
    public string BreakdownJson { get; set; } = "[]";
    public string? ModelUsed { get; set; }
    public DateTime ScoredAt { get; set; } = DateTime.UtcNow;
}
