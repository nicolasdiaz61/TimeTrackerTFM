using System.Text;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using TimeTracker.Application.Authentication;
using TimeTracker.Domain.Users;

namespace TimeTracker.Api.Authentication;

/// <summary>Emite el JWT HS256 de 30 minutos con los tiempos del <see cref="TimeProvider"/> de la aplicación (DD-8).</summary>
public sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider clock) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false };
    private readonly SigningCredentials _credentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.SigningKey!)),
        SecurityAlgorithms.HmacSha256);

    public AccessToken Issue(UserAccount user, string companyName)
    {
        // Un JWT guarda segundos enteros: se trunca para que expiresAt de la respuesta coincida con exp.
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(clock.GetUtcNow().ToUnixTimeSeconds());
        var expiresAt = issuedAt + JwtDefaults.TokenLifetime;
        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
            [JwtRegisteredClaimNames.Name] = user.DisplayName,
            [JwtRegisteredClaimNames.Email] = user.Email,
            ["role"] = user.Role.ToString(),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            [JwtDefaults.CompanyClaim] = user.CompanyId.ToString(),
            [JwtDefaults.CompanyNameClaim] = companyName,
        };

        // Solo con el cambio de contraseña pendiente: su ausencia significa que no hay nada pendiente.
        if (user.MustChangePassword)
        {
            claims[JwtDefaults.PasswordChangePendingClaim] = JwtDefaults.PasswordChangePendingValue;
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = JwtDefaults.Issuer,
            Audience = JwtDefaults.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _credentials,
            Claims = claims,
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
