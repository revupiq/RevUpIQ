using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace RevUpIQ.Admin.Models.Faqs
{
    // A row of public.faqs. The app's FAQ page lists the active questions by
    // sort_order (then id), with a chip per category to filter them.
    [Table("faqs")]
    public class Faq : BaseModel
    {
        // The categories the table allows, in the order the app shows their chips
        public static readonly string[] Categories = { "Gameplay", "Subscriptions", "Account", "General" };

        [PrimaryKey("id")]
        public long Id { get; set; }

        [Column("category")]
        public string Category { get; set; } = "General";

        [Column("question")]
        public string Question { get; set; } = "";

        [Column("answer")]
        public string Answer { get; set; } = "";

        [Column("sort_order")]
        public int SortOrder { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        // Set by the database on insert
        [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at", ignoreOnInsert: true)]
        public DateTime UpdatedAt { get; set; }
    }

    public class FaqIndexViewModel
    {
        public List<Faq> Faqs { get; set; } = new();     // every FAQ, in the app's order
        public string? Category { get; set; }            // the category shown; null for all
        public string? Error { get; set; }

        public List<Faq> Shown => Faqs.Where(f => Category == null || f.Category == Category).ToList();
    }

    // Add / edit form
    public class FaqForm
    {
        public long? Id { get; set; }
        public string? Category { get; set; }
        public string? Question { get; set; }
        public string? Answer { get; set; }
        public bool IsActive { get; set; }
        public string? ReturnCategory { get; set; }      // the list's category filter to go back to
    }

    public class ReorderFaqsRequest
    {
        public List<long> Ids { get; set; } = new();     // the shown FAQs, in their new order
        public string? Category { get; set; }            // the category shown; null for all
    }
}
