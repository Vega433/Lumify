namespace Lumify.Application.DTOs
{
    public record UserDto(
        Guid UserId,
        string UserName,
        string Email,
        string? AvatarUrl,
        DateOnly BirthDate,
        DateTime CreatedAt
    );
}
