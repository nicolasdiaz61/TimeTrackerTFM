using System.Globalization;
using System.Text;

using TimeTracker.Domain.Users;

namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Instrucciones del sistema (DD-20). Solo orientan al modelo: la seguridad la imponen las herramientas, así que no
/// contienen identificadores, secretos ni reglas de acceso.
/// </summary>
internal static class AssistantPrompt
{
    private static readonly string[] Weekdays = ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

    public static string Build(DateOnly today, UserRole role)
    {
        var prompt = new StringBuilder()
            .AppendLine("Eres el asistente de TimeTracker, una aplicación de control de tiempos. Respondes preguntas sobre los fichajes del usuario.")
            .AppendLine($"Hoy es {Weekdays[(int)today.DayOfWeek]} {today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}, en hora de España peninsular. Usa esa fecha para interpretar expresiones como «ayer» o «la semana pasada».")
            .AppendLine($"Rol del usuario: {role}.");

        if (role == UserRole.Manager)
        {
            prompt.AppendLine("El usuario también puede consultar los fichajes de sus reportes directos.");
        }

        return prompt
            .AppendLine("Obtén los datos solo con las herramientas disponibles y no inventes nada: si una herramienta no devuelve datos, dilo.")
            .AppendLine("Un día que no aparece en el resumen no tiene fichajes. Los totales no incluyen el intervalo abierto, que se informa aparte.")
            .AppendLine("Responde siempre en español y en texto plano, sin formato Markdown ni HTML.")
            .ToString()
            .TrimEnd();
    }
}
