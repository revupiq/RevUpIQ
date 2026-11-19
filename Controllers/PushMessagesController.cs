using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevUpIQ.Admin.Models.PushMessages;
using static Supabase.Postgrest.Constants;

namespace RevUpIQ.Admin.Controllers
{
    // Manages public.push_messages, the daily reminder texts. The daily-reminders function
    // reads the active ones on each run, so changes apply from the next day's reminders.
    // Writes go through the server-side service-role connection only.
    [Authorize]
    public class PushMessagesController : Controller
    {
        private const string MissingTable =
            "The push_messages table does not exist yet. Apply the app repo's 202611190001_push_messages.sql migration first.";

        private readonly Supabase.Client _supabase;

        public PushMessagesController(Supabase.Client supabase)
        {
            _supabase = supabase;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var vm = new PushMessageIndexViewModel();
            try
            {
                vm.Messages = await LoadAllAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Push messages load failed: {ex.Message}");
                vm.Error = ex.Message.Contains("PGRST205") ? MissingTable : "Could not load the push notification texts.";
            }
            return View(vm);
        }

        [HttpGet]
        public IActionResult Create(string? kind) =>
            View("Edit", new PushMessageForm { Kind = PushMessageTexts.FindKind(kind)?.Key ?? "general" });

        [HttpGet]
        public async Task<IActionResult> Edit(long id)
        {
            var message = await FindAsync(id);
            if (message == null)
            {
                TempData["Error"] = "That text no longer exists.";
                return RedirectToAction(nameof(Index));
            }

            return View(new PushMessageForm
            {
                Id = message.Id,
                Kind = message.Kind,
                Title = message.Title,
                Body = message.Body,
                IsActive = message.IsActive
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(PushMessageForm form)
        {
            var kind = PushMessageTexts.FindKind(form.Kind);
            var title = Normalize(form.Title);
            var body = Normalize(form.Body);

            if (kind == null)
                ModelState.AddModelError(nameof(form.Kind), "Choose when this text is sent.");
            Check(nameof(form.Title), "title", title, PushMessageTexts.MaxTitleLength, kind);
            Check(nameof(form.Body), "message", body, PushMessageTexts.MaxBodyLength, kind);

            if (!ModelState.IsValid)
                return View("Edit", form);

            try
            {
                if (form.Id is long id)
                {
                    if (await FindAsync(id) == null)
                    {
                        TempData["Error"] = "That text no longer exists.";
                        return RedirectToAction(nameof(Index));
                    }

                    await _supabase.From<PushMessage>()
                        .Where(m => m.Id == id)
                        .Set(m => m.Kind, kind!.Key)
                        .Set(m => m.Title, title)
                        .Set(m => m.Body, body)
                        .Set(m => m.IsActive, form.IsActive)
                        .Set(m => m.UpdatedAt, DateTime.UtcNow)
                        .Update();
                }
                else
                {
                    // New texts go to the end of their turn order
                    var last = (await _supabase.From<PushMessage>()
                            .Select("id,sort_order")
                            .Order("sort_order", Ordering.Descending)
                            .Limit(1)
                            .Get())
                        .Models.FirstOrDefault();

                    await _supabase.From<PushMessage>().Insert(new PushMessage
                    {
                        Kind = kind!.Key,
                        Title = title,
                        Body = body,
                        IsActive = form.IsActive,
                        SortOrder = (last?.SortOrder ?? 0) + 10
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Push message save failed: {ex.Message}");
                ModelState.AddModelError("", ex.Message.Contains("PGRST205") ? MissingTable : "Saving failed. Please try again.");
                return View("Edit", form);
            }

            TempData["Message"] = form.Id.HasValue ? "Text updated." : "Text added.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetActive(long id, bool active)
        {
            try
            {
                await _supabase.From<PushMessage>()
                    .Where(m => m.Id == id)
                    .Set(m => m.IsActive, active)
                    .Set(m => m.UpdatedAt, DateTime.UtcNow)
                    .Update();
                TempData["Message"] = active ? "Text turned on: it's used in the reminders." : "Text turned off: it's no longer sent.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Push message update failed: {ex.Message}");
                TempData["Error"] = "Could not change the text. Please try again.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                await _supabase.From<PushMessage>().Where(m => m.Id == id).Delete();
                TempData["Message"] = "Text deleted.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Push message delete failed: {ex.Message}");
                TempData["Error"] = "Could not delete the text. Please try again.";
            }
            return RedirectToAction(nameof(Index));
        }

        private void Check(string field, string what, string text, int max, PushMessageKind? kind)
        {
            if (text.Length == 0)
            {
                ModelState.AddModelError(field, $"Enter the {what}.");
                return;
            }
            if (text.Length > max)
                ModelState.AddModelError(field, $"The {what} is limited to {max} characters.");
            if (kind == null) return;

            var unknown = PushMessageTexts.UnknownPlaceholders(kind, text);
            if (unknown.Count > 0)
                ModelState.AddModelError(field,
                    $"{string.Join(", ", unknown.Select(u => "{" + u + "}"))} can't be used here. " +
                    $"\"{kind.Label}\" texts can use {string.Join(", ", kind.Placeholders.Select(p => "{" + p + "}"))}.");
        }

        // In the function's order: by type, then sort_order, then id
        private async Task<List<PushMessage>> LoadAllAsync() =>
            (await _supabase.From<PushMessage>()
                .Order("sort_order", Ordering.Ascending)
                .Order("id", Ordering.Ascending)
                .Get())
            .Models.ToList();

        private async Task<PushMessage?> FindAsync(long id) =>
            (await _supabase.From<PushMessage>().Where(m => m.Id == id).Get()).Models.FirstOrDefault();

        // One line, trimmed: notifications don't show line breaks reliably
        private static string Normalize(string? text) =>
            string.Join(" ", (text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }
}
