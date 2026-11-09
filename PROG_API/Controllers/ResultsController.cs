// Original source: Microsoft Learn - "Create web APIs with ASP.NET Core"
// URL: https://learn.microsoft.com/en-us/aspnet/core/web-api/
// Accessed: 14 October 2026
// Adapted for: RaceDay result capture, validation, authorization, and leaderboard queries.

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROG_API.Data;
using PROG_API.DTOs;
using PROG_API.Models;
using ResultsEntity = PROG_API.Models.Results;

namespace PROG_API.Controllers;

[ApiController]
[Route("api")]
public class ResultsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ResultsController(AppDbContext context)
    {
        _context = context;
    }

    [Authorize(Roles = "Participant")]
    [HttpGet("results/mine")]
    [HttpGet("results/my")]
    public async Task<ActionResult<List<ResultDto>>> GetMyResults()
    {
        var userId = GetCurrentUserId();
        var results = await _context.Results
            .AsNoTracking()
            .Include(r => r.Enrolment)
                .ThenInclude(e => e.Participant)
            .Include(r => r.Enrolment)
                .ThenInclude(e => e.Category)
                    .ThenInclude(c => c.Event)
            .Where(r => r.Enrolment.ParticipantId == userId)
            .OrderByDescending(r => r.CapturedAt)
            .ToListAsync();

        var eventIds = results.Select(r => r.Enrolment.Category.EventId).Distinct().ToList();
        var counts = await _context.Results
            .AsNoTracking()
            .Where(r => eventIds.Contains(r.Enrolment.Category.EventId))
            .GroupBy(r => r.Enrolment.Category.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count);

        var dtos = results.Select(MapResult).ToList();
        foreach (var dto in dtos)
        {
            if (counts.TryGetValue(dto.EventId, out var c))
            {
                dto.TotalFinishers = c;
            }
        }
        return Ok(dtos);
    }

    [AllowAnonymous]
    [HttpGet("events/{eventId:int}/results")]
    public async Task<ActionResult<List<ResultDto>>> GetEventResults(int eventId)
    {
        var results = await _context.Results
            .AsNoTracking()
            .Include(r => r.Enrolment)
                .ThenInclude(e => e.Participant)
            .Include(r => r.Enrolment)
                .ThenInclude(e => e.Category)
                    .ThenInclude(c => c.Event)
            .Where(r => r.Enrolment.Category.EventId == eventId)
            .OrderBy(r => r.Position ?? int.MaxValue)
            .ToListAsync();

        var dtos = results.Select(MapResult).ToList();
        foreach (var dto in dtos)
        {
            dto.TotalFinishers = results.Count;
        }
        return Ok(dtos);
    }

    [Authorize(Roles = "Organiser")]
    [HttpPost("events/{eventId:int}/results")]
    [HttpPost("results")]
    public async Task<ActionResult<ResultDto>> CreateResult(int? eventId, [FromBody] ResultCreateRequest request)
    {
        var organiserId = GetCurrentUserId();
        if (request.EnrollmentId <= 0)
        {
            return BadRequest(new { message = "EnrollmentId is required." });
        }

        var enrollment = await _context.Enrolments
            .Include(e => e.Participant)
            .Include(e => e.Category)
                .ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(e =>
                e.Id == request.EnrollmentId
                && (!eventId.HasValue || e.Category.EventId == eventId.Value));

        if (enrollment is null)
        {
            return NotFound(new { message = "Enrollment not found." });
        }

        if (enrollment.Category.Event.OrganiserId != organiserId)
        {
            return Forbid();
        }

        var finishTime = request.FinishTime ?? ParseFinishTime(request.FinishTimeText);
        if (!IsValidResultData(request.Status, finishTime, request.Position))
        {
            return BadRequest(new { message = "Finished results require a finish time and positive position; DNF/DSQ results must not include either." });
        }

        var result = await _context.Results.FirstOrDefaultAsync(r => r.EnrolmentId == enrollment.Id);
        if (result is null)
        {
            result = new ResultsEntity
            {
                EnrolmentId = enrollment.Id,
                CapturedBy = organiserId
            };
            _context.Results.Add(result);
        }

        result.FinishTime = finishTime;
        result.Position = request.Position;
        result.Status = request.Status;
        result.CapturedBy = organiserId;
        result.CapturedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        result.Enrolment = enrollment;
        var dto = MapResult(result);
        dto.TotalFinishers = await _context.Results.CountAsync(r => r.Enrolment.Category.EventId == enrollment.Category.EventId);
        return Ok(dto);
    }

    [Authorize(Roles = "Organiser")]
    [HttpPut("results/{resultId:int}")]
    public async Task<ActionResult<ResultDto>> UpdateResult(int resultId, [FromBody] ResultCreateRequest request)
    {
        var organiserId = GetCurrentUserId();

        var result = await _context.Results
            .Include(r => r.Enrolment)
                .ThenInclude(e => e.Participant)
            .Include(r => r.Enrolment)
                .ThenInclude(e => e.Category)
                    .ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(r => r.Id == resultId);

        if (result is null)
        {
            return NotFound(new { message = "Result not found." });
        }

        if (result.Enrolment.Category.Event.OrganiserId != organiserId)
        {
            return Forbid();
        }

        var finishTime = request.FinishTime ?? ParseFinishTime(request.FinishTimeText);
        if (!IsValidResultData(request.Status, finishTime, request.Position))
        {
            return BadRequest(new { message = "Finished results require a finish time and positive position; DNF/DSQ results must not include either." });
        }

        result.FinishTime = finishTime;
        result.Position = request.Position;
        result.Status = request.Status;
        result.CapturedBy = organiserId;
        result.CapturedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var updatedDto = MapResult(result);
        updatedDto.TotalFinishers = await _context.Results.CountAsync(r => r.Enrolment.Category.EventId == result.Enrolment.Category.EventId);
        return Ok(updatedDto);
    }

    private static ResultDto MapResult(ResultsEntity result)
    {
        return new ResultDto
        {
            Id = result.Id,
            EnrollmentId = result.EnrolmentId,
            UserId = result.Enrolment.ParticipantId,
            EventId = result.Enrolment.Category.EventId,
            CapturedBy = result.CapturedBy,
            UserFullName = result.Enrolment.Participant.FullName,
            EventName = result.Enrolment.Category.Event.Name,
            EventDate = result.Enrolment.Category.Event.EventDate,
            CategoryName = result.Enrolment.Category.Name,
            FinishTime = result.FinishTime,
            Position = result.Position,
            Status = result.Status,
            CreatedAt = result.CapturedAt
        };
    }

    private static bool IsValidResultData(string status, TimeSpan? finishTime, int? position)
    {
        return status switch
        {
            "Finished" => finishTime.HasValue && position > 0,
            "DNF" or "DSQ" => !finishTime.HasValue && !position.HasValue,
            _ => false
        };
    }

    private static TimeSpan? ParseFinishTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return TimeSpan.TryParse(value, out var parsed) ? parsed : null;
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
