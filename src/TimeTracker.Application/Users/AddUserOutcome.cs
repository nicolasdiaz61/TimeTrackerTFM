namespace TimeTracker.Application.Users;

/// <summary>Resultado de añadir una persona respetando el tope de la compañía (DD-21).</summary>
public enum AddUserOutcome
{
    Created,

    /// <summary>La compañía ya tiene tantos usuarios como permite el tope.</summary>
    CapReached,

    /// <summary>El correo ya existe en alguna compañía.</summary>
    DuplicateEmail,

    /// <summary>La base de datos rechazó al responsable (red de seguridad de la clave foránea compuesta).</summary>
    InvalidManager,

    /// <summary>Los reintentos acotados ante conflictos de serialización se agotaron sin resolverse; no se creó nada.</summary>
    Busy,
}
