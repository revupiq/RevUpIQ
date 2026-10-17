using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace RevUpIQ.Admin.Models.Users
{
    [Table("user_divisions")]
    public class UserDivision : BaseModel
    {
        [PrimaryKey("id")]
        [Column("id")]
        public long Id { get; set; }

        [Column("user_id")]
        public Guid User_Id { get; set; }

        [Column("division_id")]
        public int Division_Id { get; set; }

        [Column("subscribed_at")]
        public DateTime Subscribed_At { get; set; }

        // "monthly" or "annual"
        [Column("subscription_type")]
        public string? Subscription_Type { get; set; }

        // Raw store token the app saved at purchase; see StoreReceipt
        [Column("purchase_token")]
        public string? Purchase_Token { get; set; }

        // When access ends. The store's expiry once verified; before that the database
        // fills it with subscribed_at + 30/365 days, so check Verified_At before trusting it
        [Column("ends_at")]
        public DateTime? Ends_At { get; set; }

        // Set only when Google Play or the App Store confirmed this subscription
        [Column("verified_at")]
        public DateTime? Verified_At { get; set; }

        // "google" or "apple"
        [Column("store")]
        public string? Store { get; set; }

        // Store's own state: active, canceled, in_grace_period, on_hold, expired, revoked...
        [Column("store_status")]
        public string? Store_Status { get; set; }

        // Renewal state from the stores (null until verified on the server)
        [Column("auto_renew_enabled")]
        public bool? Auto_Renew_Enabled { get; set; }

        [Column("billing_issue")]
        public bool? Billing_Issue { get; set; }

        [Column("trial_ends_at")]
        public DateTime? Trial_Ends_At { get; set; }
    }
}


namespace RevUpIQ.Admin.Models.Users
{
    public class UserGameHistoryViewModel
    {
        public List<UserGamePlay> Games { get; set; } = new();
        public List<RevUpIQ.Admin.Models.Division.DivisionDto> Divisions { get; set; } = new();
    }
}
