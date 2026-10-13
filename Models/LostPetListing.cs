using System;

namespace LoyalAnimal.Shared;

public class LostPetListing
{
    public int Id { get; set; }

    public int OwnerUserId { get; set; }

    public string PetName { get; set; } = string.Empty;

    public string PetType { get; set; } = string.Empty;

    public string Breed { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string District { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string PhotoUrl { get; set; } = string.Empty;

    public string ContactInfo { get; set; } = string.Empty;

    public DateTime LostAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsFound { get; set; } = false;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
