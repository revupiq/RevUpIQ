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

        // Set when the player entered the code sent to their email or phone
        public DateTime? Email_Confirmed_At { get; set; }
        public DateTime? Phone_Confirmed_At { get; set; }

        // Filled in by the admin panel, not part of the Supabase auth payload
        public bool HasProfile { get; set; }

        // Where the account is in the app's sign-up: verify an email or phone, link a
        // phone, set up a profile. Unverified sign-ups are deleted after 24 hours
        // (202610190002_expire_unverified_signups in the app repo).
        public string Registration =>
            HasProfile ? "Complete"
            : Email_Confirmed_At == null && Phone_Confirmed_At == null ? "Unverified"
            : Phone_Confirmed_At == null ? "Needs phone"
            : "Needs profile";

        // The profile's Free Account toggle: every active division, no store subscription needed
        public bool FreeAccount { get; set; }

        // "Free", "Trial", "Subscribed", "Canceling", "Billing Issue" or "Inactive" (see SubscriptionStatus)
        public string SubscriptionStatus { get; set; } = "Inactive";
    }

}
