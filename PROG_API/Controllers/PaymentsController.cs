// Original source: Microsoft Learn - "Create web APIs with ASP.NET Core"
// URL: https://learn.microsoft.com/en-us/aspnet/core/web-api/
// Accessed: 14 October 2026
// Adapted for: RaceDay payment recording, authorization, and server-side amount derivation.

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
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _context;

    public PaymentsController(AppDbContext context)
    {
        _context = context;
    }

    [Authorize(Roles = "Participant")]
    [HttpPost("payments")]
    public async Task<ActionResult<PaymentDto>> CreatePayment([FromBody] PaymentCreateRequest request)
    {
        var userId = GetCurrentUserId();

        var enrolment = await _context.Enrolments
            .Include(e => e.Category)
            .FirstOrDefaultAsync(e => e.Id == request.EnrolmentId && e.ParticipantId == userId);

        if (enrolment is null)
        {
            return NotFound(new { message = "Enrolment not found for this user." });
        }

        var paymentExists = await _context.Payments.AnyAsync(p => p.EnrolmentId == request.EnrolmentId);
        if (paymentExists)
        {
            return Conflict(new { message = "A payment already exists for this enrolment." });
        }

        var validMethods = new[] { "Card", "EFT", "Cash" };
        if (!validMethods.Contains(request.Method, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Payment method is invalid." });
        }

        var payment = new Payments
        {
            EnrolmentId = request.EnrolmentId,
            Amount = enrolment.Category.Price,
            PaymentDate = DateTime.UtcNow,
            Method = request.Method.Trim(),
            Status = "Paid"
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        return Created($"/api/payments/{payment.Id}", new PaymentDto
        {
            Id = payment.Id,
            EnrolmentId = payment.EnrolmentId,
            Amount = payment.Amount,
            PaymentDate = payment.PaymentDate,
            Method = payment.Method,
            Status = payment.Status
        });
    }

    [Authorize(Roles = "Participant")]
    [HttpGet("payments/mine")]
    public async Task<ActionResult<List<PaymentDto>>> GetMyPayments()
    {
        var userId = GetCurrentUserId();

        var payments = await _context.Payments
            .AsNoTracking()
            .Include(p => p.Enrolment)
            .ThenInclude(e => e.Category)
            .Where(p => p.Enrolment.ParticipantId == userId)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new PaymentDto
            {
                Id = p.Id,
                EnrolmentId = p.EnrolmentId,
                Amount = p.Amount,
                PaymentDate = p.PaymentDate,
                Method = p.Method,
                Status = p.Status
            })
            .ToListAsync();

        return Ok(payments);
    }

    [Authorize(Roles = "Organiser")]
    [HttpGet("enrolments/{enrolmentId:int}/payment")]
    public async Task<ActionResult<PaymentDto>> GetEnrolmentPayment(int enrolmentId)
    {
        var organiserId = GetCurrentUserId();

        var enrolment = await _context.Enrolments
            .Include(e => e.Category)
                .ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(e => e.Id == enrolmentId && e.Category.Event.OrganiserId == organiserId);

        if (enrolment is null)
        {
            return NotFound();
        }

        var payment = await _context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.EnrolmentId == enrolmentId);

        if (payment is null)
        {
            return NotFound();
        }

        return Ok(new PaymentDto
        {
            Id = payment.Id,
            EnrolmentId = payment.EnrolmentId,
            Amount = payment.Amount,
            PaymentDate = payment.PaymentDate,
            Method = payment.Method,
            Status = payment.Status
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
