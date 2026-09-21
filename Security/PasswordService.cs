using LoyalAnimal.Shared;
using Microsoft.AspNetCore.Identity;

namespace LoyalAnimal.Server.Security;

public static class PasswordService
{
    private static readonly PasswordHasher<User> Hasher = new();

    public static string Hash(User user, string password)
    {
        return Hasher.HashPassword(user, password);
    }

    public static bool Verify(User user, string password)
    {
        if (string.IsNullOrWhiteSpace(user.PasswordHash))
            return false;

        var result = Hasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            password);

        return result != PasswordVerificationResult.Failed;
    }
}
