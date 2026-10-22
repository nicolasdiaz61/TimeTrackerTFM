using TimeTracker.Application.Identity;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.Security;

/// <summary>
/// Fija la compañía de la petición desde el claim <c>cid</c> ya validado (DD-7). Va tras la autenticación y antes de la
/// autorización. Una petición anónima o con token inválido deja el contexto sin compañía y no lee a <see cref="ICurrentUser"/>. Un
/// endpoint marcado con <see cref="SkipCompanyContextMetadata"/> (solo el registro) tampoco lo fija: ignora el token.
/// </summary>
public sealed class CompanyContextMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context, ICompanyContextSetter setter, ICurrentUser currentUser)
    {
        if (context.User.Identity?.IsAuthenticated == true && context.GetEndpoint()?.Metadata.GetMetadata<SkipCompanyContextMetadata>() is null)
        {
            setter.Set(currentUser.CompanyId);
        }

        return next(context);
    }
}
