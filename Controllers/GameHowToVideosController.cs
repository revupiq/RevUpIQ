using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevUpIQ.Admin.Infrastructure;

namespace RevUpIQ.Admin.Controllers
{
    // Edits public.game_how_to_play. Writes go through the server-side service-role
    // connection only; the credentials are never sent to the browser.
    [Authorize]
    public class GameHowToVideosController : Controller
    {
        private static readonly (string Key, string Title)[] Games =
        {
            ("trivia", "Trivia"),
            ("photoGuess", "Photo Guess"),
            ("raceWord", "Race Word"),
        };

        private static readonly Regex HttpsUrl =
            new(@"^https://[^\s]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private const int MaxTextLength = 10000;
        private const int MaxUrlLength = 2048;

        [HttpGet]
        public IActionResult Index()
        {
            ViewBag.Games = Games;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Data()
        {
            using var http = CreateClient();

            var res = await http.GetAsync(
                "rest/v1/game_how_to_play?select=game_type,video_link,instructions,scoring_text,example_text,updated_at");
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"GameHowToVideos load failed: {(int)res.StatusCode} {body}");

                var message = body.Contains("PGRST205")
                    ? "The game_how_to_play table does not exist yet. Apply the 202610140003_game_how_to_play.sql migration first."
                    : "Could not load the how-to videos.";

                return StatusCode(500, new { error = message });
            }

            var rows = JsonSerializer.Deserialize<List<GameHowToRow>>(body, JsonOptions) ?? new();

            var result = Games.Select(g =>
            {
                var row = rows.FirstOrDefault(r => r.Game_Type == g.Key);
                return new
                {
                    gameType = g.Key,
                    title = g.Title,
                    exists = row != null,
                    videoLink = row?.Video_Link,
                    instructions = row?.Instructions ?? "",
                    scoringText = row?.Scoring_Text ?? "",
                    exampleText = row?.Example_Text ?? "",
                    updatedAt = row?.Updated_At
                };
            });

            return Json(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save([FromBody] SaveGameHowToRequest request)
        {
            if (request == null || !Games.Any(g => g.Key == request.GameType))
                return BadRequest(new { error = "Unknown game." });

            var videoLink = string.IsNullOrWhiteSpace(request.VideoLink) ? null : request.VideoLink.Trim();

            if (videoLink != null)
            {
                if (videoLink.Length > MaxUrlLength
                    || !HttpsUrl.IsMatch(videoLink)
                    || !Uri.TryCreate(videoLink, UriKind.Absolute, out var uri)
                    || uri.Scheme != Uri.UriSchemeHttps
                    || string.IsNullOrEmpty(uri.Host))
                {
                    return BadRequest(new { error = "Video URL must be a full https:// link." });
                }
            }

            var instructions = request.Instructions ?? "";
            var scoring = request.ScoringText ?? "";
            var example = request.ExampleText ?? "";

            if (instructions.Length > MaxTextLength || scoring.Length > MaxTextLength || example.Length > MaxTextLength)
                return BadRequest(new { error = $"Text fields are limited to {MaxTextLength} characters." });

            var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["video_link"] = videoLink,
                ["instructions"] = instructions,
                ["scoring_text"] = scoring,
                ["example_text"] = example
            });

            using var http = CreateClient();
            using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

            // Only the selected game's row is touched
            var url = $"rest/v1/game_how_to_play?game_type=eq.{Uri.EscapeDataString(request.GameType)}";
            using var msg = new HttpRequestMessage(HttpMethod.Patch, url) { Content = content };
            msg.Headers.Add("Prefer", "return=representation");

            var res = await http.SendAsync(msg);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"GameHowToVideos save failed: {(int)res.StatusCode} {body}");
                return StatusCode(500, new { error = "Saving failed. Please try again." });
            }

            var rows = JsonSerializer.Deserialize<List<GameHowToRow>>(body, JsonOptions) ?? new();
            var saved = rows.FirstOrDefault();

            if (saved == null)
                return NotFound(new { error = "This game has no row in game_how_to_play yet." });

            return Json(new { updatedAt = saved.Updated_At });
        }

        private static HttpClient CreateClient()
        {
            var http = new HttpClient { BaseAddress = new Uri(ApiClientFactory.BaseUrl.TrimEnd('/') + "/") };
            http.DefaultRequestHeaders.Add("apikey", ApiClientFactory.ServiceRole);
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClientFactory.ServiceRole);
            return http;
        }

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private class GameHowToRow
        {
            public string Game_Type { get; set; } = "";
            public string? Video_Link { get; set; }
            public string Instructions { get; set; } = "";
            public string Scoring_Text { get; set; } = "";
            public string Example_Text { get; set; } = "";
            public DateTime? Updated_At { get; set; }
        }

        public class SaveGameHowToRequest
        {
            public string GameType { get; set; } = "";
            public string? VideoLink { get; set; }
            public string? Instructions { get; set; }
            public string? ScoringText { get; set; }
            public string? ExampleText { get; set; }
        }
    }
}
