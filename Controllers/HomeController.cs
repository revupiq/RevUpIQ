using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevUpIQ.Admin.Infrastructure;
using RevUpIQ.Admin.Models;
using RevUpIQ.Admin.Models.MiniGames;
using RevUpIQ.Admin.Models.Sponsors;
using Supabase.Postgrest.Models;
using System.Diagnostics;
using static Supabase.Postgrest.Constants;

namespace RevUpIQ.Admin.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly Supabase.Client _supabase;

        public HomeController(Supabase.Client supabase)
        {
            _supabase = supabase;
        }

        public IActionResult Index()
        {
            return View();
        }

        // The dashboard's "new" badges: user suggestions (all three games) and sponsor
        // requests created since this browser last opened each page. A count that fails
        // comes back null and the dashboard leaves that badge off.
        [HttpGet]
        public async Task<IActionResult> NewSubmissionCounts()
        {
            var suggestionsSince = LastSeenCookie.Since(HttpContext, LastSeenCookie.UserSuggestions);
            var sponsorsSince = LastSeenCookie.Since(HttpContext, LastSeenCookie.SponsorRequests);

            var raceWord = CountCreatedSinceAsync<RaceWordSubmission>(suggestionsSince);
            var trivia = CountCreatedSinceAsync<TriviaSubmission>(suggestionsSince);
            var photoGuess = CountCreatedSinceAsync<PhotoGuessSubmission>(suggestionsSince);
            var sponsors = CountCreatedSinceAsync<BusinessInquiry>(sponsorsSince);
            await Task.WhenAll(raceWord, trivia, photoGuess, sponsors);

            int? suggestions = raceWord.Result is int r && trivia.Result is int t && photoGuess.Result is int p
                ? r + t + p
                : null;

            // Comments waiting for review (Racing Memories); null if the table isn't there yet
            int? reportedComments = null;
            if (User.CanAccess("comments"))
            {
                try
                {
                    reportedComments = await _supabase.From<RevUpIQ.Admin.Models.Comments.GameComment>()
                        .Filter("report_count", Operator.GreaterThan, 0)
                        .Count(CountType.Exact);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Reported comments count failed: {ex.Message}");
                }
            }

            // Only for the sections this account can open
            return Json(new
            {
                suggestions = User.CanAccess("suggestions") ? suggestions : null,
                sponsorRequests = User.CanAccess("sponsor_requests") ? sponsors.Result : null,
                reportedComments
            });
        }

        // A HEAD request with count=exact: only the number comes back, not the rows
        private async Task<int?> CountCreatedSinceAsync<TModel>(DateTimeOffset since) where TModel : BaseModel, new()
        {
            try
            {
                return await _supabase.From<TModel>()
                    .Filter("created_at", Operator.GreaterThan,
                        since.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture))
                    .Count(CountType.Exact);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"New submission count failed for {typeof(TModel).Name}: {ex.Message}");
                return null;
            }
        }
    }
}
