using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevUpIQ.Admin.Infrastructure;

namespace RevUpIQ.Admin.Controllers
{
    // Manages the default profile cover photo used when a user has not set their own
    [Authorize]
    public class DefaultCoverController : Controller
    {
        private const long MaxBytes = 5 * 1024 * 1024;

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewBag.CurrentUrl = await AppSettingsClient.GetAsync(AppSettingsClient.DefaultCoverKey);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "Choose an image to upload." });

            if (file.Length > MaxBytes)
                return BadRequest(new { error = "The image must be 5 MB or smaller." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();

            // Check the real file signature rather than trusting the name or content type
            var kind = DetectImage(bytes);
            if (kind == null)
                return BadRequest(new { error = "Only JPG, PNG or WebP images are allowed." });

            // A new file name each time so apps and CDNs never serve a stale cover
            var path = $"default-cover/{DateTime.UtcNow:yyyyMMddHHmmssfff}.{kind.Value.Ext}";

            var upload = await AppSettingsClient.UploadAsync(path, bytes, kind.Value.Mime);
            if (!upload.Ok)
            {
                Console.WriteLine($"Default cover upload failed: {upload.Error}");
                return StatusCode(500, new { error = "Upload failed. Please try again." });
            }

            if (!await AppSettingsClient.SetAsync(AppSettingsClient.DefaultCoverKey, upload.Url))
                return StatusCode(500, new { error = "The image uploaded but the setting could not be saved." });

            return Json(new { url = upload.Url });
        }

        // Clears the setting so the app falls back to its bundled image
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UseBuiltIn()
        {
            if (!await AppSettingsClient.SetAsync(AppSettingsClient.DefaultCoverKey, null))
                return StatusCode(500, new { error = "Could not clear the default cover." });

            return Ok();
        }

        private static (string Ext, string Mime)? DetectImage(byte[] b)
        {
            if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
                return ("jpg", "image/jpeg");

            if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
                return ("png", "image/png");

            if (b.Length >= 12
                && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F'
                && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P')
                return ("webp", "image/webp");

            return null;
        }
    }
}
