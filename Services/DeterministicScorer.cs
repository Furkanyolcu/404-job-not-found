using System.Text.RegularExpressions;
using _404JobNotFound.Models;
using _404JobNotFound.Services.Llm;

namespace _404JobNotFound.Services;

/// <summary>
/// Keyword/regex based scorer that works with zero external dependencies or cost.
/// Used as the sole scorer when no LLM API key is configured, and as a cheap pre-filter
/// candidate for future use (not wired as a hard gate yet — see ScoringService).
/// </summary>
public static class DeterministicScorer
{
    private static readonly string[] SeniorityFlags =
    {
        "senior", "kıdemli", "lead developer", "takım lideri", "principal", "staff engineer"
    };

    public static JobScoreResult Score(string jobDescription, IReadOnlyList<Skill> profileSkills, int candidateYears)
    {
        var text = jobDescription;
        var breakdown = new List<ScoreBreakdownItem>();
        int metCount = 0, totalCount = 0;

        foreach (var skill in profileSkills)
        {
            var terms = new[] { skill.Name }
                .Concat(skill.Aliases.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(t => !string.IsNullOrWhiteSpace(t));

            if (terms.Any(t => ContainsWord(text, t)))
            {
                totalCount++;
                metCount++;
                breakdown.Add(new ScoreBreakdownItem
                {
                    Requirement = skill.Name,
                    Met = true,
                    Note = "İlanda geçiyor, profilde mevcut"
                });
            }
        }

        var expMatch = Regex.Match(text, @"(\d+)\+?\s*(yıl|yil|year)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (expMatch.Success && int.TryParse(expMatch.Groups[1].Value, out var requiredYears) && requiredYears > 0)
        {
            totalCount++;
            var ok = candidateYears >= requiredYears;
            if (ok) metCount++;
            breakdown.Add(new ScoreBreakdownItem
            {
                Requirement = $"{requiredYears}+ yıl deneyim gerekli",
                Met = ok,
                Note = ok ? "Uygun" : $"Adayın deneyimi ({candidateYears} yıl) yetersiz olabilir"
            });
        }

        if (SeniorityFlags.Any(f => text.Contains(f, StringComparison.OrdinalIgnoreCase)))
        {
            totalCount++;
            breakdown.Add(new ScoreBreakdownItem
            {
                Requirement = "Kıdem seviyesi (Senior/Lead vb.)",
                Met = false,
                Note = "İlan kıdemli bir seviye istiyor gibi görünüyor — junior profil için uygun olmayabilir"
            });
        }

        var score = totalCount == 0 ? 30 : (int)Math.Round(100.0 * metCount / totalCount);
        return new JobScoreResult { Score = score, Breakdown = breakdown };
    }

    private static bool ContainsWord(string text, string term)
        => Regex.IsMatch(text, $@"(?<![\w]){Regex.Escape(term)}(?![\w])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
