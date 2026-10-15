// // Original source: Microsoft Learn - "Data annotations for model validation"
// // URL: https://learn.microsoft.com/en-us/aspnet/core/mvc/models/data-annotations
// // Accessed: 3 September 2026
// //
// // Adapted for: RaceDay Payments model - validation attributes for payment data,
// // including amount range, method constraints, and status values. Modified to include
// // database-level check constraints for payment status and amount validation.
//
using System.ComponentModel.DataAnnotations;

namespace PROG_API.Models;

public class Payments
{
    public int Id { get; set; }

    public int EnrolmentId { get; set; }
    public Enrolments Enrolment { get; set; } = default!;

    [Range(0, double.MaxValue)]
    public decimal Amount { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(30)]
    public string Method { get; set; } = "Card";

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";
}
