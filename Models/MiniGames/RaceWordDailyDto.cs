using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

[Table("raceword_daily")]
public class RaceWordDailyDto : BaseModel
{
    [PrimaryKey("id")]
    public long Id { get; set; }

    [Column("division_id")]
    public long DivisionId { get; set; }

    [Column("game_date")]
    public DateTime GameDate { get; set; }

    [Column("target_word")]
    public string TargetWord { get; set; } = string.Empty;
}