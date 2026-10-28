namespace Lumify.Application.DTOs;

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    UserProfileDto User
);
