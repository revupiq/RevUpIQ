using System;
using System.Collections.Generic;

namespace RevUpIQ.Admin.Models.Users
{
    public class AuthUsersResponse
    {
        public List<AuthUser> Users { get; set; } = new();
    }

    public class AuthUser
    {
        public string Id { get; set; } = "";
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Username { get; set; }
        public DateTime Created_At { get; set; }
        public DateTime? Last_Sign_In_At { get; set; }

        public DateTime? Banned_Until { get; set; }

        // Filled in by the admin panel, not part of the Supabase auth payload
        public bool HasProfile { get; set; }
    }

}
