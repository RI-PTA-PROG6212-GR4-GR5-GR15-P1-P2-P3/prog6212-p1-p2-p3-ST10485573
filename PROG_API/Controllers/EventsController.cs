// Original source: Microsoft Learn - "Create web APIs with ASP.NET Core"
// URL: https://learn.microsoft.com/en-us/aspnet/core/web-api/
// Accessed: 14 October 2026
// Adapted for: RaceDay event CRUD, filtering, organizer ownership, and soft deletion.

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROG_API.Data;
using PROG_API.DTOs;
using PROG_API.Models;

namespace PROG_API.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly AppDbContext _context;

    public EventsController(AppDbContext context)
    {
        _context = context;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<List<EventDto>>> GetEvents(
        [FromQuery] string? province = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? q = null)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            return BadRequest(new { message = "The 'from' date must be earlier than or equal to the 'to' date." });
        }

        var query = _context.Events
            .AsNoTracking()
            .Include(e => e.Organiser)
            .Include(e => e.Venue)
            .Include(e => e.Categories)
            .Where(e => e.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(province))
        {
            var provinceName = province.Trim();
            query = query.Where(e => e.Venue != null && e.Venue.Province == provinceName);
        }

        if (from.HasValue)
        {
            query = query.Where(e => e.EventDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(e => e.EventDate <= to.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var keyword = q.Trim();
            query = query.Where(e =>
                e.Name.Contains(keyword) ||
                (e.Description != null && e.Description.Contains(keyword)) ||
                (e.Venue != null && (e.Venue.City.Contains(keyword) || e.Venue.Province.Contains(keyword))));
        }

        var events = await query
            .OrderByDescending(e => e.EventDate)
            .Select(e => new EventDto
            {
                Id = e.Id,
                Name = e.Name,
                Description = e.Description ?? string.Empty,
                EventDate = e.EventDate,
                RegistrationDeadline = e.RegistrationDeadline,
                Status = e.Status,
                VenueId = e.VenueId,
                VenueName = e.Venue != null ? e.Venue.Name : string.Empty,
                Location = e.Venue != null ? e.Venue.City : string.Empty,
                DistanceKm = e.Categories.Select(c => c.Distance).FirstOrDefault(),
                EventType = "Run",
                OrganiserId = e.OrganiserId,
                OrganiserName = e.Organiser.FullName,
                IsActive = e.IsActive,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt,
                Categories = e.Categories.Select(c => new EventCategoryDto
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
            })
            .ToListAsync();

        return Ok(events);
    }

    [Authorize(Roles = "Organiser")]
    [HttpGet("mine")]
    public async Task<ActionResult<List<EventDto>>> GetMyEvents()
    {
        var organiserId = GetCurrentUserId();
        var events = await _context.Events
            .AsNoTracking()
            .Include(e => e.Organiser)
            .Include(e => e.Venue)
            .Include(e => e.Categories)
            .Where(e => e.OrganiserId == organiserId)
            .OrderByDescending(e => e.EventDate)
            .Select(e => new EventDto
            {
                Id = e.Id,
                Name = e.Name,
                Description = e.Description ?? string.Empty,
                EventDate = e.EventDate,
                RegistrationDeadline = e.RegistrationDeadline,
                Status = e.Status,
                VenueId = e.VenueId,
                VenueName = e.Venue.Name,
                Location = e.Venue.City,
                DistanceKm = e.Categories.Select(c => c.Distance).FirstOrDefault(),
                EventType = "Run",
                OrganiserId = e.OrganiserId,
                OrganiserName = e.Organiser.FullName,
                IsActive = e.IsActive,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt,
                Categories = e.Categories.Select(c => new EventCategoryDto
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
            })
            .ToListAsync();

        return Ok(events);
    }

    [AllowAnonymous]
    [HttpGet("{eventId:int}")]
    public async Task<ActionResult<EventDto>> GetEvent(int eventId)
    {
        var eventEntity = await _context.Events
            .AsNoTracking()
            .Include(e => e.Organiser)
            .Include(e => e.Venue)
            .Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.Id == eventId && e.IsActive);

        if (eventEntity is null)
        {
            return NotFound();
        }

        return Ok(MapEventDto(eventEntity));
    }

    [Authorize(Roles = "Organiser")]
    [HttpPost]
    public async Task<ActionResult<EventDto>> CreateEvent([FromBody] EventCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Event name is required." });
        }

        var organiserId = GetCurrentUserId();

        var venueId = request.VenueId ?? await _context.Venues
            .Select(v => (int?)v.Id)
            .FirstOrDefaultAsync();
        if (!venueId.HasValue)
        {
            return BadRequest(new { message = "VenueId is required." });
        }

        var venueExists = await _context.Venues.AnyAsync(v => v.Id == venueId.Value);
        if (!venueExists)
        {
            return NotFound(new { message = "Venue not found." });
        }

        var eventEntity = new Events
        {
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? "" : request.Description.Trim(),
            EventDate = request.EventDate,
            RegistrationDeadline = request.RegistrationDeadline ?? request.EventDate.AddDays(-1),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Draft" : request.Status,
            VenueId = venueId.Value,
            OrganiserId = organiserId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var created = await _context.Events
            .Include(e => e.Organiser)
            .Include(e => e.Venue)
            .Include(e => e.Categories)
            .FirstAsync(e => e.Id == eventEntity.Id);

        return CreatedAtAction(nameof(GetEvent), new { eventId = created.Id }, MapEventDto(created));
    }

    [Authorize(Roles = "Organiser")]
    [HttpPut("{eventId:int}")]
    public async Task<IActionResult> UpdateEvent(int eventId, [FromBody] EventCreateRequest request)
    {
        var organiserId = GetCurrentUserId();
        var eventEntity = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId && e.OrganiserId == organiserId);
        if (eventEntity is null)
        {
            return Forbid();
        }

        eventEntity.Name = request.Name.Trim();
        eventEntity.Description = string.IsNullOrWhiteSpace(request.Description) ? string.Empty : request.Description.Trim();
        eventEntity.EventDate = request.EventDate;
        eventEntity.RegistrationDeadline = request.RegistrationDeadline ?? request.EventDate.AddDays(-1);
        eventEntity.Status = string.IsNullOrWhiteSpace(request.Status) ? "Draft" : request.Status;
        if (request.VenueId.HasValue)
        {
            var venueExists = await _context.Venues.AnyAsync(v => v.Id == request.VenueId.Value);
            if (!venueExists)
            {
                return NotFound(new { message = "Venue not found." });
            }

            eventEntity.VenueId = request.VenueId.Value;
        }
        eventEntity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Roles = "Organiser")]
    [HttpDelete("{eventId:int}")]
    public async Task<IActionResult> DeleteEvent(int eventId)
    {
        var organiserId = GetCurrentUserId();
        var eventEntity = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId && e.OrganiserId == organiserId);
        if (eventEntity is null)
        {
            return Forbid();
        }

        eventEntity.IsActive = false;
        eventEntity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private EventDto MapEventDto(Events eventEntity)
    {
        return new EventDto
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
        };
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
