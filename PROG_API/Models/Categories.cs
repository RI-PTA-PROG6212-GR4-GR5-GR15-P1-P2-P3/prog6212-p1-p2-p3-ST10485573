// // Original source: Microsoft Learn - "Data annotations for model validation"
// // URL: https://learn.microsoft.com/en-us/aspnet/core/mvc/models/data-annotations
// // Accessed: 3 September 2026
// //
// // Adapted for: RaceDay Categories model - validation attributes for event categories,
// // including range constraints for distance, max participants, and price. Modified to
// // include database-level check constraints for data integrity and validation.
// //
using System.ComponentModel.DataAnnotations;

namespace PROG_API.Models;

public class Categories
{
    public int Id { get; set; }

    public int EventId { get; set; }
    public Events Event { get; set; } = default!;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal Distance { get; set; }

    [Range(1, int.MaxValue)]
    public int MaxParticipants { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Enrolments> Enrolments { get; set; } = new List<Enrolments>();
}
