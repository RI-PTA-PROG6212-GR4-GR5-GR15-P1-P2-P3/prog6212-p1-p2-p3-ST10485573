// // Original source: Microsoft Learn - "Data annotations for model validation"
// // URL: https://learn.microsoft.com/en-us/aspnet/core/mvc/models/data-annotations
// // Accessed: 3 September 2026
// //
// // Adapted for: RaceDay Results model - validation attributes for race results,
// // including status validation, finish time and position requirements. Modified to
// // include database-level check constraints for result status data and position values.
//
using System.ComponentModel.DataAnnotations;

namespace PROG_API.Models;

public class Results
{
    public int Id { get; set; }

    public int EnrolmentId { get; set; }
    public Enrolments Enrolment { get; set; } = default!;

    public TimeSpan? FinishTime { get; set; }

    public int? Position { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Finished";

    public int CapturedBy { get; set; }
    public Users Capturer { get; set; } = default!;

    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}
