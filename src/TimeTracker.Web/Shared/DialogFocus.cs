using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace TimeTracker.Web.Shared;

/// <summary>
/// Puente mínimo con js/dialog.js para los diálogos modales: mueve el foco al diálogo, lo mantiene dentro mientras está abierto,
/// bloquea el desplazamiento del fondo y devuelve el foco a quien lo abrió. Solo mejora la experiencia: sin JS el diálogo sigue
/// funcionando con Esc, el fondo y los botones.
/// </summary>
public sealed class DialogFocus(IJSRuntime js)
{
    public Task OpenAsync(ElementReference dialog) => InvokeAsync("timeTrackerDialog.open", dialog);

    public Task CloseAsync() => InvokeAsync("timeTrackerDialog.close");

    private async Task InvokeAsync(string identifier, params object[] arguments)
    {
        try
        {
            await js.InvokeVoidAsync(identifier, arguments);
        }
        catch (Exception exception) when (exception is JSException or InvalidOperationException or JSDisconnectedException)
        {
            // Solo mejora la experiencia: sin JS el diálogo se cierra igualmente con Esc o los botones.
        }
    }
}
