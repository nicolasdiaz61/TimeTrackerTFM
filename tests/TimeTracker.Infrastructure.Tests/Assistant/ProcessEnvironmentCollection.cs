namespace TimeTracker.Infrastructure.Tests.Assistant;

/// <summary>
/// Las pruebas que modifican variables de entorno del proceso van en esta colección: xUnit no ejecuta en paralelo ninguna
/// otra colección mientras corre una con la paralelización desactivada, así que el valor temporal no contamina a otras pruebas.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessEnvironmentCollection
{
    public const string Name = "ProcessEnvironment";
}
