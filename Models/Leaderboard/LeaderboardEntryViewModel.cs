using RevUpIQ.Admin.Models.Users;

namespace RevUpIQ.Admin.Models.Leaderboard;

public class LeaderboardEntryViewModel
{
    public required UserProfileViewModel User { get; init; }
    public double TotalPoints { get; init; }
    public int Rank { get; init; }
}
