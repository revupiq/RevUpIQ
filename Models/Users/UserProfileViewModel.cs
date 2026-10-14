using System.Collections.Generic;

namespace RevUpIQ.Admin.Models.Users
{
    public class UserProfileViewModel
    {
        public Profile Profile { get; set; }
        public AuthUser AuthUser { get; set; }
        public List<UserDivision> Divisions { get; set; } = new();
        public List<UserGamePlay> GamePlays { get; set; } = new();
        public List<YourApp.Models.Support.Ticket> SupportTickets { get; set; } = new();
        public bool SupportTicketsLoadFailed { get; set; }
    }
}
