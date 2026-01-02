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
        public DateTime Created_At { get; set; }
        public DateTime? Last_Sign_In_At { get; set; }

        public DateTime? Banned_Until { get; set; }
    }

}
