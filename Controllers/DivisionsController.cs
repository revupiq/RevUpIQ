using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevUpIQ.Admin.Models.Division;
using RevUpIQ.Admin.Models.Divisions;
namespace RevUpIQ.Admin.Controllers
{
    [Authorize]
    public class DivisionsController : Controller
    {
        private readonly Supabase.Client _supabase;
        private const string BucketName = "division_logos";

        public DivisionsController(Supabase.Client supabase)
        {
            _supabase = supabase;
        }

        public async Task<IActionResult> Index()
        {
            var result = await _supabase.From<DivisionDto>().Get();
            var divisions = result.Models.ToList();
            return PartialView("_Index", divisions);
        }

        [HttpGet]
        public IActionResult Create() => View(new DivisionCreateViewModel());

        [HttpPost]
        public async Task<IActionResult> Create(DivisionCreateViewModel model)
        {
            if (!ModelState.IsValid || model.Logo == null || model.Logo.Length == 0)
                return View(model);

            var google = CleanStoreId(model.GoogleProductId);
            var appleMonthly = CleanStoreId(model.AppleProductIdMonthly);
            var appleAnnual = CleanStoreId(model.AppleProductIdAnnual);

            var existing = (await _supabase.From<DivisionDto>().Get()).Models;

            var storeIdError = ValidateStoreIds(null, google, appleMonthly, appleAnnual, existing);
            if (storeIdError != null)
            {
                ViewData["Error"] = storeIdError + " Choose the logo again before submitting.";
                return View(model);
            }

            var logoUrl = await UploadIconAsync(model.Logo);

            string? purchaseIconUrl = null;
            if (model.PurchaseIcon != null && model.PurchaseIcon.Length > 0)
                purchaseIconUrl = await UploadIconAsync(model.PurchaseIcon);

            // New divisions go to the end instead of colliding at sort_index 0
            var nextIndex = existing.Count == 0 ? 0 : existing.Max(d => d.SortIndex) + 1;

            await _supabase.From<DivisionDto>().Insert(new DivisionDto
            {
                Name = model.Name,
                LogoUrl = logoUrl,
                PurchaseIconUrl = purchaseIconUrl,
                SortIndex = nextIndex,
                GoogleProductId = google,
                AppleProductIdMonthly = appleMonthly,
                AppleProductIdAnnual = appleAnnual
            });

            return RedirectToAction("Index", "Home");
        }

        private async Task<string> UploadIconAsync(IFormFile file)
        {
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            var bucket = _supabase.Storage.From(BucketName);
            await bucket.Upload(ms.ToArray(), fileName);

            return bucket.GetPublicUrl(fileName);
        }


        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var divisionResult = await _supabase
                .From<DivisionDto>()
                .Where(d => d.Id == id)
                .Get();

            var division = divisionResult.Models.FirstOrDefault();

            if (division == null)
                return NotFound();

            var backgroundResult = await _supabase
                .From<DivisionBackground>()
                .Where(b => b.Division_Id == id)
                .Get();

            var background = backgroundResult.Models.FirstOrDefault();

            ViewData["DivisionBackground"] = background;

