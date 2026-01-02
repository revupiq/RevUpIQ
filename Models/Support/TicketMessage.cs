using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;

namespace YourApp.Models.Support
{
    [Table("ticket_messages")]
    public sealed class TicketMessage : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("ticket_id")]
        public Guid TicketId { get; set; }

        [Column("sender_id")]
        public Guid? SenderId { get; set; }

        [Column("sender_role")]
        public string SenderRole { get; set; } = "user";

        [Column("body")]
        public string Body { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("is_read")]
        public bool Is_Read { get; set; }
    }
}
