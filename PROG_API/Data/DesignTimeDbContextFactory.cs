// Original source: Microsoft Learn - "EF Core Code-First Development"
// URL: https://learn.microsoft.com/en-us/ef/core/modeling/
// Accessed: 14 October 2026
// Adapted for: RaceDay design-time DbContext factory.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PROG_API.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=RaceDayDb;Trusted_Connection=True;MultipleActiveResultSets=true");
        return new AppDbContext(optionsBuilder.Options);
    }
}