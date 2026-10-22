using System.Net;

using TimeTracker.Web.Auth;
using TimeTracker.Web.Http;

namespace TimeTracker.Web.Account;

// Contrato de POST /api/auth/change-password (docs/api/openapi.json): solo las dos contraseñas; la persona sale del token.
public sealed record ChangePasswordRequestDto(string CurrentPassword, string NewPassword);

public interface IPasswordChangeApi
{
    Task<ApiCallResult<LoginResponseDto>> ChangePasswordAsync(ChangePasswordRequestDto request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Estado de /cambiar-contrasena sin dependencia del navegador: coincidencia de la confirmación (solo presentación), vaciado de los
/// tres campos tras cada respuesta, sustitución de la sesión por el token nuevo y destino posterior seguro.
/// </summary>
public sealed class ChangePasswordForm(IPasswordChangeApi api, ISessionSignIn session)
{
    public const string TooManyRequestsMessage = "Demasiados intentos de cambio de contraseña.";
    public const string ForbiddenMessage = "No tiene permiso para cambiar la contraseña.";
    public const string MandatoryChangeNotice = "Debe cambiar la contraseña temporal antes de continuar.";

    private static readonly string[] ShownFields = ["currentPassword", "newPassword"];

    private readonly FormFeedback feedback = new();

    public string CurrentPassword { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>Valor del parámetro <c>returnUrl</c> de la URL; solo se respeta si es una ruta relativa segura.</summary>
    public string? ReturnUrlParameter { get; set; }

    public bool IsBusy { get; private set; }

    public string? ErrorMessage => feedback.Message;

    /// <summary>Destino tras un cambio correcto; el componente navega a él.</summary>
    public string? NavigateTo { get; private set; }

    public IReadOnlyList<string> ErrorsFor(string field) => feedback.ErrorsFor(field);

    public IReadOnlyList<string> OtherErrors => feedback.OtherErrors(ShownFields);

    public async Task SubmitAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        feedback.Clear();
        NavigateTo = null;
        if (!PasswordConfirmation.Matches(NewPassword, ConfirmPassword))
        {
            feedback.ShowField(PasswordConfirmation.Field, PasswordConfirmation.MismatchMessage);
            return;
        }

        IsBusy = true;
        try
        {
            var result = await api.ChangePasswordAsync(new ChangePasswordRequestDto(CurrentPassword, NewPassword), cancellationToken);
            ClearPasswords();
            if (result.Error is { } failure)
            {
                // El 401 lo trata el BearerTokenHandler (sesión cerrada y redirección a /login): aquí no se muestra.
                if (failure.StatusCode != HttpStatusCode.Unauthorized)
                {
                    feedback.ShowFailure(failure, TooManyRequestsMessage, Specific);
                }

                return;
            }

            await session.SignInAsync(result.Value!.AccessToken);
            NavigateTo = ReturnUrl.IsSafe(ReturnUrlParameter) ? ReturnUrlParameter! : ReturnUrl.DefaultTarget;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string? Specific(ApiFailure failure) => failure.StatusCode switch
    {
        HttpStatusCode.Forbidden => ForbiddenMessage,
        HttpStatusCode.Conflict => failure.Message,
        _ => null,
    };

    private void ClearPasswords()
    {
        CurrentPassword = string.Empty;
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
    }
}
