using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RevUpIQ.Admin.Infrastructure;
using RevUpIQ.Admin.Models.AdminUsers;
using RevUpIQ.Admin.Models.Users;
using static Supabase.Postgrest.Constants;

namespace RevUpIQ.Admin.Controllers
{
    // The owner's page for the panel's other accounts (public.admin_users): who can sign
    // in and which sections each one sees. Not in AdminAccess.Sections, so only the owner
    // can open it (AdminAccessFilter).
    public class AdminUsersController : Controller
    {
        private const string MissingTable =
            "The admin_users table does not exist yet. Apply supabase/migrations/202611190001_admin_users.sql first.";

        private const string MissingPlayerColumn =
            "Linking a player needs supabase/migrations/202611200002_admin_users_player.sql applied first.";

        private const int MaxPlayerResults = 10;

        private readonly Supabase.Client _supabase;

        public AdminUsersController(Supabase.Client supabase)
        {
            _supabase = supabase;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var vm = new AdminUserIndexViewModel();
            try
            {
                vm.Users = (await _supabase.From<AdminUser>().Order("username", Ordering.Ascending).Get()).Models;
                vm.Players = (await DescribePlayersAsync(vm.Users.Where(u => u.UserId.HasValue).Select(u => u.UserId!.Value)))
                    .ToDictionary(p => p.Id, p => p.Label);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Admin users load failed: {ex.Message}");
                vm.Error = ex.Message.Contains("PGRST205") ? MissingTable : "Could not load the admin accounts.";
            }
            return View(vm);
        }

        [HttpGet]
        public IActionResult Create() => View("Edit", new AdminUserForm());

