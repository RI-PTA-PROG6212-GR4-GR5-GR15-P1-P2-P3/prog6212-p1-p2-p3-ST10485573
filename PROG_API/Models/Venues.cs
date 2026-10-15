// Original source: Microsoft Learn - "Data annotations for model validation"
// URL: https://learn.microsoft.com/en-us/aspnet/core/mvc/models/data-annotations
// Accessed: 14 October 2026
// Adapted for: RaceDay venue model validation and relational entity fields.

using System.ComponentModel.DataAnnotations;

namespace PROG_API.Models;

public class Venues
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Address { get; set; }

    [Required]
    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Province { get; set; } = string.Empty;

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    [MaxLength(500)]
    public string? RouteMapUrl { get; set; }

    public ICollection<Events> Events { get; set; } = new List<Events>();
}
