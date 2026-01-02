using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace RevUpIQ.Admin.Models.MiniGames
{
    [Table("race_word_submissions")]
    public class RaceWordSubmission : BaseModel
    {
        [PrimaryKey("id")]
        public long Id { get; set; }

        [Column("creator_id")]
        public Guid Creator_Id { get; set; }

        [Column("word")]
        public string Word { get; set; }

        [Column("created_at")]
        public DateTime? Created_At { get; set; }

        [Column("updated_at")]
        public DateTime? Updated_At { get; set; }
    }
}
