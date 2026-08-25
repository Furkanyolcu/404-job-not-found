using System.Text.Json;
using _404JobNotFound.Data;
using _404JobNotFound.Models;
using _404JobNotFound.Services;
using _404JobNotFound.Services.Llm;
using Microsoft.EntityFrameworkCore;

namespace _404JobNotFound.Endpoints;

public record JobListItemDto(int Id, string Title, string CompanyName, string? Location, string WorkMode,
    string Status, int? Score, bool? AiAssisted, DateTime DiscoveredAt, bool HasContactEmail, string? ApplicationStatus);

public record ScoreBreakdownDto(string Requirement, bool Met, string Note);

public record JobDetailDto(int Id, string Title, string CompanyName, string? CompanyWebsite, string Description,
    string? Location, string WorkMode, string? ApplyUrl, string? ContactEmail, string? ContactSource,
    string Status, DateTime DiscoveredAt, int? Score, bool? AiAssisted, List<ScoreBreakdownDto>? Breakdown,
    ApplicationDto? Application);

public record RawTextIntakeRequest(string RawText);

public record ManualJobRequest(string Title, string CompanyName, string Description, string? Location,
    string WorkMode, string? ApplyUrl, string? ContactEmail);

public record ContactUpdateRequest(string? ContactEmail, string? ContactSource);

public static class JobEndpoints
{
    public static void MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jobs");

        group.MapGet("/", async (AppDbContext db) =>
        {
            var jobs = await db.Jobs
                .Include(j => j.Score)
                .Include(j => j.Application)
                .OrderByDescending(j => j.Score != null ? j.Score.Score : -1)
                .ThenByDescending(j => j.DiscoveredAt)
                .Select(j => new JobListItemDto(j.Id, j.Title, j.CompanyName, j.Location, j.WorkMode.ToString(),
                    j.Status.ToString(), j.Score != null ? j.Score.Score : (int?)null,
                    j.Score != null ? j.Score.AiAssisted : (bool?)null, j.DiscoveredAt,
                    j.ContactEmail != null, j.Application != null ? j.Application.Status.ToString() : null))
                .ToListAsync();
            return Results.Ok(jobs);
        });

        group.MapGet("/{id:int}", async (int id, AppDbContext db) =>
        {
            var job = await db.Jobs
                .Include(j => j.Score)
                .Include(j => j.Application)
                .FirstOrDefaultAsync(j => j.Id == id);
            if (job is null) return Results.NotFound();
            return Results.Ok(ToDetailDto(job));
        });

        group.MapPost("/intake", async (RawTextIntakeRequest req, JobIntakeService intake, ScoringService scoring, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.RawText))
                return Results.BadRequest(new { error = "İlan metni boş olamaz." });
            try
            {
                var job = await intake.IntakeFromRawTextAsync(req.RawText);
                await scoring.ScoreJobAsync(job);
                var full = await db.Jobs.Include(j => j.Score).Include(j => j.Application).FirstAsync(j => j.Id == job.Id);
                return Results.Ok(ToDetailDto(full));
            }
            catch (DuplicateJobException ex)
            {
                return Results.Conflict(new { error = ex.Message, existingJobId = ex.ExistingJobId });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/manual", async (ManualJobRequest req, JobIntakeService intake, ScoringService scoring, AppDbContext db) =>
        {
            Enum.TryParse<WorkMode>(req.WorkMode, true, out var workMode);
            try
            {
                var job = await intake.CreateManualAsync(req.Title, req.CompanyName, req.Description, req.Location,
                    workMode, req.ApplyUrl, req.ContactEmail);
                await scoring.ScoreJobAsync(job);
                var full = await db.Jobs.Include(j => j.Score).Include(j => j.Application).FirstAsync(j => j.Id == job.Id);
                return Results.Ok(ToDetailDto(full));
            }
            catch (DuplicateJobException ex)
            {
                return Results.Conflict(new { error = ex.Message, existingJobId = ex.ExistingJobId });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/{id:int}/rescore", async (int id, AppDbContext db, ScoringService scoring) =>
        {
            var job = await db.Jobs.FindAsync(id);
            if (job is null) return Results.NotFound();
            await scoring.ScoreJobAsync(job);
            var full = await db.Jobs.Include(j => j.Score).Include(j => j.Application).FirstAsync(j => j.Id == id);
            return Results.Ok(ToDetailDto(full));
        });

        group.MapPatch("/{id:int}/contact", async (int id, ContactUpdateRequest req, AppDbContext db) =>
        {
            var job = await db.Jobs.FindAsync(id);
            if (job is null) return Results.NotFound();
            job.ContactEmail = req.ContactEmail;
            job.ContactSource = req.ContactSource ?? "Manuel girildi";
            await db.SaveChangesAsync();
            return Results.Ok(new { ok = true });
        });

        group.MapPost("/{id:int}/reject", async (int id, AppDbContext db) =>
        {
            var job = await db.Jobs.FindAsync(id);
            if (job is null) return Results.NotFound();
            job.Status = JobStatus.Rejected;
            await db.SaveChangesAsync();
            return Results.Ok(new { ok = true });
        });

        group.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var job = await db.Jobs.FindAsync(id);
            if (job is null) return Results.NotFound();
            db.Jobs.Remove(job);
            await db.SaveChangesAsync();
            return Results.Ok(new { ok = true });
        });
    }

    public static JobDetailDto ToDetailDto(Job j)
    {
        List<ScoreBreakdownDto>? breakdown = null;
        if (j.Score is not null)
        {
            var parsed = JsonSerializer.Deserialize<List<ScoreBreakdownItem>>(j.Score.BreakdownJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            breakdown = parsed?.Select(b => new ScoreBreakdownDto(b.Requirement, b.Met, b.Note)).ToList();
        }

        return new JobDetailDto(j.Id, j.Title, j.CompanyName, j.CompanyWebsite, j.Description, j.Location,
            j.WorkMode.ToString(), j.ApplyUrl, j.ContactEmail, j.ContactSource, j.Status.ToString(), j.DiscoveredAt,
            j.Score?.Score, j.Score?.AiAssisted, breakdown,
            j.Application is null ? null : ApplicationEndpoints.ToDto(j.Application));
    }
}
