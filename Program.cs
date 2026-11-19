using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using RevUpIQ.Admin.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";

        // Page loads and form posts go to the login page; fetch() calls from a page
        // whose session expired get a 401 instead of the login page's HTML
        o.Events.OnRedirectToLogin = context =>
        {
            var fetchMode = context.Request.Headers["Sec-Fetch-Mode"].ToString();
            if (fetchMode is "" or "navigate")
                context.Response.Redirect(context.RedirectUri);
            else
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };

        // Accounts from the Admin Users page: turned off or changed sections take effect
        // within a minute (see AdminSignIn)
        o.Events.OnValidatePrincipal = AdminSignIn.ValidateAsync;
    });

// Every page and action needs a signed-in admin unless it is marked [AllowAnonymous]
builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Each account may only open the sections it was given (see AdminAccess)
builder.Services.AddControllersWithViews(o => o.Filters.Add<AdminAccessFilter>());

var url = ApiClientFactory.BaseUrl;
var key = ApiClientFactory.ServiceRole;

var supabase = new Supabase.Client(url, key);
await supabase.InitializeAsync();
builder.Services.AddSingleton(supabase);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// CSS, JS and images stay public so the login page can load them
app.MapStaticAssets().AllowAnonymous();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
