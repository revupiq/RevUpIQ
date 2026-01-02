using RevUpIQ.Admin.Models.Division;
using RevUpIQ.Admin.Models.Leagues;
using RevUpIQ.Admin.Models.Users;

namespace RevUpIQ.Admin.Models.Leagues
{
    public class LeagueDetailsViewModel
    {
        public LeagueDto League { get; set; }
        public DivisionDto Division { get; set; }
        public Profile Owner { get; set; }
        public List<Profile> Users { get; set; } = new();
    }
}
