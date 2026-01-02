using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace RevUpIQ.Admin.Models.Sponsors
{
    [Table("sponsor_bar")]
    public class SponsorBarEntry : BaseModel
    {
        [PrimaryKey("sponsor_id")]
        [Column("sponsor_id")]
        public Guid Sponsor_Id { get; set; }

        [Column("image_url")]
        public string Image_Url { get; set; } = "";

        [Column("link_url")]
        public string Link_Url { get; set; } = "";

        [Column("text")]
        public string Text { get; set; } = "";

        [Column("year")]
        public int Year { get; set; }

        [Column("week_number")]
        public int Week_Number { get; set; }
    }
}
