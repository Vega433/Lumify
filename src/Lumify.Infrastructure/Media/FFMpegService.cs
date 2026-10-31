using System.Globalization;
using FFMpegCore;
using Lumify.Application.Interfaces;

namespace Lumify.Infrastructure.Media;

public sealed class FFMpegService : IMediaService
{
    private const int HlsSegmentSeconds = 4;
    private const string ScaleFilter = "scale=w='min(1920,iw)':h=-2";

    // Верхняя ступенька — эталон 1080p. Остальные ниже по высоте кадра, без апскейла.
    private static readonly HlsRendition[] Ladder =
    [
        new("1080", 1080, 5000, 160),
        new("720", 720, 2800, 128),
        new("480", 480, 1400, 96),
        new("360", 360, 800, 96)
    ];

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
        ClearPreviousHls(outputDir);

        IMediaAnalysis analysis = await FFProbe.AnalyseAsync(inputPath, ffOptions: null, cancellationToken: ct);
        var video = analysis.PrimaryVideoStream;
        if (video == null || video.Width <= 0 || video.Height <= 0)
        {
            throw new InvalidOperationException("В файле нет видеодорожки, HLS собрать нельзя.");
        }

        bool hasAudio = analysis.AudioStreams.Count > 0;
        IReadOnlyList<HlsRendition> renditions = SelectRenditions(video.Height);
        string variantPattern = Path.Combine(outputDir, "%v.m3u8").Replace('\\', '/');
        string segmentPattern = Path.Combine(outputDir, "%v_%03d.ts").Replace('\\', '/');

        await FFMpegArguments
            .FromFileInput(inputPath, verifyExists: false)
            .OutputToFile(variantPattern, overwrite: true, options =>
                ConfigureHlsOutput(options, renditions, hasAudio, segmentPattern))
            .CancellableThrough(ct)
            .ProcessAsynchronously(throwOnError: true);

