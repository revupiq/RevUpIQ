using RevUpIQ.Admin.Models.Users;

namespace RevUpIQ.Admin.Infrastructure;

// Ordered by how much a user needs attention; ForUser shows the highest one
public enum SubscriptionState
{
    Inactive,
    Trial,
    Subscribed,
    Canceling,
    BillingIssue
}

// The one place the admin decides a subscription's status.
//
// Access ends at ends_at. Rows the server has verified with Google Play or the App
// Store have verified_at set, and then ends_at, auto_renew_enabled, billing_issue and
// trial_ends_at are the store's answer (see docs/store_verified_subscriptions.md in
// the app repo). Unverified rows only have the app's estimate: ends_at (or, before
// that migration, subscribed_at) + 30/365 days. For those an Apple purchase is a trial
// when its signed transaction says FREE_TRIAL, and a Google annual plan in its first
// 3 days is shown as Trial, because the app offers a 3-day trial on annual plans but
// Google tokens don't say whether a purchase used it.
public static class SubscriptionStatus
{
    public const int TrialDays = 3;

    public static DateTime EndsAt(UserDivision division)
    {
        if (division.Ends_At.HasValue)
            return AsUtc(division.Ends_At.Value);

        var start = AsUtc(division.Subscribed_At);
        return IsAnnual(division) ? start.AddDays(365) : start.AddDays(30);
    }

    // When the free trial ends, or null when this purchase isn't in a trial
    public static DateTime? TrialEndsAt(UserDivision division)
    {
        if (division.Trial_Ends_At.HasValue)
            return AsUtc(division.Trial_Ends_At.Value);

        // Verified purchases always say whether there was a trial
        if (IsVerified(division))
            return null;

        var receipt = StoreReceipt.Parse(division.Purchase_Token);
        if (receipt.Store == "Apple")
            return receipt.FreeTrial == true ? receipt.PeriodEndsAt : null;

        return IsAnnual(division) ? AsUtc(division.Subscribed_At).AddDays(TrialDays) : null;
    }

    // "Apple", "Apple (Sandbox)", "Google" or "Unknown". The store column is set by the
    // server; the token covers older rows and adds Apple's test environment.
    public static string Source(UserDivision division)
    {
        var receipt = StoreReceipt.Parse(division.Purchase_Token);

        var store = division.Store?.ToLowerInvariant() switch
        {
            "apple" => "Apple",
            "google" => "Google",
            _ => receipt.Store
        };

        return store == receipt.Store ? receipt.Label : store;
    }

    // e.g. "Renews 2026-11-12 05:18 (est.)"; "(est.)" means the store hasn't confirmed the date
    public static string DateText(UserDivision division, DateTime nowUtc)
    {
        var state = For(division, nowUtc);

        if (state == SubscriptionState.Trial)
        {
            var trialEnds = TrialEndsAt(division)!.Value;
            var trialConfirmed = division.Trial_Ends_At.HasValue
                || StoreReceipt.Parse(division.Purchase_Token).FreeTrial == true;
            return "Trial ends " + DisplayTime.Format(trialEnds) + (trialConfirmed ? "" : " (est.)");
        }

        var label = state switch
        {
            SubscriptionState.Subscribed => "Renews",
            SubscriptionState.Canceling => "Expires",
            SubscriptionState.BillingIssue => "Renewal due",
            _ => "Expired"
        };
        return label + " " + DisplayTime.Format(EndsAt(division)) + (IsVerified(division) ? "" : " (est.)");
    }

    // Distinct sources across a user's divisions, e.g. "Apple (Sandbox), Google"
    public static string Sources(IEnumerable<UserDivision> divisions)
    {
        var sources = divisions.Select(Source).Distinct().OrderBy(s => s).ToList();
        return sources.Count == 0 ? "—" : string.Join(", ", sources);
    }

    // Only verified_at says a store answered; ends_at is filled for every row
    private static bool IsVerified(UserDivision division) => division.Verified_At.HasValue;

    public static SubscriptionState For(UserDivision division, DateTime nowUtc)
    {
        // A failing payment can also suspend access (Google "on hold"), so check it first
        if (division.Billing_Issue == true)
            return SubscriptionState.BillingIssue;

        if (nowUtc >= EndsAt(division))
            return SubscriptionState.Inactive;

        if (division.Auto_Renew_Enabled == false)
            return SubscriptionState.Canceling;

        var trialEnds = TrialEndsAt(division);
        return trialEnds.HasValue && nowUtc < trialEnds.Value
            ? SubscriptionState.Trial
            : SubscriptionState.Subscribed;
    }

    // A user's overall status is the one most worth attention across their divisions
    public static SubscriptionState ForUser(IEnumerable<UserDivision> divisions, DateTime nowUtc) =>
        divisions
            .Select(d => For(d, nowUtc))
            .DefaultIfEmpty(SubscriptionState.Inactive)
            .Max();

    public static string Label(SubscriptionState state) => state switch
    {
        SubscriptionState.Trial => "Trial",
        SubscriptionState.Subscribed => "Subscribed",
        SubscriptionState.Canceling => "Canceling",
        SubscriptionState.BillingIssue => "Billing Issue",
        _ => "Inactive"
    };

    // CSS modifier for the status badge, e.g. "status-billing-issue"
    public static string CssClass(SubscriptionState state) => CssClass(Label(state));

    public static string CssClass(string label) =>
        "status-" + label.Trim().ToLowerInvariant().Replace(' ', '-');

    private static bool IsAnnual(UserDivision division) =>
        string.Equals(division.Subscription_Type, "annual", StringComparison.OrdinalIgnoreCase);

    // Supabase timestamps without a Kind are UTC
    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
}
