# ADR 0027: Horas extra en el resumen de equipo

- Estado: Aceptado
- Fecha: 2026-10-23

## Contexto

Un responsable pregunta al asistente «el listado de personas que más horas extra han hecho». La herramienta `get_team_summary` ([ADR 0025](0025-resumen-de-equipo-del-asistente.md)) no devolvía horas extra y el modelo las inventaba. La jornada (`WorkSchedule`, asignada a cada persona) guarda la hora de entrada, el almuerzo, la pausa esperada y los días de la semana, pero **no una duración diaria prevista**, así que «horas extra» no puede calcularse solo con lo almacenado. Añadir ese dato exige migración y cambios en la API y en los formularios de la Web, lo que queda fuera de este cambio.

## Decisión

- **Suposición explícita en lugar de un cambio de esquema.** `get_team_summary` acepta un parámetro opcional `dailyHours` (número, por defecto 8, de 1 a 12; fuera de rango la herramienta devuelve un error como el de las fechas inválidas, sin consultar nada). El resultado devuelve `assumedDailyHours` y un `overtimeNote` en español que enuncia la regla, para que el modelo pueda decir qué suposición se ha usado.
- **Regla, calculada en el caso de uso.** Por miembro y por día local de España (se reutiliza `DailySummaryBuilder`, sin duplicar su cálculo), las horas previstas son `dailyHours` × 60 minutos si el día es laborable en la jornada asignada a la persona, y 0 si no lo es; sin jornada asignada se toman de lunes a viernes. Las horas extra del día son `max(0, trabajado - previsto)`, de modo que todo el trabajo en un día no laborable cuenta como extra. Por miembro se devuelven `overtimeMinutes`, `overtimeDays`, `overtimeText`, `overtimeVsAverage` y `aboveAverageOvertime`, y en las medias del equipo `overtimeMinutes` y `overtimeText`. Solo cuentan los intervalos de tipo trabajo cerrados; el abierto sigue fuera. Se mantienen el orden por minutos de pausa, el periodo máximo de 31 días y el tope de 50 miembros.
- **Jornadas sin N+1.** Las jornadas de la compañía del contexto se leen una sola vez (`IWorkScheduleRepository.ListAsync`) y solo si algún reporte directo tiene una asignada. El equipo sigue saliendo de `ICurrentUser`; la herramienta no recibe compañía ni identificadores.
- **Prompt (solo Manager).** Para horas extra debe llamar a `get_team_summary`, responder con `overtimeMinutes` y `overtimeText`, nunca calcularlas, indicar siempre la suposición (`assumedDailyHours` y que depende de los días de la jornada de cada persona) y pasar `dailyHours` si el usuario indica otras horas diarias. El prompt del empleado y sus herramientas no cambian.

## Consecuencias

- Las preguntas sobre horas extra se responden con cifras calculadas en código, siempre acompañadas de la suposición, y la regla queda fijada por pruebas (incluidas las de dos compañías).
- El cálculo no usa la hora de entrada ni el almuerzo de la jornada: solo sus días. Una persona con una jornada diaria distinta de la supuesta da resultados aproximados salvo que el responsable indique `dailyHours`; el valor es único para todo el equipo en cada consulta.
- Un día con trabajo en un día no laborable cuenta entero como extra, incluso si la persona recuperó horas otro día.
- Si en el futuro la jornada guarda una duración prevista, bastará sustituir el origen de las horas previstas por día en el caso de uso; el contrato de la herramienta no cambia salvo que `dailyHours` pase a ser opcional y secundario.

## Alternativas

- **Añadir la duración diaria prevista a `WorkSchedule`:** descartada ahora; exige migración, cambios en la API y en los formularios, y se reconsidera cuando se pida la gestión de jornadas completa.
- **Derivar la duración de la hora de entrada, el almuerzo y la pausa:** descartada; no hay hora de salida y el resultado parecería exacto sin serlo.
- **Que el modelo calcule las horas extra a partir de los resúmenes por día:** descartada; es la causa de los números inventados ([ADR 0025](0025-resumen-de-equipo-del-asistente.md)).
- **Una constante fija de 8 horas sin parámetro:** descartada; impide atender a quien indica otra jornada.
