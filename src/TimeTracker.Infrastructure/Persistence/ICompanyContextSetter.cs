namespace TimeTracker.Infrastructure.Persistence;

/// <summary>
/// Fija la compañía de un ámbito. Es de escritura única: un segundo valor distinto es un error. Es una interfaz de
/// Infrastructure (no un puerto de Application): la implementa el adaptador de Api y solo la usan el middleware y
/// <see cref="ICompanyScopeFactory"/> (DD-7).
/// </summary>
public interface ICompanyContextSetter
{
    void Set(Guid companyId);
}
