namespace TimeTracker.Application.DemoData;

/// <summary>Contadores de una siembra: días-empleado creados y omitidos (ya tenían fichajes) e intervalos creados.</summary>
public sealed record DemoSeedResult(int CreatedDays, int SkippedDays, int CreatedEntries);
