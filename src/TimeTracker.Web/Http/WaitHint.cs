using System.Globalization;

namespace TimeTracker.Web.Http;

/// <summary>Frase de espera para un 429 a partir de su <c>Retry-After</c> en segundos; vacía si no llegó.</summary>
public static class WaitHint
{
    public static string Describe(int? seconds) => seconds switch
    {
        null or <= 0 => string.Empty,
        1 => " Espere 1 segundo antes de reintentar.",
        _ => string.Create(CultureInfo.InvariantCulture, $" Espere unos {seconds} segundos antes de reintentar."),
    };
}
