using System.Globalization;
using FFMpegCore;
using FFMpegCore.Enums;
using Lumify.Application.Interfaces;

namespace Lumify.Infrastructure.Media;

public sealed class FFMpegService : IMediaService
{
    private const int HlsSegmentSeconds = 5;
    private const int ConstantRateFactor = 21;
    private const string ScaleFilter = "scale=w='min(1920,iw)':h=-2";

    public Task<IMediaAnalysis> GetMediaAnalysisAsync(string inputPath, CancellationToken ct) =>
        FFProbe.AnalyseAsync(inputPath, ffOptions: null, cancellationToken: ct);

    public async Task ProcessVideoAsync(
        string inputPath,
        string outputPath,
        string previewPath,
        bool shouldExtractPreview,
        TimeSpan duration,
        CancellationToken ct)
    {
        if (!outputPath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("HLS outputPath must point to an .m3u8 file.", nameof(outputPath));
        }

        if (shouldExtractPreview)
        {
            await GeneratePreviewAsync(inputPath, previewPath, duration, ct);
        }

        await PackageAsHlsAsync(inputPath, outputPath, ct);
    }

    private async Task GeneratePreviewAsync(
        string inputPath, string previewPath, TimeSpan duration, CancellationToken ct)
    {
        TimeSpan captureTime = duration.TotalSeconds > 1 ? TimeSpan.FromSeconds(1) : TimeSpan.Zero;

        await FFMpegArguments
            .FromFileInput(inputPath, verifyExists: false, input =>
            {
                if (captureTime > TimeSpan.Zero)
                {
                    input.Seek(captureTime);
                }
            })
            .OutputToFile(previewPath, overwrite: true, options => options
                .WithFrameOutputCount(1)
                .WithCustomArgument("-vf")
                .WithCustomArgument(ScaleFilter))
            .CancellableThrough(ct)
            .ProcessAsynchronously(throwOnError: true);
    }

    private static async Task PackageAsHlsAsync(
        string inputPath, string outputPath, CancellationToken ct)
    {
        string outputDir = Path.GetDirectoryName(Path.GetFullPath(outputPath))!;
        Directory.CreateDirectory(outputDir);

        string segmentPattern = Path
            .Combine(outputDir, "segment_%03d.ts")
            .Replace('\\', '/');

        await FFMpegArguments
            .FromFileInput(inputPath, verifyExists: false)
            .OutputToFile(outputPath, overwrite: true, options =>
                ConfigureHlsOutput(options, segmentPattern))
            .CancellableThrough(ct)
            .ProcessAsynchronously(throwOnError: true);

        await NormalizePlaylistPathsAsync(outputPath, ct);
    }

    private static void ConfigureHlsOutput(
        FFMpegArgumentOptions options, string segmentPattern)
    {
        options
            .WithVideoCodec(VideoCodec.LibX264)
            .WithAudioCodec(AudioCodec.Aac)
            .ForceFormat("hls")
            .WithCustomArgument("-hls_time")
            .WithCustomArgument(HlsSegmentSeconds.ToString(CultureInfo.InvariantCulture))
            .WithCustomArgument("-hls_playlist_type")
            .WithCustomArgument("vod") // VOD, не live
            .WithCustomArgument("-hls_list_size")
            .WithCustomArgument("0")
            .WithCustomArgument("-hls_segment_filename")
            .WithCustomArgument(segmentPattern)
            .WithCustomArgument("-vf")
            .WithCustomArgument(ScaleFilter)
            .WithConstantRateFactor(ConstantRateFactor)
            .WithSpeedPreset(Speed.Medium);
    }

    private static async Task NormalizePlaylistPathsAsync(string playlistPath, CancellationToken ct)
    {
        string[] lines = await File.ReadAllLinesAsync(playlistPath, ct);

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = Path.GetFileName(lines[i]);
            }
        }

        await File.WriteAllLinesAsync(playlistPath, lines, ct);
    }
}
