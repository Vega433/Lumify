using Lumify.Domain.ValueObjects;
using EmailAddress = Lumify.Domain.ValueObjects.Email;

namespace Lumify.Domain.Entities
{
    public class User
    {
        public const int MIN_AGE = 12;

        public Guid UserId { get; private set; }
        public string UserName { get; private set; } = null!;
        public string PasswordHash { get; private set; } = null!;
        public string Email { get; private set; } = null!;
        public string? AvatarPath { get; private set; }
        public BirthDate BirthDate { get; private set; } = null!;
        public DateTime CreatedAt { get; private set; }
        public RefreshToken? RefreshToken { get; private set; }

        private List<Video> _videos = new List<Video>();
        public IReadOnlyCollection<Video> Videos => _videos.AsReadOnly();

        private List<Comment> _comments = new List<Comment>();
        public IReadOnlyCollection<Comment> Comments => _comments.AsReadOnly();

        private User() { }

        public User(string userName, string email, DateTime birthDate, string passwordHash, string? avatarPath)
        {
            UserId = Guid.NewGuid();

            if (string.IsNullOrWhiteSpace(userName))
                throw new ArgumentNullException(nameof(userName), "The name cannot be empty");

            UserName = userName.Length >= 4 ? userName : throw new ArgumentException("The name must contain 4 or more characters.");
            Email = EmailAddress.Create(email).Value;
            PasswordHash = !string.IsNullOrWhiteSpace(passwordHash) ? passwordHash : throw new ArgumentException(nameof(passwordHash));
            AvatarPath = !string.IsNullOrWhiteSpace(avatarPath) ? avatarPath : null;
            BirthDate = BirthDate.Create(birthDate, MIN_AGE);
            CreatedAt = DateTime.UtcNow;
        }

        public void ChangeUserName(string newUserName)
        {
            if (string.IsNullOrWhiteSpace(newUserName))
                throw new ArgumentException(nameof(newUserName));
            if (newUserName.Length < 4)
                throw new ArgumentException("The name must contain 4 or more characters.");

            UserName = newUserName;
        }

        public void ChangePassword(string newPasswordHash)
        {
            if (string.IsNullOrWhiteSpace(newPasswordHash))
                throw new ArgumentException("Hash cannot be empty");

            PasswordHash = newPasswordHash;
        }

        public void ChangeEmail(string newEmail) => Email = EmailAddress.Create(newEmail).Value;

        public void ChangeBirthDate(DateTime newBirthDate) => BirthDate = BirthDate.Create(newBirthDate, MIN_AGE);

        public void ChangeAvatar(string newAvatarPath)
        {
            if (string.IsNullOrWhiteSpace(newAvatarPath))
                throw new ArgumentException("Avatar path cannot be empty");

            AvatarPath = newAvatarPath;
        }

        public void RemoveAvatar() => AvatarPath = null;

        public void UpdateToken(string token) => RefreshToken = RefreshToken.Create(token);
    }
}
