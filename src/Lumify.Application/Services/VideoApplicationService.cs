using Lumify.Application.DTOs;
using Lumify.Application.Interfaces;
using Lumify.Application.Extensions;
using Lumify.Application.Models;
using Lumify.Domain.Entities;
using Lumify.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Lumify.Application.Services
{
    public class VideoApplicationService
    {
        private static readonly HashSet<string> AllowedVideoExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mov", ".mkv", ".webm", ".avi" };

        private readonly ILumifyDbContext _context;
        private readonly IFileStorageService _fileStorageService;
        private readonly IVideoProcessingQueue _videoProcessingQueue;

        public VideoApplicationService(
            ILumifyDbContext context,
            IFileStorageService fileStorageService,
            IVideoProcessingQueue videoProcessingQueue)
        {
            _context = context;
            _fileStorageService = fileStorageService;
            _videoProcessingQueue = videoProcessingQueue;
        }

        public async Task<VideoDto> UploadAsync(UploadVideoRequest request, CancellationToken ct)
        {
            if (request.AuthorId == Guid.Empty)
                throw new InvalidOperationException("Author not found");

            bool authorExist = await _context.Users.AnyAsync(u => u.UserId == request.AuthorId, ct);
            if (!authorExist)
                throw new InvalidOperationException("Author not found");

            string videoExt = Path.GetExtension(request.FileName);
            if (string.IsNullOrWhiteSpace(videoExt) || !AllowedVideoExtensions.Contains(videoExt))
                throw new ArgumentException("Unsupported video file extension");

            string? previewExt = request.PreviewName != null ? Path.GetExtension(request.PreviewName) : null;

            Video video = new Video(
                    request.AuthorId,
                    request.Title,
                    request.Description,
                    previewExt
                    );

            video.UpdateStatus(Status.Pending);

            string tempVideoPath = await _fileStorageService.SaveFileAsync(
                request.FileStream,
                video.VideoId,
                videoExt,
                Paths.tempPath,
                ct);

            string? tempPreviewPath = null;
            try
            {
                if (request.Preview != null && request.PreviewName != null)
                    tempPreviewPath = await _fileStorageService.SaveFileAsync(
                        request.Preview,
                        video.VideoId,
                        Path.GetExtension(request.PreviewName),
                        Paths.tempPath,
                        ct);

                using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(ct);
                try
                {
                    _context.Videos.Add(video);
                    await _context.SaveChangesAsync(ct);

                    await _videoProcessingQueue.QueueVideoAsync(
                        new VideoProcessingTask(
                            video.VideoId,
                            tempVideoPath,
                            tempPreviewPath
                            ));

                    await transaction.CommitAsync(ct);
                    return video.ToDto();
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync(ct);
                    throw;
                }
            }
            catch (Exception)
            {
                _fileStorageService.DeleteFile(tempVideoPath, Paths.tempPath);
                if (tempPreviewPath != null)
                    _fileStorageService.DeleteFile(tempPreviewPath, Paths.tempPath);
                throw;
            }
        }

        public async Task<List<VideoDto>> GetVideoListAsync(CancellationToken ct)
        {
            List<Video> videos = await _context.Videos
                .OrderByDescending(v => v.UploadAt)
                .ToListAsync(ct);

            return videos.Select(v => v.ToDto()).ToList();
        }

        public async Task<VideoDto?> GetVideoByIdAsync(Guid videoId, CancellationToken ct)
        {
            Video? video = await _context.Videos.FirstOrDefaultAsync(v => v.VideoId == videoId, ct);
            return video?.ToDto();
        }

        public async Task<List<VideoDto>> GetVideoListByAuthorAsync(Guid authorId, CancellationToken ct)
        {
            List<Video> videos = await _context.Videos
                .Where(v => v.AuthorId == authorId)
                .OrderByDescending(v => v.UploadAt)
                .ToListAsync(ct);
            return videos.Select(v => v.ToDto()).ToList();
        }

        public async Task<VideoDeleteResult> DeleteVideoById(Guid videoId, Guid userId, CancellationToken ct)
        {
            Video? video = await _context.Videos.FirstOrDefaultAsync(v => v.VideoId == videoId, ct);

            if (video == null)
                return VideoDeleteResult.NotFound;
            if (video.AuthorId != userId)
                return VideoDeleteResult.Forbidden;

            using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(ct);

            try
            {
                _context.Videos.Remove(video);
                await _context.SaveChangesAsync(ct);

                _fileStorageService.DeleteFile(video.VideoFileName, Paths.processedPath);
                _fileStorageService.DeleteFile($"{video.VideoId}{Path.GetExtension(video.VideoFileName)}", Paths.tempPath);

                // Temp upload may keep original extension before transcode.
                foreach (string ext in AllowedVideoExtensions)
                    _fileStorageService.DeleteFile($"{video.VideoId}{ext}", Paths.tempPath);

                if (!string.IsNullOrEmpty(video.PreviewFileName))
                {
                    _fileStorageService.DeleteFile(video.PreviewFileName, Paths.previewPath);
                    _fileStorageService.DeleteFile(video.PreviewFileName, Paths.tempPath);
                }

                await transaction.CommitAsync(ct);
                return VideoDeleteResult.Deleted;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(ct);
                return VideoDeleteResult.Failed;
            }
        }
    }
}
