namespace Lumify.Domain.Entities;

public class VideoLike
{
    public Guid UserId { get; private set; }
    public Guid VideoId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private VideoLike() { }

    public VideoLike(Guid userId, Guid videoId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User is required", nameof(userId));
        }

        if (videoId == Guid.Empty)
        {
            throw new ArgumentException("Video is required", nameof(videoId));
        }

        UserId = userId;
        VideoId = videoId;
        CreatedAt = DateTime.UtcNow;
    }
}
