using Lumify.Application.Interfaces;
using FFMpegCore;
using FFMpegCore.Enums;

namespace Lumify.Infrastructure.Media
{
    public class FFMpegService : IMediaService
    {
        public async Task<IMediaAnalysis> GetMediaAnalysisAsync(string inputPath, CancellationToken ct) => 
            await FFProbe.AnalyseAsync(inputPath, ffOptions: null, ct);


        public async Task ProcessVideoAsync(string inputPath, string outputPath, string previewPath, bool shouldExtractPreview, TimeSpan duration, CancellationToken ct)
        {
            if (shouldExtractPreview)
            {   
                TimeSpan captureTime = duration.TotalSeconds > 1 ? TimeSpan.FromSeconds(1) : TimeSpan.Zero;
                await FFMpeg.SnapshotAsync(inputPath, previewPath, new System.Drawing.Size(1920, 1080), captureTime);
            }

            await FFMpegArguments
                .FromFileInput(inputPath)
                .OutputToFile(outputPath, overwrite: true, options => 
                    options.WithVideoCodec(VideoCodec.LibX264)
                    .WithCustomArgument("-vf \"scale='min(1920,iw)':-2\"")
                    .WithConstantRateFactor(21)
                    .WithFastStart()
                    .WithSpeedPreset(Speed.Medium))
                .CancellableThrough(ct)
                .ProcessAsynchronously();
        }
    }
}
