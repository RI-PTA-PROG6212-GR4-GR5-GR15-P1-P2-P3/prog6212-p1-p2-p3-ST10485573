// Original source: Microsoft Learn - "Create web APIs with ASP.NET Core"
// URL: https://learn.microsoft.com/en-us/aspnet/core/web-api/
// Accessed: 14 October 2026
// Adapted for: RaceDay MVC participant controller.
using Microsoft.AspNetCore.Mvc;
using PROG_API.DTOs;
using PROG_MVC.Services;

namespace PROG_MVC.Controllers;

public class ParticipantController : Controller
{
    private readonly IApiClient _api;
    public ParticipantController(IApiClient api) { _api = api; }

    private bool IsParticipant() => HttpContext.Session.GetString("UserRole") == "Participant";

    public async Task<IActionResult> Index(string? q = null, string? province = null, DateTime? from = null, DateTime? to = null, int page = 1)
    {
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(q)) qs.Add($"q={Uri.EscapeDataString(q)}");
        if (!string.IsNullOrWhiteSpace(province)) qs.Add($"province={Uri.EscapeDataString(province)}");
        if (from.HasValue) qs.Add($"from={from.Value:O}");
        if (to.HasValue) qs.Add($"to={to.Value:O}");
        var url = qs.Count == 0 ? "/api/events" : "/api/events?" + string.Join("&", qs);
        var all = await _api.GetAsync<List<EventDto>>(url) ?? new();
        const int pageSize = 6;
        var totalPages = (int)Math.Ceiling(all.Count / (double)pageSize);
        if (page < 1) page = 1;
        if (totalPages > 0 && page > totalPages) page = totalPages;
        var events = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        ViewBag.Q = q;
        ViewBag.Province = province;
        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.Page = page;
        ViewBag.TotalPages = totalPages;
        return View(events);
    }

    public async Task<IActionResult> Details(int id)
    {
        var ev = await _api.GetAsync<EventDto>($"/api/events/{id}");
        if (ev is null) return NotFound();
        var categories = await _api.GetAsync<List<EventCategoryDto>>($"/api/events/{id}/categories") ?? new();
        ViewBag.Categories = categories;
        return View(ev);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enrol(int eventId, int categoryId)
    {
        if (!IsParticipant()) return RedirectToAction("Login", "Auth");
        var request = new EnrollmentCreateRequest { CategoryId = categoryId, Status = "Confirmed" };
        var response = await _api.PostAsync($"/api/events/{eventId}/enrolments", request);
        if (!response.IsSuccessStatusCode)
        {
            TempData["Error"] = response.StatusCode == System.Net.HttpStatusCode.Conflict
                ? "You already have an active entry for this event. You can manage it from My Events."
                : "Enrolment failed. Check the event deadline and selected category, then try again.";
            return RedirectToAction("Details", new { id = eventId });
        }
        TempData["Success"] = "You have successfully entered the event.";
        return RedirectToAction("MyEvents");
    }

    public async Task<IActionResult> MyEvents()
    {
        if (!IsParticipant()) return RedirectToAction("Login", "Auth");
        var list = await _api.GetAsync<List<EnrollmentDto>>("/api/enrolments") ?? new();
        var payments = await _api.GetAsync<List<PaymentDto>>("/api/payments/mine") ?? new();
        ViewBag.PaidEnrolmentIds = payments.Select(payment => payment.EnrolmentId).ToHashSet();
        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> EditEnrolment(int id)
    {
        if (!IsParticipant()) return RedirectToAction("Login", "Auth");
        var enrolments = await _api.GetAsync<List<EnrollmentDto>>("/api/enrolments") ?? new();
        var enrolment = enrolments.FirstOrDefault(e => e.Id == id);
        if (enrolment is null) return NotFound();
        if (string.Equals(enrolment.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "This enrolment is cancelled and cannot be edited.";
            return RedirectToAction("MyEvents");
        }

        var categories = await _api.GetAsync<List<EventCategoryDto>>($"/api/events/{enrolment.EventId}/categories") ?? new();
        ViewBag.Enrolment = enrolment;
        ViewBag.Categories = categories;
        return View(enrolment);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEnrolment(int id, EnrollmentUpdateRequest request)
    {
        if (!IsParticipant()) return RedirectToAction("Login", "Auth");
        var response = await _api.PutAsync($"/api/enrolments/{id}", request);
        if (!response.IsSuccessStatusCode)
        {
            TempData["Error"] = "Enrolment update failed (deadline may have passed or category already taken).";
            return RedirectToAction("MyEvents");
        }

        TempData["Success"] = "Enrolment updated.";
        return RedirectToAction("MyEvents");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelEnrolment(int id)
    {
        if (!IsParticipant()) return RedirectToAction("Login", "Auth");
        var response = await _api.DeleteAsync($"/api/enrolments/{id}");
        if (!response.IsSuccessStatusCode)
        {
            TempData["Error"] = "Could not cancel this enrolment (deadline may have passed).";
        }
        else
        {
            TempData["Success"] = "Enrolment cancelled.";
        }
        return RedirectToAction("MyEvents");
    }

    public async Task<IActionResult> MyPayments()
    {
        if (!IsParticipant()) return RedirectToAction("Login", "Auth");
        var list = await _api.GetAsync<List<PaymentDto>>("/api/payments/mine") ?? new();
        return View(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int enrolmentId, string method = "Card")
    {
        if (!IsParticipant()) return RedirectToAction("Login", "Auth");
        var response = await _api.PostAsync("/api/payments", new PaymentCreateRequest
        {
            EnrolmentId = enrolmentId,
            Method = method
        });
        if (!response.IsSuccessStatusCode)
        {
            TempData["Error"] = "Payment failed.";
        }
        else
        {
            TempData["Success"] = "Payment recorded.";
        }
        return RedirectToAction("MyPayments");
    }

    public async Task<IActionResult> MyResults()
    {
        if (!IsParticipant()) return RedirectToAction("Login", "Auth");
        var list = await _api.GetAsync<List<ResultDto>>("/api/results/mine") ?? new();
        return View(list);
    }

    public async Task<IActionResult> Profile()
    {
        if (!IsParticipant()) return RedirectToAction("Login", "Auth");
        var user = await _api.GetAsync<UserDto>("/api/users/me");
        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, IFormFile? picture, string? currentPassword, string? newPassword)
    {
        if (!IsParticipant()) return RedirectToAction("Login", "Auth");
        var response = await _api.PutAsync("/api/users/me", request);
        if (!response.IsSuccessStatusCode)
        {
            TempData["Error"] = "Profile update failed.";
        }
        if (picture is not null)
        {
            var picResponse = await _api.PostFileAsync("/api/users/me/profile-picture", picture);
            if (picResponse.IsSuccessStatusCode)
            {
                var updatedUser = await picResponse.Content.ReadFromJsonAsync<UserDto>();
                if (updatedUser is not null)
                {
                    HttpContext.Session.SetString("ProfilePictureUrl", updatedUser.ProfilePictureUrl ?? "");
                }
            }
            else
            {
                TempData["Error"] = "Your profile was saved, but the picture upload failed. Please try again.";
            }
        }
        if (!string.IsNullOrWhiteSpace(currentPassword) && !string.IsNullOrWhiteSpace(newPassword))
        {
            var pwdResponse = await _api.PutAsync("/api/users/me/password", new UserPasswordChangeRequest
            {
                CurrentPassword = currentPassword,
                NewPassword = newPassword
            });
            if (!pwdResponse.IsSuccessStatusCode)
            {
                TempData["Error"] = "Password change failed.";
            }
        }
        return RedirectToAction("Profile");
    }
}
