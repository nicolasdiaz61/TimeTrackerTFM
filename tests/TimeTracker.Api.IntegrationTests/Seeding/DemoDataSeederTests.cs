using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using TimeTracker.Api.IntegrationTests.Postgres;
using TimeTracker.Api.IntegrationTests.Support;
using TimeTracker.Application.DemoData;
using TimeTracker.Application.Time;
using TimeTracker.Domain.Companies;
using TimeTracker.Domain.TimeTracking;
using TimeTracker.Domain.Users;

namespace TimeTracker.Api.IntegrationTests.Seeding;

[Collection("Postgres")]
public class DemoDataSeederTests(PostgresFixture postgres)
{
    private const string DemoPassword = "Clave-Demo-De-Prueba-26";
    private const string MartaEmail = "marta.responsable@example.test";
    private const string LuisEmail = "luis.empleado@example.test";
    private const string AnaEmail = "ana.empleada@example.test";
    private const string PabloEmail = "pablo.empleado@example.test";

    // El reloj de ApiFactory arranca el lunes 12/10/2026 a las 08:00Z (10:00 locales): ventana del 12/9 al 11/10.
    private static readonly DateOnly Today = new(2026, 10, 12);

    [Fact]
    public async Task Seeding_CreatesClosedWeekdayEntriesForEveryUserBeforeToday()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var root = new ApiFactory();
        using var factory = Configure(root, connectionString, "true");
        _ = factory.CreateClient();

        var (users, entries) = await LoadAsync(connectionString);

