using Lumify.Application.DTOs;

namespace Lumify.Application.Interfaces
{
    public interface ITokenService
    {
        string GenerateAccessToken(UserAuthDto auth);

        string GenerateRefreshToken();
    }
}
