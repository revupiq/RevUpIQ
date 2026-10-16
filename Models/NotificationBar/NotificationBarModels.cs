using System.Text.Json.Serialization;

namespace RevUpIQ.Admin.Models.NotificationBar
{
    // A row of public.notification_bar as returned by PostgREST
    public class NotificationBarRow
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";

        [JsonPropertyName("link_url")]
        public string? Link_Url { get; set; }

        [JsonPropertyName("link_label")]
        public string? Link_Label { get; set; }

        [JsonPropertyName("starts_at")]
        public DateTimeOffset Starts_At { get; set; }

        [JsonPropertyName("ends_at")]
        public DateTimeOffset Ends_At { get; set; }

        [JsonPropertyName("is_active")]
        public bool Is_Active { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset Created_At { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset Updated_At { get; set; }

        // What app users see right now
        public NotificationBarStatus StatusAt(DateTimeOffset now)
        {
            if (!Is_Active) return NotificationBarStatus.Inactive;
            if (now < Starts_At) return NotificationBarStatus.Scheduled;
            if (now >= Ends_At) return NotificationBarStatus.Ended;
            return NotificationBarStatus.Live;
        }
    }

    public enum NotificationBarStatus
    {
        Live,
        Scheduled,
        Ended,
        Inactive
    }

    // Create/edit form. Dates are datetime-local values in the panel's display
    // time zone (UTC-7), kept as text so binding never depends on server culture.
    public class NotificationBarForm
    {
        public Guid? Id { get; set; }
        public string? Message { get; set; }
        public string? LinkUrl { get; set; }
        public string? LinkLabel { get; set; }
        public string? StartsAt { get; set; }
        public string? EndsAt { get; set; }
        public bool IsActive { get; set; }
    }
}
