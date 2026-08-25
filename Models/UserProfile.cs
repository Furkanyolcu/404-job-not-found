namespace _404JobNotFound.Models;

public class UserProfile
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string Summary { get; set; } = "";
    public int YearsOfExperience { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? GithubUrl { get; set; }
    public string? PortfolioUrl { get; set; }

    public List<Skill> Skills { get; set; } = new();
    public List<ExperienceBullet> ExperienceBullets { get; set; } = new();
    public List<Resume> Resumes { get; set; } = new();
}
