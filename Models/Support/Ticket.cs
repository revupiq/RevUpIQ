using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;

namespace YourApp.Models.Support
{
    [Table("tickets")]
    public sealed class Ticket : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("status")]
        public string Status { get; set; } = "open";

        [Column("subject")]
        public string? Subject { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }
    }
}

