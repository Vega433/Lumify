using Lumify.Domain.Enums;

namespace Lumify.Domain.Entities
{
    public class Video
    {
        public Guid VideoId { get; private set; }
        public Guid AuthorId { get; private set; }
        public string VideoFileName { get; private set; } = null!;
        public string? PreviewFileName { get; private set; }
        public string Title { get; private set; } = null!;
        public string? Description { get; private set; }
        public DateTime UploadAt { get; private set; }
        public long Views { get; private set; }
        public bool IsSharing { get; private set; }
        public long Likes { get; private set; }
        public TimeSpan Duration { get; private set; }
        public Status Status { get; private set; }

        private readonly List<Comment> _comments = new List<Comment>();
        public IReadOnlyCollection<Comment> Comments => _comments.AsReadOnly();

        public virtual User User { get; private set; } = null!;

        private Video() { }

        public Video(Guid authorId, string title, string? decsription, string? previewExtension, bool isSharing = false)
        {
            VideoId = Guid.NewGuid();
            AuthorId = authorId;
            VideoFileName = $"{VideoId}.mp4";
            Title = !string.IsNullOrWhiteSpace(title) ? title : throw new ArgumentException("The title cannot be empty");

            if (decsription != null && decsription.Length > 1000)
                throw new ArgumentException("description is too long");
            else
                Description = decsription ?? null;

            UploadAt = DateTime.UtcNow;
            Views = 0;
            PreviewFileName = !string.IsNullOrEmpty(previewExtension) ? $"{VideoId}{previewExtension}" : null;
            IsSharing = isSharing;
            Likes = 0;
            Duration = TimeSpan.Zero;
            Status = Status.Created;
        }

        public void AddView() => Views++;

        public void UploadTitle(string newTitle)
        {
            if (string.IsNullOrWhiteSpace(newTitle))
                throw new ArgumentException("The title cannot be empty");

            Title = newTitle;
        }

        public void UploadDescription(string? newDescription)
        {
            if (newDescription?.Length > 1000)
                throw new ArgumentException("Description is too long");

            Description = string.IsNullOrWhiteSpace(newDescription) ? null : newDescription;
        }

        public void like() => Likes++;

        public void UpdateStatus(Status newStatus) => Status = newStatus;

        public void SetPreview(string extension = ".jpg") => PreviewFileName = $"{VideoId}{extension}";

        public void SetDuration(TimeSpan duration) => Duration = duration;
    } 
}
