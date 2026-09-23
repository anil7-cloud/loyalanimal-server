namespace LoyalAnimal.Shared;

public class CreateVetClinicRequest
{
    public string Name { get; set; } = string.Empty;
    public string VeterinarianName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
