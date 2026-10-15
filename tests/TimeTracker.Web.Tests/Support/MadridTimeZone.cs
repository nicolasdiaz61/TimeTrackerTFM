using TimeTracker.Web.Time;

namespace TimeTracker.Web.Tests.Support;

/// <summary>Zona de prueba Europe/Madrid (cambio de hora de la UE) para los cálculos puros de la Web.</summary>
internal sealed class MadridTimeZone : ILocalTimeZone
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");

    public TimeSpan OffsetAt(DateTimeOffset instant) => Zone.GetUtcOffset(instant);

    public DateTimeOffset StartOfDay(DateOnly date)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Zone), TimeSpan.Zero);
    }
}
