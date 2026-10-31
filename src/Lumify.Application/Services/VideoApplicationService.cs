using Lumify.Application.DTOs;
using Lumify.Application.Extensions;
using Lumify.Application.Interfaces;
using Lumify.Application.Models;
using Lumify.Domain.Entities;
using Lumify.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Lumify.Application.Services;

public class VideoApplicationService
{
    public const int DefaultFeedSize = 24;
    public const int MaxFeedSize = 48;

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
        {
            throw new InvalidOperationException("Author not found");
        }

        bool authorExist = await _context.Users.AnyAsync(u => u.UserId == request.AuthorId, ct);
        if (!authorExist)
        {
            throw new InvalidOperationException("Author not found");
        }

        string videoExt = Path.GetExtension(request.FileName);
        if (string.IsNullOrWhiteSpace(videoExt) || !AllowedVideoExtensions.Contains(videoExt))
        {
            throw new ArgumentException("Unsupported video file extension");
        }

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
            Paths.Temp,
            ct);

        string? tempPreviewPath = null;
        try
        {
            if (request.Preview != null && request.PreviewName != null)
            {
                tempPreviewPath = await _fileStorageService.SaveFileAsync(
                    request.Preview,
                    video.VideoId,
                    Path.GetExtension(request.PreviewName),
                    Paths.Temp,
                    ct);
            }

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
            _fileStorageService.DeleteFile(tempVideoPath, Paths.Temp);
            if (tempPreviewPath != null)
            {
                _fileStorageService.DeleteFile(tempPreviewPath, Paths.Temp);
            }

            throw;
        }
    }

    public async Task<VideoFeedDto> GetVideoListAsync(int limit, CancellationToken ct)
    {
        int take = NormalizeFeedLimit(limit);

        IQueryable<Video> ready = _context.Videos.Where(v => v.Status == Status.Ready);

        List<Video> recent = await ready
            .OrderByDescending(v => v.UploadAt)
            .Take(take)
            .ToListAsync(ct);

        // Placeholder until a real recommendation algorithm exists:
        // currently "most viewed", then newest.
        List<Video> recommended = await ready
            .OrderByDescending(v => v.Views)
            .ThenByDescending(v => v.UploadAt)
            .Take(take)
            .ToListAsync(ct);

        return new VideoFeedDto(
            recommended.Select(v => v.ToDto()).ToList(),
            recent.Select(v => v.ToDto()).ToList());
    }

    private static int NormalizeFeedLimit(int limit)
    {
        if (limit <= 0)
        {
            return DefaultFeedSize;
        }

        return Math.Min(limit, MaxFeedSize);
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

    public async Task<VideoDeleteResult> DeleteVideoByIdAsync(Guid videoId, Guid userId, CancellationToken ct)
    {
        Video? video = await _context.Videos.FirstOrDefaultAsync(v => v.VideoId == videoId, ct);

        if (video == null)
        {
            return VideoDeleteResult.NotFound;
        }

        if (video.AuthorId != userId)
        {
            return VideoDeleteResult.Forbidden;
        }

        using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(ct);

        try
        {
            _context.Videos.Remove(video);
            await _context.SaveChangesAsync(ct);

            _fileStorageService.DeleteFile(video.VideoFileName, Paths.Processed);
            _fileStorageService.DeleteFile($"{video.VideoId}{Path.GetExtension(video.VideoFileName)}", Paths.Temp);

            // Temp upload may keep original extension before transcode.
            foreach (string ext in AllowedVideoExtensions)
            {
                _fileStorageService.DeleteFile($"{video.VideoId}{ext}", Paths.Temp);
            }

            if (!string.IsNullOrEmpty(video.PreviewFileName))
            {
                _fileStorageService.DeleteFile(video.PreviewFileName, Paths.Preview);
                _fileStorageService.DeleteFile(video.PreviewFileName, Paths.Temp);
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

    public async Task<List<VideoDto>> GetMyVideosAsync(Guid authorId, CancellationToken ct)
    {
        List<Video> videos = await _context.Videos
            .Where(v => v.AuthorId == authorId)
            .OrderByDescending(v => v.UploadAt)
            .ToListAsync(ct);

        return videos.Select(v => v.ToDto()).ToList();
    }

    public async Task<VideoDto?> UpdateVideoAsync(Guid videoId, Guid userId, UpdateVideoRequest request, CancellationToken ct)
    {
        if (request.Title == null && request.Description == null)
        {
            throw new ArgumentException("Title or description is required");
        }

        Video? video = await _context.Videos.FirstOrDefaultAsync(v => v.VideoId == videoId, ct);
        if (video == null)
        {
            return null;
        }

        if (video.AuthorId != userId)
        {
            throw new UnauthorizedAccessException("You can edit only your own video");
        }

        if (request.Title != null)
        {
            video.UploadTitle(request.Title.Trim());
        }

        if (request.Description != null)
        {
            video.UploadDescription(request.Description);
        }

        await _context.SaveChangesAsync(ct);
        return video.ToDto();
    }

    public async Task<VideoStatsDto?> AddViewAsync(Guid videoId, Guid userId, CancellationToken ct)
    {
        Video? video = await _context.Videos.FirstOrDefaultAsync(v => v.VideoId == videoId, ct);
        if (video == null)
        {
            return null;
        }

        video.AddView();
        await _context.SaveChangesAsync(ct);

        bool likedByMe = userId != Guid.Empty && await _context.VideoLikes
            .AnyAsync(like => like.VideoId == videoId && like.UserId == userId, ct);
        return new VideoStatsDto(video.VideoId, video.Views, video.Likes, likedByMe);
    }

    public async Task<VideoStatsDto?> LikeAsync(Guid videoId, Guid userId, CancellationToken ct)
    {
        Video? video = await _context.Videos.FirstOrDefaultAsync(v => v.VideoId == videoId, ct);
        if (video == null)
        {
            return null;
        }

        bool alreadyLiked = await _context.VideoLikes
            .AnyAsync(like => like.VideoId == videoId && like.UserId == userId, ct);
        if (alreadyLiked)
        {
            return new VideoStatsDto(video.VideoId, video.Views, video.Likes, true);
        }

        using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            _context.VideoLikes.Add(new VideoLike(userId, videoId));
            video.Like();
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new VideoStatsDto(video.VideoId, video.Views, video.Likes, true);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            ClearTracker();

            Video current = await _context.Videos.FirstAsync(v => v.VideoId == videoId, ct);
            bool liked = await _context.VideoLikes
                .AnyAsync(like => like.VideoId == videoId && like.UserId == userId, ct);
            if (!liked)
            {
                throw;
            }

            return new VideoStatsDto(current.VideoId, current.Views, current.Likes, true);
        }
    }

    public async Task<VideoStatsDto?> UnlikeAsync(Guid videoId, Guid userId, CancellationToken ct)
    {
        Video? video = await _context.Videos.FirstOrDefaultAsync(v => v.VideoId == videoId, ct);
        if (video == null)
        {
            return null;
        }

        VideoLike? like = await _context.VideoLikes
            .FirstOrDefaultAsync(item => item.VideoId == videoId && item.UserId == userId, ct);
        if (like == null)
        {
            return new VideoStatsDto(video.VideoId, video.Views, video.Likes, false);
        }

        _context.VideoLikes.Remove(like);
        video.Unlike();
        await _context.SaveChangesAsync(ct);
        return new VideoStatsDto(video.VideoId, video.Views, video.Likes, false);
    }

    private void ClearTracker()
    {
        if (_context is DbContext dbContext)
        {
            dbContext.ChangeTracker.Clear();
        }
    }
}
