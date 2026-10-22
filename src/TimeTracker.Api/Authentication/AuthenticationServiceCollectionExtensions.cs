using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using TimeTracker.Application.Authentication;
using TimeTracker.Application.Identity;
using TimeTracker.Domain.Users;

namespace TimeTracker.Api.Authentication;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateOnStart();

        // Singletons: el hash ficticio se calcula una vez y la clave de firma se prepara una vez.
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddScoped<SessionIssuer>();
        services.AddScoped<LoginUseCase>();
        services.AddScoped<ChangePasswordUseCase>();

        // La identidad del empleado sale del claim sub del token (DD-14); ya no existe identidad provisional.
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, TimeProvider>(ConfigureBearer);
        // Seguro por defecto: todo endpoint que no declare otra cosa exige un usuario autenticado (DD-13).
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }

    private static void ConfigureBearer(JwtBearerOptions bearer, IOptions<JwtOptions> jwt, TimeProvider clock)
    {
        // Sin traducción de nombres: `sub` y `role` llegan tal como están en el token (V4).
        bearer.MapInboundClaims = false;
        // El 401 solo anuncia `Bearer`: sin error_description que oriente a quien prueba tokens (A05, V23).
        bearer.IncludeErrorDetails = false;
        bearer.Events = new JwtBearerEvents { OnTokenValidated = RejectInvalidClaims };
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            // Solo HS256 con la clave configurada: cierra alg=none y la confusión de algoritmo (DD-10).
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.SigningKey!)),
            ValidIssuer = JwtDefaults.Issuer,
            ValidAudience = JwtDefaults.Audience,
            ValidateLifetime = true,
            // IdentityModel no usa el TimeProvider de la aplicación para la vigencia (V6): se evalúa aquí con él,
            // de modo que un FakeTimeProvider gobierne también la caducidad (DD-11).
            LifetimeValidator = (notBefore, expires, _, parameters) => IsWithinLifetime(clock.GetUtcNow().UtcDateTime, notBefore, expires, parameters.ClockSkew),
            ClockSkew = JwtDefaults.ClockSkew,
            NameClaimType = "name",
            RoleClaimType = "role",
        };
    }

    // `sub` debe ser un GUID real, `role` uno de los roles conocidos y `cid` un GUID no vacío: así ICurrentUser nunca ve un token
    // a medias (DD-10) y no existe una compañía por defecto: sin cid válido, falla cerrado (DD-6). Un `mcp` distinto de «true»
    // no invalida el token: se trata como sin cambio de contraseña pendiente.
    private static Task RejectInvalidClaims(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var subjectIsValid = Guid.TryParse(principal?.FindFirst("sub")?.Value, out var subject) && subject != Guid.Empty;
        var roleIsValid = Enum.TryParse<UserRole>(principal?.FindFirst("role")?.Value, ignoreCase: false, out var role) && Enum.IsDefined(role);
        var companyIsValid = Guid.TryParse(principal?.FindFirst(JwtDefaults.CompanyClaim)?.Value, out var company) && company != Guid.Empty;
        if (!subjectIsValid || !roleIsValid || !companyIsValid)
        {
            context.Fail("Los claims sub, role o cid no son válidos.");
        }

        return Task.CompletedTask;
    }

    // La caducidad es obligatoria: un token sin `exp` no es válido.
    private static bool IsWithinLifetime(DateTime now, DateTime? notBefore, DateTime? expires, TimeSpan skew)
        => expires is not null
            && expires.Value + skew >= now
            && (notBefore is null || notBefore.Value - skew <= now);
}
