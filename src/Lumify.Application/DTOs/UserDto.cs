namespace Lumify.Application.DTOs;

public record UserDto(
    Guid UserId,
    string UserName,
    string? AvatarUrl,
    DateOnly BirthDate,
    DateTime CreatedAt
);
