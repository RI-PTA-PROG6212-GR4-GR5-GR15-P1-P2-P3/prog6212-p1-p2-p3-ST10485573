// Original source: Microsoft Learn - "Create web APIs with ASP.NET Core"
// URL: https://learn.microsoft.com/en-us/aspnet/core/web-api/
// Accessed: 14 October 2026
// Adapted for: RaceDay MVC organiser controller.
using Microsoft.AspNetCore.Mvc;
using PROG_API.DTOs;
using PROG_MVC.Services;

namespace PROG_MVC.Controllers;

public class OrganiserController : Controller
{
    private readonly IApiClient _api;
    public OrganiserController(IApiClient api) { _api = api; }

    private bool IsOrganiser() => HttpContext.Session.GetString("UserRole") == "Organiser";

    public async Task<IActionResult> Index(int page = 1)
    {
        if (!IsOrganiser()) return RedirectToAction("Login", "Auth");
        var all = await _api.GetAsync<List<EventDto>>("/api/events/mine") ?? new();
        const int pageSize = 6;
        var totalPages = (int)Math.Ceiling(all.Count / (double)pageSize);
        if (page < 1) page = 1;
        if (totalPages > 0 && page > totalPages) page = totalPages;
        var events = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        ViewBag.Page = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalEvents = all.Count;
        ViewBag.TotalEnrolments = all.Sum(e => e.EnrolmentCount);
        ViewBag.UpcomingCount = all.Count(e => e.EventDate >= DateTime.UtcNow);
        ViewBag.UpcomingEvents = all.Where(e => e.EventDate >= DateTime.UtcNow).OrderBy(e => e.EventDate).Take(5).ToList();
        return View(events);
    }

