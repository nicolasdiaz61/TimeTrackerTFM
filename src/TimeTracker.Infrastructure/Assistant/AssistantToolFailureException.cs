namespace TimeTracker.Infrastructure.Assistant;

/// <summary>
/// Marca un fallo ocurrido al ejecutar una herramienta (incluido el enlace de sus argumentos), para distinguirlo de un
/// fallo del proveedor. El mensaje es fijo: el de la excepción original puede traer datos y solo viaja en
/// <see cref="Exception.InnerException"/>, de la que el motor registra únicamente el nombre del tipo.
/// </summary>
internal sealed class AssistantToolFailureException(Exception inner)
    : Exception("Ha fallado la ejecución de una herramienta del asistente.", inner);
