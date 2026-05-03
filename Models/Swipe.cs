namespace LoyalAnimal.Shared;

public class Swipe
{
    public int Id { get; set; }
    public int FromUserId { get; set; }
    public int ToUserId { get; set; }
    public bool IsLike { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
