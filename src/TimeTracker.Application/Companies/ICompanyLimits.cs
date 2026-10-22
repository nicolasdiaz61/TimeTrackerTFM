namespace TimeTracker.Application.Companies;

/// <summary>Topes por compañía que fija el entorno de despliegue; ningún Manager los edita (DD-16, DD-21).</summary>
public interface ICompanyLimits
{
    /// <summary>Máximo de usuarios (Employees y Managers) de una compañía.</summary>
    int MaxEmployees { get; }
}
