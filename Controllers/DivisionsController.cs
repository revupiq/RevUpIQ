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

            await _supabase.From<DivisionDto>().Insert(new DivisionDto
            {
                Name = model.Name,
                LogoUrl = logoUrl,
                PurchaseIconUrl = purchaseIconUrl
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
