// Original source: Microsoft Learn - "Create web APIs with ASP.NET Core"
// URL: https://learn.microsoft.com/en-us/aspnet/core/web-api/
// Accessed: 14 October 2026
// Adapted for: RaceDay MVC home controller.
using Microsoft.AspNetCore.Mvc;
using PROG_API.DTOs;
using PROG_MVC.Services;

namespace PROG_MVC.Controllers;

public class HomeController : Controller
{
    private readonly IApiClient _api;
    public HomeController(IApiClient api) { _api = api; }

    public async Task<IActionResult> Index()
    {
        var events = (await _api.GetAsync<List<EventDto>>("/api/events") ?? new())
            .OrderBy(eventItem => eventItem.EventDate)
            .ToList();
        return View(events);
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
