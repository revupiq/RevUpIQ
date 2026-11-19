using Microsoft.AspNetCore.Mvc;
using RevUpIQ.Admin.Infrastructure;
using RevUpIQ.Admin.Models.AdminUsers;
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

            return View(new AdminUserForm
            {
                Id = user.Id,
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
                return View("Edit", form);

            try
            {
                var sameName = await AdminSignIn.FindByUsernameAsync(_supabase, username);
                if (sameName != null && sameName.Id != form.Id)
                {
                    ModelState.AddModelError(nameof(form.Username), "Another account already has this name.");
                    return View("Edit", form);
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
                        IsActive = form.IsActive
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Admin user save failed: {ex.Message}");
                ModelState.AddModelError("", ex.Message.Contains("PGRST205") ? MissingTable : "Saving failed. Please try again.");
                return View("Edit", form);
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

        private async Task<AdminUser?> FindAsync(long id) =>
            (await _supabase.From<AdminUser>().Where(u => u.Id == id).Get()).Models.FirstOrDefault();
    }
}
