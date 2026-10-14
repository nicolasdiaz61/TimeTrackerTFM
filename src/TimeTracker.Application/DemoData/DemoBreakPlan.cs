using TimeTracker.Domain.TimeTracking;

namespace TimeTracker.Application.DemoData;

/// <summary>
/// Una pausa prevista de un día de demostración: tipo, hora local nominal, duración nominal en minutos,
/// variación diaria en minutos (de inicio y de duración) y probabilidad (0 a 100) de que ocurra ese día.
/// </summary>
public sealed record DemoBreakPlan(TimeEntryType Type, TimeOnly At, int Minutes, int Variation, int Probability);