            return View("Details", division);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetActive(int id, bool isActive)
        {
            if (id <= 0)
                return BadRequest("Invalid division id.");

            var result = await _supabase.From<DivisionDto>()
                .Where(d => d.Id == id)
                .Get();

            if (result.Models.FirstOrDefault() == null)
                return NotFound();

            await _supabase.From<DivisionDto>()
                .Where(d => d.Id == id)
                .Set(d => d.IsActive, isActive)
                .Update();

            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {

            var division = new DivisionDto { Id = id };
            await _supabase.From<DivisionDto>().Delete(division);
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> Edit(DivisionDto model, IFormFile? Logo, IFormFile? PurchaseIcon)
        {
            if (!ModelState.IsValid)
                return View(model);

            var update = _supabase.From<DivisionDto>()
                .Where(d => d.Id == model.Id)
                .Set(d => d.Name, model.Name);

            if (Logo != null && Logo.Length > 0)
                update = update.Set(d => d.LogoUrl, await UploadIconAsync(Logo));

            if (PurchaseIcon != null && PurchaseIcon.Length > 0)
                update = update.Set(d => d.PurchaseIconUrl, await UploadIconAsync(PurchaseIcon));

            await update.Update();

            return RedirectToAction("Details", new { id = model.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStoreIds(int id, string? googleProductId,
            string? appleProductIdMonthly, string? appleProductIdAnnual)
        {
            if (id <= 0)
                return BadRequest("Invalid division id.");

            var all = (await _supabase.From<DivisionDto>().Get()).Models;
            var division = all.FirstOrDefault(d => d.Id == id);
            if (division == null)
                return NotFound();

            var google = CleanStoreId(googleProductId);
            var appleMonthly = CleanStoreId(appleProductIdMonthly);
            var appleAnnual = CleanStoreId(appleProductIdAnnual);

            var error = ValidateStoreIds(id, google, appleMonthly, appleAnnual, all);
            if (error != null)
            {
                TempData["StoreIdsError"] = error;
                return RedirectToAction("Details", "Divisions", new { id }, "store-ids");
            }

            // Whole-row update so a blank field is written as null and clears the ID
            division.GoogleProductId = google;
            division.AppleProductIdMonthly = appleMonthly;
            division.AppleProductIdAnnual = appleAnnual;
            await _supabase.From<DivisionDto>().Update(division);

            TempData["StoreIdsMessage"] = "Store IDs saved.";
            return RedirectToAction("Details", "Divisions", new { id }, "store-ids");
        }

        private static string? CleanStoreId(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        // Returns an error message, or null when the IDs are fine to save.
        // The iOS app tells annual from monthly by "annual"/"yearly" in the product ID, and
        // resolves Apple purchases to a division by ID, so IDs must be unique across divisions.
        private static string? ValidateStoreIds(int? divisionId, string? google,
            string? appleMonthly, string? appleAnnual, IEnumerable<DivisionDto> all)
        {
            if (google != null && !Regex.IsMatch(google, "^[a-z0-9][a-z0-9._]*$"))
                return "The Google Play ID can only use lowercase letters, numbers, underscores and periods, and must start with a letter or number.";

            foreach (var appleId in new[] { appleMonthly, appleAnnual })
            {
                if (appleId != null && !Regex.IsMatch(appleId, "^[A-Za-z0-9._]+$"))
                    return "App Store IDs can only use letters, numbers, underscores and periods.";
            }

            if (appleMonthly != null && IsAnnualId(appleMonthly))
                return "The App Store monthly ID can't contain \"annual\" or \"yearly\", or the app records it as an annual plan.";

            if (appleAnnual != null && !IsAnnualId(appleAnnual))
                return "The App Store annual ID must contain \"annual\" or \"yearly\", or the app records it as a monthly plan.";

            if (appleMonthly != null && appleMonthly == appleAnnual)
                return "The App Store monthly and annual IDs must be different.";

            foreach (var other in all.Where(d => d.Id != divisionId))
            {
                if (google != null && google == other.GoogleProductId)
                    return $"The Google Play ID \"{google}\" is already used by {other.Name}.";

                foreach (var appleId in new[] { appleMonthly, appleAnnual })
                {
                    if (appleId != null && (appleId == other.AppleProductIdMonthly || appleId == other.AppleProductIdAnnual))
                        return $"The App Store ID \"{appleId}\" is already used by {other.Name}.";
                }
            }

            return null;
        }

        private static bool IsAnnualId(string productId) =>
            productId.Contains("annual", StringComparison.OrdinalIgnoreCase) ||
            productId.Contains("yearly", StringComparison.OrdinalIgnoreCase);

        // Saves the whole display order at once (drag & drop and the arrow buttons).
        // Position 1 is the first division players see, so sort_index is 0..n-1 in order.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reorder([FromBody] ReorderDivisionsRequest request)
        {
            var ids = request?.Ids;
            if (ids == null || ids.Count == 0 || ids.Distinct().Count() != ids.Count)
                return BadRequest(new { error = "Invalid order." });

            var all = (await _supabase.From<DivisionDto>().Get()).Models;

            // Must be the complete set, so a stale page can't drop or duplicate a division
            if (all.Count != ids.Count || !all.Select(d => d.Id).ToHashSet().SetEquals(ids))
                return Conflict(new { error = "The division list changed. Reload the page and try again." });

            try
            {
                for (var position = 0; position < ids.Count; position++)
                {
                    // Plain locals: the Supabase filter can't translate list indexers
                    var divisionId = ids[position];
                    var newIndex = position;

                    var current = all.First(d => d.Id == divisionId);
                    if (current.SortIndex == newIndex) continue;

                    await _supabase.From<DivisionDto>()
                        .Where(d => d.Id == divisionId)
                        .Set(d => d.SortIndex, newIndex)
                        .Update();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Division reorder failed: {ex}");
                return StatusCode(500, new { error = "Could not save the new order: " + ex.Message });
            }

            return Ok();
        }

        public class ReorderDivisionsRequest
        {
            public List<int> Ids { get; set; } = new();
        }

        [HttpPost]
        public async Task<IActionResult> Move(int id, string direction)
        {
            if (string.IsNullOrWhiteSpace(direction))
                return BadRequest();

            await _supabase.Rpc("move_division_sort", new
            {
                p_division_id = id,
                p_direction = direction
            });

            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadIcon(int divisionId, string kind, IFormFile icon)
        {
            if (divisionId <= 0)
                return BadRequest("Invalid divisionId.");

            if (icon == null || icon.Length == 0)
                return BadRequest("No image uploaded.");

            var ext = Path.GetExtension(icon.FileName)?.ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (string.IsNullOrWhiteSpace(ext) || !allowed.Contains(ext))
                return BadRequest("Invalid file type. Use a PNG, JPG or WebP image.");

            if (kind != "logo" && kind != "purchase")
                return BadRequest("Unknown icon type.");

            var url = await UploadIconAsync(icon);

            var update = _supabase.From<DivisionDto>().Where(d => d.Id == divisionId);

            update = kind == "logo"
                ? update.Set(d => d.LogoUrl, url)
                : update.Set(d => d.PurchaseIconUrl, url);

            await update.Update();

            return Ok(new { url });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadDivisionBackground(long divisionId, IFormFile backgroundImage)
        {
            if (divisionId <= 0)
                return BadRequest("Invalid divisionId.");

            if (backgroundImage == null || backgroundImage.Length == 0)
                return BadRequest("No image uploaded.");

            var ext = Path.GetExtension(backgroundImage.FileName)?.ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (string.IsNullOrWhiteSpace(ext) || !allowed.Contains(ext))
                return BadRequest("Invalid file type.");

            const string bucket = "division_backgrounds";
            var filePath = $"{divisionId}/background{ext}";

            await using var ms = new MemoryStream();
            await backgroundImage.CopyToAsync(ms);
            var bytes = ms.ToArray();

            await _supabase.Storage
                .From(bucket)
                .Upload(bytes, filePath, new Supabase.Storage.FileOptions { Upsert = true });

            var publicUrl = _supabase.Storage
                .From(bucket)
                .GetPublicUrl(filePath)
                .ToString();

            // Add a cache-busting timestamp so the browser knows the image changed
            publicUrl += (publicUrl.Contains("?") ? "&" : "?") + "t=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var existingResult = await _supabase
                .From<DivisionBackground>()
                .Where(b => b.Division_Id == divisionId)
                .Get();

            var existingRecord = existingResult.Models.FirstOrDefault();

            if (existingRecord != null)
            {
                existingRecord.Background_Url = publicUrl;
                existingRecord.Updated_At = DateTime.UtcNow;
                await _supabase.From<DivisionBackground>().Update(existingRecord);
            }
            else
            {
                await _supabase.From<DivisionBackground>().Insert(new DivisionBackground
                {
                    Division_Id = divisionId,
                    Background_Url = publicUrl,
                    Created_At = DateTime.UtcNow,
                    Updated_At = DateTime.UtcNow
                });
            }

            return Ok(new { url = publicUrl });
        }


    }
}
