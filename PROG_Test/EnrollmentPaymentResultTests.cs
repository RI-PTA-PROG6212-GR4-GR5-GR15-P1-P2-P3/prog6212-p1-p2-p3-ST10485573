// Original source: Microsoft Learn - "Integration tests in ASP.NET Core"
// URL: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
// Accessed: 14 October 2026
// Adapted for: RaceDay API integration and endpoint behavior tests.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PROG_API.Data;
using PROG_API.DTOs;

namespace PROG_Test;

public partial class RaceDayApiTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private string _databaseName = null!;

    [SetUp]
    public void Setup()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        _databaseName = $"RaceDayTests-{Guid.NewGuid()}";

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RaceDayDbName"] = _databaseName,
                    ["UseInMemoryDatabase"] = "true"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.RemoveAll(typeof(IDbContextOptionsConfiguration<AppDbContext>));
                services.AddSingleton(databaseRoot);

                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName, databaseRoot));
            });
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        SeedData.Seed(db);
    }

    [TearDown]
    public void TearDown()
    {
        _factory.Dispose();
    }

    [Test]
    public async Task Participant_ShouldBeAbleToEnrollAndViewOwnEnrolments()
    {
        using var organiser = await CreateAuthenticatedClientAsync("organiser@raceday.co.za", "Password123!");
        using var participant = await CreateAuthenticatedClientAsync("participant@raceday.co.za", "Password123!");

        var eventResponse = await organiser.PostAsJsonAsync("/api/events", new EventCreateRequest
        {
            Name = "Cape Town Race",
            Description = "South coast event",
            EventDate = DateTime.UtcNow.AddDays(18),
            Location = "Cape Town",
            DistanceKm = 10,
            EventType = "Run"
        });

        var createdEvent = await eventResponse.Content.ReadFromJsonAsync<EventDto>();
        Assert.That(createdEvent, Is.Not.Null);

        var categoryResponse = await organiser.PostAsJsonAsync($"/api/events/{createdEvent!.Id}/categories", new EventCategoryCreateRequest
        {
            Name = "Open 10km",
            CategoryType = "Distance",
            Distance = 10,
            MinDistanceKm = 10,
            MaxDistanceKm = 10,
            Price = 120m
        });

        Assert.That(categoryResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var category = await categoryResponse.Content.ReadFromJsonAsync<EventCategoryDto>();
        Assert.That(category, Is.Not.Null);

        var enrolmentResponse = await participant.PostAsJsonAsync($"/api/events/{createdEvent.Id}/enrolments", new EnrollmentCreateRequest
        {
            CategoryId = category!.Id,
            Status = "Confirmed"
        });

        Assert.That(enrolmentResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var myEnrolments = await participant.GetAsync("/api/enrolments");
        Assert.That(myEnrolments.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var enrolments = await myEnrolments.Content.ReadFromJsonAsync<List<EnrollmentDto>>();
        Assert.That(enrolments, Is.Not.Null);
        Assert.That(enrolments!.Count, Is.GreaterThan(0));
    }

    [Test]
    public async Task ParticipantCannotEnrollTwiceInTheSameEvent()
    {
        using var organiser = await CreateAuthenticatedClientAsync("organiser@raceday.co.za", "Password123!");
        using var participant = await CreateAuthenticatedClientAsync("participant@raceday.co.za", "Password123!");
        var (_, enrollment) = await CreateEnrollmentAsync(organiser, participant, "Duplicate Enrolment Race", 10m, 100m);

        var duplicateResponse = await participant.PostAsJsonAsync("/api/enrolments", new EnrollmentCreateRequest
        {
            CategoryId = enrollment.CategoryId
        });

        Assert.That(duplicateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task PaymentAmountComesFromCategoryPriceAndDuplicatePaymentIsRejected()
    {
        using var organiser = await CreateAuthenticatedClientAsync("organiser@raceday.co.za", "Password123!");
        using var participant = await CreateAuthenticatedClientAsync("participant@raceday.co.za", "Password123!");
        var (_, enrollment) = await CreateEnrollmentAsync(organiser, participant, "Payment Test Race", 5m, 137.50m);

        var paymentResponse = await participant.PostAsJsonAsync("/api/payments", new PaymentCreateRequest
        {
            EnrolmentId = enrollment.Id,
            Method = "Card"
        });

        Assert.That(paymentResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var payment = await paymentResponse.Content.ReadFromJsonAsync<PaymentDto>();
        Assert.That(payment, Is.Not.Null);
        Assert.That(payment!.Amount, Is.EqualTo(137.50m));

        var duplicateResponse = await participant.PostAsJsonAsync("/api/payments", new PaymentCreateRequest
        {
            EnrolmentId = enrollment.Id,
            Method = "EFT"
        });
        Assert.That(duplicateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task Organiser_ShouldCaptureResultForEnrollment()
    {
        using var organiser = await CreateAuthenticatedClientAsync("organiser@raceday.co.za", "Password123!");
        using var participant = await CreateAuthenticatedClientAsync("participant@raceday.co.za", "Password123!");

        var eventResponse = await organiser.PostAsJsonAsync("/api/events", new EventCreateRequest
        {
            Name = "Zulu Night 5km",
            Description = "Evening event",
            EventDate = DateTime.UtcNow.AddDays(20),
            Location = "Durban",
            DistanceKm = 5,
            EventType = "Run"
        });

        var createdEvent = await eventResponse.Content.ReadFromJsonAsync<EventDto>();
        var categoryResponse = await organiser.PostAsJsonAsync($"/api/events/{createdEvent!.Id}/categories", new EventCategoryCreateRequest
        {
            Name = "Junior 5km",
            CategoryType = "Distance",
            Distance = 5,
            MinDistanceKm = 5,
            MaxDistanceKm = 5,
            Price = 90m
        });

        var category = await categoryResponse.Content.ReadFromJsonAsync<EventCategoryDto>();
        var enrolmentResponse = await participant.PostAsJsonAsync($"/api/events/{createdEvent.Id}/enrolments", new EnrollmentCreateRequest
        {
            CategoryId = category!.Id,
            Status = "Confirmed"
        });

        var enrollment = await enrolmentResponse.Content.ReadFromJsonAsync<EnrollmentDto>();
        Assert.That(enrollment, Is.Not.Null);

        var resultResponse = await organiser.PostAsJsonAsync($"/api/events/{createdEvent.Id}/results", new ResultCreateRequest
        {
            EnrollmentId = enrollment!.Id,
            FinishTime = TimeSpan.FromMinutes(30),
            Position = 1,
            Status = "Finished"
        });

        Assert.That(resultResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await resultResponse.Content.ReadFromJsonAsync<ResultDto>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Position, Is.EqualTo(1));
    }

    [Test]
    public async Task ParticipantCanCancelTheirOwnEnrollmentBeforeDeadline()
    {
        using var organiser = await CreateAuthenticatedClientAsync("organiser@raceday.co.za", "Password123!");
        using var participant = await CreateAuthenticatedClientAsync("participant@raceday.co.za", "Password123!");

        var eventResponse = await organiser.PostAsJsonAsync("/api/events", new EventCreateRequest
        {
            Name = "Cancel Test Race",
            Description = "Check cancellation flow",
            EventDate = DateTime.UtcNow.AddDays(18),
            RegistrationDeadline = DateTime.UtcNow.AddDays(7),
            Location = "Cape Town",
            DistanceKm = 12,
            EventType = "Run"
        });

        var createdEvent = await eventResponse.Content.ReadFromJsonAsync<EventDto>();
        Assert.That(createdEvent, Is.Not.Null);

        var categoryResponse = await organiser.PostAsJsonAsync($"/api/events/{createdEvent!.Id}/categories", new EventCategoryCreateRequest
        {
            Name = "Open 12km",
            CategoryType = "Distance",
            Distance = 12,
            MaxParticipants = 50,
            Price = 110m
        });

        var category = await categoryResponse.Content.ReadFromJsonAsync<EventCategoryDto>();
        Assert.That(category, Is.Not.Null);

        var enrolmentResponse = await participant.PostAsJsonAsync($"/api/events/{createdEvent.Id}/enrolments", new EnrollmentCreateRequest
        {
            CategoryId = category!.Id,
            Status = "Confirmed"
        });

        var enrollment = await enrolmentResponse.Content.ReadFromJsonAsync<EnrollmentDto>();
        Assert.That(enrollment, Is.Not.Null);

        var cancelResponse = await participant.DeleteAsync($"/api/enrolments/{enrollment!.Id}");
        Assert.That(cancelResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task OrganiserCanUpdateExistingResult()
    {
        using var organiser = await CreateAuthenticatedClientAsync("organiser@raceday.co.za", "Password123!");
        using var participant = await CreateAuthenticatedClientAsync("participant@raceday.co.za", "Password123!");

        var eventResponse = await organiser.PostAsJsonAsync("/api/events", new EventCreateRequest
        {
            Name = "Update Result Race",
            EventDate = DateTime.UtcNow.AddDays(25),
            RegistrationDeadline = DateTime.UtcNow.AddDays(12),
            Location = "Johannesburg",
            DistanceKm = 15,
            EventType = "Run"
        });

        var createdEvent = await eventResponse.Content.ReadFromJsonAsync<EventDto>();
        var categoryResponse = await organiser.PostAsJsonAsync($"/api/events/{createdEvent!.Id}/categories", new EventCategoryCreateRequest
        {
            Name = "Open 15km",
            CategoryType = "Distance",
            Distance = 15,
            MaxParticipants = 80,
            Price = 140m
        });

        var category = await categoryResponse.Content.ReadFromJsonAsync<EventCategoryDto>();
        var enrolmentResponse = await participant.PostAsJsonAsync($"/api/events/{createdEvent.Id}/enrolments", new EnrollmentCreateRequest
        {
            CategoryId = category!.Id,
            Status = "Confirmed"
        });

        var enrollment = await enrolmentResponse.Content.ReadFromJsonAsync<EnrollmentDto>();
        var createResultResponse = await organiser.PostAsJsonAsync($"/api/events/{createdEvent.Id}/results", new ResultCreateRequest
        {
            EnrollmentId = enrollment!.Id,
            FinishTime = TimeSpan.FromMinutes(45),
            Position = 2,
            Status = "Finished"
        });

        Assert.That(createResultResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var createdResult = await createResultResponse.Content.ReadFromJsonAsync<ResultDto>();
        Assert.That(createdResult, Is.Not.Null);

        var updateResponse = await organiser.PutAsJsonAsync($"/api/results/{createdResult!.Id}", new ResultCreateRequest
        {
            FinishTime = TimeSpan.FromMinutes(42),
            Position = 1,
            Status = "Finished"
        });

        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updatedResult = await updateResponse.Content.ReadFromJsonAsync<ResultDto>();
        Assert.That(updatedResult, Is.Not.Null);
        Assert.That(updatedResult!.Position, Is.EqualTo(1));
    }

    [Test]
    public async Task PublicEventsEndpoint_ShouldReturnPublishedEvents()
    {
        using var publicClient = CreateApiClient();

        var response = await publicClient.GetAsync("/api/events");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var events = await response.Content.ReadFromJsonAsync<List<EventDto>>();
        Assert.That(events, Is.Not.Null);
        Assert.That(events!.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(events.All(e => e.IsActive), Is.True);
    }

    private HttpClient CreateApiClient()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    private async Task<(EventDto Event, EnrollmentDto Enrollment)> CreateEnrollmentAsync(
        HttpClient organiser,
        HttpClient participant,
        string eventName,
        decimal distance,
        decimal price)
    {
        var eventResponse = await organiser.PostAsJsonAsync("/api/events", new EventCreateRequest
        {
            Name = eventName,
            EventDate = DateTime.UtcNow.AddDays(20),
            RegistrationDeadline = DateTime.UtcNow.AddDays(10),
            DistanceKm = distance
        });
        Assert.That(eventResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var eventDto = await eventResponse.Content.ReadFromJsonAsync<EventDto>();
        Assert.That(eventDto, Is.Not.Null);

        var categoryResponse = await organiser.PostAsJsonAsync($"/api/events/{eventDto!.Id}/categories", new EventCategoryCreateRequest
        {
            Name = $"Category {Guid.NewGuid():N}",
            Distance = distance,
            MaxParticipants = 25,
            Price = price
        });
        Assert.That(categoryResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var category = await categoryResponse.Content.ReadFromJsonAsync<EventCategoryDto>();
        Assert.That(category, Is.Not.Null);

        var enrollmentResponse = await participant.PostAsJsonAsync("/api/enrolments", new EnrollmentCreateRequest
        {
            CategoryId = category!.Id
        });
        Assert.That(enrollmentResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var enrollmentDto = await enrollmentResponse.Content.ReadFromJsonAsync<EnrollmentDto>();
        Assert.That(enrollmentDto, Is.Not.Null);

        return (eventDto, enrollmentDto!);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
    {
        var client = CreateApiClient();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });

        Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.That(auth, Is.Not.Null);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }
}
