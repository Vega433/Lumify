namespace Lumify.Domain.Entities
{
    public class Comment
    {
        public Guid CommentId { get; private set; }
        public Guid VideoId { get; private set; }
        public Guid AuthorId { get; private set; } 
        public string Text { get; private set; } = null!;
        public DateTime CreatedAt { get; private set; }
        public DateTime? LastModifiedAt { get; private set; }

        public Guid? ParentCommentId { get; private set; }
        public virtual Comment? ParentComment { get; private set; }


        private readonly List<Comment> _replies = new();
        public IReadOnlyCollection<Comment> Replies => _replies.AsReadOnly();

        public virtual User Author { get; private set; } = null!;
        public virtual Video Video { get; private set; } = null!;

        private Comment() { } 

        public Comment(Guid authorId, Guid videoId, string text, Guid? parentCommentId = null)
        {
            ValidateText(text);

            CommentId = Guid.NewGuid();
            AuthorId = authorId;
            VideoId = videoId;
            Text = text;
            CreatedAt = DateTime.UtcNow;
            ParentCommentId = parentCommentId;
        }

        public void AddReply(Guid authorId, string text)
        {
            Comment reply = new Comment(authorId, this.VideoId, text, this.CommentId);
            _replies.Add(reply);
        }

        public void Update(string newText)
        {
            ValidateText(newText);

            if (Text == newText) return; 

            Text = newText;
            LastModifiedAt = DateTime.UtcNow;
        }

        private void ValidateText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Comment cannot be empty");
            if (text.Length > 2000)
                throw new ArgumentException("The comment is too long");
        }
    }
}
