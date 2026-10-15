// Original source: Microsoft Learn - "Integration tests in ASP.NET Core"
// URL: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
// Accessed: 14 October 2026
// Adapted for: RaceDay event filtering, creation, and role authorization.

using System.Net;
using System.Net.Http.Json;
using PROG_API.DTOs;

namespace PROG_Test;

public partial class RaceDayApiTests
{
    [Test]
    public async Task Events_ShouldSupportProvinceDateAndKeywordFilters()
    {
        using var client = CreateApiClient();
        var response = await client.GetAsync("/api/events?province=Gauteng&from=" + DateTime.UtcNow.AddDays(1).ToString("O") + "&to=" + DateTime.UtcNow.AddDays(30).ToString("O") + "&q=Challenge");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var events = await response.Content.ReadFromJsonAsync<List<EventDto>>();
        Assert.That(events?.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(events!.All(e => e.Name.Contains("Challenge")), Is.True);
    }

    [Test]
    public async Task Participant_ShouldNotCreateEvents()
    {
        using var client = await CreateAuthenticatedClientAsync("participant@raceday.co.za", "Password123!");
        var response = await client.PostAsJsonAsync("/api/events", new EventCreateRequest { Name = "Unauthorised Event", Description = "Bad", EventDate = DateTime.UtcNow.AddDays(5), Location = "Durban", DistanceKm = 8, EventType = "Run" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Organiser_ShouldCreateEvent()
    {
        using var organiser = await CreateAuthenticatedClientAsync("organiser@raceday.co.za", "Password123!");
        var response = await organiser.PostAsJsonAsync("/api/events", new EventCreateRequest { Name = "Weekend Run", Description = "Road race", EventDate = DateTime.UtcNow.AddDays(12), Location = "Pretoria", DistanceKm = 21, EventType = "Run" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var body = await response.Content.ReadFromJsonAsync<EventDto>();
        Assert.That(body?.Name, Is.EqualTo("Weekend Run"));
    }
}
