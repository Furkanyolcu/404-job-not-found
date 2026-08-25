using _404JobNotFound.Data;
using _404JobNotFound.Models;
using Microsoft.EntityFrameworkCore;

namespace _404JobNotFound.Endpoints;

public record BlacklistDto(int Id, string Type, string Value, string? Reason, DateTime CreatedAt);
public record BlacklistCreateRequest(string Type, string Value, string? Reason);

public static class BlacklistEndpoints
{
    public static void MapBlacklistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/blacklist");

        group.MapGet("/", async (AppDbContext db) =>
        {
            var items = await db.BlacklistEntries.OrderByDescending(b => b.CreatedAt)
                .Select(b => new BlacklistDto(b.Id, b.Type.ToString(), b.Value, b.Reason, b.CreatedAt))
                .ToListAsync();
            return Results.Ok(items);
        });

        group.MapPost("/", async (BlacklistCreateRequest req, AppDbContext db) =>
        {
            if (!Enum.TryParse<BlacklistType>(req.Type, true, out var type))
                return Results.BadRequest(new { error = "Geçersiz tip. CompanyName, Domain veya Email olmalı." });

            db.BlacklistEntries.Add(new BlacklistEntry { Type = type, Value = req.Value, Reason = req.Reason });
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Results.Conflict(new { error = "Bu değer zaten kara listede." });
            }
            return Results.Ok(new { ok = true });
        });

        group.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var entry = await db.BlacklistEntries.FindAsync(id);
            if (entry is null) return Results.NotFound();
            db.BlacklistEntries.Remove(entry);
            await db.SaveChangesAsync();
            return Results.Ok(new { ok = true });
        });
    }
}
