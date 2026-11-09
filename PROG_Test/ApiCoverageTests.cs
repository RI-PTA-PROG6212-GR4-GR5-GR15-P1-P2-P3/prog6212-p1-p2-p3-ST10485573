using System.Net;
using System.Net.Http.Json;
using PROG_API.DTOs;

namespace PROG_Test;

public partial class RaceDayApiTests
{
    [Test]
    public async Task Organiser_CanUpdateAndDeleteEvent()
    {
        using var organiser = await CreateAuthenticatedClientAsync("organiser@raceday.co.za", "Password123!");
        var create = await organiser.PostAsJsonAsync("/api/events", new EventCreateRequest
        {
            Name = "Update Delete Race",
            Description = "desc",
            EventDate = DateTime.UtcNow.AddDays(20),
            Location = "Durban",
            DistanceKm = 8,
            EventType = "Run"
        });
        var ev = await create.Content.ReadFromJsonAsync<EventDto>();
        Assert.That(ev, Is.Not.Null);

        var update = await organiser.PutAsJsonAsync($"/api/events/{ev!.Id}", new EventCreateRequest
        {
            Name = "Updated Race",
            Description = "updated",
            EventDate = DateTime.UtcNow.AddDays(25),
            Location = "Durban",
            DistanceKm = 8,
            EventType = "Run"
        });
        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var del = await organiser.DeleteAsync($"/api/events/{ev.Id}");
        Assert.That(del.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var get = await organiser.GetAsync($"/api/events/{ev.Id}");
        Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Participant_CannotDeleteEvent()
    {
        using var participant = await CreateAuthenticatedClientAsync("participant@raceday.co.za", "Password123!");
        var response = await participant.DeleteAsync("/api/events/1");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Organiser_CanManageCategories()
    {
        using var organiser = await CreateAuthenticatedClientAsync("organiser@raceday.co.za", "Password123!");
        var eventResponse = await organiser.PostAsJsonAsync("/api/events", new EventCreateRequest
        {
            Name = "Category Mgmt Race",
            EventDate = DateTime.UtcNow.AddDays(20),
            Location = "Cape Town",
            DistanceKm = 5,
            EventType = "Run"
        });
        var ev = await eventResponse.Content.ReadFromJsonAsync<EventDto>();

        var create = await organiser.PostAsJsonAsync($"/api/events/{ev!.Id}/categories", new EventCategoryCreateRequest
        {
            Name = "Open 5km",
            Distance = 5,
            MaxParticipants = 50,
            Price = 100m
        });
        Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var cat = await create.Content.ReadFromJsonAsync<EventCategoryDto>();
        Assert.That(cat, Is.Not.Null);

        var update = await organiser.PutAsJsonAsync($"/api/categories/{cat!.Id}", new EventCategoryCreateRequest
        {
            Name = "Open 5km Updated",
            Distance = 5,
            MaxParticipants = 60,
            Price = 110m
        });
        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var del = await organiser.DeleteAsync($"/api/categories/{cat.Id}");
        Assert.That(del.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task Organiser_CanUpdateResult()
    {
        using var organiser = await CreateAuthenticatedClientAsync("organiser@raceday.co.za", "Password123!");
        using var participant = await CreateAuthenticatedClientAsync("participant@raceday.co.za", "Password123!");

        var eventResponse = await organiser.PostAsJsonAsync("/api/events", new EventCreateRequest
        {
            Name = "Result Update Race",
            EventDate = DateTime.UtcNow.AddDays(20),
            RegistrationDeadline = DateTime.UtcNow.AddDays(10),
            Location = "Joburg",
            DistanceKm = 10,
            EventType = "Run"
        });
        var ev = await eventResponse.Content.ReadFromJsonAsync<EventDto>();
        var catResponse = await organiser.PostAsJsonAsync($"/api/events/{ev!.Id}/categories", new EventCategoryCreateRequest
        {
            Name = "Open 10km",
            Distance = 10,
            MaxParticipants = 50,
            Price = 120m
        });
        var cat = await catResponse.Content.ReadFromJsonAsync<EventCategoryDto>();
        var enrolResponse = await participant.PostAsJsonAsync($"/api/events/{ev.Id}/enrolments", new EnrollmentCreateRequest
        {
            CategoryId = cat!.Id,
            Status = "Confirmed"
        });
        var enrollment = await enrolResponse.Content.ReadFromJsonAsync<EnrollmentDto>();

        var resultResponse = await organiser.PostAsJsonAsync($"/api/events/{ev.Id}/results", new ResultCreateRequest
        {
            EnrollmentId = enrollment!.Id,
            FinishTime = TimeSpan.FromMinutes(40),
            Position = 3,
            Status = "Finished"
        });
        var result = await resultResponse.Content.ReadFromJsonAsync<ResultDto>();
        Assert.That(result, Is.Not.Null);

        var update = await organiser.PutAsJsonAsync($"/api/results/{result!.Id}", new ResultCreateRequest
        {
            FinishTime = TimeSpan.FromMinutes(38),
            Position = 2,
            Status = "Finished"
        });
        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updated = await update.Content.ReadFromJsonAsync<ResultDto>();
        Assert.That(updated!.Position, Is.EqualTo(2));
    }

    [Test]
    public async Task Profile_UpdateAndChangePassword()
    {
        using var client = await CreateAuthenticatedClientAsync("participant@raceday.co.za", "Password123!");
        var update = await client.PutAsJsonAsync("/api/users/me", new UpdateProfileRequest
        {
            FullName = "Updated Name",
            Phone = "0123456789"
        });
        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var user = await update.Content.ReadFromJsonAsync<UserDto>();
        Assert.That(user!.FullName, Is.EqualTo("Updated Name"));

        var pwd = await client.PutAsJsonAsync("/api/users/me/password", new UserPasswordChangeRequest
        {
            CurrentPassword = "Password123!",
            NewPassword = "NewPassword123!"
        });
        Assert.That(pwd.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task Venue_CreateAndPublicRead()
    {
        using var organiser = await CreateAuthenticatedClientAsync("organiser@raceday.co.za", "Password123!");
        using var publicClient = CreateApiClient();
        var name = $"Test Venue {Guid.NewGuid():N}";
        var create = await organiser.PostAsJsonAsync("/api/venues", new VenueCreateRequest
        {
            Name = name,
            City = "Cape Town",
            Province = "Western Cape",
            Latitude = -33.9249m,
            Longitude = 18.4241m
        });
        Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var venue = await create.Content.ReadFromJsonAsync<VenueDto>();
        Assert.That(venue, Is.Not.Null);

        var get = await publicClient.GetAsync($"/api/venues/{venue!.Id}");
        Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Unauthenticated_AccessToProtectedEndpoints_Returns401()
    {
        using var client = CreateApiClient();
        Assert.That((await client.GetAsync("/api/users/me")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await client.GetAsync("/api/enrolments")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await client.PostAsJsonAsync("/api/events", new EventCreateRequest { Name = "x", EventDate = DateTime.UtcNow })).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task DuplicateEnrolment_ReturnsConflict()
    {
        using var organiser = await CreateAuthenticatedClientAsync("organiser@raceday.co.za", "Password123!");
        using var participant = await CreateAuthenticatedClientAsync("participant@raceday.co.za", "Password123!");
        var (_, enrollment) = await CreateEnrollmentAsync(organiser, participant, "Dup Race 2", 10m, 100m);

        var duplicate = await participant.PostAsJsonAsync("/api/enrolments", new EnrollmentCreateRequest
        {
            CategoryId = enrollment.CategoryId
        });
        Assert.That(duplicate.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }
}