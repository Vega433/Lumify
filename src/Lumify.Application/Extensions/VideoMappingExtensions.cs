using Lumify.Application.DTOs;
using Lumify.Domain.Entities;
using Lumify.Domain.Enums;

namespace Lumify.Application.Extensions
{
    public static class VideoMappingExtensions
    {
        public static VideoDto ToDto(this Video video) => new VideoDto(
            video.VideoId,
            video.Title,
            video.Description,
            BuildUrl(Paths.processedPath, video.VideoFileName),
            string.IsNullOrEmpty(video.PreviewFileName)
                ? null
                : BuildUrl(Paths.previewPath, video.PreviewFileName),
            video.AuthorId,
            video.UploadAt,
            video.Views,
            video.Duration,
            video.Status
            );

        private static string BuildUrl(Paths folder, string fileName) => folder switch
        {
            Paths.processedPath => $"/uploads/processed/{fileName}",
            Paths.previewPath => $"/uploads/previews/{fileName}",
            _ => $"/uploads/{fileName}"
        };
    }
}
