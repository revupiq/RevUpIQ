using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace RevUpIQ.Admin.Models.MiniGames
{
    [Table("mini_game_settings")]
    public class MiniGameSetting : BaseModel
    {
        [PrimaryKey("id")]
        public long Id { get; set; }

        [Column("game_mode")]
        public string GameMode { get; set; } = string.Empty; 

        [Column("timer_seconds")]
        public int TimerSeconds { get; set; }

        [Column("max_score")]
        public int MaxScore { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
