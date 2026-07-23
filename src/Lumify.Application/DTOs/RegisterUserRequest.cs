namespace Lumify.Application.DTOs
{
    public record RegisterUserRequest(
        string UserName,
        string Email,
        string Password,
        DateTime BirthDate,
        Stream? Avatar,
        string? AvatarPath
        );
}