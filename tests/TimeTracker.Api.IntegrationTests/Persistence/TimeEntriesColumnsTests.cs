using Microsoft.EntityFrameworkCore;

using Npgsql;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Api.IntegrationTests.Persistence;

/// <summary>Tabla <c>time_entries</c> tras la migración B: columnas, restricción CHECK e ida y vuelta del instante UTC con la compañía.</summary>
[Collection("Postgres")]
public class TimeEntriesColumnsTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset EightAm = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TimeEntries_HasExactlyTheSixColumnsAndOnlyClockOutAtIsNullable()
    {
        var columns = await MigrationDatabase.QueryAsync(
            postgres.ConnectionString,
            "SELECT column_name || ':' || data_type || ':' || is_nullable FROM information_schema.columns WHERE table_name = 'time_entries' ORDER BY column_name");

        Assert.Equal(
            [
                "clock_in_at:timestamp with time zone:NO",
                "clock_out_at:timestamp with time zone:YES",
                "company_id:uuid:NO",
                "employee_id:uuid:NO",
                "id:uuid:NO",
                "type:character varying:NO",
            ],
            columns);
    }

    [Fact]
    public async Task ClockOutBeforeClockIn_IsRejectedByTheCheck()
    {
        var employee = Guid.NewGuid();
        await UserSeed.InsertRawAsync(postgres.ConnectionString, employee, $"fichaje.{employee:N}@example.test");

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => MigrationDatabase.ExecuteAsync(
                postgres.ConnectionString,
                $"INSERT INTO time_entries (id, company_id, employee_id, clock_in_at, clock_out_at, type) VALUES (gen_random_uuid(), '{Company.DefaultId}', '{employee}', '2026-10-12T10:00:00Z', '2026-10-12T09:00:00Z', 'Work')"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_time_entries_clock_out_after_clock_in", error.ConstraintName);
    }

    [Fact]
    public async Task ATimeEntry_RoundTripsTheUtcInstantAndTheCompany()
    {
        var employee = UserSeed.Create("fichaje");
        await using (var seed = postgres.CreateContext(postgres.ConnectionString))
        {
            await UserSeed.AddAsync(seed, employee);
        }

        var entry = TimeEntry.Start(Company.DefaultId, employee.Id, TimeEntryType.Work, EightAm);
        await using (var writer = postgres.CreateContext(postgres.ConnectionString))
        {
            writer.Add(entry);
            await writer.SaveChangesAsync(CancellationToken.None);
        }

        await using var reader = postgres.CreateContext(postgres.ConnectionString);
        var read = await reader.Set<TimeEntry>().AsNoTracking().SingleAsync(candidate => candidate.Id == entry.Id, CancellationToken.None);

        Assert.Equal((Company.DefaultId, employee.Id, EightAm, TimeSpan.Zero, (DateTimeOffset?)null), (read.CompanyId, read.EmployeeId, read.ClockInAt, read.ClockInAt.Offset, read.ClockOutAt));
    }
}
