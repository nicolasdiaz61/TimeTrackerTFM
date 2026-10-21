using System.Security.Claims;

using TimeTracker.Application.Identity;
using TimeTracker.Domain.Users;

namespace TimeTracker.Api.Authentication;

/// <summary>
/// Adaptador de <see cref="ICurrentUser"/>: el empleado es el claim <c>sub</c>, la compañía el claim <c>cid</c> y el rol el claim <c>role</c> del
/// token ya validado, nunca un parámetro del cliente. Sin usuario autenticado o con claims inválidos lanza: el pipeline lo
/// impide, esto es defensa en profundidad.
/// </summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid EmployeeId
    {
        get
        {
            if (Guid.TryParse(Authenticated().FindFirst("sub")?.Value, out var id) && id != Guid.Empty)
            {
                return id;
            }

            throw new InvalidOperationException("No hay un usuario autenticado con un identificador válido.");
        }
    }

    public Guid CompanyId
    {
        get
        {
            if (Guid.TryParse(Authenticated().FindFirst(JwtDefaults.CompanyClaim)?.Value, out var id) && id != Guid.Empty)
            {
                return id;
            }

            throw new InvalidOperationException("No hay un usuario autenticado con una compañía válida.");
        }
    }

    public UserRole Role
    {
        get
        {
            return Authenticated().FindFirst("role")?.Value switch
            {
                nameof(UserRole.Employee) => UserRole.Employee,
                nameof(UserRole.Manager) => UserRole.Manager,
                _ => throw new InvalidOperationException("No hay un usuario autenticado con un rol válido."),
            };
        }
    }

    private ClaimsPrincipal Authenticated()
    {
        var user = accessor.HttpContext?.User;
        return user?.Identity?.IsAuthenticated == true
            ? user
            : throw new InvalidOperationException("No hay un usuario autenticado.");
    }
}
