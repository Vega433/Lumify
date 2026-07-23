using Lumify.Application.DTOs;
using Lumify.Domain.Entities;

namespace Lumify.Application.Extensions
{
    public static class UserMapper
    {
        public static UserDto ToDto(this User user) => new UserDto(
            user.UserId,
            user.UserName,
            user.Email,
            user.BirthDate.Value,
            user.CreatedAt
                );

        public static UserAuthDto ToAuthDto(this User user) => new UserAuthDto(
            user.UserId,
            user.Email,
            user.UserName
            );

        public static AuthResponse ToAuthResponse(this User user, string accessToken, string refreshToken)
            => new AuthResponse(
            accessToken,
            refreshToken,
            user.ToDto()
            );
    }
}
