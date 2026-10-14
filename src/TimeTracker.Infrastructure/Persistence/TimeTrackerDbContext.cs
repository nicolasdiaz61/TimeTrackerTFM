using Microsoft.EntityFrameworkCore;

using TimeTracker.Domain.TimeTracking;
using TimeTracker.Domain.Users;

namespace TimeTracker.Infrastructure.Persistence;

public sealed class TimeTrackerDbContext(DbContextOptions<TimeTrackerDbContext> options) : DbContext(options)
{
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();

    public DbSet<UserAccount> Users => Set<UserAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(TimeTrackerDbContext).Assembly);
}
