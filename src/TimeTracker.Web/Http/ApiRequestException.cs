using System.Net;

namespace TimeTracker.Web.Http;

/// <summary>Error de la API ya traducido a un mensaje en español apto para mostrar al usuario.</summary>
/// <param name="message">Mensaje para el usuario, sin datos técnicos.</param>
/// <param name="statusCode">Estado HTTP de la respuesta; <see langword="null"/> si no hubo respuesta (red caída, CORS, cuerpo ilegible).</param>
public sealed class ApiRequestException(string message, HttpStatusCode? statusCode = null) : Exception(message)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
}
