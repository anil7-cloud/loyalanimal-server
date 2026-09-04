namespace LoyalAnimal.Shared;

public class UserReport
{
    public int Id { get; set; }
    public int ReporterUserId { get; set; }
    public int ReportedUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
