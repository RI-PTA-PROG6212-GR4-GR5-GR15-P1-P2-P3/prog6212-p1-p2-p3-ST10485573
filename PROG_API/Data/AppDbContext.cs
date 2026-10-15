// // Original source: Microsoft Learn - "EF Core Code-First Development"
// // URL: https://learn.microsoft.com/en-us/ef/core/modeling/
// // Accessed: 3 September 2026
// //
// // Adapted for: RaceDay relational database schema using Code-First approach.
// // Modification: Configured domain entities with custom check constraints,
// // index configurations, and relationship mappings for SQL Server.
// //
using Microsoft.EntityFrameworkCore;
using PROG_API.Models;
using ResultsEntity = PROG_API.Models.Results;

namespace PROG_API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Users> Users => Set<Users>();
    public DbSet<Venues> Venues => Set<Venues>();
    public DbSet<Events> Events => Set<Events>();
    public DbSet<Categories> Categories => Set<Categories>();
    public DbSet<Enrolments> Enrolments => Set<Enrolments>();
    public DbSet<Payments> Payments => Set<Payments>();
    public DbSet<ResultsEntity> Results => Set<ResultsEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Users>(entity =>
        {
            entity.ToTable("Users", table =>
                table.HasCheckConstraint("CK_Users_Role", "[Role] IN ('Organiser', 'Participant')"));
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id).HasColumnName("UserId").ValueGeneratedOnAdd();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.DateOfBirth).HasColumnType("date");
        });

        modelBuilder.Entity<Venues>(entity =>
        {
            entity.ToTable("Venues", table =>
            {
                table.HasCheckConstraint("CK_Venues_Latitude", "[Latitude] IS NULL OR [Latitude] BETWEEN -90 AND 90");
                table.HasCheckConstraint("CK_Venues_Longitude", "[Longitude] IS NULL OR [Longitude] BETWEEN -180 AND 180");
                table.HasCheckConstraint("CK_Venues_CoordinatePair",
                    "([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL)");
            });
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Id).HasColumnName("VenueId").ValueGeneratedOnAdd();
            entity.Property(v => v.Latitude).HasPrecision(9, 6);
            entity.Property(v => v.Longitude).HasPrecision(9, 6);
        });

        modelBuilder.Entity<Events>(entity =>
        {
            entity.ToTable("Events", table =>
            {
                table.HasCheckConstraint("CK_Events_Status", "[Status] IN ('Draft', 'Published', 'Cancelled', 'Completed')");
                table.HasCheckConstraint("CK_Events_DeadlineBeforeEvent", "[RegistrationDeadline] <= [EventDate]");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("EventId").ValueGeneratedOnAdd();
            entity.Property(e => e.Status).HasDefaultValue("Draft");
            entity.Property(e => e.EventDate).HasColumnType("date");
            entity.Property(e => e.RegistrationDeadline).HasColumnType("date");

            entity.HasOne(e => e.Organiser)
                .WithMany(u => u.EventsCreated)
                .HasForeignKey(e => e.OrganiserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Venue)
                .WithMany(v => v.Events)
                .HasForeignKey(e => e.VenueId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Categories>(entity =>
        {
            entity.ToTable("Categories", table =>
            {
                table.HasCheckConstraint("CK_Categories_Distance", "[Distance] > 0");
                table.HasCheckConstraint("CK_Categories_MaxParticipants", "[MaxParticipants] > 0");
                table.HasCheckConstraint("CK_Categories_Price", "[Price] >= 0");
            });
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).HasColumnName("CategoryId").ValueGeneratedOnAdd();
            entity.HasOne(c => c.Event)
                .WithMany(e => e.Categories)
                .HasForeignKey(c => c.EventId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(c => new { c.EventId, c.Name }).IsUnique();
            entity.Property(c => c.Distance).HasPrecision(6, 2);
            entity.Property(c => c.Price).HasPrecision(8, 2);
        });

        modelBuilder.Entity<Enrolments>(entity =>
        {
            entity.ToTable("Enrolments", table =>
                table.HasCheckConstraint("CK_Enrolments_Status", "[Status] IN ('Pending', 'Confirmed', 'Cancelled')"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("EnrolmentId").ValueGeneratedOnAdd();
            entity.HasIndex(e => new { e.ParticipantId, e.CategoryId }).IsUnique();
            entity.HasIndex(e => new { e.CategoryId, e.BibNumber }).IsUnique();
            entity.Property(e => e.EnrolmentDate).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.Status).HasDefaultValue("Confirmed");

            entity.HasOne(e => e.Participant)
                .WithMany(u => u.Enrolments)
                .HasForeignKey(e => e.ParticipantId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Category)
                .WithMany(c => c.Enrolments)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Payments>(entity =>
        {
            entity.ToTable("Payments", table =>
            {
                table.HasCheckConstraint("CK_Payments_Amount", "[Amount] >= 0");
                table.HasCheckConstraint("CK_Payments_Method", "[Method] IN ('Card', 'EFT', 'Cash')");
                table.HasCheckConstraint("CK_Payments_Status", "[Status] IN ('Pending', 'Paid', 'Refunded', 'Failed')");
            });
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id).HasColumnName("PaymentId").ValueGeneratedOnAdd();
            entity.HasIndex(p => p.EnrolmentId).IsUnique();
            entity.Property(p => p.Amount).HasPrecision(8, 2);
            entity.Property(p => p.PaymentDate).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(p => p.Status).HasDefaultValue("Pending");

            entity.HasOne(p => p.Enrolment)
                .WithOne(e => e.Payment)
                .HasForeignKey<Payments>(p => p.EnrolmentId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ResultsEntity>(entity =>
        {
            entity.ToTable("Results", table =>
            {
                table.HasCheckConstraint("CK_Results_Status", "[Status] IN ('Finished', 'DNF', 'DSQ')");
                table.HasCheckConstraint("CK_Results_Position", "[Position] IS NULL OR [Position] > 0");
                table.HasCheckConstraint("CK_Results_StatusData",
                    "([Status] = 'Finished' AND [FinishTime] IS NOT NULL AND [Position] IS NOT NULL) OR ([Status] IN ('DNF', 'DSQ') AND [FinishTime] IS NULL AND [Position] IS NULL)");
            });
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).HasColumnName("ResultId").ValueGeneratedOnAdd();
            entity.HasIndex(r => r.EnrolmentId).IsUnique();
            entity.Property(r => r.FinishTime).HasColumnType("time(0)");
            entity.Property(r => r.Status).HasDefaultValue("Finished");
            entity.Property(r => r.CapturedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(r => r.Enrolment)
                .WithOne(e => e.Result)
                .HasForeignKey<ResultsEntity>(r => r.EnrolmentId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(r => r.Capturer)
                .WithMany(u => u.ResultsCaptured)
                .HasForeignKey(r => r.CapturedBy)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
