// Original source: Microsoft Learn - "Model validation in ASP.NET Core MVC"
// URL: https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation
// Accessed: 14 October 2026
// Adapted for: RaceDay request/response DTOs and API-specific serialization rules.

namespace PROG_API.DTOs;

public record RegisterRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    /// <summary>Account role. Valid values are Organiser and Participant; Participant is the default.</summary>
    public string Role { get; set; } = "Participant";
    public string? Phone { get; set; }
    public DateTime? DateOfBirth { get; set; }
}

public record UserPasswordChangeRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public record LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public record UpdateProfileRequest
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public DateTime? DateOfBirth { get; set; }
}

public record AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = new();
}

public record UserDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public record EventCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public DateTime? RegistrationDeadline { get; set; }
    public string? Status { get; set; }
    public int? VenueId { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public string EventType { get; set; } = "Run";
}

public record EventDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public DateTime RegistrationDeadline { get; set; }
    public string Status { get; set; } = "Draft";
    public int VenueId { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int OrganiserId { get; set; }
    public string OrganiserName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<EventCategoryDto> Categories { get; set; } = new();
}

public record EventCategoryCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string CategoryType { get; set; } = "Age";
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public decimal? MinDistanceKm { get; set; }
    public decimal? MaxDistanceKm { get; set; }
    public decimal Distance { get; set; }
    public int MaxParticipants { get; set; } = 100;
    public decimal Price { get; set; }
}

public record EventCategoryDto
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryType { get; set; } = string.Empty;
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public decimal? MinDistanceKm { get; set; }
    public decimal? MaxDistanceKm { get; set; }
    public decimal Distance { get; set; }
    public int MaxParticipants { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public record EnrollmentCreateRequest
{
    public int CategoryId { get; set; }
    public string Status { get; set; } = "Confirmed";
    public string BibNumber { get; set; } = string.Empty;
}

public record EnrollmentDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string BibNumber { get; set; } = string.Empty;
    public DateTime EnrolledAt { get; set; }
}

public record ResultCreateRequest
{
    public int EnrollmentId { get; set; }
    public string? FinishTimeText { get; set; }
    public TimeSpan? FinishTime { get; set; }
    public int? Position { get; set; }
    public string Status { get; set; } = "Finished";
}

public record ResultDto
{
    public int Id { get; set; }
    public int EnrollmentId { get; set; }
    public int UserId { get; set; }
    public int CapturedBy { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public TimeSpan? FinishTime { get; set; }
    public int? Position { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public record VenueDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? RouteMapUrl { get; set; }
}

public record VenueCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? RouteMapUrl { get; set; }
}

public record PaymentCreateRequest
{
    public int EnrolmentId { get; set; }
    public string Method { get; set; } = "Card";
}

public record PaymentDto
{
    public int Id { get; set; }
    public int EnrolmentId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
