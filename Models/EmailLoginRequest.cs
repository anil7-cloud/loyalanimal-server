namespace LoyalAnimal.Server.Models;

public sealed class EmailLoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}
