// Original source: Microsoft Learn - "Integration tests in ASP.NET Core"
// URL: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
// Accessed: 14 October 2026
// Adapted for: RaceDay authentication, role validation, and credential tests.

using System.Net;
using System.Net.Http.Json;
using PROG_API.DTOs;

namespace PROG_Test;

public partial class RaceDayApiTests
{
    [Test]
    public async Task RegisterAndLogin_ShouldWork()
    {
        using var client = CreateApiClient();
        var email = $"alice{Guid.NewGuid():N}@mail.com";
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { FullName = "Alice New", Email = email, Password = "Password123!" });
        Assert.That(registerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = "Password123!" });
        Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.That(auth?.Token, Is.Not.Empty);
    }

    [Test]
    public async Task Register_ShouldCreateRequestedOrganiserRole()
    {
        using var client = CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { FullName = "Role Check", Email = $"role-check-{Guid.NewGuid():N}@mail.com", Password = "Password123!", Role = "Organiser" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.That(auth?.User.Role, Is.EqualTo("Organiser"));
        Assert.That(auth?.Token, Is.Not.Empty);
    }

    [Test]
    public async Task Register_ShouldRejectUnknownRole()
    {
        using var client = CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { FullName = "Invalid Role", Email = $"invalid-role-{Guid.NewGuid():N}@mail.com", Password = "Password123!", Role = "Administrator" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task DuplicateEmail_ShouldBeRejected()
    {
        using var client = CreateApiClient();
        var email = $"duplicate-{Guid.NewGuid():N}@mail.com";
        var first = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { FullName = "First User", Email = email, Password = "Password123!" });
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var second = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { FullName = "Second User", Email = email, Password = "Password123!" });
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task InvalidPassword_ShouldBeRejectedDuringRegistration()
    {
        using var client = CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { FullName = "Short Password", Email = $"short-pass-{Guid.NewGuid():N}@mail.com", Password = "short" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
