namespace LoyalAnimal.Server.Models;

public sealed class AiVetChatRequest
{
    public string? Message { get; set; }
    public List<AiVetChatMessage>? History { get; set; }
}

public sealed class AiVetChatMessage
{
    public string? Role { get; set; }
    public string? Text { get; set; }
}
