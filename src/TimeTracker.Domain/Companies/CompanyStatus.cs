namespace TimeTracker.Domain.Companies;

/// <summary>Estado de una compañía. Solo un operador lo cambia (por SQL); una compañía suspendida no inicia sesión.</summary>
public enum CompanyStatus
{
    Active,
    Suspended,
}
