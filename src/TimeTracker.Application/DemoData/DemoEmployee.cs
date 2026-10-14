namespace TimeTracker.Application.DemoData;

/// <summary>Un empleado de demostración ya existente y el perfil con el que se generan sus fichajes.</summary>
public sealed record DemoEmployee(Guid Id, DemoScheduleProfile Profile);
