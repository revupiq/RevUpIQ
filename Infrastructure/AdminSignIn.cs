using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using RevUpIQ.Admin.Models.AdminUsers;
using static Supabase.Postgrest.Constants;

namespace RevUpIQ.Admin.Infrastructure
{
    // Signing in to the panel: the built-in owner login, and the accounts the owner adds
    // on the Admin Users page (public.admin_users). See AdminAccess for what each may open.
    public static class AdminSignIn
    {
        // The panel's original login, unchanged: it is the owner
        public const string OwnerUsername = "admin";
        private const string OwnerPassword = "Admin123";

        public const int MinPasswordLength = 8;
        public const int MaxPasswordLength = 128;
        public static readonly Regex UsernamePattern = new("^[A-Za-z0-9._-]{3,40}$");

        // How long an account's row is trusted before the cookie is checked again
        private static readonly TimeSpan RecheckAfter = TimeSpan.FromMinutes(1);

        private static readonly PasswordHasher<AdminUser> Hasher = new();
        private static readonly ConcurrentDictionary<long, (DateTime CheckedAt, AdminUser? User)> Checked = new();

        public static string HashPassword(string password) => Hasher.HashPassword(new AdminUser(), password);

        public static bool IsOwnerLogin(string username, string password) =>
            username == OwnerUsername && password == OwnerPassword;

        public static ClaimsPrincipal OwnerPrincipal() =>
            Principal(new List<Claim> { new(ClaimTypes.Name, OwnerUsername) });

        public static ClaimsPrincipal PrincipalFor(AdminUser user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, user.Username),
                new(AdminAccess.AdminUserIdClaim, user.Id.ToString())
            };
            claims.AddRange(KnownSections(user.Sections).Select(s => new Claim(AdminAccess.SectionClaim, s)));
            return Principal(claims);
        }

        public static IEnumerable<string> KnownSections(IEnumerable<string>? sections) =>
            (sections ?? Enumerable.Empty<string>()).Where(AdminAccess.IsKnownSection).Distinct();

        // The active account with this name and password, or null
        public static async Task<AdminUser?> FindAccountAsync(Supabase.Client supabase, string username, string password)
        {
            if (!UsernamePattern.IsMatch(username) || password.Length == 0 || password.Length > MaxPasswordLength)
                return null;

            var user = await FindByUsernameAsync(supabase, username);
            if (user == null || !user.IsActive) return null;

            var result = Hasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (result == PasswordVerificationResult.Failed) return null;

            try
            {
                var update = supabase.From<AdminUser>()
                    .Where(u => u.Id == user.Id)
                    .Set(u => u.LastLoginAt, (DateTime?)DateTime.UtcNow);
                // An older hash format is upgraded on the next successful sign-in
                if (result == PasswordVerificationResult.SuccessRehashNeeded)
                    update = update.Set(u => u.PasswordHash, HashPassword(password));
                await update.Update();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Recording the sign-in of admin user {user.Id} failed: {ex.Message}");
            }

            return user;
        }

        // Case-insensitive, like the table's unique index. Usernames only allow letters,
        // digits, ".", "_" and "-", so only "_" needs escaping for ILIKE.
        public static async Task<AdminUser?> FindByUsernameAsync(Supabase.Client supabase, string username) =>
            (await supabase.From<AdminUser>()
                .Filter("username", Operator.ILike, username.Replace("_", "\\_"))
                .Get())
            .Models.FirstOrDefault();

        // Runs on every request (cookie OnValidatePrincipal). An account that was deleted or
        // turned off is signed out; changed sections replace the cookie's.
        public static async Task ValidateAsync(CookieValidatePrincipalContext context)
        {
            var principal = context.Principal;
            if (principal == null || principal.AdminUserId() is not long id) return; // the owner

            AdminUser? user;
            try
            {
                user = await CurrentAsync(context.HttpContext.RequestServices.GetRequiredService<Supabase.Client>(), id);
            }
            catch (Exception ex)
            {
                // Database unreachable: keep the cookie as it is rather than locking everyone out
                Console.WriteLine($"Checking admin user {id} failed: {ex.Message}");
                return;
            }

            if (user == null || !user.IsActive)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var had = principal.FindAll(AdminAccess.SectionClaim).Select(c => c.Value).OrderBy(s => s);
            var has = KnownSections(user.Sections).OrderBy(s => s);
            if (!had.SequenceEqual(has) || principal.Identity?.Name != user.Username)
            {
                context.ReplacePrincipal(PrincipalFor(user));
                context.ShouldRenew = true;
            }
        }

        // After the owner changes an account, its next request re-reads the row
        public static void Forget(long id) => Checked.TryRemove(id, out _);

        private static async Task<AdminUser?> CurrentAsync(Supabase.Client supabase, long id)
        {
            if (Checked.TryGetValue(id, out var seen) && DateTime.UtcNow - seen.CheckedAt < RecheckAfter)
                return seen.User;

            var user = (await supabase.From<AdminUser>().Where(u => u.Id == id).Get()).Models.FirstOrDefault();
            Checked[id] = (DateTime.UtcNow, user);
            return user;
        }

        private static ClaimsPrincipal Principal(List<Claim> claims) =>
            new(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
