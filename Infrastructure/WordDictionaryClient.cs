using System.Text.Json;
using System.Text.RegularExpressions;

namespace RevUpIQ.Admin.Infrastructure
{
    public enum WordDictionaryStatus
    {
        InDictionary,   // found in the Datamuse dictionary the app checks first
        InCustom,       // found in public.custom_dictionary
        NotFound,       // in neither, so players could not guess it
        Unverified      // the dictionary service could not be reached
    }

    // Mirrors the Flutter app's WordValidatorApi so the admin and the game agree on
    // which words players can type: Datamuse first, then public.custom_dictionary.
    public static class WordDictionaryClient
    {
        private static readonly Regex ValidWord =
            new("^[a-z]{4,6}$", RegexOptions.Compiled);

        private static readonly HttpClient Datamuse = new() { Timeout = TimeSpan.FromSeconds(5) };

        public static string? Normalize(string? word)
        {
            var w = (word ?? "").Trim().ToLowerInvariant();
            return ValidWord.IsMatch(w) ? w : null;
        }

        public static async Task<WordDictionaryStatus> CheckAsync(string word)
        {
            var reachable = true;

            // 1. Datamuse: exact match with definitions and a real score (same rule as the app)
            try
            {
                var res = await Datamuse.GetAsync(
                    $"https://api.datamuse.com/words?sp={Uri.EscapeDataString(word)}&max=1&md=d");

                if (res.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
                    var first = doc.RootElement.EnumerateArray().FirstOrDefault();

                    if (first.ValueKind == JsonValueKind.Object
                        && first.TryGetProperty("word", out var w)
                        && string.Equals(w.GetString(), word, StringComparison.OrdinalIgnoreCase)
                        && first.TryGetProperty("defs", out var defs)
                        && defs.ValueKind == JsonValueKind.Array && defs.GetArrayLength() > 0
                        && first.TryGetProperty("score", out var score)
                        && score.ValueKind == JsonValueKind.Number && score.GetDouble() > 100)
                    {
                        return WordDictionaryStatus.InDictionary;
                    }
                }
                else
                {
                    reachable = false;
                }
            }
            catch
            {
                reachable = false;
            }

            // 2. The app's custom dictionary
            try
            {
                using var http = CreateSupabase();
                var res = await http.GetAsync(
                    $"rest/v1/custom_dictionary?select=word&word=ilike.{Uri.EscapeDataString(word)}&limit=1");

                if (res.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
                    if (doc.RootElement.GetArrayLength() > 0)
                        return WordDictionaryStatus.InCustom;
                }
            }
            catch
            {
                // fall through
            }

            return reachable ? WordDictionaryStatus.NotFound : WordDictionaryStatus.Unverified;
        }

        // Adds the word to public.custom_dictionary (lowercase, duplicates ignored)
        public static async Task<bool> AddToCustomAsync(string word)
        {
            using var http = CreateSupabase();

            var payload = JsonSerializer.Serialize(new[] { new { word } });
            using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
            using var msg = new HttpRequestMessage(HttpMethod.Post, "rest/v1/custom_dictionary?on_conflict=word")
            {
                Content = content
            };
            msg.Headers.Add("Prefer", "resolution=ignore-duplicates");

            var res = await http.SendAsync(msg);
            return res.IsSuccessStatusCode;
        }

        private static HttpClient CreateSupabase()
        {
            var http = new HttpClient { BaseAddress = new Uri(ApiClientFactory.BaseUrl.TrimEnd('/') + "/") };
            http.DefaultRequestHeaders.Add("apikey", ApiClientFactory.ServiceRole);
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClientFactory.ServiceRole);
            return http;
        }
    }
}
