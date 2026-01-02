using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

[Table("photoguess_daily")]
public class PhotoGuessDailyDto : BaseModel
{
    [PrimaryKey("id")]
    public long Id { get; set; }

    [Column("division_id")]
    public long DivisionId { get; set; }

    [Column("game_date")]
    public DateTime GameDate { get; set; }

    [Column("question")]
    public string Question { get; set; } = string.Empty;

    [Column("image_url")]
    public string ImageUrl { get; set; } = string.Empty;

    [Column("real_answer")]
    public string RealAnswer { get; set; } = string.Empty;

    [Column("answer1")]
    public string Answer1 { get; set; } = string.Empty;

    [Column("answer2")]
    public string Answer2 { get; set; } = string.Empty;

    [Column("answer3")]
    public string Answer3 { get; set; } = string.Empty;
}
