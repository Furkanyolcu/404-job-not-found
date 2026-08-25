using System.Text.Json;
using _404JobNotFound.Data;
using _404JobNotFound.Models;
using _404JobNotFound.Services.Llm;
using Microsoft.EntityFrameworkCore;

namespace _404JobNotFound.Services;

public class ScoringService
{
    private readonly AppDbContext _db;
    private readonly ILlmClient _llm;

    public ScoringService(AppDbContext db, ILlmClient llm)
    {
        _db = db;
        _llm = llm;
    }

    public async Task<JobScore> ScoreJobAsync(Job job, CancellationToken ct = default)
    {
        var profile = await _db.UserProfiles
            .Include(p => p.Skills)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Önce profilini oluşturmalısın (Profil sekmesi).");

        JobScoreResult result;
        bool aiAssisted;

        if (_llm.IsConfigured)
        {
            var context = new ProfileContext
            {
                FullName = profile.FullName,
                Summary = profile.Summary,
                YearsOfExperience = profile.YearsOfExperience,
                Skills = profile.Skills.Select(s => s.Name).ToList()
            };
            result = await _llm.ScoreJobAsync(job.Title, job.Description, context, ct);
            aiAssisted = true;
        }
        else
        {
            result = DeterministicScorer.Score(job.Description, profile.Skills, profile.YearsOfExperience);
            aiAssisted = false;
        }

        var existing = await _db.JobScores.FirstOrDefaultAsync(s => s.JobId == job.Id, ct);
        if (existing is null)
        {
            existing = new JobScore { JobId = job.Id };
            _db.JobScores.Add(existing);
        }

        existing.Score = result.Score;
        existing.AiAssisted = aiAssisted;
        existing.BreakdownJson = JsonSerializer.Serialize(result.Breakdown);
        existing.ModelUsed = aiAssisted ? "anthropic" : "deterministic";
        existing.ScoredAt = DateTime.UtcNow;

        job.Status = JobStatus.Scored;

        await _db.SaveChangesAsync(ct);
        return existing;
    }
}
