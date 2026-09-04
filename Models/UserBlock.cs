namespace LoyalAnimal.Shared;

public class UserBlock
{
    public int Id { get; set; }
    public int BlockerUserId { get; set; }
    public int BlockedUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
