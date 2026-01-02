using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace RevUpIQ.Admin.Models.Users
{
    [Table("profiles")]
    public class Profile : BaseModel
    {
        [PrimaryKey("id")]
        [Column("id")]
        public Guid Id { get; set; }


        [Column("full_name")]
        public string? Full_Name { get; set; }

        [Column("free_account")]
        public bool Free_Account { get; set; }

        [Column("username")]
        public string? Username { get; set; }


        [Column("birthday")]
        public DateTime? Birthday { get; set; }


        [Column("sex")]
        public string? Sex { get; set; }


        [Column("public_profile")]
        public bool Public_Profile { get; set; }


        [Column("country")]
        public string? Country { get; set; }


        [Column("state")]
        public string? State { get; set; }


        [Column("cover_photo_url")]
        public string? Cover_Photo_Url { get; set; }


        [Column("profile_photo_url")]
        public string? Profile_Photo_Url { get; set; }


        [Column("description")]
        public string? Description { get; set; }


        [Column("created_at")]
        public DateTime Created_At { get; set; }


        [Column("updated_at")]
        public DateTime Updated_At { get; set; }
    }
}
