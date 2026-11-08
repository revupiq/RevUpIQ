using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevUpIQ.Admin.Models.Faqs;
using static Supabase.Postgrest.Constants;

namespace RevUpIQ.Admin.Controllers
{
    // Manages public.faqs, the app's FAQ page: changes show up in the app without an
    // update. Players only see active questions; writes go through the server-side
    // service-role connection only.
    [Authorize]
    public class FaqsController : Controller
    {
        public const int MaxQuestionLength = 200;
        public const int MaxAnswerLength = 2000;

        private const string MissingTable =
            "The faqs table does not exist yet. Apply the 202611080002_faqs.sql migration first.";

        private readonly Supabase.Client _supabase;

        public FaqsController(Supabase.Client supabase)
        {
            _supabase = supabase;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? category)
        {
            var vm = new FaqIndexViewModel { Category = KnownCategory(category) };

            try
            {
                vm.Faqs = await LoadAllAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAQ load failed: {ex.Message}");
                vm.Error = ex.Message.Contains("PGRST205") ? MissingTable : "Could not load the FAQs.";
            }

            return View(vm);
        }

        [HttpGet]
        public IActionResult Create(string? category)
        {
            return View("Edit", new FaqForm
            {
                Category = KnownCategory(category) ?? "General",
                IsActive = true,
                ReturnCategory = KnownCategory(category)
            });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(long id, string? category)
        {
            var faq = await FindAsync(id);
            if (faq == null)
            {
                TempData["Error"] = "That FAQ no longer exists.";
                return RedirectToAction(nameof(Index), new { category = KnownCategory(category) });
            }

            return View(new FaqForm
            {
                Id = faq.Id,
                Category = faq.Category,
                Question = faq.Question,
                Answer = faq.Answer,
                IsActive = faq.IsActive,
                ReturnCategory = KnownCategory(category)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(FaqForm form)
        {
            var category = KnownCategory(form.Category);
            var question = Normalize(form.Question);
            var answer = Normalize(form.Answer);

            if (category == null)
                ModelState.AddModelError(nameof(form.Category), "Choose a category.");
            if (question.Length == 0)
                ModelState.AddModelError(nameof(form.Question), "Enter the question.");
            else if (question.Length > MaxQuestionLength)
                ModelState.AddModelError(nameof(form.Question), $"The question is limited to {MaxQuestionLength} characters.");
            if (answer.Length == 0)
                ModelState.AddModelError(nameof(form.Answer), "Enter the answer.");
            else if (answer.Length > MaxAnswerLength)
                ModelState.AddModelError(nameof(form.Answer), $"The answer is limited to {MaxAnswerLength} characters.");

            if (!ModelState.IsValid)
                return View("Edit", form);

            try
            {
                if (form.Id is long id)
                {
                    if (await FindAsync(id) == null)
                    {
                        TempData["Error"] = "That FAQ no longer exists.";
                        return RedirectToAction(nameof(Index), new { category = KnownCategory(form.ReturnCategory) });
                    }

                    await _supabase.From<Faq>()
                        .Where(f => f.Id == id)
                        .Set(f => f.Category, category!)
                        .Set(f => f.Question, question)
                        .Set(f => f.Answer, answer)
                        .Set(f => f.IsActive, form.IsActive)
                        .Set(f => f.UpdatedAt, DateTime.UtcNow)
                        .Update();
                }
                else
                {
                    // New questions go to the end of the list
                    var last = (await _supabase.From<Faq>()
                            .Select("id,sort_order")
                            .Order("sort_order", Ordering.Descending)
                            .Limit(1)
                            .Get())
                        .Models.FirstOrDefault();

                    await _supabase.From<Faq>().Insert(new Faq
                    {
                        Category = category!,
                        Question = question,
                        Answer = answer,
                        IsActive = form.IsActive,
                        SortOrder = (last?.SortOrder ?? 0) + 10
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAQ save failed: {ex.Message}");
                ModelState.AddModelError("", ex.Message.Contains("PGRST205") ? MissingTable : "Saving failed. Please try again.");
                return View("Edit", form);
            }

            TempData["Message"] = form.Id.HasValue ? "FAQ updated." : "FAQ added.";
            return RedirectToAction(nameof(Index), new { category = KnownCategory(form.ReturnCategory) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetActive(long id, bool active, string? category)
        {
            try
            {
                await _supabase.From<Faq>()
                    .Where(f => f.Id == id)
                    .Set(f => f.IsActive, active)
                    .Set(f => f.UpdatedAt, DateTime.UtcNow)
                    .Update();
                TempData["Message"] = active ? "FAQ activated: players see it." : "FAQ deactivated: players no longer see it.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAQ update failed: {ex.Message}");
                TempData["Error"] = "Could not change the FAQ. Please try again.";
            }

            return RedirectToAction(nameof(Index), new { category = KnownCategory(category) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(long id, string? category)
        {
            try
            {
                await _supabase.From<Faq>().Where(f => f.Id == id).Delete();
                TempData["Message"] = "FAQ deleted.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAQ delete failed: {ex.Message}");
                TempData["Error"] = "Could not delete the FAQ. Please try again.";
            }

            return RedirectToAction(nameof(Index), new { category = KnownCategory(category) });
        }

        // Saves the order of the questions on the page (drag & drop and the arrow buttons).
        // With a category chosen, only its questions are on the page: they trade the places
        // they already hold, so the other categories keep theirs. sort_order is rewritten as
        // 10, 20, 30… for the whole list so it never has ties.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reorder([FromBody] ReorderFaqsRequest request)
        {
            var ids = request?.Ids;
            if (ids == null || ids.Count == 0 || ids.Distinct().Count() != ids.Count)
                return BadRequest(new { error = "Invalid order." });

            var category = KnownCategory(request!.Category);

            try
            {
                var all = await LoadAllAsync();
                var shownIds = all.Where(f => category == null || f.Category == category).Select(f => f.Id).ToHashSet();

                // Must be exactly the questions on the page, so a stale page can't drop or duplicate one
                if (!shownIds.SetEquals(ids))
                    return Conflict(new { error = "The FAQ list changed. Reload the page and try again." });

                var byId = all.ToDictionary(f => f.Id);
                var next = new Queue<long>(ids);
                var order = all.Select(f => shownIds.Contains(f.Id) ? byId[next.Dequeue()] : f).ToList();

                for (var position = 0; position < order.Count; position++)
                {
                    // Plain locals: the Supabase filter can't translate list indexers
                    var faqId = order[position].Id;
                    var sortOrder = (position + 1) * 10;
                    if (order[position].SortOrder == sortOrder) continue;

                    await _supabase.From<Faq>()
                        .Where(f => f.Id == faqId)
                        .Set(f => f.SortOrder, sortOrder)
                        .Update();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAQ reorder failed: {ex}");
                return StatusCode(500, new { error = "Could not save the new order: " + ex.Message });
            }

            return Ok();
        }

        // In the app's order: sort_order, then id
        private async Task<List<Faq>> LoadAllAsync() =>
            (await _supabase.From<Faq>()
                .Order("sort_order", Ordering.Ascending)
                .Order("id", Ordering.Ascending)
                .Get())
            .Models.ToList();

        private async Task<Faq?> FindAsync(long id) =>
            (await _supabase.From<Faq>().Where(f => f.Id == id).Get()).Models.FirstOrDefault();

        private static string? KnownCategory(string? category) =>
            Faq.Categories.FirstOrDefault(c => string.Equals(c, category?.Trim(), StringComparison.OrdinalIgnoreCase));

        // Trimmed, with line breaks counted as one character like the form's counters
        private static string Normalize(string? text) => (text ?? "").Replace("\r\n", "\n").Trim();
    }
}
