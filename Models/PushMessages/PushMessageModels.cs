using System.Text.RegularExpressions;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace RevUpIQ.Admin.Models.PushMessages
{
    // A row of public.push_messages (the app repo's 202611190001_push_messages.sql): a
    // text the daily-reminders function sends as the daily game reminder.
    [Table("push_messages")]
    public class PushMessage : BaseModel
    {
        [PrimaryKey("id")]
        public long Id { get; set; }

        [Column("kind")]
        public string Kind { get; set; } = "general";

        [Column("title")]
        public string Title { get; set; } = "";

        [Column("body")]
        public string Body { get; set; } = "";

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; }

        [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at", ignoreOnInsert: true)]
        public DateTime UpdatedAt { get; set; }
    }

    // The four kinds of reminder text, as the daily-reminders function picks them
    public record PushMessageKind(string Key, string Label, string WhenSent, string[] Placeholders);

    public record Placeholder(string Name, string Meaning, string Sample);

    public static class PushMessageTexts
    {
        // As the table's checks; long texts get cut off on the lock screen anyway
        public const int MaxTitleLength = 65;
        public const int MaxBodyLength = 178;

        public static readonly PushMessageKind[] Kinds =
        {
            new("general", "Today's games",
                "Today's games are waiting for the player.",
                new[] { "division", "games_left" }),
            new("competitive", "Others have played",
                "Other players have already played today's games.",
                new[] { "division", "games_left", "players" }),
            new("first_player", "Nobody has played yet",
                "Sent instead of \"Others have played\" while nobody has played today's games.",
                new[] { "division", "games_left" }),
            new("streak", "Streak on the line",
                "The player has won every game 2 or more days in a row. Only sent to players on a streak.",
                new[] { "division", "games_left", "streak" }),
        };

        public static readonly Placeholder[] Placeholders =
        {
            new("division", "the division's name", "Dirt Late Models"),
            new("games_left", "games left to play today: \"2 games\" or \"1 game\"", "2 games"),
            new("players", "players who played today: \"12 players\" or \"1 player\"", "12 players"),
            new("streak", "days in a row the player won every game", "4"),
        };

        private static readonly Regex Token = new(@"\{([^{}]*)\}");

        public static PushMessageKind? FindKind(string? key) =>
            Kinds.FirstOrDefault(k => string.Equals(k.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase));

        // {names} in the text that this kind doesn't fill in
        public static List<string> UnknownPlaceholders(PushMessageKind kind, string text) =>
            Token.Matches(text)
                .Select(m => m.Groups[1].Value)
                .Where(name => !kind.Placeholders.Contains(name.Trim().ToLowerInvariant()) || name != name.Trim())
                .Distinct()
                .ToList();

        // The text with sample values, as a player would see it
        public static string Preview(string text) =>
            Token.Replace(text, m => Placeholders.FirstOrDefault(p => p.Name == m.Groups[1].Value.ToLowerInvariant())?.Sample ?? m.Value);
    }

    public class PushMessageIndexViewModel
    {
        public List<PushMessage> Messages { get; set; } = new();
        public string? Error { get; set; }
    }

    // Add / edit form
    public class PushMessageForm
    {
        public long? Id { get; set; }
        public string? Kind { get; set; }
        public string? Title { get; set; }
        public string? Body { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