        [HttpGet]
        public async Task<IActionResult> Edit(long id)
        {
            var user = await FindAsync(id);
            if (user == null)
            {
                TempData["Error"] = "That account no longer exists.";
                return RedirectToAction(nameof(Index));
            }

            return await ShowFormAsync(new AdminUserForm
            {
                Id = user.Id,
                UserId = user.UserId,
                Username = user.Username,
                Sections = AdminSignIn.KnownSections(user.Sections).ToList(),
                IsActive = user.IsActive
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(AdminUserForm form)
        {
            var username = (form.Username ?? "").Trim();
            var password = form.Password ?? "";
            var sections = AdminSignIn.KnownSections(form.Sections).ToArray();
            var isNew = !form.Id.HasValue;

            if (!AdminSignIn.UsernamePattern.IsMatch(username))
                ModelState.AddModelError(nameof(form.Username), "Use 3 to 40 letters, digits, dots, dashes or underscores.");
            else if (string.Equals(username, AdminSignIn.OwnerUsername, StringComparison.OrdinalIgnoreCase))
                ModelState.AddModelError(nameof(form.Username), "\"admin\" is the owner's login. Choose another name.");

            if (isNew && password.Length == 0)
                ModelState.AddModelError(nameof(form.Password), "Choose a password.");
            else if (password.Length > 0 && password.Length < AdminSignIn.MinPasswordLength)
                ModelState.AddModelError(nameof(form.Password), $"Use at least {AdminSignIn.MinPasswordLength} characters.");
            else if (password.Length > AdminSignIn.MaxPasswordLength)
                ModelState.AddModelError(nameof(form.Password), $"Use at most {AdminSignIn.MaxPasswordLength} characters.");

            if (sections.Length == 0)
                ModelState.AddModelError(nameof(form.Sections), "Choose at least one section.");

            form.Password = null; // never sent back to the page
            if (!ModelState.IsValid)
                return await ShowFormAsync(form);

            try
            {
                var sameName = await AdminSignIn.FindByUsernameAsync(_supabase, username);
                if (sameName != null && sameName.Id != form.Id)
                {
                    ModelState.AddModelError(nameof(form.Username), "Another account already has this name.");
                    return await ShowFormAsync(form);
                }

                if (form.UserId is Guid playerId)
                {
                    var taken = (await _supabase.From<AdminUser>()
                        .Filter("user_id", Operator.Equals, playerId.ToString()).Get()).Models.FirstOrDefault();
                    if (taken != null && taken.Id != form.Id)
                        ModelState.AddModelError(nameof(form.UserId), $"This player already has an admin account: {taken.Username}.");
                    else if ((await DescribePlayersAsync(new[] { playerId })).Count == 0)
                        ModelState.AddModelError(nameof(form.UserId), "That player's app account no longer exists. Choose another or remove it.");
                    if (!ModelState.IsValid)
                        return await ShowFormAsync(form);
                }

                if (form.Id is long id)
                {
                    if (await FindAsync(id) == null)
                    {
                        TempData["Error"] = "That account no longer exists.";
                        return RedirectToAction(nameof(Index));
                    }

                    var update = _supabase.From<AdminUser>()
                        .Where(u => u.Id == id)
                        .Set(u => u.Username, username)
                        .Set(u => u.Sections, sections)
                        .Set(u => u.IsActive, form.IsActive)
                        .Set(u => u.UserId!, form.UserId)
                        .Set(u => u.UpdatedAt, DateTime.UtcNow);
                    if (password.Length > 0)
                        update = update.Set(u => u.PasswordHash, AdminSignIn.HashPassword(password));
                    await update.Update();
                    AdminSignIn.Forget(id);
                }
                else
                {
                    await _supabase.From<AdminUser>().Insert(new AdminUser
                    {
                        Username = username,
                        PasswordHash = AdminSignIn.HashPassword(password),
                        Sections = sections,
                        IsActive = form.IsActive,
                        UserId = form.UserId
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Admin user save failed: {ex.Message}");
                ModelState.AddModelError("", ex.Message.Contains("PGRST205") ? MissingTable
                    : ex.Message.Contains("user_id") ? MissingPlayerColumn
                    : "Saving failed. Please try again.");
                return await ShowFormAsync(form);
            }

            TempData["Message"] = isNew ? $"Account {username} added." : $"Account {username} updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetActive(long id, bool active)
        {
            try
            {
                await _supabase.From<AdminUser>()
                    .Where(u => u.Id == id)
                    .Set(u => u.IsActive, active)
                    .Set(u => u.UpdatedAt, DateTime.UtcNow)
                    .Update();
                AdminSignIn.Forget(id);
                TempData["Message"] = active ? "Account turned on: it can sign in." : "Account turned off: it is signed out and can't sign in.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Admin user update failed: {ex.Message}");
                TempData["Error"] = "Could not change the account. Please try again.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                await _supabase.From<AdminUser>().Where(u => u.Id == id).Delete();
                AdminSignIn.Forget(id);
                TempData["Message"] = "Account deleted.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Admin user delete failed: {ex.Message}");
                TempData["Error"] = "Could not delete the account. Please try again.";
            }
            return RedirectToAction(nameof(Index));
        }

        // Players whose username, email or phone contains the text, for the form's picker
        [HttpGet]
        public async Task<IActionResult> Players(string? q)
        {
            var text = (q ?? "").Trim();
            if (text.Length < 2) return Json(Array.Empty<PlayerOption>());

            var ids = new List<Guid>();
            try
            {
                var byName = await _supabase.From<Profile>().Select("id,username")
                    .Filter("username", Operator.ILike, $"%{text}%")
                    .Limit(MaxPlayerResults).Get();
                ids.AddRange(byName.Models.Select(p => p.Id));
            }
            catch (Exception ex) { Console.WriteLine($"Player search by username failed: {ex.Message}"); }
            try
            {
                var byContact = await _supabase.Rpc<List<Dictionary<string, Guid>>>("search_auth_users", new { search_text = text });
                if (byContact != null) ids.AddRange(byContact.Select(x => x["user_id"]));
            }
            catch (Exception ex) { Console.WriteLine($"Player search by email or phone failed: {ex.Message}"); }

            return Json(await DescribePlayersAsync(ids.Distinct().Take(MaxPlayerResults)));
        }

        private async Task<IActionResult> ShowFormAsync(AdminUserForm form)
        {
            form.PlayerLabel = form.UserId is Guid id
                ? (await DescribePlayersAsync(new[] { id })).FirstOrDefault()?.Label ?? "(player's account deleted)"
                : null;
            return View("Edit", form);
        }

        // "username · email or phone" for each player that still exists
        private async Task<List<PlayerOption>> DescribePlayersAsync(IEnumerable<Guid> ids)
        {
            var list = ids.Distinct().ToList();
            if (list.Count == 0) return new();

            var names = new Dictionary<Guid, string?>();
            try
            {
                names = (await _supabase.From<Profile>().Select("id,username")
                        .Filter("id", Operator.In, list).Get())
                    .Models.ToDictionary(p => p.Id, p => p.Username);
            }
            catch (Exception ex) { Console.WriteLine($"Player names failed: {ex.Message}"); }

            using var http = new HttpClient { BaseAddress = new Uri(ApiClientFactory.BaseUrl) };
            http.DefaultRequestHeaders.Add("apikey", ApiClientFactory.ServiceRole);
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiClientFactory.ServiceRole);
            var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var players = new List<PlayerOption>();
            foreach (var id in list)
            {
                AuthUser? account = null;
                try
                {
                    var res = await http.GetAsync($"auth/v1/admin/users/{id}");
                    if (res.IsSuccessStatusCode)
                        account = JsonSerializer.Deserialize<AuthUser>(await res.Content.ReadAsStringAsync(), json);
                }
                catch (Exception ex) { Console.WriteLine($"Player lookup failed: {ex.Message}"); }
                if (account == null && !names.ContainsKey(id)) continue;

                var username = names.GetValueOrDefault(id);
                var contact = !string.IsNullOrWhiteSpace(account?.Email) ? account!.Email : account?.Phone;
                var label = string.Join(" · ", new[] { username, contact }.Where(s => !string.IsNullOrWhiteSpace(s)));
                players.Add(new PlayerOption(id, label.Length > 0 ? label : id.ToString(), username));
            }
            return players;
        }

        private async Task<AdminUser?> FindAsync(long id) =>
            (await _supabase.From<AdminUser>().Where(u => u.Id == id).Get()).Models.FirstOrDefault();
    }
}
