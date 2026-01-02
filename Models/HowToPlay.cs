using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace RevUpIQ.Admin.Models
{
    [Table("how_to_play")]
    public class HowToPlay : BaseModel
    {
        [PrimaryKey("id", false)]
        public long Id { get; set; }

        [Column("created_at")]
        public DateTime Created_At { get; set; }

        [Column("video_link")]
        public string? Video_Link { get; set; }

        [Column("trivia_text")]
        public string Trivia_Text { get; set; } = string.Empty;

        [Column("photo_guess_text")]
        public string Photo_Guess_Text { get; set; } = string.Empty;

        [Column("race_word_text")]
        public string Race_Word_Text { get; set; } = string.Empty;
    }
}
