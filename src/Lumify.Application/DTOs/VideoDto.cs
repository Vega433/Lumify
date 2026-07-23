using Lumify.Domain.Enums;

namespace Lumify.Application.DTOs
{
    public record VideoDto(
        Guid VideoId,
        string Title,
        string? Description,
        string VideoFile, // ДЛЯ ОТЛАДКИ 
        string? PreviewFile, // ДЛЯ ОТЛАДКИ 
        Guid AuthorId,
        DateTime UploadAt,
        long Views,
        TimeSpan Duration,
        Status Status // ДЛЯ ОТЛАДКИ 
        );
}
