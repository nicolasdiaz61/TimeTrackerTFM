namespace TimeTracker.Application.Assistant.Configuration;

/// <summary>Motivo categórico por el que la protección de claves no está disponible; nunca lleva valores ni versiones.</summary>
public enum ProtectionUnavailableReason
{
    None = 0,
    NoActiveVersionConfigured = 1,
    ActiveVersionWithoutKey = 2,
    InvalidOrDuplicateMasterKey = 3,
}
