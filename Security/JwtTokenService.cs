using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LoyalAnimal.Shared;
using Microsoft.IdentityModel.Tokens;

namespace LoyalAnimal.Server.Security;

public static class JwtTokenService
{
    public static string CreateToken(User user)
    {
        var secret = Environment.GetEnvironmentVariable("JWT_SECRET");

        if (string.IsNullOrWhiteSpace(secret) ||
            Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException(
                "JWT_SECRET ortam değişkeni eksik veya çok kısa.");
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(secret));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(
                ClaimTypes.Role,
                user.IsAdmin ? "Admin" : "User"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "LoyalAnimal",
            audience: "LoyalAnimal.Mobile",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
