using Lumify.Application.DTOs;
using Lumify.Domain.Entities;

namespace Lumify.Application.Extensions
{
    public static class VideoMappingExtensions
    {
        public static VideoDto ToDto(this Video video) => new VideoDto(
            video.VideoId,
            video.Title,
            video.Description,
            video.VideoFileName, // ДЛЯ ОТЛАДКИ 
            video.PreviewFileName, // ДЛЯ ОТЛАДКИ 
            video.AuthorId,
            video.UploadAt,
            video.Views,
            video.Duration,
            video.Status // ДЛЯ ОТЛАДКИ 
            );
    }
}
