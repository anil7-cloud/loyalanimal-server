namespace LoyalAnimal.Shared;

public class PetVaccination
{
    public int Id { get; set; }

    public int PetId { get; set; }

    public int OwnerUserId { get; set; }

    public string VaccineName { get; set; } = string.Empty;

    public DateTime VaccinationDateUtc { get; set; }

    public DateTime? NextDueDateUtc { get; set; }

    public string VeterinarianName { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
