using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System;

namespace RevUpIQ.Admin.Models.Sponsors
{
    [Table("business_inquiries")]
    public class BusinessInquiry : BaseModel
    {
        [PrimaryKey("id", false)]
        [Column("id")]
        public long Id { get; set; }

        [Column("business_name")]
        public string Business_Name { get; set; }

        [Column("contact_name")]
        public string Contact_Name { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("phone")]
        public string Phone { get; set; }

        [Column("created_at")]
        public DateTime Created_At { get; set; }
    }
}
