using Lumify.Application.Models;

namespace Lumify.Application.Interfaces
{
    public interface IVideoProcessingQueue
    {
        ValueTask QueueVideoAsync(VideoProcessingTask task);

        IAsyncEnumerable<VideoProcessingTask> ReadAllAsync(CancellationToken ct);
    }
}
