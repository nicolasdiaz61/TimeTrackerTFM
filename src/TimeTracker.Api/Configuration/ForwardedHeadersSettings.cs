namespace TimeTracker.Api.Configuration;

public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>Saltos de proxy de confianza por defecto: el borde de Railway entrega «cliente, borde».</summary>
    public const int DefaultHops = 2;

    /// <summary>
    /// Confía en <c>X-Forwarded-For</c> y <c>X-Forwarded-Proto</c> del proxy inverso que precede a la API. Desactivado
    /// por defecto: sin proxy de confianza, esas cabeceras las controla el cliente y permitirían falsear su IP.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Número de entradas, contadas desde la derecha, que se consideran añadidas por proxies de confianza.</summary>
    public int Hops { get; set; } = DefaultHops;
}
