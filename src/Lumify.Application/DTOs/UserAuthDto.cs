namespace Lumify.Application.DTOs
{
    public record UserAuthDto(
        Guid UserId,
        string Email,
        string UserName
        );
}
