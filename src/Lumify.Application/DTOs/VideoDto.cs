using Lumify.Domain.Enums;

namespace Lumify.Application.DTOs
{
    public record VideoDto(
        Guid VideoId,
        string Title,
        string? Description,
        string VideoUrl,
        string? PreviewUrl,
        Guid AuthorId,
        DateTime UploadAt,
        long Views,
        TimeSpan Duration,
        Status Status
        );
}
