using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RevUpIQ.Admin.Infrastructure;
using RevUpIQ.Admin.Models.Division;
using RevUpIQ.Admin.Models.Leaderboard;
using RevUpIQ.Admin.Models.MiniGames;
using RevUpIQ.Admin.Models.Users;

namespace RevUpIQ.Admin.Controllers
{
    [Authorize]
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
            ViewData["LeaderboardToday"] = GameToday().ToString("yyyy-MM-dd");
            return View("Index", divisions);
        }

        [HttpGet]
        public async Task<IActionResult> LoadLeaderboard(GameModeType mode, LeaderboardTimeType time, int divisionId, DateOnly? date)
        {
            var selectedDate = date ?? GameToday();
            if (!ModelState.IsValid || !Enum.IsDefined(mode) || !Enum.IsDefined(time)
                || divisionId < 0 || selectedDate > GameToday())
                return BadRequest("Select a valid date, game mode, and division.");

            try
            {
                var scores = await GetLeaderboardScoresAsync(mode, time, divisionId, selectedDate);
                var entries = new List<LeaderboardEntryViewModel>();
                var userController = new UsersController(_supabase);

                for (var i = 0; i < scores.Count; i++)
                {
                    var user = await userController.LoadUserProfileAsync(scores[i].UserId);
                    if (user?.Profile != null)
                        entries.Add(new LeaderboardEntryViewModel
                        {
                            User = user,
                            TotalPoints = scores[i].Score,
                            Rank = i + 1
                        });
                }

                ViewData["LeaderboardDate"] = selectedDate.ToString("yyyy-MM-dd");
                ViewData["LeaderboardPeriod"] = time.ToString();
                return PartialView("_LeaderboardResults", entries);
            }
            catch (Exception ex)
            {
                HttpContext.RequestServices.GetRequiredService<ILogger<LeaderboardController>>()
                    .LogError(ex, "Failed to load leaderboard for {Date}", selectedDate);
                return StatusCode(502, "Unable to load the leaderboard. Please try again.");
            }
        }




        private static DateOnly GameToday() => DateOnly.FromDateTime(DisplayTime.Today);

        private async Task<List<(string UserId, double Score)>> GetLeaderboardScoresAsync(
            GameModeType mode,
            LeaderboardTimeType time,
            int divisionId,
            DateOnly date)
        {
                var gameType = MapGameMode(mode);
                var timeFilter = MapTimeFilter(time);

                var parameters = new Dictionary<string, object?>
                {
                    ["p_game_type"] = gameType,
                    ["p_division_id"] = divisionId == 0 ? null : divisionId,
                    ["p_time_filter"] = timeFilter,
                    ["p_date"] = date.ToString("yyyy-MM-dd")
                };

                var response = await _supabase.Rpc("get_admin_leaderboard", parameters);

                if (string.IsNullOrWhiteSpace(response.Content))
                    return new();

                using var doc = JsonDocument.Parse(response.Content);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Array)
                    throw new InvalidOperationException("Unexpected leaderboard response.");

                var result = new List<(string UserId, double Score)>();

                foreach (var row in root.EnumerateArray())
                {
                    if (!row.TryGetProperty("user_id", out var userIdProp)) continue;
                    if (!row.TryGetProperty("total_score", out var scoreProp)) continue;

                    var userId = userIdProp.GetString();
                    var score = scoreProp.GetDouble();

                    if (!string.IsNullOrEmpty(userId))
                        result.Add((userId, score));
                }

                return result.Take(100).ToList();
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
