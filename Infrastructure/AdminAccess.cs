using System.Security.Claims;

namespace RevUpIQ.Admin.Infrastructure
{
    // Who can open which part of the panel.
    //
    // The built-in "admin" login is the owner: every section, plus the Admin Users page
    // where the owner creates other accounts (public.admin_users) and picks the sections
    // each one may open. An account's sections are claims in its sign-in cookie; the
    // cookie is re-checked against the table every minute (see AdminSignIn), so turning
    // an account off or changing its sections takes effect without it signing out.
    public static class AdminAccess
    {
        public const string AdminUserIdClaim = "admin_user_id";
        public const string SectionClaim = "admin_section";

        public record Section(string Key, string Label, string Description, string[] Controllers);

        // In the order of the dashboard. Every controller with pages must be listed here,
        // or only the owner can open it.
        public static readonly Section[] Sections =
        {
            new("divisions", "Divisions & daily games", "Divisions, their daily Trivia, Photo Guess and RaceWord games, and imports", new[] { "Divisions", "MiniGames" }),
            new("leaderboard", "Leaderboard", "Daily, weekly and monthly standings", new[] { "Leaderboard" }),
            new("users", "Users", "Player accounts: profiles, subscriptions, bans, demo accounts, deletion", new[] { "Users" }),
            new("leagues", "Leagues", "Player leagues", new[] { "Leagues" }),
            new("support", "Support", "Support tickets, categories and the support notice", new[] { "Support" }),
            new("sponsor_bar", "Sponsor Bar", "The sponsor bar in the app", new[] { "SponsorBar" }),
            new("notification_bar", "Notification Bar", "The notification bar in the app", new[] { "NotificationBar" }),
            new("suggestions", "User Suggestions", "Questions and words players suggest", new[] { "UsersSuggestion" }),
            new("sponsor_requests", "Sponsors Requests", "Business inquiries from the app", new[] { "SponsorsRequests" }),
            new("how_to_play", "How To Play", "How-to-play videos", new[] { "GameHowToVideos" }),
            new("app_settings", "App Settings", "Default cover and app settings", new[] { "DefaultCover" }),
            new("faqs", "FAQs", "The app's FAQ page", new[] { "Faqs" }),
            new("push", "Push Notifications", "The daily reminder texts", new[] { "PushMessages" }),
        };

        // Open to every signed-in account
        private static readonly HashSet<string> OpenControllers = new(StringComparer.OrdinalIgnoreCase) { "Home", "Account" };

        private static readonly Dictionary<string, string> SectionByController = Sections
            .SelectMany(s => s.Controllers.Select(c => (Controller: c, s.Key)))
            .ToDictionary(x => x.Controller, x => x.Key, StringComparer.OrdinalIgnoreCase);

        public static bool IsKnownSection(string key) => Sections.Any(s => s.Key == key);

        // The owner's cookie has no admin_user_id: only the built-in login issues one like that
        public static bool IsOwner(this ClaimsPrincipal user) =>
            user.Identity?.IsAuthenticated == true && !user.HasClaim(c => c.Type == AdminUserIdClaim);

        public static long? AdminUserId(this ClaimsPrincipal user) =>
            long.TryParse(user.FindFirst(AdminUserIdClaim)?.Value, out var id) ? id : null;

        public static bool CanAccess(this ClaimsPrincipal user, string section) =>
            user.IsOwner() || user.HasClaim(SectionClaim, section);

        // Whether the signed-in account may open a page of this controller
        public static bool CanOpen(this ClaimsPrincipal user, string? controller)
        {
            if (user.Identity?.IsAuthenticated != true || controller == null) return false;
            if (OpenControllers.Contains(controller)) return true;
            if (user.IsOwner()) return true;
            return SectionByController.TryGetValue(controller, out var section) && user.CanAccess(section);
        }

        public static IEnumerable<string> SectionsOf(ClaimsPrincipal user) =>
            user.IsOwner()
                ? Sections.Select(s => s.Key)
                : user.FindAll(SectionClaim).Select(c => c.Value);
    }
}
