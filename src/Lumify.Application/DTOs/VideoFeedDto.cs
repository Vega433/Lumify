namespace Lumify.Application.DTOs;

public record VideoFeedDto(
    IReadOnlyList<VideoDto> Recommended,
    IReadOnlyList<VideoDto> Recent
);
