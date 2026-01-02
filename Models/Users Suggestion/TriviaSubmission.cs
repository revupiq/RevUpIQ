using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace RevUpIQ.Admin.Models.MiniGames
{
    [Table("trivia_submissions")]
    public class TriviaSubmission : BaseModel
    {
        [PrimaryKey("id")]
        public long Id { get; set; }

        [Column("creator_id")]
        public Guid Creator_Id { get; set; }

        [Column("question")]
        public string Question { get; set; }

        [Column("correct_answer")]
        public string Correct_Answer { get; set; }

        [Column("incorrect_answer_1")]
        public string Incorrect_Answer_1 { get; set; }

        [Column("incorrect_answer_2")]
        public string Incorrect_Answer_2 { get; set; }

        [Column("incorrect_answer_3")]
        public string Incorrect_Answer_3 { get; set; }

        [Column("created_at")]
        public DateTime? Created_At { get; set; }

        [Column("updated_at")]
        public DateTime? Updated_At { get; set; }
    }
}
