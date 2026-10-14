namespace TimeTracker.Api.Seeding;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    // Un valor no booleano falla al enlazar y el mensaje del binder nombra la clave.
    public bool DemoUsers { get; set; }

    // Fichajes de un mes para los usuarios demo; exige DemoUsers (lo valida SeedOptionsValidator).
    public bool DemoData { get; set; }

    // Texto para que el validador controle el mensaje y el valor nunca se repita.
    public string? DemoPassword { get; set; }
}
