// Original source: Microsoft Learn - "Data Seeding - EF Core"
// URL: https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding/
// Accessed: 14 October 2026
// Adapted for: RaceDay development seed users, events, venues, categories, and enrolments.

using PROG_API.Models;
using PROG_API.Services;

namespace PROG_API.Data;

public static class SeedData
{
    public static void Seed(AppDbContext context)
    {
        if (context.Users.Any())
        {
            return;
        }

        var organiser = new Users
        {
            FullName = "Alice Organiser",
            Email = "organiser@raceday.co.za",
            PasswordHash = PasswordHasher.HashPassword("Password123!"),
            Role = "Organiser",
            Phone = "0821111111",
            DateOfBirth = new DateTime(1990, 5, 12),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var participant = new Users
        {
            FullName = "Bob Runner",
            Email = "participant@raceday.co.za",
            PasswordHash = PasswordHasher.HashPassword("Password123!"),
            Role = "Participant",
            Phone = "0832222222",
            DateOfBirth = new DateTime(1998, 8, 25),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Users.AddRange(organiser, participant);
        context.SaveChanges();

        var venue = new Venues
        {
            Name = "Johannesburg Sports Park",
            Address = "Main Road",
            City = "Johannesburg",
            Province = "Gauteng",
            Latitude = -26.2041m,
            Longitude = 28.0473m,
            RouteMapUrl = null
        };

        context.Venues.Add(venue);
        context.SaveChanges();

        var eventEntity = new Events
        {
            Name = "City Run Challenge",
            Description = "An urban 10km race for all runners.",
            EventDate = DateTime.UtcNow.AddDays(15),
            RegistrationDeadline = DateTime.UtcNow.AddDays(7),
            Status = "Published",
            VenueId = venue.Id,
            OrganiserId = organiser.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Events.Add(eventEntity);
        context.SaveChanges();

        var category = new Categories
        {
            EventId = eventEntity.Id,
            Name = "Open 10km",
            Distance = 10m,
            MaxParticipants = 200,
            Price = 150m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Categories.Add(category);
        context.SaveChanges();

        var enrollment = new Enrolments
        {
            ParticipantId = participant.Id,
            CategoryId = category.Id,
            Status = "Confirmed",
            BibNumber = "1001",
            EnrolmentDate = DateTime.UtcNow
        };

        context.Enrolments.Add(enrollment);
        context.SaveChanges();
    }
}
