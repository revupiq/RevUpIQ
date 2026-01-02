using Microsoft.AspNetCore.Authentication.Cookies;
using RevUpIQ.Admin.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o => { o.LoginPath = "/Account/Login"; });

builder.Services.AddControllersWithViews();

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

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
