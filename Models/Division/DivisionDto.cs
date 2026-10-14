using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace RevUpIQ.Admin.Models.Division
{
    [Table("divisions")]
    public class DivisionDto : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("logo_url")]
        public string? LogoUrl { get; set; }

        [Column("purchase_icon_url")]
        public string? PurchaseIconUrl { get; set; }

        [Column("sort_index")]
        public int SortIndex { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }
}
