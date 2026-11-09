using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PROG_API.DTOs;

namespace PROG_Test;

public partial class RaceDayApiTests
{
    private static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@mail.com";

    private async Task<HttpResponseMessage> TryLoginAsync(HttpClient client, string email, string password) =>
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });

    [Test]
    public async Task AccountLocksOutAfterThreeFailedLogins()
    {
        using var client = CreateApiClient();
        var email = UniqueEmail("lockout");

        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FullName = "Lockout User",
            Email = email,
            Password = "Password123!",
            Role = "Participant"
        });
        Assert.That(register.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Two wrong passwords → 401
        Assert.That((await TryLoginAsync(client, email, "WrongPassword1!")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await TryLoginAsync(client, email, "WrongPassword2!")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        // Third wrong password → also 401 (now locked)
        var third = await TryLoginAsync(client, email, "WrongPassword3!");
        Assert.That(third.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        // Correct password while locked → still rejected
        Assert.That((await TryLoginAsync(client, email, "Password123!")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task LockoutExpiryAllowsLoginAgain()
    {
        using var client = CreateApiClient();
        var email = UniqueEmail("lockout-expiry");

        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FullName = "Lockout Expiry",
            Email = email,
            Password = "Password123!",
            Role = "Participant"
        });
        Assert.That(register.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        for (var i = 0; i < 3; i++)
        {
            await TryLoginAsync(client, email, $"Wrong{i}!");
        }

        // Simulate the lock expiring.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PROG_API.Data.AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var login = await TryLoginAsync(client, email, "Password123!");
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var auth = await login.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.That(auth?.Token, Is.Not.Empty);
    }

    [Test]
    public async Task FailedLoginCountResetsAfterSuccessfulLogin()
    {
        using var client = CreateApiClient();
        var email = UniqueEmail("lockout-reset");

        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FullName = "Reset Counter",
            Email = email,
            Password = "Password123!",
            Role = "Participant"
        });
        Assert.That(register.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Two failures, then success → lockout should clear
        Assert.That((await TryLoginAsync(client, email, "Wrong1!")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await TryLoginAsync(client, email, "Wrong2!")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await TryLoginAsync(client, email, "Password123!")).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PROG_API.Data.AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            Assert.That(user.FailedLoginCount, Is.EqualTo(0));
            Assert.That(user.LockoutEndUtc, Is.Null);
        }

        // Two more failures do not lock out
        Assert.That((await TryLoginAsync(client, email, "Wrong3!")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await TryLoginAsync(client, email, "Password123!")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Participant_CanChangeEnrolmentCategory()
    {
        using var organiser = await CreateAuthenticatedClientAsync("organiser@raceday.co.za", "Password123!");
        using var participant = await CreateAuthenticatedClientAsync("participant@raceday.co.za", "Password123!");

        var (enrollment, primary, alternate) = await CreateEnrolmentWithAlternateCategoryAsync(
            organiser, participant, "Edit Enrolment Race");

        var update = await participant.PutAsJsonAsync($"/api/enrolments/{enrollment.Id}", new EnrollmentUpdateRequest
        {
            CategoryId = alternate.Id
        });
        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updated = await update.Content.ReadFromJsonAsync<EnrollmentDto>();
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.CategoryId, Is.EqualTo(alternate.Id));
    }

    [Test]
    public async Task OtherParticipant_CannotEditEnrolment()
    {
        using var organiser = await CreateAuthenticatedClientAsync("organiser@raceday.co.za", "Password123!");
        using var owner = await CreateAuthenticatedClientAsync("participant@raceday.co.za", "Password123!");

        // Register a second participant
        var email = UniqueEmail("intruder");
        var register = await CreateApiClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FullName = "Intruder",
            Email = email,
            Password = "Password123!",
            Role = "Participant"
        });
        var intruderAuth = await register.Content.ReadFromJsonAsync<AuthResponseDto>();
        using var intruder = CreateApiClient();
        intruder.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", intruderAuth!.Token);

        var (enrollment, _, alternate) = await CreateEnrolmentWithAlternateCategoryAsync(
            organiser, owner, "Intruder Edit Race");

        var update = await intruder.PutAsJsonAsync($"/api/enrolments/{enrollment.Id}", new EnrollmentUpdateRequest
        {
            CategoryId = alternate.Id
        });
        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    /// <summary>
    /// Adds a second category to the enrolment's event so edits have something to switch to.
    /// </summary>
    private async Task<(EnrollmentDto Enrollment, EventCategoryDto Primary, EventCategoryDto Alternate)>
        CreateEnrolmentWithAlternateCategoryAsync(
            HttpClient organiser,
            HttpClient participant,
            string eventName)
    {
        var eventResponse = await organiser.PostAsJsonAsync("/api/events", new EventCreateRequest
        {
            Name = eventName,
            EventDate = DateTime.UtcNow.AddDays(14),
            RegistrationDeadline = DateTime.UtcNow.AddDays(5),
            DistanceKm = 10
        });
        Assert.That(eventResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var ev = await eventResponse.Content.ReadFromJsonAsync<EventDto>();
        Assert.That(ev, Is.Not.Null);

        var primaryResponse = await organiser.PostAsJsonAsync($"/api/events/{ev!.Id}/categories", new EventCategoryCreateRequest
        {
            Name = $"Primary {Guid.NewGuid():N}",
            Distance = 10,
            MaxParticipants = 25,
            Price = 100m
        });
        Assert.That(primaryResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var primary = await primaryResponse.Content.ReadFromJsonAsync<EventCategoryDto>();
        Assert.That(primary, Is.Not.Null);

        var alternateResponse = await organiser.PostAsJsonAsync($"/api/events/{ev.Id}/categories", new EventCategoryCreateRequest
        {
            Name = $"Alternate {Guid.NewGuid():N}",
            Distance = 15,
            MaxParticipants = 25,
            Price = 140m
        });
        Assert.That(alternateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var alternate = await alternateResponse.Content.ReadFromJsonAsync<EventCategoryDto>();
        Assert.That(alternate, Is.Not.Null);

        var enrol = await participant.PostAsJsonAsync("/api/enrolments", new EnrollmentCreateRequest
        {
            CategoryId = primary.Id
        });
        Assert.That(enrol.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var enrollment = await enrol.Content.ReadFromJsonAsync<EnrollmentDto>();
        Assert.That(enrollment, Is.Not.Null);

        return (enrollment!, primary!, alternate!);
    }
}