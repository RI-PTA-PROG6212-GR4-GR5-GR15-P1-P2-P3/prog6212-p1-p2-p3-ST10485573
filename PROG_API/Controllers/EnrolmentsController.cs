// Original source: Microsoft Learn - "Create web APIs with ASP.NET Core"
// URL: https://learn.microsoft.com/en-us/aspnet/core/web-api/
// Accessed: 14 October 2026
// Adapted for: RaceDay enrolment workflows, participant authorization, and capacity validation.

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROG_API.Data;
using PROG_API.DTOs;
using PROG_API.Models;

namespace PROG_API.Controllers;

[ApiController]
[Route("api")]
public class EnrolmentsController : ControllerBase
{
    private readonly AppDbContext _context;

    public EnrolmentsController(AppDbContext context)
    {
        _context = context;
    }

    [Authorize(Roles = "Participant")]
    [HttpGet("enrolments")]
    [HttpGet("enrolments/mine")]
    public async Task<ActionResult<List<EnrollmentDto>>> GetMyEnrolments()
    {
        var userId = GetCurrentUserId();

        var enrolments = await _context.Enrolments
            .AsNoTracking()
            .Include(e => e.Participant)
            .Include(e => e.Category)
            .ThenInclude(c => c.Event)
            .Where(e => e.ParticipantId == userId)
            .OrderByDescending(e => e.EnrolmentDate)
            .Select(e => new EnrollmentDto
            {
                Id = e.Id,
                UserId = e.ParticipantId,
                UserFullName = e.Participant.FullName,
                EventId = e.Category.EventId,
                EventName = e.Category.Event.Name,
                CategoryId = e.CategoryId,
                CategoryName = e.Category.Name,
                Status = e.Status,
                BibNumber = e.BibNumber,
                EnrolledAt = e.EnrolmentDate
            })
            .ToListAsync();

        return Ok(enrolments);
    }

    [Authorize(Roles = "Organiser")]
    [HttpGet("events/{eventId:int}/enrolments")]
    public async Task<ActionResult<List<EnrollmentDto>>> GetEventEnrolments(int eventId)
    {
        var organiserId = GetCurrentUserId();
        var eventEntity = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId && e.OrganiserId == organiserId);
        if (eventEntity is null)
        {
            return Forbid();
        }

        var enrolments = await _context.Enrolments
            .AsNoTracking()
            .Include(e => e.Participant)
            .Include(e => e.Category)
            .ThenInclude(c => c.Event)
            .Where(e => e.Category.EventId == eventId)
            .OrderByDescending(e => e.EnrolmentDate)
            .Select(e => new EnrollmentDto
            {
                Id = e.Id,
                UserId = e.ParticipantId,
                UserFullName = e.Participant.FullName,
                EventId = e.Category.EventId,
                EventName = e.Category.Event.Name,
                CategoryId = e.CategoryId,
                CategoryName = e.Category.Name,
                Status = e.Status,
                BibNumber = e.BibNumber,
                EnrolledAt = e.EnrolmentDate
            })
            .ToListAsync();

        return Ok(enrolments);
    }

    [Authorize(Roles = "Participant")]
    [HttpPost("events/{eventId:int}/enrolments")]
    [HttpPost("enrolments")]
    public async Task<ActionResult<EnrollmentDto>> CreateEnrollment([FromBody] EnrollmentCreateRequest request, int? eventId = null)
    {
        var userId = GetCurrentUserId();
        var targetEventId = eventId ?? (await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CategoryId))?.EventId;

        if (!targetEventId.HasValue)
        {
            return BadRequest(new { message = "A valid category or event is required." });
        }

        var eventEntity = await _context.Events.Include(e => e.Categories).FirstOrDefaultAsync(e => e.Id == targetEventId.Value);
        if (eventEntity is null)
        {
            return NotFound();
        }

        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.EventId == targetEventId.Value);
        if (category is null)
        {
            return BadRequest(new { message = "Selected category is invalid for this event." });
        }

        if (!category.IsActive)
        {
            return BadRequest(new { message = "Selected category is inactive." });
        }

        if (eventEntity.RegistrationDeadline <= DateTime.UtcNow)
        {
            return BadRequest(new { message = "Registration for this event is closed." });
        }

        var alreadyEnrolled = await _context.Enrolments.AnyAsync(e =>
            e.ParticipantId == userId && e.Category.EventId == targetEventId.Value && e.Status != "Cancelled");
        if (alreadyEnrolled)
        {
            return Conflict(new { message = "You have already enrolled for this event." });
        }

        var enrollment = new Enrolments
        {
            ParticipantId = userId,
            CategoryId = request.CategoryId,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Confirmed" : request.Status,
            BibNumber = string.IsNullOrWhiteSpace(request.BibNumber)
                ? Guid.NewGuid().ToString("N")[..10]
                : request.BibNumber.Trim(),
            EnrolmentDate = DateTime.UtcNow
        };

        _context.Enrolments.Add(enrollment);
        await _context.SaveChangesAsync();

        var stored = await _context.Enrolments
            .Include(e => e.Participant)
            .Include(e => e.Category)
            .ThenInclude(c => c.Event)
            .FirstAsync(e => e.Id == enrollment.Id);

        return Created($"/api/enrolments/{stored.Id}", new EnrollmentDto
        {
            Id = stored.Id,
            UserId = stored.ParticipantId,
            UserFullName = stored.Participant.FullName,
            EventId = stored.Category.EventId,
            EventName = stored.Category.Event.Name,
            CategoryId = stored.CategoryId,
            CategoryName = stored.Category.Name,
            Status = stored.Status,
            BibNumber = stored.BibNumber,
            EnrolledAt = stored.EnrolmentDate
        });
    }

    [Authorize(Roles = "Participant")]
    [HttpDelete("enrolments/{enrolmentId:int}")]
    public async Task<IActionResult> CancelEnrollment(int enrolmentId)
    {
        var userId = GetCurrentUserId();
        var enrolment = await _context.Enrolments
            .Include(e => e.Category)
                .ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(e => e.Id == enrolmentId && e.ParticipantId == userId);

        if (enrolment is null)
        {
            return NotFound();
        }

        if (enrolment.Status == "Cancelled")
        {
            return Conflict(new { message = "This enrolment is already cancelled." });
        }

        if (enrolment.Category.Event.RegistrationDeadline <= DateTime.UtcNow)
        {
            return Conflict(new { message = "Registration deadline has passed." });
        }

        enrolment.Status = "Cancelled";
        enrolment.EnrolmentDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return NoContent();
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
