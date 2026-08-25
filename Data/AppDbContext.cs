using Microsoft.EntityFrameworkCore;
using _404JobNotFound.Models;

namespace _404JobNotFound.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<ExperienceBullet> ExperienceBullets => Set<ExperienceBullet>();
    public DbSet<Resume> Resumes => Set<Resume>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobScore> JobScores => Set<JobScore>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<BlacklistEntry> BlacklistEntries => Set<BlacklistEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserProfile>()
            .HasMany(p => p.Skills)
            .WithOne(s => s.UserProfile)
            .HasForeignKey(s => s.UserProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserProfile>()
            .HasMany(p => p.ExperienceBullets)
            .WithOne(b => b.UserProfile)
            .HasForeignKey(b => b.UserProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserProfile>()
            .HasMany(p => p.Resumes)
            .WithOne(r => r.UserProfile)
            .HasForeignKey(r => r.UserProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Job>()
            .HasIndex(j => j.ContentHash);

        modelBuilder.Entity<Job>()
            .HasIndex(j => new { j.SourceName, j.ExternalId })
            .IsUnique()
            .HasFilter("ExternalId IS NOT NULL");

        modelBuilder.Entity<Job>()
            .HasOne(j => j.Score)
            .WithOne(s => s.Job!)
            .HasForeignKey<JobScore>(s => s.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Job>()
            .HasOne(j => j.Application)
            .WithOne(a => a.Job!)
            .HasForeignKey<Application>(a => a.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Application>()
            .HasIndex(a => a.JobId)
            .IsUnique();

        modelBuilder.Entity<Application>()
            .HasOne(a => a.Resume)
            .WithMany()
            .HasForeignKey(a => a.ResumeId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<BlacklistEntry>()
            .HasIndex(b => new { b.Type, b.Value })
            .IsUnique();
    }
}
