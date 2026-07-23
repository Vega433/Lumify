namespace Lumify.Application.DTOs
{
    public record AuthResponse(
        string AccessToken, 
        string RefreshToken,
        UserDto User
        );
}
