// Original source: Microsoft Learn - "Session state in ASP.NET Core"
// URL: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/app-state
// Accessed: 14 October 2026
// Adapted for: RaceDay MVC application startup and session configuration.
using PROG_MVC.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
    .ConfigureApplicationPartManager(manager =>
    {
        // PROG_API is referenced for DTOs, but its controllers belong to the API host.
        // Discovering them here gives the API and MVC AuthController.Login actions
        // competing routes during URL generation.
        foreach (var apiPart in manager.ApplicationParts
                     .Where(part => part.Name == "PROG_API")
                     .ToArray())
        {
            manager.ApplicationParts.Remove(apiPart);
        }
    });
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AssetUrlResolver>();
builder.Services.AddHttpClient<IApiClient, ApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "https://localhost:5001/");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program { }
