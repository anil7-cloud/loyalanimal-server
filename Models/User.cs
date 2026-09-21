using System;

namespace LoyalAnimal.Shared;

public class User
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;
    public bool IsEmailVerified { get; set; } = false;

    public string? EmailVerificationTokenHash { get; set; }

    public DateTime? EmailVerificationExpiresAtUtc { get; set; }
    public DateTime? EmailVerificationLastSentAtUtc { get; set; }


    public string PhotoUrl { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // 🔥 EKLENMESİ GEREKENLER
    public string City { get; set; } = string.Empty;

    public int Age { get; set; }

    public string Gender { get; set; } = string.Empty;

    public bool IsAdmin { get; set; } = false;
}