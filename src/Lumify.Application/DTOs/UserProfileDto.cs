namespace Lumify.Application.DTOs;

public record UserProfileDto(
    Guid UserId,
    string UserName,
    string Email,
    string? AvatarUrl,
    DateOnly BirthDate,
    DateTime CreatedAt
);
