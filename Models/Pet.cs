using System;
using SQLite;

namespace LoyalAnimal.Shared
{
    [Table("Pets")]
    public class Pet
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int UserId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Breed { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public int Age { get; set; }

        public string PhotoUrl { get; set; } = string.Empty;

        public long CreatedAtTicks { get; set; } = DateTime.UtcNow.Ticks;

        [Ignore]
        public DateTime CreatedAt
        {
            get => new DateTime(CreatedAtTicks, DateTimeKind.Utc);
            set => CreatedAtTicks = value.ToUniversalTime().Ticks;
        }
    }
}