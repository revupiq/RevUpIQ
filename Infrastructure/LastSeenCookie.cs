namespace RevUpIQ.Admin.Infrastructure;

// Remembers in a cookie when this browser last opened a list page, so the dashboard
// can badge what arrived since. Without the cookie (first visit, another browser)
// the last 7 days count as new.
public static class LastSeenCookie
{
    public const string UserSuggestions = "RevUpIQ.SuggestionsSeenAt";
    public const string SponsorRequests = "RevUpIQ.SponsorRequestsSeenAt";

    public static void Mark(HttpContext context, string name) =>
        context.Response.Cookies.Append(name, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(), new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddYears(1)
        });

    public static DateTimeOffset Since(HttpContext context, string name) =>
        long.TryParse(context.Request.Cookies[name], out var ms) && ms > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
            : DateTimeOffset.UtcNow.AddDays(-7);
}
