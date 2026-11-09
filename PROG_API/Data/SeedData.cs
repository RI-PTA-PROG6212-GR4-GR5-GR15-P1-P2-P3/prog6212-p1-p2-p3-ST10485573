// Original source: Microsoft Learn - "Data Seeding - EF Core"
// URL: https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding/
// Accessed: 14 October 2026
// Adapted for: RaceDay development seed users, events, venues, categories, and enrolments.

using PROG_API.Models;
using PROG_API.Services;
using Microsoft.EntityFrameworkCore;
using ResultsEntity = PROG_API.Models.Results;

namespace PROG_API.Data;

public static class SeedData
{
    public static void Seed(AppDbContext context)
    {
        if (context.Users.Any())
        {
            SeedAdditionalEvents(context);
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

        SeedAdditionalEvents(context);
    }

    private static void SeedAdditionalEvents(AppDbContext context)
    {
        var organiser = context.Users.FirstOrDefault(user => user.Email == "organiser@raceday.co.za");
        if (organiser is null)
        {
            organiser = new Users
            {
                FullName = "Alice Organiser",
                Email = "organiser@raceday.co.za",
                PasswordHash = PasswordHasher.HashPassword("Password123!"),
                Role = "Organiser",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Users.Add(organiser);
            context.SaveChanges();
        }

        var participant = context.Users.FirstOrDefault(user => user.Email == "participant@raceday.co.za");
        var now = DateTime.UtcNow;
        var eventSeeds = new[]
        {
            new { Name = "Sunrise Park 5K", Description = "Start your weekend with a welcoming loop through the park, with a relaxed community finish.", Venue = "Delta Park", Address = "Craighall Park", City = "Johannesburg", Province = "Gauteng", Lat = -26.1280m, Lon = 28.0060m, Days = 24, Distance = 5m, Price = 85m },
            new { Name = "Cape Coast 21K", Description = "A scenic half marathon with ocean views, lively support and a finish worth celebrating.", Venue = "Green Point Urban Park", Address = "Bay Road", City = "Cape Town", Province = "Western Cape", Lat = -33.9030m, Lon = 18.4110m, Days = 38, Distance = 21.1m, Price = 220m },
            new { Name = "Durban Golden Mile Run", Description = "A fast, friendly road race along Durban’s beachfront with distances for every runner.", Venue = "uShaka Beachfront", Address = "Point Road", City = "Durban", Province = "KwaZulu-Natal", Lat = -29.8700m, Lon = 31.0450m, Days = 52, Distance = 10m, Price = 150m },
            new { Name = "Magalies Trail Escape", Description = "Trade the road for mountain paths, open views and a memorable day on the trails.", Venue = "Cradle Moon Lakeside Game Lodge", Address = "R512, Muldersdrift", City = "Muldersdrift", Province = "Gauteng", Lat = -25.9990m, Lon = 27.8390m, Days = 67, Distance = 15m, Price = 185m },
            new { Name = "Stellenbosch Vineyard 10K", Description = "Run through the winelands at golden hour and celebrate with the local community.", Venue = "Coetzenburg Athletics Stadium", Address = "Coetzenburg Road", City = "Stellenbosch", Province = "Western Cape", Lat = -33.9380m, Lon = 18.8640m, Days = 81, Distance = 10m, Price = 175m },
            new { Name = "Heritage Park Run Results Demo", Description = "A completed community 5K with a sample payment and published result for trying the participant and organiser pages.", Venue = "Johannesburg Sports Park", Address = "Main Road", City = "Johannesburg", Province = "Gauteng", Lat = -26.2041m, Lon = 28.0473m, Days = -14, Distance = 5m, Price = 75m }
        };

        foreach (var seed in eventSeeds)
        {
            var eventEntity = context.Events.FirstOrDefault(item => item.Name == seed.Name);
            if (eventEntity is null)
            {
                var venue = context.Venues.FirstOrDefault(item => item.Name == seed.Venue);
                if (venue is null)
                {
                    venue = new Venues
                    {
                        Name = seed.Venue,
                        Address = seed.Address,
                        City = seed.City,
                        Province = seed.Province,
                        Latitude = seed.Lat,
                        Longitude = seed.Lon
                    };
                    context.Venues.Add(venue);
                    context.SaveChanges();
                }

                var eventDate = now.Date.AddDays(seed.Days);
                eventEntity = new Events
                {
                    Name = seed.Name,
                    Description = seed.Description,
                    EventDate = eventDate,
                    RegistrationDeadline = eventDate.AddDays(seed.Days < 0 ? -14 : -5),
                    Status = seed.Days < 0 ? "Completed" : "Published",
                    VenueId = venue.Id,
                    OrganiserId = organiser.Id,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                context.Events.Add(eventEntity);
                context.SaveChanges();
            }

            var category = context.Categories.FirstOrDefault(item => item.EventId == eventEntity.Id);
            if (category is null)
            {
                category = new Categories
                {
                    EventId = eventEntity.Id,
                    Name = $"Open {seed.Distance:0.#}km",
                    Distance = seed.Distance,
                    MaxParticipants = 300,
                    Price = seed.Price,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                context.Categories.Add(category);
                context.SaveChanges();
            }

            if (participant is not null)
            {
                var enrollment = context.Enrolments.FirstOrDefault(item => item.ParticipantId == participant.Id && item.CategoryId == category.Id);
                if (enrollment is null)
                {
                    enrollment = new Enrolments
                    {
                        ParticipantId = participant.Id,
                        CategoryId = category.Id,
                        Status = "Confirmed",
                        BibNumber = $"{7000 + eventEntity.Id}",
                        EnrolmentDate = now.AddDays(seed.Days < 0 ? seed.Days - 3 : -2)
                    };
                    context.Enrolments.Add(enrollment);
                    context.SaveChanges();
                }

                if (seed.Days < 0 && !context.Results.Any(result => result.EnrolmentId == enrollment.Id))
                {
                    context.Results.Add(new ResultsEntity
                    {
                        EnrolmentId = enrollment.Id,
                        FinishTime = TimeSpan.FromMinutes(52).Add(TimeSpan.FromSeconds(36)),
                        Position = 1,
                        Status = "Finished",
                        CapturedBy = organiser.Id,
                        CapturedAt = now.AddDays(-13)
                    });
                    context.SaveChanges();
                }
            }
        }

        if (participant is not null)
        {
            var sampleEnrollment = context.Enrolments
                .Include(enrollment => enrollment.Category)
                .OrderBy(enrollment => enrollment.Id)
                .FirstOrDefault(enrollment => enrollment.ParticipantId == participant.Id);
            if (sampleEnrollment is not null && !context.Payments.Any(payment => payment.EnrolmentId == sampleEnrollment.Id))
            {
                context.Payments.Add(new Payments
                {
                    EnrolmentId = sampleEnrollment.Id,
                    Amount = sampleEnrollment.Category.Price,
                    Method = "Card",
                    Status = "Paid",
                    PaymentDate = now.AddDays(-1)
                });
                context.SaveChanges();
            }
        }
    }
}
