namespace _404JobNotFound.Services.Llm;

/// <summary>
/// Abstraction over the AI provider used for job analysis and email drafting.
/// Swap in a different implementation (OpenAI, etc.) without touching callers.
/// </summary>
public interface ILlmClient
{
    /// <summary>False when no API key is configured — callers should fall back to deterministic-only behavior.</summary>
    bool IsConfigured { get; }

    Task<JobExtractResult> ExtractJobAsync(string rawText, CancellationToken ct = default);

    Task<JobScoreResult> ScoreJobAsync(string jobTitle, string jobDescription, ProfileContext profile, CancellationToken ct = default);

    Task<EmailDraftResult> GenerateEmailAsync(EmailGenerationContext context, CancellationToken ct = default);

    Task<FactCheckResult> FactCheckEmailAsync(string emailBody, IReadOnlyList<string> groundingBullets, CancellationToken ct = default);
}
