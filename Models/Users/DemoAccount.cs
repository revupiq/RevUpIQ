using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace RevUpIQ.Admin.Models.Users
{
    // A row makes the user a demo account: from Settings they can reset today's games
    // and replay them as practice (the app repo's 202611080004_demo_game_resets.sql).
    // Service role only.
    [Table("demo_accounts")]
    public class DemoAccount : BaseModel
    {
        [PrimaryKey("user_id", true)]
        public Guid User_Id { get; set; }
    }
}
