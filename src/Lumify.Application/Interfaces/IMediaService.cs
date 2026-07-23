using FFMpegCore;

namespace Lumify.Application.Interfaces
{
    public interface IMediaService
    {
        Task ProcessVideoAsync(string inputPath, string outputPath, string previewPath, bool shouldExtractPreview, TimeSpan duration, CancellationToken ct);

        Task<IMediaAnalysis> GetMediaAnalysisAsync(string inputPath,  CancellationToken ct);
    }
}
