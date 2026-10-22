using System.Net;

using TimeTracker.Web.Auth;
using TimeTracker.Web.Http;

namespace TimeTracker.Web.Registration;

/// <summary>
/// Estado de /registro sin dependencia del navegador: mapeo a la petición, coincidencia de la confirmación (solo presentación),
/// vaciado de la contraseña y del código tras cada respuesta y traducción de los estados HTTP. La política de contraseñas, el
/// código y la bandera de registro los decide la API.
/// </summary>
public sealed class RegisterForm(IRegistrationApi api, ISessionSignIn session)
{
    public const string ConfirmationField = "confirmPassword";
    public const string ConfirmationMismatchMessage = "Las contraseñas no coinciden.";
    public const string ReviewFieldsMessage = FormFeedback.ReviewFieldsMessage;
    public const string InvalidInviteCodeMessage = "El código de invitación no es válido.";
    public const string TooManyRequestsMessage = "Demasiados intentos de registro.";
    public const string UnavailableMessage = "El registro no está disponible.";

    private static readonly string[] ShownFields = ["companyName", "managerName", "email", "password", "inviteCode"];

    private readonly FormFeedback feedback = new();

    public string CompanyName { get; set; } = string.Empty;

    public string ManagerName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;

    public string InviteCode { get; set; } = string.Empty;

    public bool IsBusy { get; private set; }

    public string? ErrorMessage => feedback.Message;

    /// <summary>Destino tras un registro correcto; el componente navega a él.</summary>
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
        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            feedback.ShowField(ConfirmationField, ConfirmationMismatchMessage);
            return;
        }

        IsBusy = true;
        try
        {
            // La contraseña y el código viajan tal cual (los espacios cuentan); el resto se recorta.
            var result = await api.RegisterCompanyAsync(
                new RegisterCompanyRequestDto(CompanyName.Trim(), ManagerName.Trim(), Email.Trim(), Password, InviteCode),
                cancellationToken);
            ClearSecrets();
            if (result.Error is { } failure)
            {
                feedback.ShowFailure(failure, TooManyRequestsMessage, Specific);
                return;
            }

            await session.SignInAsync(result.Value!.AccessToken);
            NavigateTo = ReturnUrl.DefaultTarget;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // 403 sin indicar qué falló; 401 y 404 son el registro cerrado y no cierran la sesión ni van a /login.
    private static string? Specific(ApiFailure failure) => failure.StatusCode switch
    {
        HttpStatusCode.Forbidden => InvalidInviteCodeMessage,
        HttpStatusCode.Conflict => failure.Message,
        HttpStatusCode.Unauthorized or HttpStatusCode.NotFound => UnavailableMessage,
        _ => null,
    };

    private void ClearSecrets()
    {
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        InviteCode = string.Empty;
    }
}
