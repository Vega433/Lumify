namespace Lumify.Application.DTOs
{
    public record AuthUserRequest(
        string Email,
        string Password
        );
}