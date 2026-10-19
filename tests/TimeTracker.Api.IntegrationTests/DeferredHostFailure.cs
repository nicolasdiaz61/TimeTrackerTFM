using System.Reflection;

using Microsoft.Extensions.Hosting;

namespace TimeTracker.Api.IntegrationTests;

/// <summary>
/// Recupera el error de arranque real cuando <c>WebApplicationFactory</c> lo pierde por una carrera propia: con minimal
/// hosting, <c>DeferredHost.StartAsync</c> toca <c>Services</c> antes de esperar al resultado del hilo de la aplicación;
/// si ese hilo ya falló y destruyó el host, el hilo de la prueba ve un <see cref="ObjectDisposedException"/> aunque el
/// error real (la validación de opciones, la migración...) ya esté guardado en el <c>TaskCompletionSource</c> privado
/// <c>_hostStartedTcs</c>, que <c>EntryPointCompleted</c> completa con él. Se lee ese objeto por reflexión (tipo interno
/// del framework); si cambia su forma, <see cref="TryGet"/> devuelve <c>false</c> y queda el reintento de
/// <see cref="WebFactoryExtensions.StartExpectingFailure"/>.
/// </summary>
internal static class DeferredHostFailure
{
    private const string TcsFieldName = "_hostStartedTcs";

    // El hilo de la aplicación llama a EntryPointCompleted justo después de destruir el host; la espera es solo un margen.
    private static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(30);

    public static bool TryGet(IHost host, out Exception failure)
    {
        failure = null!;
        var field = host.GetType().GetField(TcsFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(host) is not TaskCompletionSource completion)
        {
            return false;
        }

        try
        {
            if (!completion.Task.Wait(CompletionTimeout))
            {
                return false;
            }
        }
        catch (AggregateException aggregate)
        {
            // Task.Wait envuelve el error real; se devuelve tal cual lo guardó EntryPointCompleted.
            failure = aggregate.InnerExceptions.Count == 1 ? aggregate.InnerExceptions[0] : aggregate;
            return true;
        }

        return false;
    }
}
