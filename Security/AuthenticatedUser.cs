using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace LoyalAnimal.Server.Security;

public static class AuthenticatedUser
{
    public static int? GetUserId(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return null;

        var subject = principal.FindFirstValue(
            JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrWhiteSpace(subject))
        {
            subject = principal.FindFirstValue(
                ClaimTypes.NameIdentifier);
        }

        return int.TryParse(subject, out var userId) && userId > 0
            ? userId
            : null;
    }
}
