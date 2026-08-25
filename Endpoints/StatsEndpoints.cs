using _404JobNotFound.Data;
using _404JobNotFound.Models;
using Microsoft.EntityFrameworkCore;

namespace _404JobNotFound.Endpoints;

public record StatsDto(int JobsFoundToday, int SuitableCount, int PendingApprovals, int SentCount,
    int FailedCount, int SentToday, bool LlmConfigured);

public static class StatsEndpoints
{
    public static void MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/stats", async (AppDbContext db, Services.Llm.ILlmClient llm) =>
        {
            var todayStart = DateTime.UtcNow.Date;

            var jobsToday = await db.Jobs.CountAsync(j => j.DiscoveredAt >= todayStart);
            var suitable = await db.Jobs.Include(j => j.Score)
                .CountAsync(j => j.Score != null && j.Score.Score >= 70);
            var pending = await db.Applications.CountAsync(a =>
                a.Status == ApplicationStatus.Draft || a.Status == ApplicationStatus.NeedsReview);
            var sent = await db.Applications.CountAsync(a => a.Status == ApplicationStatus.Sent);
            var failed = await db.Applications.CountAsync(a => a.Status == ApplicationStatus.Failed);
            var sentToday = await db.Applications.CountAsync(a => a.Status == ApplicationStatus.Sent && a.SentAt >= todayStart);

            return Results.Ok(new StatsDto(jobsToday, suitable, pending, sent, failed, sentToday, llm.IsConfigured));
        });
    }
}
