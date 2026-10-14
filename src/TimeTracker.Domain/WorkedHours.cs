namespace TimeTracker.Domain;

/// <summary>
/// Cantidad de tiempo trabajado, en minutos enteros no negativos.
/// </summary>
/// <remarks>
/// Es un <c>record struct</c>: <c>default</c> equivale a 0 minutos (valor válido), así que el invariante
/// se cumple siempre. El constructor es privado para que solo <see cref="FromMinutes"/> pueda crear valores.
/// </remarks>
public readonly record struct WorkedHours
{
    private WorkedHours(int minutes) => Minutes = minutes;

    public int Minutes { get; }

    public static WorkedHours FromMinutes(int minutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minutes);
        return new WorkedHours(minutes);
    }

    // `checked` evita que una suma que desborda se envuelva en silencio y produzca un valor negativo.
    public static WorkedHours operator +(WorkedHours left, WorkedHours right)
        => new(checked(left.Minutes + right.Minutes));
}
