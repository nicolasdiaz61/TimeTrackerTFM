using TimeTracker.Application.Authentication;

namespace TimeTracker.Application.Tests.Fakes;

/// <summary>Generador falso: devuelve contraseñas predecibles (<c>temporal-prueba-0001</c>, ...) y cuenta las llamadas.</summary>
internal sealed class SequenceTemporaryPasswordGenerator : ITemporaryPasswordGenerator
{
    public int Calls { get; private set; }

    public string Generate() => $"temporal-prueba-{++Calls:0000}";
}
