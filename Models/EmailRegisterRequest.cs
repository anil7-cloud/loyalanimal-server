namespace LoyalAnimal.Server.Models;

public sealed class EmailRegisterRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string City { get; set; } = "";
    public int Age { get; set; }
    public string Gender { get; set; } = "";
}
