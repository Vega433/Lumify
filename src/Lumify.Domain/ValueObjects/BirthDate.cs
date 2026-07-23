using Lumify.Domain.Entities;

namespace Lumify.Domain.ValueObjects
{
    public record BirthDate
    {
        public DateTime Value {  get; init; }

        public int Age
        {
            get
            {
                DateTime today = DateTime.UtcNow.Date;
                int age = today.Year - Value.Year;
                if (Value.Date > today.AddYears(-age)) age--;
                return age;
            }
        }

        private BirthDate(DateTime value) => Value = value.Date;

        // для чтения из БД — без повторной валидации возраста
        public static BirthDate FromDatabase(DateTime date) => new BirthDate(date);

        public static BirthDate FromDatabase(DateOnly date) =>
            new BirthDate(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        public static BirthDate Create(DateTime date, int minAge = User.MIN_AGE)
        {
            DateTime normalized = date.Date;
            BirthDate temp = new BirthDate(normalized);

            if (normalized > DateTime.UtcNow.Date)
                throw new ArgumentException("Birth date cannot be in the future");

            if (temp.Age < minAge)
                throw new ArgumentException($"User must be at least {minAge} years old");

            return temp;
        }
    }
}
