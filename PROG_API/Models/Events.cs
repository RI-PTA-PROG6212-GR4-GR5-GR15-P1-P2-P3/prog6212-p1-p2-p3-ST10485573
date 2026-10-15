// // Original source: Microsoft Learn - "Data annotations for model validation"
// // URL: https://learn.microsoft.com/en-us/aspnet/core/mvc/models/data-annotations
// // Accessed: 3 September 2026
// //
// // Adapted for: RaceDay Events model - validation attributes for event data,
// // including required fields, date constraints, status validation, and relationship
// // configuration. Modified to include check constraints for Status values and
// // deadline-before-event validation at the database level.
// //
using System.ComponentModel.DataAnnotations;

namespace PROG_API.Models;

public class Events
{
    public int Id { get; set; }

    public int OrganiserId { get; set; }
    public Users Organiser { get; set; } = default!;

    public int VenueId { get; set; }
    public Venues Venue { get; set; } = default!;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public DateTime EventDate { get; set; }

    [Required]
    public DateTime RegistrationDeadline { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Draft";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Categories> Categories { get; set; } = new List<Categories>();
}
