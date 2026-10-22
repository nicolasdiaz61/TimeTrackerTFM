namespace TimeTracker.Application.Team;

public abstract record ResetTemporaryPasswordResult
{
    private ResetTemporaryPasswordResult()
    {
    }

    /// <summary>Contraseña regenerada; <paramref name="TemporaryPassword"/> es la única vez que existe en claro.</summary>
    public sealed record Regenerated(string TemporaryPassword) : ResetTemporaryPasswordResult;

    /// <summary>Ajeno, inexistente o no es reporte directo de quien la pide: una sola respuesta para los tres casos.</summary>
    public sealed record NotFound : ResetTemporaryPasswordResult;

    /// <summary>La persona ya cambió su contraseña, o su fila cambió entre la lectura y el guardado.</summary>
    public sealed record Conflict : ResetTemporaryPasswordResult;
}
