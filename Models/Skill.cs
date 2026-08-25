namespace _404JobNotFound.Models;

public enum SkillProficiency
{
    Basic,
    Intermediate,
    Advanced
}

public class Skill
{
    public int Id { get; set; }
    public int UserProfileId { get; set; }
    public UserProfile? UserProfile { get; set; }

    public string Name { get; set; } = "";
    /// <summary>Comma-separated alternate spellings used for keyword matching, e.g. "ASP.NET Core,AspNetCore,Asp.Net Core"</summary>
    public string Aliases { get; set; } = "";
    public string Category { get; set; } = "";
    public SkillProficiency Proficiency { get; set; } = SkillProficiency.Intermediate;
}
