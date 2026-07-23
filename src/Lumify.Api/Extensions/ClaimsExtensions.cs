using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Lumify.Api.Extensions
{
    public static class ClaimsExtensions
    {
        public static Guid GetId(this ClaimsPrincipal user)
        {
            string? value =
                user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? user.FindFirst("sub")?.Value;

            return Guid.TryParse(value, out Guid guid) ? guid : Guid.Empty;
        }

        public static string GetEmail(this ClaimsPrincipal user) =>
            user.FindFirst(JwtRegisteredClaimNames.Email)?.Value
            ?? user.FindFirst("email")?.Value
            ?? string.Empty;
    }
}
