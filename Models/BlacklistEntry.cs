namespace _404JobNotFound.Models;

public enum BlacklistType
{
    CompanyName,
    Domain,
    Email
}

public class BlacklistEntry
{
    public int Id { get; set; }
    public BlacklistType Type { get; set; }
    public string Value { get; set; } = "";
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
