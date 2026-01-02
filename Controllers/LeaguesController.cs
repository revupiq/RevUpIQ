using Microsoft.AspNetCore.Mvc;
using RevUpIQ.Admin.Models.Leagues;
using RevUpIQ.Admin.Models.Users;
using Supabase;

namespace RevUpIQ.Admin.Controllers
{
    public class LeaguesController : Controller
    {
        private readonly Client _supabase;
        private const int PageSize = 10;

        public LeaguesController(Client supabase)
        {
            _supabase = supabase;
        }

        public async Task<IActionResult> Index(int page = 1)
        {
            if (page < 1) page = 1;

            var from = (page - 1) * PageSize;
            var to = from + PageSize - 1;

            var res = await _supabase
                .From<LeagueDto>()
                .Range(from, to)
                .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                .Get();

            ViewBag.Page = page;
            ViewBag.HasNext = res.Models.Count == PageSize;

            return View(res.Models);
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var league = await _supabase
                .From<LeagueDto>()
                .Where(x => x.Id == id)
                .Single();

            if (league == null)
                return NotFound();

            Profile? owner = null;
            var ownerRes = await _supabase
                .From<Profile>()
                .Where(p => p.Id == league.OwnerId)
                .Get();
            owner = ownerRes.Models.FirstOrDefault();

            var users = new List<Profile>();
            if (league.JoinedUsers != null && league.JoinedUsers.Length > 0)
            {
                var userIds = league.JoinedUsers
                    .Select(Guid.Parse)
                    .ToList();

                var usersRes = await _supabase
                    .From<Profile>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.In, userIds)
                    .Get();

                users = usersRes.Models.ToList();
            }

            Models.Division.DivisionDto? division = null;
            if (league.DivisionId.HasValue)
            {
                var divRes = await _supabase
                    .From<Models.Division.DivisionDto>()
                    .Where(d => d.Id == league.DivisionId.Value)
                    .Get();

                division = divRes.Models.FirstOrDefault();
            }

            var vm = new LeagueDetailsViewModel
            {
                League = league,
                Owner = owner,
                Users = users,
                Division = division
            };

            return View(vm);
        }

    }
}
