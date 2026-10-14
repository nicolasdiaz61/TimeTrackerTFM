using TimeTracker.Application.Identity;

namespace TimeTracker.Api.Authentication;

/// <summary>
/// Adaptador de <see cref="ICurrentUser"/>: el empleado es el claim <c>sub</c> del token ya validado, nunca un
/// parámetro del cliente. Sin usuario autenticado lanza: el pipeline lo impide, esto es defensa en profundidad.
/// </summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid EmployeeId
    {
        get
        {
            var user = accessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated == true
                && Guid.TryParse(user.FindFirst("sub")?.Value, out var id)
                && id != Guid.Empty)
            {
                return id;
            }

            throw new InvalidOperationException("No hay un usuario autenticado con un identificador válido.");
        }
    }
}
