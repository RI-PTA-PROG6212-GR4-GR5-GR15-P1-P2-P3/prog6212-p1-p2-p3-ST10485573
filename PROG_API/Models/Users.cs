// // Original source: Microsoft Learn - "Data annotations for model validation"
// // URL: https://learn.microsoft.com/en-us/aspnet/core/mvc/models/data-annotations
// // Accessed: 3 September 2026
// //
// // Adapted for: RaceDay User model - validation attributes for user profile data,
// // including required fields, length constraints, email validation, and role enforcement.
// // Modification: Added custom check constraint for Role values ('Organiser', 'Participant')
// // and database-level constraints for data integrity.
//
using System.ComponentModel.DataAnnotations;

namespace PROG_API.Models;

public class Users
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Role { get; set; } = "Participant";

    [MaxLength(20)]
    public string? Phone { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Events> EventsCreated { get; set; } = new List<Events>();
    public ICollection<Enrolments> Enrolments { get; set; } = new List<Enrolments>();
    public ICollection<Results> ResultsCaptured { get; set; } = new List<Results>();
}
