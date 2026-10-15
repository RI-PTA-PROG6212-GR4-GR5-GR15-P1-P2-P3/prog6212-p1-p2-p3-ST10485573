// Original source: Microsoft Learn - "Integration tests in ASP.NET Core"
// URL: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
// Accessed: 14 October 2026
// Adapted for: RaceDay role-based endpoint authorization tests.

using System.Net;
using System.Net.Http.Json;
using PROG_API.DTOs;

namespace PROG_Test;

public partial class RaceDayApiTests
{
    [Test]
    public async Task Participant_ShouldNotViewOrganiserEnrolmentList()
    {
        using var organiser = await CreateAuthenticatedClientAsync("organiser@raceday.co.za", "Password123!");
        using var participant = await CreateAuthenticatedClientAsync("participant@raceday.co.za", "Password123!");
        var eventResponse = await organiser.PostAsJsonAsync("/api/events", new EventCreateRequest { Name = "Restricted Enrolment Audit", EventDate = DateTime.UtcNow.AddDays(30), RegistrationDeadline = DateTime.UtcNow.AddDays(12), Location = "Pretoria", DistanceKm = 7, EventType = "Run" });
        var createdEvent = await eventResponse.Content.ReadFromJsonAsync<EventDto>();
        Assert.That(createdEvent, Is.Not.Null);
        var categoryResponse = await organiser.PostAsJsonAsync($"/api/events/{createdEvent!.Id}/categories", new EventCategoryCreateRequest { Name = "Open 7km", CategoryType = "Distance", Distance = 7, MaxParticipants = 40, Price = 95m });
        var category = await categoryResponse.Content.ReadFromJsonAsync<EventCategoryDto>();
        var enrolmentResponse = await participant.PostAsJsonAsync($"/api/events/{createdEvent.Id}/enrolments", new EnrollmentCreateRequest { CategoryId = category!.Id, Status = "Confirmed" });
        Assert.That(enrolmentResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That((await organiser.GetAsync($"/api/events/{createdEvent.Id}/enrolments")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await participant.GetAsync($"/api/events/{createdEvent.Id}/enrolments")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Participant_ShouldBeForbiddenFromAdminUserLookup()
    {
        using var participant = await CreateAuthenticatedClientAsync("participant@raceday.co.za", "Password123!");
        var response = await participant.GetAsync("/api/users/1");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }
}
