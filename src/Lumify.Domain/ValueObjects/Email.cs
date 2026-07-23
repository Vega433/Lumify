using System.Net.Mail;

namespace Lumify.Domain.ValueObjects
{
    public record Email
    {
        public string Value { get; init; }

        private Email(string value) => Value = value;

        public static Email Create(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be empty", nameof(email));

            try
            {
                MailAddress address = new MailAddress(email);
                return new Email(address.Address.ToLowerInvariant().Trim());
            }
            catch (FormatException)
            {
                throw new ArgumentException("Invalid email format", nameof(email));
            }
        }
    }
}
