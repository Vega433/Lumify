using FFMpegCore;
using Lumify.Application.Interfaces;
using Lumify.Application.Models;
using Lumify.Domain.Entities;
using Lumify.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lumify.Infrastructure.BackgroundServices;

public class VideoProcessingWorker : BackgroundService
{
    private const int MaxParallel = 2;
    private const string PlaylistFileName = "index.m3u8";
    private const string PlaylistEndMarker = "#EXT-X-ENDLIST";

    private readonly IVideoProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VideoProcessingWorker> _logger;
    private readonly string _uploadsRoot;
    private readonly SemaphoreSlim _semaphore = new(MaxParallel);
    private readonly List<Task> _running = new();
    private readonly object _runningLock = new();

    public VideoProcessingWorker(
        IVideoProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<VideoProcessingWorker> logger,
        string uploadsRoot)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _uploadsRoot = uploadsRoot;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Video worker started (max parallel: {Max})", MaxParallel);

        await RecoverUnfinishedAsync(stoppingToken);

        try
        {
            await foreach (VideoProcessingTask task in _queue.ReadAllAsync(stoppingToken))
            {
                await _semaphore.WaitAsync(stoppingToken);

                Task work = ProcessTaskAsync(task, stoppingToken);
                lock (_runningLock)
                {
                    _running.Add(work);
                    _running.RemoveAll(t => t.IsCompleted);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutdown
        }

        Task[] leftover;
        lock (_runningLock)
        {
            leftover = _running.Where(t => !t.IsCompleted).ToArray();
        }

        if (leftover.Length > 0)
        {
            _logger.LogInformation("Waiting for {Count} in-flight jobs...", leftover.Length);
            try
            {
                await Task.WhenAll(leftover);
            }
            catch
            {
                // individual failures already logged
            }
        }

        _logger.LogInformation("Video worker stopped");
    }

    private async Task ProcessTaskAsync(VideoProcessingTask task, CancellationToken ct)
    {
        try
        {
            await ProcessVideoInternalAsync(task, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed processing video {VideoId}", task.VideoId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task ProcessVideoInternalAsync(VideoProcessingTask task, CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        IMediaService mediaService = scope.ServiceProvider.GetRequiredService<IMediaService>();
        IFileStorageService fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        ILumifyDbContext db = scope.ServiceProvider.GetRequiredService<ILumifyDbContext>();

        Video? video = await db.Videos.FirstOrDefaultAsync(v => v.VideoId == task.VideoId, ct);
        if (video == null)
        {
            _logger.LogWarning("Video {VideoId} not found, skipping", task.VideoId);
            // Почистим temp, чтобы не копилось
            TryDeleteTemp(fileStorage, task);
            return;
        }

        _logger.LogInformation("Processing {VideoId}", task.VideoId);

        video.UpdateStatus(Status.Processing);
        await db.SaveChangesAsync(ct);

        // Per-video директория: processed/{videoId}/index.m3u8 (мастер) + {высота}.m3u8 и сегменты.
        string videoDir = Path.Combine(_uploadsRoot, "processed", task.VideoId.ToString());
        string playlistPath = Path.Combine(videoDir, PlaylistFileName);
        string previewDir = Path.Combine(_uploadsRoot, "previews");
        string finalPreviewPath = Path.Combine(previewDir, $"{task.VideoId}.jpg");

        Directory.CreateDirectory(videoDir);
        Directory.CreateDirectory(previewDir);

        try
        {
            bool needPreview = string.IsNullOrEmpty(task.TempPreviewPath);

            if (!needPreview && File.Exists(task.TempPreviewPath!))
            {
                File.Copy(task.TempPreviewPath!, finalPreviewPath, overwrite: true);
            }
            else
            {
                needPreview = true;
            }

            IMediaAnalysis analysis = await mediaService.GetMediaAnalysisAsync(task.TempPath, ct);
            TimeSpan duration = analysis.Duration;

            await mediaService.ProcessVideoAsync(
                task.TempPath,
                playlistPath,
                finalPreviewPath,
                needPreview,
                duration,
                ct);

            // Санити-чек: плейлист должен быть дописан до конца.
            if (!IsPlaylistComplete(playlistPath))
            {
                throw new InvalidOperationException(
                    $"HLS playlist for {task.VideoId} is incomplete ({PlaylistEndMarker} missing).");
            }

            video.SetPreview();
            video.SetDuration(duration);
            video.UpdateStatus(Status.Ready);
            _logger.LogInformation("Video {VideoId} ready ({Duration})", task.VideoId, duration);
        }
        catch (Exception)
        {
            video.UpdateStatus(Status.Failed);

            // Чистим недо-обработанную папку (сегменты могли записаться частично)
            TryDeleteDirectory(videoDir);
            TryDeleteFile(finalPreviewPath);

            throw;
        }
        finally
        {
            await db.SaveChangesAsync(ct);
            TryDeleteTemp(fileStorage, task);
        }
    }

    private async Task RecoverUnfinishedAsync(CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ILumifyDbContext db = scope.ServiceProvider.GetRequiredService<ILumifyDbContext>();
            IFileStorageService fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

            List<Video> unfinished = await db.Videos
                .Where(v => v.Status == Status.Pending || v.Status == Status.Processing)
                .ToListAsync(ct);

            if (unfinished.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Recovering {Count} unfinished videos", unfinished.Count);

            string tempDir = Path.Combine(_uploadsRoot, "temp");
            string processedDir = Path.Combine(_uploadsRoot, "processed");
            string previewDir = Path.Combine(_uploadsRoot, "previews");
            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(processedDir);
            Directory.CreateDirectory(previewDir);

            foreach (Video video in unfinished)
            {
                string videoDir = Path.Combine(processedDir, video.VideoId.ToString());
                string playlistPath = Path.Combine(videoDir, PlaylistFileName);

                // 1) Уже готовый плейлист — просто восстанавливаем статус.
                if (IsPlaylistComplete(playlistPath))
                {
                    string previewPath = Path.Combine(previewDir, $"{video.VideoId}.jpg");
                    if (File.Exists(previewPath))
                    {
                        video.SetPreview();
                    }

                    video.UpdateStatus(Status.Ready);
                    DeleteTempFiles(fileStorage, tempDir, video.VideoId);
                    _logger.LogInformation(
                        "Recovered {VideoId} as Ready (playlist already complete)", video.VideoId);
                    continue;
                }

                // 2) Недописанный плейлист — считаем битым, сносим папку.
                if (Directory.Exists(videoDir))
                {
                    TryDeleteDirectory(videoDir);
                    _logger.LogWarning(
                        "Removed incomplete HLS directory for {VideoId}", video.VideoId);
                }

                // 3) Есть temp-файлы — переотправляем в очередь.
                (string? tempVideo, string? tempPreview) = FindTempFiles(tempDir, video.VideoId);
                if (tempVideo != null)
                {
                    video.UpdateStatus(Status.Pending);
                    await _queue.QueueVideoAsync(
                        new VideoProcessingTask(video.VideoId, tempVideo, tempPreview));
                    _logger.LogInformation("Re-queued {VideoId} for processing", video.VideoId);
                }
                else
                {
                    // 4) Ничего нет — Failed.
                    video.UpdateStatus(Status.Failed);
                    _logger.LogWarning(
                        "No temp or complete playlist for {VideoId}, marked Failed", video.VideoId);
                }
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unfinished video recovery failed");
        }
    }

    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    private static (string? Video, string? Preview) FindTempFiles(string tempDir, Guid videoId)
    {
        List<string> matches = Directory.EnumerateFiles(tempDir, $"{videoId}.*").ToList();
        string? preview = matches.FirstOrDefault(path =>
            ImageExtensions.Contains(Path.GetExtension(path)));
        string? video = matches.FirstOrDefault(path =>
            !ImageExtensions.Contains(Path.GetExtension(path)));
        return (video, preview);
    }

    private static void DeleteTempFiles(IFileStorageService fileStorage, string tempDir, Guid videoId)
    {
        foreach (string path in Directory.EnumerateFiles(tempDir, $"{videoId}.*"))
        {
            fileStorage.DeleteFile(path, Paths.Temp);
        }
    }

    private void TryDeleteTemp(IFileStorageService fileStorage, VideoProcessingTask task)
    {
        try
        {
            if (!string.IsNullOrEmpty(task.TempPath))
            {
                fileStorage.DeleteFile(task.TempPath, Paths.Temp);
            }

            if (!string.IsNullOrEmpty(task.TempPreviewPath))
            {
                fileStorage.DeleteFile(task.TempPreviewPath, Paths.Temp);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete temp files for {VideoId}", task.VideoId);
        }
    }

    private void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete directory {Path}", path);
        }
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete file {Path}", path);
        }
    }

    private static bool IsPlaylistComplete(string playlistPath)
    {
        try
        {
            if (!File.Exists(playlistPath))
            {
                return false;
            }

            string directory = Path.GetDirectoryName(Path.GetFullPath(playlistPath))!;
            string[] lines = File.ReadAllLines(playlistPath);
            bool master = lines.Any(line => line.StartsWith("#EXT-X-STREAM-INF", StringComparison.Ordinal));
            if (!master)
            {
                return lines.Any(line => line.Contains(PlaylistEndMarker, StringComparison.Ordinal));
            }

            bool hasVariant = false;
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                string variantPath = Path.GetFullPath(Path.Combine(
                    directory,
                    line.Replace('/', Path.DirectorySeparatorChar)));
                string root = directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!variantPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!File.Exists(variantPath))
                {
                    return false;
                }

                string variantText = File.ReadAllText(variantPath);
                if (!variantText.Contains(PlaylistEndMarker, StringComparison.Ordinal))
                {
                    return false;
                }

                hasVariant = true;
            }

            return hasVariant;
        }
        catch
        {
            return false;
        }
    }
}
