using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace RevUpIQ.Admin.Models.Comments
{
    // Racing Memories & Stories: players' comments on a daily question (the app repo's
    // supabase/migrations/202611200002_racing_memories.sql). The panel reads and moderates
    // them with the service role; players only go through the database functions.
    [Table("game_comments")]
    public class GameComment : BaseModel
    {
        [PrimaryKey("id")]
        public long Id { get; set; }

        [Column("game_type")]
        public string GameType { get; set; } = "";

        [Column("game_daily_id")]
        public long GameDailyId { get; set; }

        [Column("division_id")]
        public long DivisionId { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("parent_id")]
        public long? ParentId { get; set; }

        [Column("body")]
        public string Body { get; set; } = "";

        [Column("like_count")]
        public int LikeCount { get; set; }

        [Column("reply_count")]
        public int ReplyCount { get; set; }

        // Reports not yet reviewed; three hide the comment
        [Column("report_count")]
        public int ReportCount { get; set; }

        [Column("is_hidden")]
        public bool IsHidden { get; set; }

        [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
        public DateTime CreatedAt { get; set; }
    }

    [Table("game_comment_reports")]
    public class GameCommentReport : BaseModel
    {
        [PrimaryKey("id")]
        public long Id { get; set; }

        [Column("comment_id")]
        public long CommentId { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("reason")]
        public string? Reason { get; set; }

        [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
        public DateTime CreatedAt { get; set; }

        [Column("reviewed_at")]
        public DateTime? ReviewedAt { get; set; }
    }

    // A comment containing one of these whole words (or phrases) is refused
    [Table("comment_blocked_words")]
    public class CommentBlockedWord : BaseModel
    {
        [PrimaryKey("word", shouldInsert: true)]
        public string Word { get; set; } = "";
    }

    public class CommentRow
    {
        public GameComment Comment { get; set; } = new();
        public string Author { get; set; } = "";
        public string Game { get; set; } = "";              // "Trivia", "Photo Guess", "RaceWord"
        public string Question { get; set; } = "";          // the question (RaceWord: its word)
        public DateTime? GameDate { get; set; }
        public string Division { get; set; } = "";
        public List<string> Reasons { get; set; } = new();  // unreviewed reports
    }

    public class CommentsIndexViewModel
    {
        public static readonly string[] Filters = { "reported", "hidden", "all" };

        public string Filter { get; set; } = "reported";
        public List<CommentRow> Rows { get; set; } = new();
        public int ReportedCount { get; set; }
        public int HiddenCount { get; set; }
        public string BlockedWords { get; set; } = "";       // one per line
        public string? SponsorLogoUrl { get; set; }
        public string? SponsorLinkUrl { get; set; }
        public string? Error { get; set; }
    }
}
