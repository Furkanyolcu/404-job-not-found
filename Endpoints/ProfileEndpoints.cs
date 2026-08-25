using _404JobNotFound.Data;
using _404JobNotFound.Models;
using Microsoft.EntityFrameworkCore;

namespace _404JobNotFound.Endpoints;

public record SkillDto(int Id, string Name, string Aliases, string Category, string Proficiency);
public record BulletDto(int Id, string Text, string RelatedSkills);
public record ResumeDto(int Id, string FileName, string TargetRoleType, bool IsDefault);

public record ProfileDto(int Id, string FullName, string Email, string? Phone, string Summary,
    int YearsOfExperience, string? LinkedInUrl, string? GithubUrl, string? PortfolioUrl,
    List<SkillDto> Skills, List<BulletDto> Bullets, List<ResumeDto> Resumes);

public record ProfileUpdateRequest(string FullName, string Email, string? Phone, string Summary,
    int YearsOfExperience, string? LinkedInUrl, string? GithubUrl, string? PortfolioUrl);

public record SkillCreateRequest(string Name, string Aliases, string Category, string Proficiency);
public record BulletCreateRequest(string Text, string RelatedSkills);

public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profile");

        group.MapGet("/", async (AppDbContext db) => Results.Ok(await GetOrCreateDto(db)));

        group.MapPut("/", async (ProfileUpdateRequest req, AppDbContext db) =>
        {
            var profile = await GetOrCreateEntity(db);
            profile.FullName = req.FullName;
            profile.Email = req.Email;
            profile.Phone = req.Phone;
            profile.Summary = req.Summary;
            profile.YearsOfExperience = req.YearsOfExperience;
            profile.LinkedInUrl = req.LinkedInUrl;
            profile.GithubUrl = req.GithubUrl;
            profile.PortfolioUrl = req.PortfolioUrl;
            await db.SaveChangesAsync();
            return Results.Ok(await GetOrCreateDto(db));
        });

        group.MapPost("/skills", async (SkillCreateRequest req, AppDbContext db) =>
        {
            var profile = await GetOrCreateEntity(db);
            Enum.TryParse<SkillProficiency>(req.Proficiency, true, out var prof);
            db.Skills.Add(new Skill
            {
                UserProfileId = profile.Id,
                Name = req.Name,
                Aliases = req.Aliases,
                Category = req.Category,
                Proficiency = prof
            });
            await db.SaveChangesAsync();
            return Results.Ok(await GetOrCreateDto(db));
        });

        group.MapDelete("/skills/{id:int}", async (int id, AppDbContext db) =>
        {
            var skill = await db.Skills.FindAsync(id);
            if (skill is null) return Results.NotFound();
            db.Skills.Remove(skill);
            await db.SaveChangesAsync();
            return Results.Ok(await GetOrCreateDto(db));
        });

        group.MapPost("/bullets", async (BulletCreateRequest req, AppDbContext db) =>
        {
            var profile = await GetOrCreateEntity(db);
            db.ExperienceBullets.Add(new ExperienceBullet
            {
                UserProfileId = profile.Id,
                Text = req.Text,
                RelatedSkills = req.RelatedSkills
            });
            await db.SaveChangesAsync();
            return Results.Ok(await GetOrCreateDto(db));
        });

        group.MapDelete("/bullets/{id:int}", async (int id, AppDbContext db) =>
        {
            var bullet = await db.ExperienceBullets.FindAsync(id);
            if (bullet is null) return Results.NotFound();
            db.ExperienceBullets.Remove(bullet);
            await db.SaveChangesAsync();
            return Results.Ok(await GetOrCreateDto(db));
        });

        group.MapPost("/resume", async (HttpContext ctx, AppDbContext db, IWebHostEnvironment env) =>
        {
            if (!ctx.Request.HasFormContentType) return Results.BadRequest(new { error = "multipart/form-data bekleniyor." });
            var form = await ctx.Request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0) return Results.BadRequest(new { error = "Dosya bulunamadı." });

            var targetRoleType = form["targetRoleType"].FirstOrDefault() ?? "General";
            var isDefault = bool.TryParse(form["isDefault"].FirstOrDefault(), out var d) && d;

            var profile = await GetOrCreateEntity(db);
            var resumesDir = Path.Combine(env.ContentRootPath, "App_Data", "Resumes");
            Directory.CreateDirectory(resumesDir);
            var safeName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var fullPath = Path.Combine(resumesDir, safeName);

            await using (var stream = File.Create(fullPath))
                await file.CopyToAsync(stream);

            if (isDefault)
                foreach (var r in await db.Resumes.Where(r => r.UserProfileId == profile.Id).ToListAsync())
                    r.IsDefault = false;

            db.Resumes.Add(new Resume
            {
                UserProfileId = profile.Id,
                FileName = file.FileName,
                StoragePath = fullPath,
                TargetRoleType = targetRoleType,
                IsDefault = isDefault
            });
            await db.SaveChangesAsync();
            return Results.Ok(await GetOrCreateDto(db));
        });

        group.MapPost("/resume/{id:int}/set-default", async (int id, AppDbContext db) =>
        {
            var resume = await db.Resumes.FindAsync(id);
            if (resume is null) return Results.NotFound();
            foreach (var r in await db.Resumes.Where(r => r.UserProfileId == resume.UserProfileId).ToListAsync())
                r.IsDefault = r.Id == id;
            await db.SaveChangesAsync();
            return Results.Ok(await GetOrCreateDto(db));
        });

        group.MapDelete("/resume/{id:int}", async (int id, AppDbContext db) =>
        {
            var resume = await db.Resumes.FindAsync(id);
            if (resume is null) return Results.NotFound();
            db.Resumes.Remove(resume);
            await db.SaveChangesAsync();
            if (File.Exists(resume.StoragePath)) File.Delete(resume.StoragePath);
            return Results.Ok(await GetOrCreateDto(db));
        });
    }

    private static async Task<UserProfile> GetOrCreateEntity(AppDbContext db)
    {
        var profile = await db.UserProfiles.FirstOrDefaultAsync();
        if (profile is not null) return profile;

        profile = new UserProfile { FullName = "", Email = "", Summary = "" };
        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync();
        return profile;
    }

    private static async Task<ProfileDto> GetOrCreateDto(AppDbContext db)
    {
        var profile = await db.UserProfiles
            .Include(p => p.Skills)
            .Include(p => p.ExperienceBullets)
            .Include(p => p.Resumes)
            .FirstOrDefaultAsync();

        if (profile is null)
        {
            profile = await GetOrCreateEntity(db);
            profile = await db.UserProfiles
                .Include(p => p.Skills)
                .Include(p => p.ExperienceBullets)
                .Include(p => p.Resumes)
                .FirstAsync(p => p.Id == profile.Id);
        }

        return new ProfileDto(profile.Id, profile.FullName, profile.Email, profile.Phone, profile.Summary,
            profile.YearsOfExperience, profile.LinkedInUrl, profile.GithubUrl, profile.PortfolioUrl,
            profile.Skills.Select(s => new SkillDto(s.Id, s.Name, s.Aliases, s.Category, s.Proficiency.ToString())).ToList(),
            profile.ExperienceBullets.Select(b => new BulletDto(b.Id, b.Text, b.RelatedSkills)).ToList(),
            profile.Resumes.Select(r => new ResumeDto(r.Id, r.FileName, r.TargetRoleType, r.IsDefault)).ToList());
    }
}
