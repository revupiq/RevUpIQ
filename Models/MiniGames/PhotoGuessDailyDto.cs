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

    // Null until the admin adds an image (bulk imports can leave it for later); the app
    // skips a question without one, so it stays unpublished
    [Column("image_url")]
    public string? ImageUrl { get; set; }

    [Column("real_answer")]
    public string RealAnswer { get; set; } = string.Empty;

    [Column("answer1")]
    public string Answer1 { get; set; } = string.Empty;

    [Column("answer2")]
    public string Answer2 { get; set; } = string.Empty;

    [Column("answer3")]
    public string Answer3 { get; set; } = string.Empty;

    // Shown under the correct answer on the app's win and lose screens; null when there's none
    [Column("fun_fact")]
    public string? FunFact { get; set; }
}
