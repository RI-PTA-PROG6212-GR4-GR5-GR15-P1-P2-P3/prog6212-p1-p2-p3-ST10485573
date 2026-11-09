extern alias progMvc;

using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PROG_API.DTOs;
using MvcServices = progMvc::PROG_MVC.Services;

namespace PROG_MVC_Test;

[TestFixture]
public class MvcFlowTests
{
    [TestCase("/Auth/Login")]
    [TestCase("/Auth/Register")]
    public async Task PublicAuthPages_ReturnSuccess(string path)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [TestCase("/Organiser")]
    [TestCase("/Participant/MyEvents")]
    public async Task ProtectedPages_RequireSessionRole(string path)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(path);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(response.Headers.Location?.ToString(), Does.Contain("/Auth/Login"));
    }

    [Test]
    public async Task HomeIndex_RendersEventsReturnedByApi()
    {
        using var factory = CreateFactory(api => api.GetResponse = url => url == "/api/events"
            ? new List<EventDto> { new() { Id = 7, Name = "Test Trail Run", Location = "Pretoria" } }
            : null);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(html, Does.Contain("Test Trail Run"));
        });
        Assert.That(factory.Api.Requests.Any(request => request.Method == "GET" && request.Url == "/api/events" && request.Body is null), Is.True);
    }

    [Test]
    public async Task Login_WithValidOrganiser_RedirectsToDashboardAndStoresToken()
    {
        using var factory = CreateFactory(api => api.PostResponse = (url, _) => url == "/api/auth/login"
            ? MvcTestApiClient.Response(HttpStatusCode.OK, MvcTestApiClient.Auth("Organiser"))
            : MvcTestApiClient.Response(HttpStatusCode.BadRequest));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await PostFormAsync(client, "/Auth/Login", new Dictionary<string, string>
        {
            ["Email"] = "organiser@example.com",
            ["Password"] = "Password123!"
        });

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(response.Headers.Location?.ToString(), Does.Contain("/Organiser"));
            Assert.That(factory.Api.Requests.Any(r => r is { Method: "POST", Url: "/api/auth/login", Body: LoginRequest }), Is.True);
        });
    }

    [Test]
    public async Task Login_WhenApiRejectsCredentials_RendersValidationError()
    {
        using var factory = CreateFactory(api => api.PostResponse = (_, _) => MvcTestApiClient.Response(HttpStatusCode.Unauthorized));
        using var client = factory.CreateClient();

        var response = await PostFormAsync(client, "/Auth/Login", new Dictionary<string, string>
        {
            ["Email"] = "wrong@example.com",
            ["Password"] = "wrong"
        });
        var html = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(html, Does.Contain("Invalid credentials."));
            Assert.That(html, Does.Contain("wrong@example.com"));
        });
    }

    [Test]
    public async Task ParticipantLogin_MyEventsRendersApiResults()
    {
        using var factory = CreateFactory(api =>
        {
            api.PostResponse = (url, _) => url == "/api/auth/login"
                ? MvcTestApiClient.Response(HttpStatusCode.OK, MvcTestApiClient.Auth("Participant"))
                : MvcTestApiClient.Response(HttpStatusCode.OK);
            api.GetResponse = url => url == "/api/enrolments"
                ? new List<EnrollmentDto> { new() { Id = 12, EventName = "City 10K", CategoryName = "Open", Status = "Confirmed" } }
                : null;
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await PostFormAsync(client, "/Auth/Login", new Dictionary<string, string>
        {
            ["Email"] = "participant@example.com",
            ["Password"] = "Password123!"
        });

        var response = await client.GetAsync("/Participant/MyEvents");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(html, Does.Contain("City 10K"));
            Assert.That(html, Does.Contain("Confirmed"));
        });
        Assert.That(factory.Api.Requests.Any(request => request.Method == "GET" && request.Url == "/api/enrolments" && request.Body is null), Is.True);
    }

    [Test]
    public async Task ParticipantEnrol_WhenApiFails_ShowsFailureAndReturnsToEvent()
    {
        using var factory = CreateFactory(api =>
        {
            api.PostResponse = (url, _) => url == "/api/auth/login"
                ? MvcTestApiClient.Response(HttpStatusCode.OK, MvcTestApiClient.Auth("Participant"))
                : MvcTestApiClient.Response(HttpStatusCode.BadRequest);
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await PostFormAsync(client, "/Auth/Login", new Dictionary<string, string>
        {
            ["Email"] = "participant@example.com",
            ["Password"] = "Password123!"
        });

        var response = await PostFormAsync(client, "/Participant/Enrol", new Dictionary<string, string>
        {
            ["eventId"] = "9",
            ["categoryId"] = "3"
        });

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(response.Headers.Location?.ToString(), Does.Contain("/Participant/Details/9"));
        });
        var enrolRequest = factory.Api.Requests.Single(r => r.Url == "/api/events/9/enrolments");
        Assert.That(enrolRequest.Body, Is.TypeOf<EnrollmentCreateRequest>());
        Assert.That(((EnrollmentCreateRequest)enrolRequest.Body!).CategoryId, Is.EqualTo(3));
    }

    [Test]
    public async Task OrganiserCreateEvent_ForwardsEventAndRedirects()
    {
        using var factory = CreateFactory(api =>
        {
            api.PostResponse = (url, _) => url == "/api/auth/login"
                ? MvcTestApiClient.Response(HttpStatusCode.OK, MvcTestApiClient.Auth("Organiser"))
                : MvcTestApiClient.Response(HttpStatusCode.Created, new EventDto { Id = 25 });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await PostFormAsync(client, "/Auth/Login", new Dictionary<string, string>
        {
            ["Email"] = "organiser@example.com",
            ["Password"] = "Password123!"
        });

        var response = await PostFormAsync(client, "/Organiser/CreateEvent", new Dictionary<string, string>
        {
            ["Name"] = "Autumn 5K",
            ["Description"] = "Community race",
            ["EventDate"] = DateTime.UtcNow.AddDays(30).ToString("O"),
            ["Location"] = "Pretoria",
            ["DistanceKm"] = "5",
            ["EventType"] = "Run"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(response.Headers.Location?.ToString(), Does.Contain("/Organiser"));
        var request = factory.Api.Requests.Single(r => r.Url == "/api/events");
        Assert.That(request.Body, Is.TypeOf<EventCreateRequest>());
        Assert.That(((EventCreateRequest)request.Body!).Name, Is.EqualTo("Autumn 5K"));
    }

    private static TestFactory CreateFactory(Action<MvcTestApiClient>? configure = null)
    {
        var factory = new TestFactory();
        configure?.Invoke(factory.Api);
        return factory;
    }

    private static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string path, Dictionary<string, string> fields)
    {
        var page = await client.GetAsync("/Auth/Login");
        var html = await page.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        if (string.IsNullOrEmpty(token))
            token = Regex.Match(html, "value=\"([^\"]+)\"[^>]*name=\"__RequestVerificationToken\"").Groups[1].Value;
        Assert.That(token, Is.Not.Empty, $"No antiforgery token found on {path}");
        fields["__RequestVerificationToken"] = token;
        return await client.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    private sealed class TestFactory : WebApplicationFactory<progMvc::Program>
    {
        public MvcTestApiClient Api { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<MvcServices.IApiClient>();
                services.AddSingleton<MvcServices.IApiClient>(Api);
            });
        }
    }
}
