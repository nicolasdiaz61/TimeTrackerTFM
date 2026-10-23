using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace TimeTracker.Web.Shared;

/// <summary>
/// Puente mínimo con js/chat.js para la lista de mensajes de un chat: desplazarla al último mensaje y hacer que Intro
/// envíe el formulario. Solo presentación; si el navegador no puede ejecutar JS (pruebas, desconexión) no hace nada.
/// </summary>
public sealed class ChatViewport(IJSRuntime js)
{
    public Task ScrollToEndAsync(ElementReference list) => InvokeAsync("timeTrackerChat.scrollToEnd", list);

    /// <summary>Intro envía el formulario y Mayús+Intro inserta un salto de línea.</summary>
    public Task SendOnEnterAsync(ElementReference textarea) => InvokeAsync("timeTrackerChat.sendOnEnter", textarea);

    private async Task InvokeAsync(string identifier, ElementReference element)
    {
        try
        {
            await js.InvokeVoidAsync(identifier, element);
        }
        catch (Exception exception) when (exception is JSException or InvalidOperationException or JSDisconnectedException)
        {
            // Solo mejora la experiencia: sin JS el chat sigue funcionando con el botón Enviar.
        }
    }
}
