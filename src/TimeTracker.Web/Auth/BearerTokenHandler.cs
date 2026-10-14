using System.Net;
using System.Net.Http.Headers;

using Microsoft.AspNetCore.Components;

namespace TimeTracker.Web.Auth;

/// <summary>
/// Añade «Authorization: Bearer» solo a las peticiones dirigidas a la API (nunca a otro origen). Ante un 401 con sesión
/// la cierra, deja el aviso de sesión caducada y lleva a /login conservando la ruta actual; un 403 no cierra la sesión.
/// </summary>
public sealed class BearerTokenHandler(
    Uri apiBaseUri,
    TokenStore store,
    JwtAuthenticationStateProvider authentication,
    SessionNotice notice,
    NavigationManager navigation) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var token = IsApiRequest(request.RequestUri) ? await store.GetAsync() : null;
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized && !string.IsNullOrEmpty(token))
        {
            await authentication.SignOutAsync();
            notice.MarkExpired();
            var current = "/" + navigation.ToBaseRelativePath(navigation.Uri);
            navigation.NavigateTo($"login?returnUrl={Uri.EscapeDataString(current)}");
        }

        return response;
    }

    private bool IsApiRequest(Uri? uri)
        => uri is { IsAbsoluteUri: true }
            && uri.Scheme == apiBaseUri.Scheme
            && string.Equals(uri.Host, apiBaseUri.Host, StringComparison.OrdinalIgnoreCase)
            && uri.Port == apiBaseUri.Port;
}
