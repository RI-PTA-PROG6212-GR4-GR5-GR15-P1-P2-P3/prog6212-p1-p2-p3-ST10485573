// Original source: Microsoft Learn - "Create web APIs with ASP.NET Core"
// URL: https://learn.microsoft.com/en-us/aspnet/core/web-api/
// Accessed: 14 October 2026
// Adapted for: RaceDay MVC authentication controller.
using Microsoft.AspNetCore.Mvc;
using PROG_API.DTOs;
using PROG_MVC.Services;

namespace PROG_MVC.Controllers;

public class AuthController : Controller
{
    private readonly IApiClient _api;
    public AuthController(IApiClient api) { _api = api; }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginRequest request, string? returnUrl = null)
    {
        var response = await _api.PostAsync("/api/auth/login", request);
        if (!response.IsSuccessStatusCode)
        {
            ModelState.AddModelError(string.Empty, "Invalid credentials.");
            return View(request);
        }
        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        _api.SetToken(auth?.Token);
        HttpContext.Session.SetString("UserName", auth?.User.FullName ?? "");
        HttpContext.Session.SetString("UserRole", auth?.User.Role ?? "");
        HttpContext.Session.SetInt32("UserId", auth?.User.Id ?? 0);
        HttpContext.Session.SetString("ProfilePictureUrl", auth?.User.ProfilePictureUrl ?? "");

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", auth?.User.Role == "Organiser" ? "Organiser" : "Participant");
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var response = await _api.PostAsync("/api/auth/register", request);
        if (!response.IsSuccessStatusCode)
        {
            ModelState.AddModelError(string.Empty, "Registration failed.");
            return View(request);
        }
        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        _api.SetToken(auth?.Token);
        HttpContext.Session.SetString("UserName", auth?.User.FullName ?? "");
        HttpContext.Session.SetString("UserRole", auth?.User.Role ?? "");
        HttpContext.Session.SetInt32("UserId", auth?.User.Id ?? 0);
        HttpContext.Session.SetString("ProfilePictureUrl", auth?.User.ProfilePictureUrl ?? "");
        return RedirectToAction("Index", auth?.User.Role == "Organiser" ? "Organiser" : "Participant");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        _api.SetToken(null);
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home");
    }
}