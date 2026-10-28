using Lumify.Application.DTOs;
using Lumify.Domain.Entities;

namespace Lumify.Application.Extensions;

public static class UserMappingExtensions
{
    public static UserDto ToDto(this User user) => new UserDto(
        user.UserId,
        user.UserName,
        AvatarUrl(user),
        DateOnly.FromDateTime(user.BirthDate.Value),
        user.CreatedAt
        );

    public static UserProfileDto ToProfileDto(this User user) => new UserProfileDto(
        user.UserId,
        user.UserName,
        user.Email,
        AvatarUrl(user),
        DateOnly.FromDateTime(user.BirthDate.Value),
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
        user.ToProfileDto()
        );

    private static string? AvatarUrl(User user) =>
        string.IsNullOrEmpty(user.AvatarPath)
            ? null
            : $"/uploads/avatars/{user.AvatarPath}";
}
