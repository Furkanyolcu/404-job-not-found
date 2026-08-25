using _404JobNotFound.Data;
using _404JobNotFound.Models;
using _404JobNotFound.Services;
using Microsoft.EntityFrameworkCore;

namespace _404JobNotFound.Endpoints;

public record ApplicationDto(int Id, int JobId, string Subject, string Body, bool AiAssisted,
    string? FactCheckIssues, string Status, DateTime CreatedAt, DateTime? ApprovedAt, DateTime? SentAt,
    string? ErrorMessage, int? ResumeId);

public record ApplicationEditRequest(string Subject, string Body);

public record ApplicationHistoryItemDto(int Id, int JobId, string JobTitle, string CompanyName, string Status,
    DateTime? SentAt, string? ErrorMessage);

public static class ApplicationEndpoints
{
    public static void MapApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/jobs/{jobId:int}/generate-email", async (int jobId, AppDbContext db, EmailGenerationService gen) =>
        {
            var job = await db.Jobs.FindAsync(jobId);
            if (job is null) return Results.NotFound();
            try
            {
                var application = await gen.GenerateAsync(job);
                return Results.Ok(ToDto(application));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        var group = app.MapGroup("/api/applications");

        group.MapGet("/", async (AppDbContext db) =>
        {
            var items = await db.Applications
                .Include(a => a.Job)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new ApplicationHistoryItemDto(a.Id, a.JobId, a.Job!.Title, a.Job.CompanyName,
                    a.Status.ToString(), a.SentAt, a.ErrorMessage))
                .ToListAsync();
            return Results.Ok(items);
        });

        group.MapPut("/{id:int}", async (int id, ApplicationEditRequest req, AppDbContext db) =>
        {
            var application = await db.Applications.FindAsync(id);
            if (application is null) return Results.NotFound();
            if (application.Status is ApplicationStatus.Sent)
                return Results.BadRequest(new { error = "Gönderilmiş bir başvuru düzenlenemez." });

            application.Subject = req.Subject;
            application.Body = req.Body;
            application.Status = ApplicationStatus.Draft;
            application.FactCheckIssues = null;
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(application));
        });

        group.MapPost("/{id:int}/approve", async (int id, AppDbContext db) =>
        {
            var application = await db.Applications.FindAsync(id);
            if (application is null) return Results.NotFound();
            if (application.Status is not (ApplicationStatus.Draft or ApplicationStatus.NeedsReview))
                return Results.BadRequest(new { error = "Sadece taslak/gözden geçirme durumundaki başvurular onaylanabilir." });

            application.Status = ApplicationStatus.Approved;
            application.ApprovedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(application));
        });

        group.MapPost("/{id:int}/reject", async (int id, AppDbContext db) =>
        {
            var application = await db.Applications.FindAsync(id);
            if (application is null) return Results.NotFound();
            application.Status = ApplicationStatus.Rejected;
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(application));
        });

        group.MapPost("/{id:int}/send", async (int id, EmailSendService sender) =>
        {
            try
            {
                var application = await sender.SendAsync(id);
                return Results.Ok(ToDto(application));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }

    public static ApplicationDto ToDto(Application a) => new(a.Id, a.JobId, a.Subject, a.Body, a.AiAssisted,
        a.FactCheckIssues, a.Status.ToString(), a.CreatedAt, a.ApprovedAt, a.SentAt, a.ErrorMessage, a.ResumeId);
}
