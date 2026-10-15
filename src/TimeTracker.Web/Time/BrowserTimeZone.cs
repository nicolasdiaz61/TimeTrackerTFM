using Microsoft.JSInterop;

namespace TimeTracker.Web.Time;

/// <summary>
/// Zona horaria del navegador mediante <c>wwwroot/js/local-time.js</c>. Las llamadas son síncronas (WebAssembly
/// ejecuta en el mismo hilo que el navegador) y no usan <c>eval</c>, por lo que valen con la CSP de la Web.
/// </summary>
public sealed class BrowserTimeZone(IJSInProcessRuntime javaScript) : ILocalTimeZone
{
    public TimeSpan OffsetAt(DateTimeOffset instant)
        => TimeSpan.FromMinutes(javaScript.Invoke<double>("timeTrackerLocalTime.offsetMinutes", instant.ToUnixTimeMilliseconds()));

    public DateTimeOffset StartOfDay(DateOnly date)
        => DateTimeOffset.FromUnixTimeMilliseconds((long)javaScript.Invoke<double>("timeTrackerLocalTime.startOfDay", date.Year, date.Month, date.Day));
}
