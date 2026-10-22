namespace TimeTracker.Infrastructure.Assistant;

/// <summary>Registro de instantáneas del asistente por compañía (DD-14): una compañía nunca recibe la instantánea de otra.</summary>
internal interface IAssistantRuntimeRegistry
{
    /// <summary>
    /// Instantánea vigente de la compañía; la primera pregunta la construye (una sola carga concurrente). Un fallo transitorio de la
    /// carga se propaga y no se cachea: la siguiente pregunta reintenta. Quien la use durante una petición debe leerla una sola vez.
    /// </summary>
    ValueTask<AssistantSnapshot> GetAsync(Guid companyId, CancellationToken cancellationToken);

    /// <summary>Aplica en esta instancia el cambio de la compañía si ya está cargada; si no, no hace nada (se cargará fresca al preguntar).</summary>
    Task ReloadAsync(Guid companyId, CancellationToken cancellationToken);

    /// <summary>Compañías cargadas con su versión, indicador y última versión rechazada.</summary>
    IReadOnlyList<LoadedRuntime> Loaded();

    /// <summary>Expulsa las compañías sin preguntas durante <c>Ai:RuntimeIdleMinutes</c> y devuelve cuántas.</summary>
    int ExpireIdle();
}
