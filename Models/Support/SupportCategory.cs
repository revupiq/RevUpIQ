using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace YourApp.Models.Support
{
    [Table("support_categories")]
    public sealed class SupportCategory : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("sort_order")]
        public int SortOrder { get; set; }
    }
}
