namespace TimeTracker.Api.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    // Se enlaza como texto para que el validador controle el mensaje y el valor nunca se repita.
    public string? SigningKey { get; set; }
}
