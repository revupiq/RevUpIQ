using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace RevUpIQ.Admin.Models.Divisions
{
    [Table("division_backgrounds")]
    public class DivisionBackground : BaseModel
    {
        [PrimaryKey("id", false)]
        [Column("id")]
        public long Id { get; set; }

        [Column("division_id")]
        public long Division_Id { get; set; }

        [Column("background_url")]
        public string Background_Url { get; set; }

        [Column("created_at")]
        public DateTime Created_At { get; set; }

        [Column("updated_at")]
        public DateTime Updated_At { get; set; }
    }
}
