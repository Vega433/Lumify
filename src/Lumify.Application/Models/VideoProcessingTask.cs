namespace Lumify.Application.Models
{
    public record VideoProcessingTask(
        Guid VideoId,
        string TempPath,
        string? TempPreviewPath
        );
}
