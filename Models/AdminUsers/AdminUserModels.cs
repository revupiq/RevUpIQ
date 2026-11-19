using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace RevUpIQ.Admin.Models.AdminUsers
{
    // A row of public.admin_users (supabase/migrations/202611190001_admin_users.sql): an
    // admin panel account other than the owner, with the sections it may open.
    [Table("admin_users")]
    public class AdminUser : BaseModel
    {
        [PrimaryKey("id")]
        public long Id { get; set; }

        [Column("username")]
        public string Username { get; set; } = "";

        // ASP.NET Core Identity hash (see AdminSignIn); never the password itself
        [Column("password_hash")]
        public string PasswordHash { get; set; } = "";

        // Section keys from AdminAccess.Sections
        [Column("sections")]
        public string[] Sections { get; set; } = Array.Empty<string>();

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("last_login_at")]
        public DateTime? LastLoginAt { get; set; }

        [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at", ignoreOnInsert: true)]
        public DateTime UpdatedAt { get; set; }
    }

    public class AdminUserIndexViewModel
    {
        public List<AdminUser> Users { get; set; } = new();
        public string? Error { get; set; }
    }

    // Add / edit form. Password is required for a new account; on edit, empty keeps it.
    public class AdminUserForm
    {
        public long? Id { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public List<string> Sections { get; set; } = new();
        public bool IsActive { get; set; } = true;
    }
}
