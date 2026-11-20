using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevUpIQ.Admin.Infrastructure;
using RevUpIQ.Admin.Models.Comments;
using RevUpIQ.Admin.Models.Division;
using RevUpIQ.Admin.Models.Users;
using static Supabase.Postgrest.Constants;

namespace RevUpIQ.Admin.Controllers
{
    // Racing Memories & Stories moderation: reported and hidden comments, hiding,
    // restoring and deleting them, the blocked-words list and the section's "Presented
    // by" sponsor. Players report comments in the app; three reports hide one until it's
    // reviewed here.
    [Authorize]
    public class CommentsController : Controller
    {
        public const string SponsorLogoKey = "stories_sponsor_logo_url";
        public const string SponsorLinkKey = "stories_sponsor_link_url";

        private const string MissingTable =
            "The comments tables don't exist yet. Apply the app repo's 202611200002_racing_memories.sql migration first.";

        private static readonly Regex WordPattern = new("^[a-z0-9]+( [a-z0-9]+)*$");

        private readonly Supabase.Client _supabase;

        public CommentsController(Supabase.Client supabase)
        {
            _supabase = supabase;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? filter)
        {
            var vm = new CommentsIndexViewModel
            {
                Filter = CommentsIndexViewModel.Filters.Contains(filter) ? filter! : "reported"
            };

            try
            {
                var query = _supabase.From<GameComment>();
                var comments = vm.Filter switch
                {
                    "reported" => (await query.Filter("report_count", Operator.GreaterThan, 0)
                        .Order("report_count", Ordering.Descending)
                        .Order("created_at", Ordering.Descending)
                        .Limit(100).Get()).Models,
                    "hidden" => (await query.Where(c => c.IsHidden)
                        .Order("created_at", Ordering.Descending)
                        .Limit(100).Get()).Models,
                    _ => (await query.Order("created_at", Ordering.Descending).Limit(100).Get()).Models,
                };

                vm.ReportedCount = await _supabase.From<GameComment>()
                    .Filter("report_count", Operator.GreaterThan, 0).Count(CountType.Exact);
                vm.HiddenCount = await _supabase.From<GameComment>()
                    .Where(c => c.IsHidden).Count(CountType.Exact);
                vm.Rows = await DescribeAsync(comments);

                var words = (await _supabase.From<CommentBlockedWord>().Order("word", Ordering.Ascending).Get()).Models;
                vm.BlockedWords = string.Join("\n", words.Select(w => w.Word));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Comments load failed: {ex.Message}");
                vm.Error = ex.Message.Contains("PGRST205") ? MissingTable : "Could not load the comments.";
            }

            vm.SponsorLogoUrl = await AppSettingsClient.GetAsync(SponsorLogoKey);
            vm.SponsorLinkUrl = await AppSettingsClient.GetAsync(SponsorLinkKey);
            return View(vm);
        }

        // Hide, or restore (which also clears its reports as reviewed)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetHidden(long id, bool hidden, string? filter)
        {
            try
            {
                var update = _supabase.From<GameComment>().Where(c => c.Id == id).Set(c => c.IsHidden, hidden);
                if (!hidden) update = update.Set(c => c.ReportCount, 0);
                await update.Update();

                if (!hidden)
                {
                    await _supabase.From<GameCommentReport>()
                        .Where(r => r.CommentId == id)
                        .Filter("reviewed_at", Operator.Is, "null")
                        .Set(r => r.ReviewedAt!, DateTime.UtcNow)
                        .Update();
                }
                TempData["Message"] = hidden
                    ? "Comment hidden: players no longer see it."
                    : "Comment restored: players see it again, and its reports are cleared.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Comment update failed: {ex.Message}");
                TempData["Error"] = "Could not change the comment. Please try again.";
            }
            return RedirectToAction(nameof(Index), new { filter });
        }

