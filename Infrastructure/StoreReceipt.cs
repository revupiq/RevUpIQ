using System.Text;
using System.Text.Json;

namespace RevUpIQ.Admin.Infrastructure;

// What the admin can read from user_divisions.purchase_token, for display only.
//
// Google Play tokens are short opaque strings (24 lowercase letters, a dot, then more).
// Apple tokens are StoreKit 2 signed transactions (a JWS starting "eyJ") whose payload
// says the environment, the offer used and the period end. The signature is NOT
// checked here, since the app writes the token itself; trust only server-verified fields.
public sealed record StoreReceipt(
    string Store,              // "Apple", "Google" or "Unknown"
    string? Environment,       // Apple only: "Production", "Sandbox" or "Xcode"
    bool? FreeTrial,           // Apple only: whether this transaction is a free-trial offer
    DateTime? PeriodEndsAt)    // Apple only: expiresDate of this transaction (UTC)
{
    public static readonly StoreReceipt Unknown = new("Unknown", null, null, null);

    // "Apple", "Apple (Sandbox)", "Google"...; test environments are worth seeing at a glance
    public string Label =>
        Environment is null or "Production" ? Store : $"{Store} ({Environment})";

    public static StoreReceipt Parse(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Unknown;

        if (token.StartsWith("eyJ", StringComparison.Ordinal))
            return ParseApple(token);

        if (System.Text.RegularExpressions.Regex.IsMatch(token, "^[a-z]{24}\\."))
            return new StoreReceipt("Google", null, null, null);

        return Unknown;
    }

    private static StoreReceipt ParseApple(string jws)
    {
        try
        {
            var parts = jws.Split('.');
            if (parts.Length != 3)
                return new StoreReceipt("Apple", null, null, null);

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            var root = doc.RootElement;

            string? environment = root.TryGetProperty("environment", out var env) ? env.GetString() : null;

            // offerType 1 = introductory offer; FREE_TRIAL marks the free version of it
            var freeTrial = root.TryGetProperty("offerDiscountType", out var discount)
                && discount.GetString() == "FREE_TRIAL";

            DateTime? endsAt = root.TryGetProperty("expiresDate", out var expires) && expires.TryGetInt64(out var ms)
                ? DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime
                : null;

            return new StoreReceipt("Apple", environment, freeTrial, endsAt);
        }
        catch (Exception)
        {
            return new StoreReceipt("Apple", null, null, null);
        }
    }
}
