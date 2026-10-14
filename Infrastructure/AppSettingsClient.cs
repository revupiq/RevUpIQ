using System.Text.Json;

namespace RevUpIQ.Admin.Infrastructure
{
    // Server-side access to public.app_settings (service role; never sent to the browser)
    public static class AppSettingsClient
    {
        public const string DefaultCoverKey = "default_cover_photo_url";

        private static HttpClient Create()
        {
            var http = new HttpClient { BaseAddress = new Uri(ApiClientFactory.BaseUrl.TrimEnd('/') + "/") };
            http.DefaultRequestHeaders.Add("apikey", ApiClientFactory.ServiceRole);
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClientFactory.ServiceRole);
            return http;
        }

        public static async Task<string?> GetAsync(string key)
        {
            try
            {
                using var http = Create();
                var res = await http.GetAsync($"rest/v1/app_settings?select=value&key=eq.{Uri.EscapeDataString(key)}");
                if (!res.IsSuccessStatusCode) return null;

                using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
                var first = doc.RootElement.EnumerateArray().FirstOrDefault();
                if (first.ValueKind != JsonValueKind.Object) return null;

                var value = first.GetProperty("value");
                return value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
                    ? value.GetString()
                    : null;
            }
            catch
            {
                return null;
            }
        }

        // Pass null to clear the setting
        public static async Task<bool> SetAsync(string key, string? value)
        {
            using var http = Create();

            var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["key"] = key,
                ["value"] = value,
                ["updated_at"] = DateTime.UtcNow
            });

            using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            using var msg = new HttpRequestMessage(HttpMethod.Post, "rest/v1/app_settings?on_conflict=key") { Content = content };
            msg.Headers.Add("Prefer", "resolution=merge-duplicates");

            var res = await http.SendAsync(msg);
            return res.IsSuccessStatusCode;
        }

        public static async Task<(bool Ok, string? Url, string? Error)> UploadAsync(
            string path, byte[] bytes, string contentType)
        {
            using var http = Create();
            using var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

            using var msg = new HttpRequestMessage(HttpMethod.Post, $"storage/v1/object/app-assets/{path}") { Content = content };
            msg.Headers.Add("x-upsert", "true");

            var res = await http.SendAsync(msg);
            if (!res.IsSuccessStatusCode)
                return (false, null, await res.Content.ReadAsStringAsync());

            var url = $"{ApiClientFactory.BaseUrl.TrimEnd('/')}/storage/v1/object/public/app-assets/{path}";
            return (true, url, null);
        }
    }
}
