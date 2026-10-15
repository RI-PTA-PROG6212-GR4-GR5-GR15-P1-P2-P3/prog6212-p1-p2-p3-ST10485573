// Original source: Microsoft Learn - "Create web APIs with ASP.NET Core"
// URL: https://learn.microsoft.com/en-us/aspnet/core/web-api/
// Accessed: 14 October 2026
// Adapted for: RaceDay venue CRUD and weather-proxy endpoint.

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
public class VenuesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly HttpClient _httpClient;

    public VenuesController(AppDbContext context, HttpClient httpClient)
    {
        _context = context;
        _httpClient = httpClient;
    }

    [AllowAnonymous]
    [HttpGet("venues")]
    public async Task<ActionResult<List<VenueDto>>> GetVenues()
    {
        var venues = await _context.Venues
            .AsNoTracking()
            .OrderBy(v => v.City)
            .ThenBy(v => v.Name)
            .Select(v => new VenueDto
            {
                Id = v.Id,
                Name = v.Name,
                Address = v.Address,
                City = v.City,
                Province = v.Province,
                Latitude = v.Latitude,
                Longitude = v.Longitude,
                RouteMapUrl = v.RouteMapUrl
            })
            .ToListAsync();

        return Ok(venues);
    }

    [AllowAnonymous]
    [HttpGet("venues/{venueId:int}")]
    public async Task<ActionResult<VenueDto>> GetVenue(int venueId)
    {
        var venue = await _context.Venues
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == venueId);

        if (venue is null)
        {
            return NotFound();
        }

        return Ok(new VenueDto
        {
            Id = venue.Id,
            Name = venue.Name,
            Address = venue.Address,
            City = venue.City,
            Province = venue.Province,
            Latitude = venue.Latitude,
            Longitude = venue.Longitude,
            RouteMapUrl = venue.RouteMapUrl
        });
    }

    [AllowAnonymous]
    [HttpGet("venues/{venueId:int}/weather")]
    public async Task<IActionResult> GetVenueWeather(int venueId)
    {
        var venue = await _context.Venues.AsNoTracking().FirstOrDefaultAsync(v => v.Id == venueId);
        if (venue is null)
        {
            return NotFound();
        }

        if (!venue.Latitude.HasValue || !venue.Longitude.HasValue)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Weather provider unavailable for this venue." });
        }

        try
        {
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={venue.Latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}&longitude={venue.Longitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}&current=temperature_2m,weather_code&timezone=auto";
            using var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = "Weather provider unavailable." });
            }

            var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
            return Ok(new
            {
                venueId = venue.Id,
                name = venue.Name,
                latitude = venue.Latitude,
                longitude = venue.Longitude,
                data = payload ?? new Dictionary<string, object>()
            });
        }
        catch
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Weather provider unavailable." });
        }
    }

    [Authorize(Roles = "Organiser")]
    [HttpPost("venues")]
    public async Task<ActionResult<VenueDto>> CreateVenue([FromBody] VenueCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.City) || string.IsNullOrWhiteSpace(request.Province))
        {
            return BadRequest(new { message = "Venue name, city, and province are required." });
        }

        var duplicate = await _context.Venues.AnyAsync(v => v.Name == request.Name.Trim() && v.City == request.City.Trim());
        if (duplicate)
        {
            return Conflict(new { message = "A venue with this name and city already exists." });
        }

        var venue = new Venues
        {
            Name = request.Name.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            City = request.City.Trim(),
            Province = request.Province.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            RouteMapUrl = string.IsNullOrWhiteSpace(request.RouteMapUrl) ? null : request.RouteMapUrl.Trim()
        };

        _context.Venues.Add(venue);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetVenue), new { venueId = venue.Id }, new VenueDto
        {
            Id = venue.Id,
            Name = venue.Name,
            Address = venue.Address,
            City = venue.City,
            Province = venue.Province,
            Latitude = venue.Latitude,
            Longitude = venue.Longitude,
            RouteMapUrl = venue.RouteMapUrl
        });
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
