namespace LoyalAnimal.Server.Models;

public class CloudinaryDeletionJob
{
    public int Id { get; set; }

    public string PublicIdPrefix { get; set; } = string.Empty;

    public int AttemptCount { get; set; } = 0;

    public string? LeaseOwner { get; set; }

    public DateTime? LeaseExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime NextAttemptAtUtc { get; set; } = DateTime.UtcNow;
}