        await EnsureMasterPlaylistAsync(outputDir, outputPath, ct);
        await NormalizePlaylistPathsAsync(outputPath, ct);
    }

    private static void ConfigureHlsOutput(
        FFMpegArgumentOptions options,
        IReadOnlyList<HlsRendition> renditions,
        bool hasAudio,
        string segmentPattern)
    {
        options
            .WithCustomArgument("-filter_complex")
            .WithCustomArgument(BuildFilter(renditions))
            .WithCustomArgument("-sn")
            .WithCustomArgument("-dn");

        for (int i = 0; i < renditions.Count; i++)
        {
            HlsRendition rendition = renditions[i];
            options
                .WithCustomArgument("-map")
                .WithCustomArgument($"[v{i}]");

            if (hasAudio)
            {
                options
                    .WithCustomArgument("-map")
                    .WithCustomArgument("0:a:0");
            }

            string preset = i == 0 ? "fast" : "veryfast";
            options
                .WithCustomArgument($"-c:v:{i}")
                .WithCustomArgument("libx264")
                .WithCustomArgument($"-preset:v:{i}")
                .WithCustomArgument(preset)
                .WithCustomArgument($"-profile:v:{i}")
                .WithCustomArgument("high")
                .WithCustomArgument($"-pix_fmt:v:{i}")
                .WithCustomArgument("yuv420p")
                .WithCustomArgument($"-b:v:{i}")
                .WithCustomArgument(Kbps(rendition.VideoKbps))
                .WithCustomArgument($"-maxrate:v:{i}")
                .WithCustomArgument(Kbps((int)Math.Ceiling(rendition.VideoKbps * 1.07)))
                .WithCustomArgument($"-bufsize:v:{i}")
                .WithCustomArgument(Kbps(rendition.VideoKbps * 2));

            if (hasAudio)
            {
                options
                    .WithCustomArgument($"-c:a:{i}")
                    .WithCustomArgument("aac")
                    .WithCustomArgument($"-b:a:{i}")
                    .WithCustomArgument(Kbps(rendition.AudioKbps))
                    .WithCustomArgument($"-ac:a:{i}")
                    .WithCustomArgument("2")
                    .WithCustomArgument($"-ar:a:{i}")
                    .WithCustomArgument("48000");
            }
        }

        string streamMap = string.Join(" ", renditions.Select((rendition, i) =>
            hasAudio
                ? $"v:{i},a:{i},name:{rendition.Name}"
                : $"v:{i},name:{rendition.Name}"));

        options
            .WithCustomArgument("-force_key_frames")
            .WithCustomArgument($"expr:gte(t,n_forced*{HlsSegmentSeconds})")
            .WithCustomArgument("-sc_threshold")
            .WithCustomArgument("0")
            .ForceFormat("hls")
            .WithCustomArgument("-hls_time")
            .WithCustomArgument(HlsSegmentSeconds.ToString(CultureInfo.InvariantCulture))
            .WithCustomArgument("-hls_playlist_type")
            .WithCustomArgument("vod")
            .WithCustomArgument("-hls_list_size")
            .WithCustomArgument("0")
            .WithCustomArgument("-hls_flags")
            .WithCustomArgument("independent_segments")
            .WithCustomArgument("-master_pl_name")
            .WithCustomArgument("index.m3u8")
            .WithCustomArgument("-var_stream_map")
            .WithCustomArgument(Quote(streamMap))
            .WithCustomArgument("-hls_segment_filename")
            .WithCustomArgument(Quote(segmentPattern));
    }

    private static IReadOnlyList<HlsRendition> SelectRenditions(int sourceHeight)
    {
        List<HlsRendition> list = Ladder.Where(rendition => rendition.Height <= sourceHeight).ToList();
        int evenSource = sourceHeight - (sourceHeight % 2);
        bool needsSourceRung = list.Count == 0 || evenSource - list[0].Height >= 96;
        if (needsSourceRung && evenSource >= 2 && (list.Count == 0 || list[0].Height != evenSource))
        {
            int videoKbps = Math.Clamp((int)Math.Round(5000d * evenSource / 1080d), 400, 8000);
            int audioKbps = evenSource >= 720 ? 128 : 96;
            list.Insert(0, new HlsRendition(evenSource.ToString(CultureInfo.InvariantCulture), evenSource, videoKbps, audioKbps));
        }

        return list;
    }

    private static string BuildFilter(IReadOnlyList<HlsRendition> renditions)
    {
        if (renditions.Count == 1)
        {
            return $"[0:v]scale=-2:{renditions[0].Height}:flags=lanczos[v0]";
        }

        string splits = string.Concat(renditions.Select((_, i) => $"[s{i}]"));
        string scales = string.Join(";", renditions.Select((rendition, i) =>
            $"[s{i}]scale=-2:{rendition.Height}:flags=lanczos[v{i}]"));
        return $"[0:v]split={renditions.Count}{splits};{scales}";
    }

    private static async Task EnsureMasterPlaylistAsync(
        string outputDir, string outputPath, CancellationToken ct)
    {
        if (await IsMasterPlaylistAsync(outputPath, ct))
        {
            return;
        }

        string? found = null;
        foreach (string candidate in Directory.EnumerateFiles(outputDir, "*.m3u8"))
        {
            if (await IsMasterPlaylistAsync(candidate, ct))
            {
                found = candidate;
                break;
            }
        }

        if (found == null)
        {
            throw new InvalidOperationException("FFmpeg не записал мастер-плейлист HLS с вариантами качества.");
        }

        if (!string.Equals(Path.GetFullPath(found), Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
        {
            File.Move(found, outputPath, overwrite: true);
        }
    }

    private static async Task<bool> IsMasterPlaylistAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        string[] lines = await File.ReadAllLinesAsync(path, ct);
        return lines.Any(line => line.StartsWith("#EXT-X-STREAM-INF", StringComparison.Ordinal));
    }

    private static async Task NormalizePlaylistPathsAsync(string playlistPath, CancellationToken ct)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(playlistPath))!;
        bool master = await RewritePlaylistAsync(playlistPath, directory, ct);
        if (!master)
        {
            return;
        }

        foreach (string line in await File.ReadAllLinesAsync(playlistPath, ct))
        {
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            string variantPath = Path.Combine(directory, line.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(variantPath))
            {
                await RewritePlaylistAsync(variantPath, Path.GetDirectoryName(variantPath)!, ct);
            }
        }
    }

    private static async Task<bool> RewritePlaylistAsync(
        string playlistPath, string directory, CancellationToken ct)
    {
        string[] lines = await File.ReadAllLinesAsync(playlistPath, ct);
        bool master = lines.Any(line => line.StartsWith("#EXT-X-STREAM-INF", StringComparison.Ordinal));

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            lines[i] = master
                ? ToRelativeUri(directory, line)
                : Path.GetFileName(line.Split('?')[0]);
        }

        await File.WriteAllLinesAsync(playlistPath, lines, ct);
        return master;
    }

    private static string ToRelativeUri(string directory, string reference)
    {
        string cleaned = reference.Replace('\\', '/');
        if (!Path.IsPathRooted(reference))
        {
            return cleaned;
        }

        string relative = Path.GetRelativePath(directory, Path.GetFullPath(reference));
        return relative.Replace('\\', '/');
    }

    private static void ClearPreviousHls(string outputDir)
    {
        foreach (string file in Directory.EnumerateFiles(outputDir))
        {
            string extension = Path.GetExtension(file);
            if (extension.Equals(".m3u8", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".ts", StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(file);
            }
        }
    }

    private static string Kbps(int value) => value.ToString(CultureInfo.InvariantCulture) + "k";

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    private sealed record HlsRendition(string Name, int Height, int VideoKbps, int AudioKbps);
}