    [HttpGet]
    public IActionResult CreateEvent() => IsOrganiser() ? View(new EventCreateRequest()) : RedirectToAction("Login", "Auth");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEvent(EventCreateRequest request, IFormFile? banner)
    {
        if (!IsOrganiser()) return RedirectToAction("Login", "Auth");
        var response = await _api.PostAsync("/api/events", request);
        if (!response.IsSuccessStatusCode)
        {
            ModelState.AddModelError(string.Empty, "Failed to create event.");
            return View(request);
        }
        var ev = await response.Content.ReadFromJsonAsync<EventDto>();
        if (banner is not null && ev is not null)
        {
            var upload = await _api.PostFileAsync($"/api/events/{ev.Id}/banner", banner);
            if (!upload.IsSuccessStatusCode)
                TempData["Error"] = "The event was created, but the banner upload failed. You can add it by editing the event.";
            else
                TempData["Success"] = "Event and banner created.";
        }
        else
        {
            TempData["Success"] = "Event created.";
        }
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> EditEvent(int id)
    {
        if (!IsOrganiser()) return RedirectToAction("Login", "Auth");
        var ev = await _api.GetAsync<EventDto>($"/api/events/{id}");
        if (ev is null) return NotFound();
        var request = new EventCreateRequest
        {
            Name = ev.Name,
            Description = ev.Description,
            EventDate = ev.EventDate,
            RegistrationDeadline = ev.RegistrationDeadline,
            Location = ev.Location,
            DistanceKm = ev.DistanceKm,
            EventType = ev.EventType,
            Status = ev.Status,
            VenueId = ev.VenueId
        };
        ViewBag.EventId = id;
        ViewBag.BannerImageUrl = ev.BannerImageUrl;
        return View(request);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEvent(int id, EventCreateRequest request, IFormFile? banner)
    {
        if (!IsOrganiser()) return RedirectToAction("Login", "Auth");
        var response = await _api.PutAsync($"/api/events/{id}", request);
        if (!response.IsSuccessStatusCode)
        {
            ModelState.AddModelError(string.Empty, "Failed to update event.");
            ViewBag.EventId = id;
            return View(request);
        }
        if (banner is not null)
        {
            var upload = await _api.PostFileAsync($"/api/events/{id}/banner", banner);
            if (!upload.IsSuccessStatusCode)
                TempData["Error"] = "The event was saved, but the banner upload failed. Please try again.";
            else
                TempData["Success"] = "Event and banner updated.";
        }
        else
            TempData["Success"] = "Event updated.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        if (!IsOrganiser()) return RedirectToAction("Login", "Auth");
        var response = await _api.DeleteAsync($"/api/events/{id}");
        TempData[response.IsSuccessStatusCode ? "Success" : "Error"] = response.IsSuccessStatusCode
            ? "Event deleted."
            : "The event could not be deleted. Check that you own the event and try again.";
        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Categories(int eventId)
    {
        if (!IsOrganiser()) return RedirectToAction("Login", "Auth");
        ViewBag.EventId = eventId;
        var cats = await _api.GetAsync<List<EventCategoryDto>>($"/api/events/{eventId}/categories") ?? new();
        return View(cats);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(int eventId, EventCategoryCreateRequest request)
    {
        if (!IsOrganiser()) return RedirectToAction("Login", "Auth");
        var response = await _api.PostAsync($"/api/events/{eventId}/categories", request);
        TempData[response.IsSuccessStatusCode ? "Success" : "Error"] = response.IsSuccessStatusCode
            ? "Category added."
            : "Category could not be added. Check the name, distance and price.";
        return RedirectToAction("Categories", new { eventId });
    }

    [HttpGet]
    public async Task<IActionResult> EditCategory(int eventId, int categoryId)
    {
        if (!IsOrganiser()) return RedirectToAction("Login", "Auth");
        var cats = await _api.GetAsync<List<EventCategoryDto>>($"/api/events/{eventId}/categories") ?? new();
        var cat = cats.FirstOrDefault(c => c.Id == categoryId);
        if (cat is null) return NotFound();
        ViewBag.EventId = eventId;
        var request = new EventCategoryCreateRequest
        {
            Name = cat.Name,
            CategoryType = cat.CategoryType,
            MinAge = cat.MinAge,
            MaxAge = cat.MaxAge,
            MinDistanceKm = cat.MinDistanceKm,
            MaxDistanceKm = cat.MaxDistanceKm,
            Distance = cat.Distance,
            MaxParticipants = cat.MaxParticipants,
            Price = cat.Price
        };
        ViewBag.CategoryId = categoryId;
        return View(request);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(int eventId, int categoryId, EventCategoryCreateRequest request)
    {
        if (!IsOrganiser()) return RedirectToAction("Login", "Auth");
        var response = await _api.PutAsync($"/api/categories/{categoryId}", request);
        TempData[response.IsSuccessStatusCode ? "Success" : "Error"] = response.IsSuccessStatusCode
            ? "Category updated."
            : "Category could not be updated. Check the values and try again.";
        return RedirectToAction("Categories", new { eventId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int eventId, int categoryId)
    {
        if (!IsOrganiser()) return RedirectToAction("Login", "Auth");
        var response = await _api.DeleteAsync($"/api/categories/{categoryId}");
        TempData[response.IsSuccessStatusCode ? "Success" : "Error"] = response.IsSuccessStatusCode
            ? "Category deleted."
            : "Category could not be deleted. Check that you own the event and try again.";
        return RedirectToAction("Categories", new { eventId });
    }

    public async Task<IActionResult> Enrolments(int eventId)
    {
        if (!IsOrganiser()) return RedirectToAction("Login", "Auth");
        ViewBag.EventId = eventId;
        var list = await _api.GetAsync<List<EnrollmentDto>>($"/api/events/{eventId}/enrolments") ?? new();
        return View(list);
    }

    public async Task<IActionResult> Results(int eventId)
    {
        if (!IsOrganiser()) return RedirectToAction("Login", "Auth");
        ViewBag.EventId = eventId;
        var list = await _api.GetAsync<List<EnrollmentDto>>($"/api/events/{eventId}/enrolments") ?? new();
        var results = await _api.GetAsync<List<ResultDto>>($"/api/events/{eventId}/results") ?? new();
        ViewBag.Results = results;
        return View(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CaptureResult(int eventId, int enrollmentId, int position, string status, string? finishTimeText)
    {
        if (!IsOrganiser()) return RedirectToAction("Login", "Auth");
        var request = new ResultCreateRequest
        {
            EnrollmentId = enrollmentId,
            Position = position == 0 ? null : position,
            Status = status,
            FinishTimeText = finishTimeText
        };
        var response = await _api.PostAsync($"/api/events/{eventId}/results", request);
        TempData[response.IsSuccessStatusCode ? "Success" : "Error"] = response.IsSuccessStatusCode
            ? "Result saved."
            : "Result could not be saved. Finished entries need a valid time and a positive position.";
        return RedirectToAction("Results", new { eventId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditResult(int eventId, int resultId, int position, string status, string? finishTimeText)
    {
        if (!IsOrganiser()) return RedirectToAction("Login", "Auth");
        var request = new ResultCreateRequest
        {
            EnrollmentId = 0,
            Position = position == 0 ? null : position,
            Status = status,
            FinishTimeText = finishTimeText
        };
        var response = await _api.PutAsync($"/api/results/{resultId}", request);
        TempData[response.IsSuccessStatusCode ? "Success" : "Error"] = response.IsSuccessStatusCode
            ? "Result updated."
            : "Result could not be updated. Check the finish time, position and status.";
        return RedirectToAction("Results", new { eventId });
    }
}
