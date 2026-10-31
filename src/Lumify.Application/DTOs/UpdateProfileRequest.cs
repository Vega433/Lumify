namespace Lumify.Application.DTOs;

public record UpdateProfileRequest(
    string? UserName,
    string? Email,
    DateOnly? BirthDate,
    string? CurrentPassword,
    string? NewPassword
    );
