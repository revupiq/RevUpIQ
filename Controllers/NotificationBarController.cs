using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevUpIQ.Admin.Infrastructure;
using RevUpIQ.Admin.Models.NotificationBar;

namespace RevUpIQ.Admin.Controllers
{
    // Manages public.notification_bar: the message bar every app user sees while a
    // notification is active and inside its on/off window. Writes go through the
    // server-side service-role connection only.
    [Authorize]
    public class NotificationBarController : Controller
    {
        public const int MaxMessageLength = 500;
        public const int MaxLabelLength = 40;
        private const int MaxUrlLength = 2048;
        private const string Columns = "id,message,link_url,link_label,starts_at,ends_at,is_active,created_at,updated_at";

        // datetime-local values, interpreted in the panel's display time zone
        public const string InputFormat = "yyyy-MM-dd'T'HH:mm";
        private static readonly string[] InputFormats = { InputFormat, "yyyy-MM-dd'T'HH:mm:ss" };

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            using var http = CreateClient();
            var res = await http.GetAsync($"rest/v1/notification_bar?select={Columns}&order=starts_at.desc");
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"NotificationBar load failed: {(int)res.StatusCode} {body}");
                ViewBag.Error = body.Contains("PGRST205")
                    ? "The notification_bar table does not exist yet. Apply the 202610160001_notification_bar.sql migration first."
                    : "Could not load the notifications.";
                return View(new List<NotificationBarRow>());
            }

            var rows = JsonSerializer.Deserialize<List<NotificationBarRow>>(body) ?? new();
            return View(rows);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var start = DisplayTime.Now;
            start = start.AddSeconds(-start.Second).AddMilliseconds(-start.Millisecond);

            return View("Edit", new NotificationBarForm
            {
                StartsAt = start.ToString(InputFormat, CultureInfo.InvariantCulture),
                EndsAt = start.AddDays(7).ToString(InputFormat, CultureInfo.InvariantCulture),
                IsActive = true
            });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            var row = await Find(id);
            if (row == null)
            {
                TempData["Error"] = "That notification no longer exists.";
                return RedirectToAction(nameof(Index));
            }

            return View(new NotificationBarForm
            {
                Id = row.Id,
                Message = row.Message,
                LinkUrl = row.Link_Url,
                LinkLabel = row.Link_Label,
                StartsAt = ToInput(row.Starts_At),
                EndsAt = ToInput(row.Ends_At),
                IsActive = row.Is_Active
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(NotificationBarForm form)
        {
            var payload = Validate(form);
            if (payload == null)
                return View("Edit", form);

            using var http = CreateClient();
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            HttpRequestMessage msg = form.Id.HasValue
                ? new HttpRequestMessage(HttpMethod.Patch, $"rest/v1/notification_bar?id=eq.{form.Id.Value}") { Content = content }
                : new HttpRequestMessage(HttpMethod.Post, "rest/v1/notification_bar") { Content = content };
            msg.Headers.Add("Prefer", "return=representation");

            using (msg)
            {
                var res = await http.SendAsync(msg);
                var body = await res.Content.ReadAsStringAsync();

                if (!res.IsSuccessStatusCode)
                {
                    Console.WriteLine($"NotificationBar save failed: {(int)res.StatusCode} {body}");
                    ModelState.AddModelError("", body.Contains("PGRST205")
                        ? "The notification_bar table does not exist yet. Apply the 202610160001_notification_bar.sql migration first."
                        : "Saving failed. Please try again.");
                    return View("Edit", form);
                }

                if (form.Id.HasValue && body.Trim() == "[]")
                {
                    TempData["Error"] = "That notification no longer exists.";
                    return RedirectToAction(nameof(Index));
                }
            }

            TempData["Message"] = form.Id.HasValue ? "Notification updated." : "Notification created.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetActive(Guid id, bool active)
        {
            var ok = await Patch(id, new Dictionary<string, object?> { ["is_active"] = active });
            if (ok)
                TempData["Message"] = active ? "Notification activated." : "Notification deactivated.";
            else
                TempData["Error"] = "Could not change the notification. Please try again.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            using var http = CreateClient();
            var res = await http.DeleteAsync($"rest/v1/notification_bar?id=eq.{id}");

            if (res.IsSuccessStatusCode)
            {
                TempData["Message"] = "Notification deleted.";
            }
            else
            {
                Console.WriteLine($"NotificationBar delete failed: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
                TempData["Error"] = "Could not delete the notification. Please try again.";
            }

            return RedirectToAction(nameof(Index));
        }

        public static string ToInput(DateTimeOffset value) =>
            value.ToOffset(DisplayTime.Offset).ToString(InputFormat, CultureInfo.InvariantCulture);

        // Returns the row to save, or null after adding the problems to ModelState
        private Dictionary<string, object?>? Validate(NotificationBarForm form)
        {
            var message = (form.Message ?? "").Trim();
            if (message.Length == 0)
                ModelState.AddModelError(nameof(form.Message), "Enter the message users will see.");
            else if (message.Length > MaxMessageLength)
                ModelState.AddModelError(nameof(form.Message), $"The message is limited to {MaxMessageLength} characters.");

            var link = NormalizeLink(form.LinkUrl);
            if (link == "")
                ModelState.AddModelError(nameof(form.LinkUrl), "The link must be a full https:// web address.");

            var label = string.IsNullOrWhiteSpace(form.LinkLabel) ? null : form.LinkLabel.Trim();
            if (label != null && label.Length > MaxLabelLength)
                ModelState.AddModelError(nameof(form.LinkLabel), $"The button text is limited to {MaxLabelLength} characters.");
            if (label != null && link == null)
                ModelState.AddModelError(nameof(form.LinkLabel), "Add a link, or clear the button text.");

            var starts = ParseInput(form.StartsAt);
            var ends = ParseInput(form.EndsAt);
            if (starts == null)
                ModelState.AddModelError(nameof(form.StartsAt), "Choose the on date and time.");
            if (ends == null)
                ModelState.AddModelError(nameof(form.EndsAt), "Choose the off date and time.");
            if (starts != null && ends != null && ends <= starts)
                ModelState.AddModelError(nameof(form.EndsAt), "The off date must be after the on date.");

            if (!ModelState.IsValid)
                return null;

            return new Dictionary<string, object?>
            {
                ["message"] = message,
                ["link_url"] = link,
                ["link_label"] = link == null ? null : label,
                ["starts_at"] = starts!.Value.UtcDateTime,
                ["ends_at"] = ends!.Value.UtcDateTime,
                ["is_active"] = form.IsActive
            };
        }

        // null = no link; "" = invalid. A bare "example.com" becomes https://example.com.
        private static string? NormalizeLink(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var link = value.Trim();
            if (!link.Contains("://")) link = "https://" + link;

            if (link.Length > MaxUrlLength
                || link.Any(char.IsWhiteSpace)
                || !Uri.TryCreate(link, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || string.IsNullOrEmpty(uri.Host)
                || !uri.Host.Contains('.'))
                return "";

            return link;
        }

        private static DateTimeOffset? ParseInput(string? value)
        {
            if (!DateTime.TryParseExact(value?.Trim(), InputFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var local))
                return null;

            return new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), DisplayTime.Offset);
        }

        private static async Task<NotificationBarRow?> Find(Guid id)
        {
            using var http = CreateClient();
            var res = await http.GetAsync($"rest/v1/notification_bar?select={Columns}&id=eq.{id}");
            if (!res.IsSuccessStatusCode) return null;

            var rows = JsonSerializer.Deserialize<List<NotificationBarRow>>(await res.Content.ReadAsStringAsync());
            return rows?.FirstOrDefault();
        }

        private static async Task<bool> Patch(Guid id, Dictionary<string, object?> values)
        {
            using var http = CreateClient();
            using var content = new StringContent(JsonSerializer.Serialize(values), Encoding.UTF8, "application/json");
            using var msg = new HttpRequestMessage(HttpMethod.Patch, $"rest/v1/notification_bar?id=eq.{id}") { Content = content };

            var res = await http.SendAsync(msg);
            if (!res.IsSuccessStatusCode)
                Console.WriteLine($"NotificationBar update failed: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");

            return res.IsSuccessStatusCode;
        }

        private static HttpClient CreateClient()
        {
            var http = new HttpClient { BaseAddress = new Uri(ApiClientFactory.BaseUrl.TrimEnd('/') + "/") };
            http.DefaultRequestHeaders.Add("apikey", ApiClientFactory.ServiceRole);
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClientFactory.ServiceRole);
            return http;
        }
    }
}
