using TimeTracker.Application.Time;
using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.TimeTracking;

/// <summary>
/// Resume un listado de fichajes por día local de España: salida compacta (como mucho una entrada por día) y totales
/// calculados en código, para que el modelo no tenga que sumar intervalos.
/// </summary>
public static class DailySummaryBuilder
{
    // TimeOnly no puede expresar las 24:00: un tramo que llega a la medianoche local cierra su día a las 23:59.
    private static readonly TimeOnly LastMinuteOfDay = new(23, 59);

    /// <summary>
    /// Cada intervalo cerrado se recorta a cada día local del <paramref name="range"/> que abarca; los minutos de cada
    /// tramo son <c>floor(segundos / 60)</c> y los totales la suma de los tramos. El intervalo abierto no suma: se
    /// expone aparte hasta <paramref name="now"/>. <c>Truncated</c> indica que el listado alcanzó la ventana máxima.
    /// </summary>
    public static DailySummaryResult Build(TimeEntryListResult list, LocalDateRange range, DateTimeOffset now)
    {
        var days = new SortedDictionary<DateOnly, DayTotals>();
        OpenEntrySummary? open = null;

        foreach (var item in list.Items)
        {
            if (item.ClockOutAt is not { } end)
            {
                open = OpenOf(item, now);
                continue;
            }

            var firstDay = Max(range.From, SpainTime.DateOf(item.ClockInAt));
            var lastDay = Min(range.To, SpainTime.DateOf(end));
            for (var day = firstDay; day <= lastDay; day = day.AddDays(1))
            {
                var from = Max(item.ClockInAt, SpainTime.StartOfDay(day));
                var to = Min(end, SpainTime.StartOfDay(day.AddDays(1)));
                if (to > from)
                {
                    if (!days.TryGetValue(day, out var totals))
                    {
                        days[day] = totals = new DayTotals();
                    }

                    totals.Add(item.Type, from, to);
                }
            }
        }

        var summaries = days.Select(day => day.Value.ToSummary(day.Key)).ToList();
        return new DailySummaryResult(
            range.From,
            range.To,
            summaries,
            summaries.Sum(day => day.WorkedMinutes),
            summaries.Sum(day => day.BreakMinutes),
            summaries.Sum(day => day.MealBreakMinutes),
            list.Status,
            open,
            list.Items.Count >= GetTimeEntriesUseCase.MaxWindowEntries);
    }

    private static OpenEntrySummary OpenOf(TimeEntryResult item, DateTimeOffset now)
    {
        var local = SpainTime.ToLocal(item.ClockInAt);
        return new OpenEntrySummary(
            item.Type,
            DateOnly.FromDateTime(local),
            TimeOnly.FromDateTime(local),
            Math.Max(0, (int)(now - item.ClockInAt).TotalMinutes));
    }

    private static T Max<T>(T a, T b) where T : IComparable<T> => a.CompareTo(b) >= 0 ? a : b;

    private static T Min<T>(T a, T b) where T : IComparable<T> => a.CompareTo(b) <= 0 ? a : b;

    private sealed class DayTotals
    {
        private DateTimeOffset _firstStart = DateTimeOffset.MaxValue;
        private DateTimeOffset _lastEnd = DateTimeOffset.MinValue;
        private int _worked;
        private int _break;
        private int _mealBreak;

        public void Add(TimeEntryType type, DateTimeOffset from, DateTimeOffset to)
        {
            _firstStart = Min(_firstStart, from);
            _lastEnd = Max(_lastEnd, to);
            var minutes = (int)(to - from).TotalMinutes;
            switch (type)
            {
                case TimeEntryType.Work:
                    _worked += minutes;
                    break;
                case TimeEntryType.Break:
                    _break += minutes;
                    break;
                default:
                    _mealBreak += minutes;
                    break;
            }
        }

        public DaySummary ToSummary(DateOnly date)
        {
            var lastEnd = SpainTime.ToLocal(_lastEnd);
            var endsAtNextMidnight = DateOnly.FromDateTime(lastEnd) > date;
            return new DaySummary(
                date,
                TimeOnly.FromDateTime(SpainTime.ToLocal(_firstStart)),
                endsAtNextMidnight ? LastMinuteOfDay : TimeOnly.FromDateTime(lastEnd),
                _worked,
                _break,
                _mealBreak);
        }
    }
}
