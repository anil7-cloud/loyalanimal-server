namespace LoyalAnimal.Shared;

public class PetContactRequest
{
    public int Id { get; set; }

    public int PetId { get; set; }

    public int RequesterUserId { get; set; }

    public int OwnerUserId { get; set; }

    // Pending, Accepted veya Rejected
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? RespondedAtUtc { get; set; }
}
