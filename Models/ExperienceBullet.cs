namespace _404JobNotFound.Models;

/// <summary>
/// A single, factual, atomic experience/achievement statement. This is the ONLY
/// source of "facts about the candidate" the AI is allowed to draw on when writing
/// an application email — it must never invent claims outside this table.
/// </summary>
public class ExperienceBullet
{
    public int Id { get; set; }
    public int UserProfileId { get; set; }
    public UserProfile? UserProfile { get; set; }

    public string Text { get; set; } = "";
    /// <summary>Comma-separated skill names this bullet demonstrates, used to match against a job's requirements.</summary>
    public string RelatedSkills { get; set; } = "";
}
