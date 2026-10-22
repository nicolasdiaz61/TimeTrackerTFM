namespace TimeTracker.Application.Companies;

/// <summary>Resultado de crear la compañía y su primer Manager: la unicidad la decide la base de datos, sin consulta previa (sin carrera).</summary>
public enum RegistrationOutcome
{
    Created,

    /// <summary>El nombre normalizado de la compañía ya existe.</summary>
    DuplicateName,

    /// <summary>El correo ya está registrado en alguna compañía.</summary>
    DuplicateEmail,
}
