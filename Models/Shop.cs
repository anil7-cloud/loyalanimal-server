namespace LoyalAnimal.Shared;

public class Shop
{
    public int Id { get; set; }

    // Petshop hesabının sahibi
    public int OwnerUserId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;

    public bool IsApproved { get; set; } = false;
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
