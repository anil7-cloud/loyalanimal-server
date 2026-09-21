using System.Security.Cryptography;
using System.Text;

namespace LoyalAnimal.Server.Security;

public static class EmailVerificationTokenService
{
    public static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);

        return Convert.ToHexString(bytes);
    }

    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(token));

        return Convert.ToHexString(bytes);
    }

    public static DateTime GetExpirationUtc()
    {
        return DateTime.UtcNow.AddHours(24);
    }

    public static bool VerifyToken(
        string providedToken,
        string? storedTokenHash,
        DateTime? expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(providedToken) ||
            string.IsNullOrWhiteSpace(storedTokenHash) ||
            expiresAtUtc is null ||
            expiresAtUtc <= DateTime.UtcNow)
        {
            return false;
        }

        byte[] providedHash;
        byte[] storedHash;

        try
        {
            providedHash = SHA256.HashData(
                Encoding.UTF8.GetBytes(providedToken));

            storedHash = Convert.FromHexString(storedTokenHash);
        }
        catch (FormatException)
        {
            return false;
        }

        return providedHash.Length == storedHash.Length &&
               CryptographicOperations.FixedTimeEquals(
                   providedHash,
                   storedHash);
    }
}
