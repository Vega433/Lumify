using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace Lumify.Api.Extensions
{
    public static class ClaimsExtensions
    {
        public static Guid GetId(this ClaimsPrincipal user)
        {
            string? value =
                user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? user.FindFirst("sub")?.Value;

            return Guid.TryParse(value, out Guid guid) ? guid : Guid.Empty;
        }

        public static string GetEmail(this ClaimsPrincipal user) =>
            user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst(JwtRegisteredClaimNames.Email)?.Value
            ?? user.FindFirst("email")?.Value
            ?? string.Empty;
    }
}
