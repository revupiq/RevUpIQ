using RevUpIQ.Admin.Models.Users;

namespace YourApp.Models.Support
{
    public sealed class SupportTicketsIndexViewModel
    {
        public List<Ticket> Tickets { get; set; } = new();
        public Dictionary<Guid, UserProfileViewModel?> Users { get; set; } = new();
        public Dictionary<Guid, int> UnreadUserCounts { get; set; } = new();
    }
}
