using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
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

        // from y to (UTC, ventana semiabierta) son opcionales: sin ellos, los últimos 300 como antes (ADR 0016).
        group.MapGet("", ListAsync)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static async Task<Results<Ok<TimeEntryListResponse>, ProblemHttpResult>> ListAsync(
        [FromQuery(Name = "from")] string? from,
        [FromQuery(Name = "to")] string? to,
        TimeProvider clock,
        GetTimeEntriesUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (TimeWindowQuery.TryRead(from, to, clock.GetUtcNow(), out var window) is { } error)
        {
            return InvalidWindow(error);
        }

        return TypedResults.Ok(TimeEntryListResponse.From(await useCase.ExecuteAsync(window, cancellationToken)));
    }

    // Compartido con /api/team: 400 en español con el motivo.
    internal static ProblemHttpResult InvalidWindow(string reason)
        => TypedResults.Problem(detail: reason, statusCode: StatusCodes.Status400BadRequest, title: "Solicitud no válida");
}
