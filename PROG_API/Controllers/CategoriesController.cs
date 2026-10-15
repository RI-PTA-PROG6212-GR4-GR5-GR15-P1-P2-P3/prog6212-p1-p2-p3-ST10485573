// Original source: Microsoft Learn - "Create web APIs with ASP.NET Core"
// URL: https://learn.microsoft.com/en-us/aspnet/core/web-api/
// Accessed: 14 October 2026
// Adapted for: RaceDay category CRUD, role checks, ownership checks, and soft deletion.

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
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _context;

    public CategoriesController(AppDbContext context)
    {
        _context = context;
    }

    [AllowAnonymous]
    [HttpGet("events/{eventId:int}/categories")]
    public async Task<ActionResult<List<EventCategoryDto>>> GetCategories(int eventId)
    {
        var categories = await _context.Categories
            .AsNoTracking()
            .Where(c => c.EventId == eventId && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new EventCategoryDto
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
            })
            .ToListAsync();

        return Ok(categories);
    }

    [Authorize(Roles = "Organiser")]
    [HttpPost("events/{eventId:int}/categories")]
    public async Task<ActionResult<EventCategoryDto>> CreateCategory(int eventId, [FromBody] EventCategoryCreateRequest request)
    {
        var organiserId = GetCurrentUserId();
        var eventEntity = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId && e.OrganiserId == organiserId);
        if (eventEntity is null)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Category name is required." });
        }

        var category = new Categories
        {
            EventId = eventId,
            Name = request.Name.Trim(),
            Distance = request.Distance,
            MaxParticipants = request.MaxParticipants,
            Price = request.Price,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return Ok(MapCategory(category));
    }

    [Authorize(Roles = "Organiser")]
    [HttpPut("categories/{categoryId:int}")]
    public async Task<ActionResult<EventCategoryDto>> UpdateCategory(int categoryId, [FromBody] EventCategoryCreateRequest request)
    {
        var organiserId = GetCurrentUserId();
        var category = await _context.Categories
            .Include(c => c.Event)
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.Event.OrganiserId == organiserId);

        if (category is null)
        {
            return Forbid();
        }

        category.Name = request.Name.Trim();
        category.Distance = request.Distance;
        category.MaxParticipants = request.MaxParticipants;
        category.Price = request.Price;
        category.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(MapCategory(category));
    }

    [Authorize(Roles = "Organiser")]
    [HttpDelete("categories/{categoryId:int}")]
    public async Task<IActionResult> DeleteCategory(int categoryId)
    {
        var organiserId = GetCurrentUserId();
        var category = await _context.Categories
            .Include(c => c.Event)
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.Event.OrganiserId == organiserId);

        if (category is null)
        {
            return Forbid();
        }

        category.IsActive = false;
        category.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static EventCategoryDto MapCategory(Categories category)
    {
        return new EventCategoryDto
        {
            Id = category.Id,
            EventId = category.EventId,
            Name = category.Name,
            CategoryType = "Distance",
            Distance = category.Distance,
            MaxParticipants = category.MaxParticipants,
            Price = category.Price,
            IsActive = category.IsActive,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
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
