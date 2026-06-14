using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace RevUpIQ.Admin.Models.Users
{
    [Table("user_game_plays")]
    public class UserGamePlay : BaseModel
    {
        [PrimaryKey("id")]
        [Column("id")]
        public long Id { get; set; }

        [Column("user_id")]
        public Guid User_Id { get; set; }

        [Column("division_id")]
        public int Division_Id { get; set; }

        [Column("game_type")]
        public string Game_Type { get; set; } = string.Empty;

        [Column("game_daily_id")]
        public long Game_Daily_Id { get; set; }

        [Column("score")]
        public double Score { get; set; }

        [Column("won")]
        public bool? Won { get; set; }

        [Column("played_date")]
        public DateTime Played_Date { get; set; }
    }
}
