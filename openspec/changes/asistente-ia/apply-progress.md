# Progreso de apply: asistente-ia

## PR 1 — Consultas y contexto (Application): completado (tareas 1.1-4.2)

Rama `feat/asistente-ia-pr1-resumen-diario`, con Strict TDD (RED por error de compilación o prueba fallida, luego GREEN).

| Tarea | Resultado |
|---|---|
| 1.1 | Copia previa en `..\TimeTracker.rollback\pr-1\` (11 archivos). |
| 1.2 | `ICurrentUser.Role`; `HttpContextCurrentUser` lee el claim `role` (lanza si no hay usuario, falta o no es `Employee`/`Manager`); `FixedCurrentUser(id, role = Employee)`. 8 pruebas nuevas. |
| 1.3 | `SpainTime` movido a `Application/Time` con `ToLocal` y `StartOfDay`; 7 pruebas nuevas (verano, invierno, 2026-03-29, 2026-10-25). |
| 2.1 | `LocalDateRange` (recorte del fin futuro antes de contar 31 días); 11 pruebas. `ToWindow()` reutiliza `TimeWindow.TryCreate`, sin duplicar reglas. |
| 2.2 | `DaySummary`, `OpenEntrySummary`, `DailySummaryResult`, `DailySummaryBuilder`; 13 pruebas. |
| 3.1-3.2 | Puertos, comandos, resultados y `AskAssistantUseCase`; 36 pruebas. |
| 4.1-4.2 | `DependencyRuleTests` y suite en verde; Application sin paquetes nuevos. |

## Puerta de unidad (reales)

- `RESTORE` y `BUILD -warnaserror`: 0 advertencias, 0 errores.
- `TEST`: Domain 67, Application 195 (eran 138 al inicio de la sesión: +57), Web 180, Architecture 74, Api.IntegrationTests 323 (Docker): todas en verde.
- `COV-A`: líneas 100,0 %, ramas 99,3 % (mínimo 80). `COV-D`: 100,0 % / 100,0 %.
- `FORMAT`: sin cambios. `VULN`: sin paquetes vulnerables.

## Desviaciones

1. `DailySummaryBuilder.Build(list, range, now)` no recibe `truncated`: calcula `Truncated = Items.Count >= MaxWindowEntries` por dentro (la spec y la tarea 2.2 lo exigen y así es verificable en Application). PR 2a debe llamar con tres argumentos.
2. `LastEnd` de un día cuyo último tramo llega a la medianoche local se expone como 23:59 (`TimeOnly` no admite las 24:00).
3. Mensajes de rechazo (texto fijo, en `AskAssistantUseCase`): mensaje 1-500, historial máx. 10, texto de turno 1-2000, suma 8000, y el de roles de la spec. Si el endpoint los necesita en otro idioma de contrato, ya están como constantes privadas; los límites son constantes públicas.
4. El caso de uso no usa `TimeProvider` (la tarea citaba `FakeTimeProvider`): no depende del tiempo; el reloj entra con la cuota (PR 2b).
5. **Tamaño**: el diff real es de 1.097 inserciones + 11 borrados (≈ 1.108 líneas, 23 archivos) frente a las ≈ 410 previstas; supera con mucho el umbral de 450. Ver informe del apply.

## Para PR 2a / 2b

- `SpainTime.DateOf(DateTimeOffset)` existe; la cuota de 2b necesitará un `DateOf` desde `TimeProvider` (o `GetUtcNow()`), no añadido aquí.
- `AskAssistantResult` es una jerarquía cerrada (`Answered`, `Rejected`, `QuotaExceeded`, `Unavailable(bool Disabled)`).
- `docs/api/openapi.json` se regenera al compilar con saltos de línea distintos (solo ruido); restaurar con `git checkout` si aparece modificado.
