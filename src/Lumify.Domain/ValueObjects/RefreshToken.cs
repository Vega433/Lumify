namespace Lumify.Domain.ValueObjects
{
    public class RefreshToken
    {
        public string Token { get; init; } = null!;
        public DateTime CreatedAt { get; init; }
        public DateTime ExpiresAt { get; init; }

        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

        private RefreshToken() { }

        private RefreshToken(string token, DateTime createdAt, DateTime expiresAt)
        {
            Token = token;
            CreatedAt = createdAt;
            ExpiresAt = expiresAt;
        }

        public static RefreshToken Create(string token, int daysValid = 30)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token cannot be empty");

            return new RefreshToken(token, DateTime.UtcNow, DateTime.UtcNow.AddDays(daysValid));
        }
    }
}