// Original source: Microsoft Learn - "Integration tests in ASP.NET Core"
// URL: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
// Accessed: 14 October 2026
// Adapted for: RaceDay profile, password, and venue endpoint tests.

using System.Net;
using System.Net.Http.Json;
using PROG_API.DTOs;

namespace PROG_Test;

public partial class RaceDayApiTests
{
    [Test]
    public async Task ProtectedProfile_ShouldReturnUnauthorizedWithoutToken()
    {
        using var client = CreateApiClient();
        var response = await client.GetAsync("/api/users/me");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ChangePassword_ShouldRequireCurrentPasswordAndAllowNewLogin()
    {
        using var client = await CreateAuthenticatedClientAsync("participant@raceday.co.za", "Password123!");
        var rejected = await client.PutAsJsonAsync("/api/users/me/password", new UserPasswordChangeRequest { CurrentPassword = "WrongPassword!", NewPassword = "NewPassword123!" });
        Assert.That(rejected.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var changed = await client.PutAsJsonAsync("/api/users/me/password", new UserPasswordChangeRequest { CurrentPassword = "Password123!", NewPassword = "NewPassword123!" });
        Assert.That(changed.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task OrganiserCanCreateVenueAndVenueIsPubliclyReadable()
    {
        using var organiser = await CreateAuthenticatedClientAsync("organiser@raceday.co.za", "Password123!");
        using var publicClient = CreateApiClient();
        var venueName = $"Test Venue {Guid.NewGuid():N}";
        var createResponse = await organiser.PostAsJsonAsync("/api/venues", new VenueCreateRequest { Name = venueName, City = "Cape Town", Province = "Western Cape", Latitude = -33.9249m, Longitude = 18.4241m });
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var venue = await createResponse.Content.ReadFromJsonAsync<VenueDto>();
        Assert.That(venue, Is.Not.Null);
        var getResponse = await publicClient.GetAsync($"/api/venues/{venue!.Id}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
