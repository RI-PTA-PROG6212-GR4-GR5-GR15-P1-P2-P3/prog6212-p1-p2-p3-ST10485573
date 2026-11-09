// Original source: Microsoft Learn - "Integration tests in ASP.NET Core"
// URL: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
// Accessed: 14 October 2026
// Adapted for: RaceDay blob storage integration tests.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using PROG_API.DTOs;

namespace PROG_Test;

public partial class RaceDayApiTests
{
    [Test]
    public async Task Organiser_CanUploadEventBanner()
    {
        using var organiser = await CreateAuthenticatedClientAsync("organiser@raceday.co.za", "Password123!");
        var eventResponse = await organiser.PostAsJsonAsync("/api/events", new EventCreateRequest
        {
            Name = "Banner Test Race",
            Description = "banner",
            EventDate = DateTime.UtcNow.AddDays(20),
            Location = "Pretoria",
            DistanceKm = 5,
            EventType = "Run"
        });
        var created = await eventResponse.Content.ReadFromJsonAsync<EventDto>();
        Assert.That(created, Is.Not.Null);

        using var content = new MultipartFormDataContent();
        var fileBytes = Encoding.UTF8.GetBytes("fake-image-bytes");
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "banner.png");

        var uploadResponse = await organiser.PostAsync($"/api/events/{created!.Id}/banner", content);
        Assert.That(uploadResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updated = await uploadResponse.Content.ReadFromJsonAsync<EventDto>();
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.BannerImageUrl, Is.Not.Null.And.Not.Empty);
        Assert.That(updated.BannerImageUrl, Does.StartWith("/blob/event-banners/"));

        using var imageResponse = await organiser.GetAsync(updated.BannerImageUrl);
        Assert.That(imageResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(imageResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/png"));
        Assert.That(await imageResponse.Content.ReadAsByteArrayAsync(), Is.EqualTo(fileBytes));
    }

    [Test]
    public async Task Participant_CannotUploadEventBanner()
    {
        using var participant = await CreateAuthenticatedClientAsync("participant@raceday.co.za", "Password123!");
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("x")), "file", "x.png");
        var response = await participant.PostAsync("/api/events/1/banner", content);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Participant_CanUploadProfilePicture()
    {
        using var participant = await CreateAuthenticatedClientAsync("participant@raceday.co.za", "Password123!");
        using var content = new MultipartFormDataContent();
        var imageBytes = Encoding.UTF8.GetBytes("fake-pic");
        var fileContent = new ByteArrayContent(imageBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "file", "pic.jpg");

        var response = await participant.PostAsync("/api/users/me/profile-picture", content);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.That(user, Is.Not.Null);
        Assert.That(user!.ProfilePictureUrl, Does.StartWith("/blob/profile-pictures/"));

        using var imageResponse = await participant.GetAsync(user.ProfilePictureUrl);
        Assert.That(imageResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(imageResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/jpeg"));
        Assert.That(await imageResponse.Content.ReadAsByteArrayAsync(), Is.EqualTo(imageBytes));
    }

    [Test]
    public async Task Unauthenticated_CannotUploadProfilePicture()
    {
        using var client = CreateApiClient();
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("x")), "file", "x.png");
        var response = await client.PostAsync("/api/users/me/profile-picture", content);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