        Assert.Equal(4, users.Count);
        foreach (var user in users)
        {
            var own = entries.Where(entry => entry.EmployeeId == user.Id).ToList();
            Assert.NotEmpty(own);
            Assert.All(own, entry => Assert.False(entry.IsOpen));
            var days = own.Select(entry => SpainTime.DateOf(entry.ClockInAt)).Distinct().ToList();
            Assert.All(days, day =>
            {
                Assert.True(day < Today, "Hoy se deja libre para fichar en vivo.");
                Assert.True(day >= Today.AddDays(-SeedDemoTimeEntriesUseCase.WindowDays));
                Assert.NotEqual(DayOfWeek.Saturday, day.DayOfWeek);
                Assert.NotEqual(DayOfWeek.Sunday, day.DayOfWeek);
            });
            Assert.InRange(days.Count, 17, 22);
        }
    }

    [Fact]
    public async Task Seeding_GivesEachUserADifferentProfile()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var root = new ApiFactory();
        using var factory = Configure(root, connectionString, "true");
        _ = factory.CreateClient();

        var (users, entries) = await LoadAsync(connectionString);

        var byEmail = users.ToDictionary(user => user.Email, user => entries.Where(entry => entry.EmployeeId == user.Id).ToList());
        Assert.DoesNotContain(byEmail[PabloEmail], entry => entry.Type == TimeEntryType.MealBreak);
        Assert.Contains(byEmail[LuisEmail], entry => entry.Type == TimeEntryType.MealBreak);
        Assert.Contains(byEmail[AnaEmail], entry => entry.Type == TimeEntryType.Break);
        Assert.Contains(byEmail[MartaEmail], entry => entry.Type == TimeEntryType.MealBreak);
        var workedMinutes = byEmail.Values
            .Select(own => own.Where(entry => entry.Type == TimeEntryType.Work).Sum(entry => entry.CalculateDuration()!.Value.Minutes))
            .ToList();
        Assert.Equal(4, workedMinutes.Distinct().Count());
    }

    [Fact]
    public async Task Seeding_RunTwice_DoesNotDuplicateAnything()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using (var firstRoot = new ApiFactory())
        using (var first = Configure(firstRoot, connectionString, "true"))
        {
            _ = first.CreateClient();
        }

        var (_, before) = await LoadAsync(connectionString);

        using (var secondRoot = new ApiFactory())
        using (var second = Configure(secondRoot, connectionString, "true"))
        {
            _ = second.CreateClient();
        }

        var (_, after) = await LoadAsync(connectionString);
        Assert.NotEmpty(before);
        Assert.Equal(before.Select(entry => entry.Id).Order(), after.Select(entry => entry.Id).Order());
    }

    [Fact]
    public async Task Seeding_TwoDaysLater_AddsOnlyTheNewDays()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using (var firstRoot = new ApiFactory())
        using (var first = Configure(firstRoot, connectionString, "true"))
        {
            _ = first.CreateClient();
        }

        var (_, before) = await LoadAsync(connectionString);

        using (var laterRoot = new ApiFactory())
        using (var later = Configure(laterRoot, connectionString, "true"))
        {
            laterRoot.Clock.Advance(TimeSpan.FromDays(3)); // jueves 15: faltan lunes 12, martes 13 y miércoles 14
            _ = later.CreateClient();
        }

        var (_, after) = await LoadAsync(connectionString);
        var added = after.Where(entry => before.All(old => old.Id != entry.Id)).ToList();
        Assert.NotEmpty(added);
        Assert.All(added, entry => Assert.InRange(SpainTime.DateOf(entry.ClockInAt), Today, Today.AddDays(2)));
        Assert.All(before, old => Assert.Contains(after, entry => entry.Id == old.Id));
    }

    [Fact]
    public async Task Seeding_WithAPreviousEntryOnADay_KeepsOnlyThatEntryThatDay()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using (var usersOnlyRoot = new ApiFactory())
        using (var usersOnly = Configure(usersOnlyRoot, connectionString, null))
        {
            _ = usersOnly.CreateClient();
        }

        var luisId = (await LoadAsync(connectionString)).Users.Single(user => user.Email == LuisEmail).Id;
        var manualDay = new DateOnly(2026, 10, 7);
        var manual = TimeEntry.Start(Company.DefaultId, luisId, TimeEntryType.Work, SpainTime.ToUtc(manualDay, new TimeOnly(9, 0)));
        manual.Close(SpainTime.ToUtc(manualDay, new TimeOnly(9, 40)));
        await using (var context = postgres.CreateContext(connectionString))
        {
            context.TimeEntries.Add(manual);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        using (var factoryRoot = new ApiFactory())
        using (var factory = Configure(factoryRoot, connectionString, "true"))
        {
            _ = factory.CreateClient();
        }

        var (_, entries) = await LoadAsync(connectionString);
        var thatDay = entries.Where(entry => entry.EmployeeId == luisId && SpainTime.DateOf(entry.ClockInAt) == manualDay).ToList();
        Assert.Equal(manual.Id, Assert.Single(thatDay).Id);
        Assert.Contains(entries, entry => entry.EmployeeId == luisId && SpainTime.DateOf(entry.ClockInAt) == manualDay.AddDays(1));
    }

    [Fact]
    public async Task Seeding_Disabled_CreatesNoEntries()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var root = new ApiFactory();
        using var factory = Configure(root, connectionString, null);
        _ = factory.CreateClient();

        var (users, entries) = await LoadAsync(connectionString);

        Assert.Equal(4, users.Count);
        Assert.Empty(entries);
    }

    [Fact]
    public async Task Seeding_DemoDataFalse_CreatesNoEntries()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var root = new ApiFactory();
        using var factory = Configure(root, connectionString, "false");
        _ = factory.CreateClient();

        Assert.Empty((await LoadAsync(connectionString)).Entries);
    }

    [Fact]
    public async Task SeededMarta_SeesLuisAndAnaWithDataAndNotPablo()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var root = new ApiFactory();
        using var factory = Configure(root, connectionString, "true");
        var client = factory.CreateAnonymousClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = MartaEmail, password = DemoPassword }, CancellationToken.None);
        using var loginBody = JsonDocument.Parse(await login.Content.ReadAsStringAsync(CancellationToken.None));
        client.DefaultRequestHeaders.Authorization = new("Bearer", loginBody.RootElement.GetProperty("accessToken").GetString());
        var (users, _) = await LoadAsync(connectionString);

        foreach (var email in new[] { LuisEmail, AnaEmail })
        {
            var response = await client.GetAsync($"/api/team/members/{users.Single(user => user.Email == email).Id}/time-entries", CancellationToken.None);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
            Assert.True(body.RootElement.GetProperty("items").GetArrayLength() > 40, "Un mes con pausas tiene muchos intervalos.");
            Assert.True(body.RootElement.GetProperty("totalWorkedMinutes").GetInt32() > 0);
            Assert.True(body.RootElement.GetProperty("totalBreakMinutes").GetInt32() > 0);
            Assert.True(body.RootElement.GetProperty("totalMealBreakMinutes").GetInt32() > 0);
            Assert.Equal("Idle", body.RootElement.GetProperty("status").GetString());
        }

        var pablo = await client.GetAsync($"/api/team/members/{users.Single(user => user.Email == PabloEmail).Id}/time-entries", CancellationToken.None);
        Assert.Equal(HttpStatusCode.NotFound, pablo.StatusCode);
    }

    [Fact]
    public async Task Seeding_LogsOnlyCounters()
    {
        var logs = new CapturingLoggerProvider();
        var connectionString = await postgres.CreateEmptyDatabaseAsync();
        using var root = new ApiFactory();
        using var factory = Configure(root, connectionString, "true").WithWebHostBuilder(builder => builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Trace);
            logging.AddProvider(logs);
        }));
        _ = factory.CreateClient();

        Assert.Contains(logs.Messages, message => message.Contains("Fichajes de demostración sembrados", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Messages, message => message.Contains("example.test", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Messages, message => message.Contains(DemoPassword, StringComparison.Ordinal));
    }

    private static WebApplicationFactory<Program> Configure(ApiFactory root, string connectionString, string? demoData)
        => root.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Default", connectionString);
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
            builder.UseSetting("Seed:DemoUsers", "true");
            builder.UseSetting("Seed:DemoPassword", DemoPassword);
            if (demoData is not null)
            {
                builder.UseSetting("Seed:DemoData", demoData);
            }
        });

    private async Task<(List<UserAccount> Users, List<TimeEntry> Entries)> LoadAsync(string connectionString)
    {
        await using var context = postgres.CreateContext(connectionString);
        return (
            await context.Users.AsNoTracking().ToListAsync(CancellationToken.None),
            await context.TimeEntries.AsNoTracking().ToListAsync(CancellationToken.None));
    }
}
