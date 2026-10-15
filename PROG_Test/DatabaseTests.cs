// Original source: Microsoft Learn - "Integration tests in ASP.NET Core"
// URL: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
// Accessed: 14 October 2026
// Adapted for: RaceDay EF Core schema verification.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PROG_Test;

public partial class RaceDayApiTests
{
    [Test]
    public void DataModel_ShouldContainOnlyTheSevenDatabaseTables()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PROG_API.Data.AppDbContext>();
        var tables = db.Model.GetEntityTypes().Select(entity => entity.GetTableName()).OrderBy(table => table).ToArray();
        Assert.That(tables, Is.EqualTo(new[] { "Categories", "Enrolments", "Events", "Payments", "Results", "Users", "Venues" }));
    }
}
