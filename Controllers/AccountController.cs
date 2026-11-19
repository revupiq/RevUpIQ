using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevUpIQ.Admin.Infrastructure;
using RevUpIQ.Admin.Models;

public class AccountController : Controller
{
    private readonly Supabase.Client _supabase;

    public AccountController(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    // The only pages reachable without signing in (see the fallback policy in Program.cs)
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login() => View();

    // The owner's built-in login, or an account from the Admin Users page
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        var username = (model.Username ?? "").Trim();
        var password = model.Password ?? "";

        if (AdminSignIn.IsOwnerLogin(username, password))
        {
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, AdminSignIn.OwnerPrincipal());
            return RedirectToAction("Index", "Home");
        }

        try
        {
            var account = await AdminSignIn.FindAccountAsync(_supabase, username, password);
            if (account != null)
            {
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, AdminSignIn.PrincipalFor(account));
                return RedirectToAction("Index", "Home");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Admin sign-in lookup failed: {ex.Message}");
            ModelState.AddModelError("", "Signing in failed. Please try again.");
            return View(model);
        }

        // Slows down password guessing a little
        await Task.Delay(500);
        ModelState.AddModelError("", "Invalid username or password");
        return View(model);
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return RedirectToAction("Login");
    }
}
