namespace _404JobNotFound.Models;

public class Resume
{
    public int Id { get; set; }
    public int UserProfileId { get; set; }
    public UserProfile? UserProfile { get; set; }

    public string FileName { get; set; } = "";
    public string StoragePath { get; set; } = "";
    public string TargetRoleType { get; set; } = "General";
    public bool IsDefault { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
