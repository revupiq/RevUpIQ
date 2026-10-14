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

            var logoUrl = await UploadIconAsync(model.Logo);

            string? purchaseIconUrl = null;
            if (model.PurchaseIcon != null && model.PurchaseIcon.Length > 0)
                purchaseIconUrl = await UploadIconAsync(model.PurchaseIcon);

            // New divisions go to the end instead of colliding at sort_index 0
            var existing = (await _supabase.From<DivisionDto>().Get()).Models;
            var nextIndex = existing.Count == 0 ? 0 : existing.Max(d => d.SortIndex) + 1;

            await _supabase.From<DivisionDto>().Insert(new DivisionDto
            {
                Name = model.Name,
                LogoUrl = logoUrl,
                PurchaseIconUrl = purchaseIconUrl,
                SortIndex = nextIndex
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

            for (var position = 0; position < ids.Count; position++)
            {
                var current = all.First(d => d.Id == ids[position]);
                if (current.SortIndex == position) continue;

                await _supabase.From<DivisionDto>()
                    .Where(d => d.Id == ids[position])
                    .Set(d => d.SortIndex, position)
                    .Update();
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
