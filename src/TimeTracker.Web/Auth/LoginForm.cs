using System.Net;

using TimeTracker.Web.Http;

namespace TimeTracker.Web.Auth;

public interface ILoginApi
{
    /// <summary>Lanza <see cref="ApiRequestException"/> ante cualquier fallo.</summary>
    Task<LoginResponseDto> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
}

/// <summary>
/// Estado de /login sin dependencia del navegador: validación de campos vacíos, mensajes por estado HTTP (el mismo
/// ante contraseña errónea y correo inexistente), vaciado de la contraseña tras cada respuesta y destino posterior
/// (cambio de contraseña pendiente, <c>returnUrl</c> seguro o el Inicio).
/// </summary>
public sealed class LoginForm(ILoginApi api, ISessionSignIn session)
{
    public const string EmptyFieldsMessage = "Introduzca el correo electrónico y la contraseña.";
    public const string InvalidCredentialsMessage = "Correo o contraseña incorrectos.";
    public const string TooManyRequestsMessage = "Demasiados intentos. Espere un minuto e inténtelo de nuevo.";

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>Valor del parámetro <c>returnUrl</c> de la URL; solo se respeta si es una ruta relativa segura.</summary>
    public string? ReturnUrlParameter { get; set; }

    public bool IsBusy { get; private set; }

    public string? ErrorMessage { get; private set; }

    /// <summary>Destino tras un inicio de sesión correcto; el componente navega a él.</summary>
    public string? NavigateTo { get; private set; }

    public async Task SubmitAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        NavigateTo = null;
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrEmpty(Password))
        {
            ErrorMessage = EmptyFieldsMessage;
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await api.LoginAsync(Email.Trim(), Password, cancellationToken);
            Password = string.Empty;
            await session.SignInAsync(response.AccessToken);
            NavigateTo = DestinationFor(response.AccessToken);
        }
        catch (ApiRequestException exception)
        {
            Password = string.Empty;
            ErrorMessage = exception.StatusCode switch
            {
                HttpStatusCode.Unauthorized => InvalidCredentialsMessage,
                HttpStatusCode.TooManyRequests => TooManyRequestsMessage,
                _ => ApiRequest.GenericErrorMessage,
            };
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string DestinationFor(string token)
        => SessionClaims.IsPasswordChangePending(token)
            ? PasswordChangeRedirect.WithReturnUrl(ReturnUrlParameter)
            : ReturnUrl.IsSafe(ReturnUrlParameter) ? ReturnUrlParameter! : ReturnUrl.DefaultTarget;
}
