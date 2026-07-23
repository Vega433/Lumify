namespace Lumify.Application.DTOs
{
    public record UserDto(
        Guid UserId,
        string UserName,
        string Email,
        DateTime BirthDate,
        DateTime CreatedAt
    );
}