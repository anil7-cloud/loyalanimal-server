namespace LoyalAnimal.Shared;

public class VetClinic
{
    public int Id { get; set; }
    public int OwnerUserId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string VeterinarianName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public bool IsApproved { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
