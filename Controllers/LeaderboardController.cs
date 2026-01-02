using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RevUpIQ.Admin.Models.Division;
using RevUpIQ.Admin.Models.MiniGames;
using RevUpIQ.Admin.Models.Users;

namespace RevUpIQ.Admin.Controllers
{
    public class LeaderboardController : Controller
    {
        private readonly Supabase.Client _supabase;

        public LeaderboardController(Supabase.Client supabase)
        {
            _supabase = supabase;
        }
        [HttpGet]
        public async Task<IActionResult> IndexAsync()
        {
            var result = await _supabase.From<DivisionDto>().Get();
            var divisions = result.Models.ToList();
            return View("Index", divisions);
        }

        [HttpGet]
        public async Task<IActionResult> LoadLeaderboard(GameModeType mode, LeaderboardTimeType time, int divisionId)
        {
            Console.WriteLine($"[LoadLeaderboard] mode={mode}, time={time}, divisionId={divisionId}");

            var ids = await GetLeaderboardUserIdsAsync(mode, time, divisionId);

            var users = new List<UserProfileViewModel?>();
            var userController = new UsersController(_supabase);

            foreach (var entry in ids)
            {
                var userId = entry.Split('|')[0];   
                var user = await userController.LoadUserProfileAsync(userId);

                if (user != null)
                    users.Add(user);
            }

            return PartialView("_LeaderboardResults", users);
        }




        private async Task<List<string>> GetLeaderboardUserIdsAsync(
            GameModeType mode,
            LeaderboardTimeType time,
            int divisionId)
        {
            try
            {
                var gameType = MapGameMode(mode);
                var timeFilter = MapTimeFilter(time);

                var parameters = new Dictionary<string, object?>
                {
                    ["p_game_type"] = gameType,
                    ["p_division_id"] = divisionId == 0 ? null : divisionId,
                    ["p_time_filter"] = timeFilter
                };

                var response = await _supabase.Rpc("get_leaderboard", parameters);

                if (string.IsNullOrWhiteSpace(response.Content))
                    return new List<string>();

                using var doc = JsonDocument.Parse(response.Content);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Array)
                    return new List<string>();

                var result = new List<string>();

                foreach (var row in root.EnumerateArray())
                {
                    if (!row.TryGetProperty("user_id", out var userIdProp)) continue;
                    if (!row.TryGetProperty("total_score", out var scoreProp)) continue;

                    var userId = userIdProp.GetString();
                    var score = scoreProp.GetInt32();

                    if (!string.IsNullOrEmpty(userId))
                        result.Add($"{userId}|{score}");
                }

                return result.Take(100).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private string? MapGameMode(GameModeType mode)
        {
            return mode switch
            {
                GameModeType.Trivia => "trivia",
                GameModeType.PhotoGuess => "photoGuess",
                GameModeType.RaceWord => "raceWord",
                GameModeType.All => null,
                _ => null
            };
        }
        private string MapTimeFilter(LeaderboardTimeType time)
        {
            return time switch
            {
                LeaderboardTimeType.Daily => "daily",
                LeaderboardTimeType.Weekly => "weekly",
                LeaderboardTimeType.Monthly => "monthly",
                _ => "daily"
            };
        }
    }
}