        // Deletes the comment with its replies, likes and reports
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(long id, string? filter)
        {
            try
            {
                var comment = (await _supabase.From<GameComment>().Where(c => c.Id == id).Get()).Models.FirstOrDefault();
                await _supabase.From<GameComment>().Where(c => c.Id == id).Delete();
                if (comment?.ParentId is long parentId)
                {
                    var parent = (await _supabase.From<GameComment>().Where(c => c.Id == parentId).Get()).Models.FirstOrDefault();
                    if (parent != null)
                        await _supabase.From<GameComment>().Where(c => c.Id == parentId)
                            .Set(c => c.ReplyCount, Math.Max(parent.ReplyCount - 1, 0)).Update();
                }
                TempData["Message"] = "Comment deleted.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Comment delete failed: {ex.Message}");
                TempData["Error"] = "Could not delete the comment. Please try again.";
            }
            return RedirectToAction(nameof(Index), new { filter });
        }

        // Replaces the list: one word or phrase per line (or comma-separated)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveBlockedWords(string? words, string? filter)
        {
            var list = (words ?? "")
                .Split(new[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => Regex.Replace(w.Trim().ToLowerInvariant(), @"\s+", " "))
                .Where(w => w.Length > 0)
                .Distinct()
                .ToList();

            var bad = list.Where(w => w.Length > 40 || !WordPattern.IsMatch(w)).ToList();
            if (bad.Count > 0)
            {
                TempData["Error"] = $"Use only letters and digits (and single spaces), up to 40 characters: {string.Join(", ", bad.Take(5))}";
                return RedirectToAction(nameof(Index), new { filter });
            }

            try
            {
                await _supabase.From<CommentBlockedWord>().Where(w => w.Word != "").Delete();
                if (list.Count > 0)
                    await _supabase.From<CommentBlockedWord>().Insert(list.Select(w => new CommentBlockedWord { Word = w }).ToList());
                TempData["Message"] = $"Blocked words saved ({list.Count}). New comments with any of them are refused.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Blocked words save failed: {ex.Message}");
                TempData["Error"] = "Could not save the blocked words. Please try again.";
            }
            return RedirectToAction(nameof(Index), new { filter });
        }

        // "Presented by" logo and link on the app's section; remove clears both
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSponsor(IFormFile? logo, string? linkUrl, bool remove, string? filter)
        {
            try
            {
                if (remove)
                {
                    await AppSettingsClient.SetAsync(SponsorLogoKey, null);
                    await AppSettingsClient.SetAsync(SponsorLinkKey, null);
                    TempData["Message"] = "Sponsor removed from Racing Memories & Stories.";
                    return RedirectToAction(nameof(Index), new { filter });
                }

                var link = (linkUrl ?? "").Trim();
                if (link.Length > 0 && !(Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http"))
                {
                    TempData["Error"] = "The link must be a web address starting with https://.";
                    return RedirectToAction(nameof(Index), new { filter });
                }

                if (logo != null && logo.Length > 0)
                {
                    if (!logo.ContentType.StartsWith("image/"))
                    {
                        TempData["Error"] = "The logo must be an image (PNG, JPG or WebP).";
                        return RedirectToAction(nameof(Index), new { filter });
                    }
                    var fileName = $"stories_sponsor_{Guid.NewGuid()}{Path.GetExtension(logo.FileName)}";
                    using var ms = new MemoryStream();
                    await logo.CopyToAsync(ms);
                    await _supabase.Storage.From("sponsor-bar").Upload(ms.ToArray(), fileName);
                    await AppSettingsClient.SetAsync(SponsorLogoKey, _supabase.Storage.From("sponsor-bar").GetPublicUrl(fileName));
                }
                else if (await AppSettingsClient.GetAsync(SponsorLogoKey) == null)
                {
                    TempData["Error"] = "Choose the sponsor's logo.";
                    return RedirectToAction(nameof(Index), new { filter });
                }

                await AppSettingsClient.SetAsync(SponsorLinkKey, link.Length > 0 ? link : null);
                TempData["Message"] = "Sponsor saved. The app shows it from its next start.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Stories sponsor save failed: {ex.Message}");
                TempData["Error"] = "Could not save the sponsor. Please try again.";
            }
            return RedirectToAction(nameof(Index), new { filter });
        }

        // Author, division and question for each comment, plus its unreviewed reports
        private async Task<List<CommentRow>> DescribeAsync(List<GameComment> comments)
        {
            if (comments.Count == 0) return new();

            var userIds = comments.Select(c => c.UserId).Distinct().ToList();
            var names = (await _supabase.From<Profile>().Filter("id", Operator.In, userIds).Get())
                .Models.ToDictionary(p => p.Id, p => string.IsNullOrWhiteSpace(p.Username) ? "(no username)" : p.Username!);

            var divisions = (await _supabase.From<DivisionDto>().Get()).Models.ToDictionary(d => (long)d.Id, d => d.Name);

            var commentIds = comments.Select(c => c.Id).ToList();
            var reports = (await _supabase.From<GameCommentReport>()
                    .Filter("comment_id", Operator.In, commentIds)
                    .Filter("reviewed_at", Operator.Is, "null")
                    .Get()).Models
                .GroupBy(r => r.CommentId)
                .ToDictionary(g => g.Key, g => g.Select(r => string.IsNullOrWhiteSpace(r.Reason) ? "(no reason)" : r.Reason!).ToList());

            var questions = new Dictionary<(string, long), (string Text, DateTime Date)>();
            async Task LoadAsync<T>(string type, Func<T, long> id, Func<T, string> text, Func<T, DateTime> date)
                where T : Supabase.Postgrest.Models.BaseModel, new()
            {
                var ids = comments.Where(c => c.GameType == type).Select(c => c.GameDailyId).Distinct().ToList();
                if (ids.Count == 0) return;
                foreach (var q in (await _supabase.From<T>().Filter("id", Operator.In, ids).Get()).Models)
                    questions[(type, id(q))] = (text(q), date(q));
            }
            await LoadAsync<TriviaDailyDto>("trivia", q => q.Id, q => q.Question, q => q.GameDate);
            await LoadAsync<PhotoGuessDailyDto>("photoGuess", q => q.Id, q => q.Question, q => q.GameDate);
            await LoadAsync<RaceWordDailyDto>("raceWord", q => q.Id, q => $"Word: {q.TargetWord}", q => q.GameDate);

            return comments.Select(c =>
            {
                questions.TryGetValue((c.GameType, c.GameDailyId), out var q);
                return new CommentRow
                {
                    Comment = c,
                    Author = names.GetValueOrDefault(c.UserId, "(deleted player)"),
                    Game = c.GameType switch { "trivia" => "Trivia", "photoGuess" => "Photo Guess", _ => "RaceWord" },
                    Question = q.Text ?? "(question deleted)",
                    GameDate = q.Text == null ? null : q.Date,
                    Division = divisions.GetValueOrDefault(c.DivisionId, ""),
                    Reasons = reports.GetValueOrDefault(c.Id, new List<string>()),
                };
            }).ToList();
        }
    }
}
