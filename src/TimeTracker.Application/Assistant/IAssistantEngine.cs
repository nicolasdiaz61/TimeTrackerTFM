namespace TimeTracker.Application.Assistant;

/// <summary>Disponibilidad del asistente para la compañía del usuario: habilitado, deshabilitado por configuración o no disponible por un fallo transitorio al cargarla.</summary>
public enum AssistantAvailability
{
    Enabled,
    Disabled,
    Unavailable,
}

/// <summary>Puerto del motor del asistente (modelo y herramientas); lo implementa Infrastructure.</summary>
public interface IAssistantEngine
{
    /// <summary>Estado del asistente de la compañía del usuario. Un fallo transitorio de carga no se oculta ni se cachea: es <c>Unavailable</c>.</summary>
    Task<AssistantAvailability> GetAvailabilityAsync(CancellationToken cancellationToken);

    /// <summary>Respuesta del asistente, o <c>null</c> si el proveedor no está disponible.</summary>
    Task<string?> ReplyAsync(AssistantConversation conversation, CancellationToken cancellationToken);
}
