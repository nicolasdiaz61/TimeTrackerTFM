using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.DemoData;

/// <summary>Los cuatro perfiles de las personas de demostración (todas inventadas). Horas locales de España.</summary>
public static class DemoScheduleProfiles
{
    /// <summary>Jornada regular con reuniones: pausas de duración y presencia variables.</summary>
    public static DemoScheduleProfile Manager { get; } = new(
        Seed: 0x4D41525441UL,
        AbsenceOffset: 2,
        Variation: 12,
        Standard: new(
            At(9, 0),
            At(17, 30),
            [
                Pause(TimeEntryType.Break, At(11, 0), minutes: 20, variation: 8, probability: 70),
                Pause(TimeEntryType.MealBreak, At(14, 0), minutes: 45, variation: 10, probability: 100),
                Pause(TimeEntryType.Break, At(16, 0), minutes: 10, variation: 4, probability: 50),
            ]));

    /// <summary>Madrugador: 08:00 a 17:00, comida de una hora y una pausa de unos 15 minutos casi todos los días.</summary>
    public static DemoScheduleProfile EarlyBird { get; } = new(
        Seed: 0x4C554953UL,
        AbsenceOffset: 5,
        Variation: 6,
        Standard: new(
            At(8, 0),
            At(17, 0),
            [
                Pause(TimeEntryType.Break, At(10, 30), minutes: 15, variation: 4, probability: 90),
                Pause(TimeEntryType.MealBreak, At(13, 0), minutes: 60, variation: 6, probability: 100),
            ]));

    /// <summary>
    /// Jornada de tarde con comida larga y alguna pausa de última hora. Los miércoles teletrabaja: no registra
    /// comida como tal, sino una pausa normal de media hora.
    /// </summary>
    public static DemoScheduleProfile LateShift { get; } = new(
        Seed: 0x414E41UL,
        AbsenceOffset: 8,
        Variation: 10,
        Standard: new(
            At(9, 30),
            At(18, 30),
            [
                Pause(TimeEntryType.MealBreak, At(14, 0), minutes: 75, variation: 10, probability: 100),
                Pause(TimeEntryType.Break, At(17, 0), minutes: 10, variation: 4, probability: 40),
            ]),
        Overrides: new Dictionary<DayOfWeek, DemoDayPlan>
        {
            [DayOfWeek.Wednesday] = new(
                At(9, 0),
                At(17, 0),
                [
                    Pause(TimeEntryType.Break, At(11, 0), minutes: 15, variation: 5, probability: 80),
                    Pause(TimeEntryType.Break, At(14, 0), minutes: 30, variation: 8, probability: 100),
                ]),
        });

    /// <summary>Media jornada de 08:00 a 14:00 con una sola pausa corta y sin comida.</summary>
    public static DemoScheduleProfile HalfDay { get; } = new(
        Seed: 0x5041424C4FUL,
        AbsenceOffset: 11,
        Variation: 5,
        Standard: new(
            At(8, 0),
            At(14, 0),
            [Pause(TimeEntryType.Break, At(10, 30), minutes: 15, variation: 5, probability: 100)]));

    private static TimeOnly At(int hour, int minute) => new(hour, minute);

    private static DemoBreakPlan Pause(TimeEntryType type, TimeOnly at, int minutes, int variation, int probability)
        => new(type, at, minutes, variation, probability);
}
