using Lumify.Application.Interfaces;
using Lumify.Application.Models;
using System.Threading.Channels;

namespace Lumify.Application.Services
{
    public class VideoProcessingQueue : IVideoProcessingQueue
    {
        private readonly Channel<VideoProcessingTask> _queue;

        public VideoProcessingQueue()
        {
            BoundedChannelOptions options = new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.Wait
            };

            _queue = Channel.CreateBounded<VideoProcessingTask>(options);
        }

        public async ValueTask QueueVideoAsync(VideoProcessingTask task) => await _queue.Writer.WriteAsync(task);

        public IAsyncEnumerable<VideoProcessingTask> ReadAllAsync(CancellationToken ct) => _queue.Reader.ReadAllAsync(ct);
    }
}
