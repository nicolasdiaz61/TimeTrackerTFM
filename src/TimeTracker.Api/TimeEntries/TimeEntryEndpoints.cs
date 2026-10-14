using Microsoft.Extensions.DependencyInjection.Extensions;

using TimeTracker.Application.TimeTracking;
using TimeTracker.Domain.TimeTracking;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.TimeEntries;

public static class TimeEntryEndpoints
{
    public static IServiceCollection AddTimeEntries(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddPersistence();
        services.AddScoped<ClockInUseCase>();
        services.AddScoped<ClockOutUseCase>();
        services.AddScoped<StartBreakUseCase>();
        services.AddScoped<EndBreakUseCase>();
        services.AddScoped<GetTimeEntriesUseCase>();

        return services;
    }

    public static IEndpointRouteBuilder MapTimeEntryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/time-entries").WithTags("TimeEntries");

        // Sin Location: no existe un GET por id y un enlace roto sería peor que ninguno (DD-15).
        group.MapPost("clock-in", async (ClockInUseCase useCase, CancellationToken cancellationToken)
            => TypedResults.Created((string?)null, TimeEntryResponse.From(await useCase.ExecuteAsync(cancellationToken))))
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("clock-out", async (ClockOutUseCase useCase, CancellationToken cancellationToken)
            => TypedResults.Ok(TimeEntryResponse.From(await useCase.ExecuteAsync(cancellationToken))))
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("start-break", async (StartBreakUseCase useCase, CancellationToken cancellationToken)
            => TypedResults.Ok(TimeEntryResponse.From(await useCase.ExecuteAsync(TimeEntryType.Break, cancellationToken))))
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("start-meal-break", async (StartBreakUseCase useCase, CancellationToken cancellationToken)
            => TypedResults.Ok(TimeEntryResponse.From(await useCase.ExecuteAsync(TimeEntryType.MealBreak, cancellationToken))))
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("end-break", async (EndBreakUseCase useCase, CancellationToken cancellationToken)
            => TypedResults.Ok(TimeEntryResponse.From(await useCase.ExecuteAsync(cancellationToken))))
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("", async (GetTimeEntriesUseCase useCase, CancellationToken cancellationToken)
            => TypedResults.Ok(TimeEntryListResponse.From(await useCase.ExecuteAsync(cancellationToken))))
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }
}
