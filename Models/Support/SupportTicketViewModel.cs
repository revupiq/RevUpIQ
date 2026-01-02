using RevUpIQ.Admin.Models.Users;
using YourApp.Models.Support;

public sealed class SupportTicketViewModel
{
    public Ticket Ticket { get; set; } = null!;
    public List<TicketMessage> Messages { get; set; } = new();
    public UserProfileViewModel? UserProfile { get; set; }
}