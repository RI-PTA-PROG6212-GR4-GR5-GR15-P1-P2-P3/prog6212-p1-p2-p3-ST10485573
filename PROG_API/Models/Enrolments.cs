// // Original source: Microsoft Learn - "Data annotations for model validation"
// // URL: https://learn.microsoft.com/en-us/aspnet/core/mvc/models/data-annotations
// // Accessed: 3 September 2026
// //
// // Adapted for: RaceDay Enrolments model - validation attributes for enrolment data,
// // including status constraints, bib number length, and relationship configuration.
// // Modified to include database-level unique constraints and check constraints for
// // status values and capacity management.
//
using System.ComponentModel.DataAnnotations;

namespace PROG_API.Models;

public class Enrolments
{
    public int Id { get; set; }

    public int ParticipantId { get; set; }
    public Users Participant { get; set; } = default!;

    public int CategoryId { get; set; }
    public Categories Category { get; set; } = default!;

    public DateTime EnrolmentDate { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Confirmed";

    [Required]
    [MaxLength(10)]
    public string BibNumber { get; set; } = string.Empty;

    public Payments? Payment { get; set; }
    public Results? Result { get; set; }
}
