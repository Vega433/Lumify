using Lumify.Application.Interfaces;
using Lumify.Application.Models;
using Lumify.Domain.Entities;
using Lumify.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lumify.Infrastructure.BackgroundServices
{
    public class VideoProcessingWorker : BackgroundService
    {
        private const int MaxParallel = 2;

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

            await RecoverPendingAsync(stoppingToken);

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
                leftover = _running.Where(t => !t.IsCompleted).ToArray();

            if (leftover.Length > 0)
            {
                _logger.LogInformation("Waiting for {Count} in-flight jobs...", leftover.Length);
                try { await Task.WhenAll(leftover); }
                catch { /* individual failures already logged */ }
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
                return;
            }

            _logger.LogInformation("Processing {VideoId}", task.VideoId);

            string finalVideoPath = Path.Combine(_uploadsRoot, "processed", $"{task.VideoId}.mp4");
            string finalPreviewPath = Path.Combine(_uploadsRoot, "previews", $"{task.VideoId}.jpg");

            Directory.CreateDirectory(Path.GetDirectoryName(finalVideoPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(finalPreviewPath)!);

            try
            {
                bool needPreview = string.IsNullOrEmpty(task.TempPreviewPath);

                if (!needPreview)
                {
                    if (File.Exists(task.TempPreviewPath))
                        File.Copy(task.TempPreviewPath, finalPreviewPath, overwrite: true);
                    else
                        needPreview = true;
                }

                var analysis = await mediaService.GetMediaAnalysisAsync(task.TempPath, ct);
                TimeSpan duration = analysis.Duration;

                await mediaService.ProcessVideoAsync(
                    task.TempPath,
                    finalVideoPath,
                    finalPreviewPath,
                    needPreview,
                    duration,
                    ct);

                video.SetPreview();
                video.SetDuration(duration);
                video.UpdateStatus(Status.Ready);
                _logger.LogInformation("Video {VideoId} ready ({Duration})", task.VideoId, duration);
            }
            catch (Exception)
            {
                video.UpdateStatus(Status.Failed);
                throw;
            }
            finally
            {
                await db.SaveChangesAsync(ct);

                fileStorage.DeleteFile(task.TempPath, Paths.tempPath);
                if (task.TempPreviewPath != null)
                    fileStorage.DeleteFile(task.TempPreviewPath, Paths.tempPath);
            }
        }

        private async Task RecoverPendingAsync(CancellationToken ct)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                ILumifyDbContext db = scope.ServiceProvider.GetRequiredService<ILumifyDbContext>();

                List<Guid> pending = await db.Videos
                    .Where(v => v.Status == Status.Pending)
                    .Select(v => v.VideoId)
                    .ToListAsync(ct);

                if (pending.Count == 0)
                    return;

                _logger.LogInformation("Recovering {Count} pending videos", pending.Count);

                string tempDir = Path.Combine(_uploadsRoot, "temp");
                if (!Directory.Exists(tempDir))
                    Directory.CreateDirectory(tempDir);

                foreach (Guid videoId in pending)
                {
                    string? tempFile = Directory.EnumerateFiles(tempDir, $"{videoId}.*").FirstOrDefault();

                    if (tempFile != null)
                    {
                        await _queue.QueueVideoAsync(new VideoProcessingTask(videoId, tempFile, null));
                    }
                    else
                    {
                        Video? video = await db.Videos.FirstOrDefaultAsync(v => v.VideoId == videoId, ct);
                        if (video != null)
                            video.UpdateStatus(Status.Failed);

                        _logger.LogWarning("No temp file for pending video {VideoId}, marked Failed", videoId);
                    }
                }

                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pending video recovery failed");
            }
        }
    }
}
