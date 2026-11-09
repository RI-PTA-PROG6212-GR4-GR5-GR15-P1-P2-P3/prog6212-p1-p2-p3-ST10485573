// Original source: Microsoft Learn - "Create web APIs with ASP.NET Core"
// URL: https://learn.microsoft.com/en-us/aspnet/core/web-api/
// Accessed: 14 October 2026
// Adapted for: RaceDay blob storage upload endpoints.
using System.Security.Claims;
using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROG_API.Data;
using PROG_API.DTOs;
using PROG_API.Models;
using PROG_API.Services;

namespace PROG_API.Controllers;

[ApiController]
[Route("api")]
public class BlobStorageController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly BlobStorageService _blobStorage;

    public BlobStorageController(AppDbContext context, BlobStorageService blobStorage)
    {
        _context = context;
        _blobStorage = blobStorage;
    }

    [AllowAnonymous]
    [HttpGet("blob/{container}/{fileName}")]
    public async Task<IActionResult> GetBlob(string container, string fileName, CancellationToken ct)
    {
        try
        {
            var (content, contentType) = await _blobStorage.OpenReadAsync(container, fileName, ct);
            return File(content, contentType, enableRangeProcessing: true);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            return NotFound();
        }
    }

    [Authorize(Roles = "Organiser")]
    [HttpPost("events/{eventId:int}/banner")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<EventDto>> UploadEventBanner(int eventId, IFormFile file, CancellationToken ct)
    {
        var organiserId = GetCurrentUserId();
        var eventEntity = await _context.Events
            .Include(e => e.Organiser)
            .Include(e => e.Venue)
            .Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.Id == eventId && e.OrganiserId == organiserId, ct);

        if (eventEntity is null)
        {
            return Forbid();
        }

        string url;
        try
        {
            url = await _blobStorage.UploadAsync(file, "event-banners", ct);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        eventEntity.BannerImageUrl = url;
        eventEntity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return Ok(new EventDto
        {
            Id = eventEntity.Id,
            Name = eventEntity.Name,
            Description = eventEntity.Description ?? string.Empty,
            EventDate = eventEntity.EventDate,
            RegistrationDeadline = eventEntity.RegistrationDeadline,
            Status = eventEntity.Status,
            VenueId = eventEntity.VenueId,
            VenueName = eventEntity.Venue?.Name ?? string.Empty,
            Location = eventEntity.Venue?.City ?? string.Empty,
            DistanceKm = eventEntity.Categories.Select(c => c.Distance).FirstOrDefault(),
            EventType = "Run",
            OrganiserId = eventEntity.OrganiserId,
            OrganiserName = eventEntity.Organiser?.FullName ?? string.Empty,
            BannerImageUrl = eventEntity.BannerImageUrl,
            EnrolmentCount = eventEntity.Categories.SelectMany(c => c.Enrolments).Count(),
            IsActive = eventEntity.IsActive,
            CreatedAt = eventEntity.CreatedAt,
            UpdatedAt = eventEntity.UpdatedAt,
            Categories = eventEntity.Categories.Select(c => new EventCategoryDto
            {
                Id = c.Id,
                EventId = c.EventId,
                Name = c.Name,
                CategoryType = "Distance",
                Distance = c.Distance,
                MaxParticipants = c.MaxParticipants,
                Price = c.Price,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            }).ToList()
        });
    }

    [Authorize]
    [HttpPost("users/me/profile-picture")]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<UserDto>> UploadProfilePicture(IFormFile file, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return NotFound();
        }

        string url;
        try
        {
            url = await _blobStorage.UploadAsync(file, "profile-pictures", ct);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        user.ProfilePictureUrl = url;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return Ok(AuthService.ToUserDto(user));
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(claim) || !int.TryParse(claim, out var userId))
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        return userId;
    }
}
