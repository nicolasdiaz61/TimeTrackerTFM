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

---

## PR 2a — Herramientas y proyecto de pruebas (Infrastructure): completado (tareas 1.1-3.1)

Rama `feat/asistente-ia-pr2a-herramientas` (desde `cf5f8cb`), con Strict TDD (RED por error de compilación o por pruebas fallidas, luego GREEN y REFACTOR). Ninguna prueba llama a un modelo: solo `FakeChatClient`. Docker en marcha para `Api.IntegrationTests`.

| Tarea | Resultado |
|---|---|
| 1.1 | Copia previa en `..\TimeTracker.rollback\pr-2a\` (`Directory.Packages.props`, `TimeTracker.Infrastructure.csproj`, `TimeTracker.sln`, `RepositoryConventionsTests.cs`). |
| 1.2 | V-1 (parte 1) correcto: `Microsoft.Extensions.AI` 10.10.0 + Abstractions 10.10.0, 0 advertencias con `-warnaserror`, sin vulnerables. Detalle en `design.md` sección 11. |
| 1.3 | V-3 resuelto contra el paquete restaurado y una prueba de caracterización desechable: nombres correctos; al tope de iteraciones hay 1 + `MaximumIterationsPerRequest` llamadas, el SDK retira las herramientas en la última y la respuesta puede quedar con `Text` vacío y una llamada pendiente; el error de herramienta se relanza al superar `MaximumConsecutiveErrorsPerRequest` (la excepción original). Detalle en `design.md` sección 11. |
| 1.4 | `Microsoft.Extensions.AI` en `Directory.Packages.props` (comentado) y referencia sin `Version` en Infrastructure; `InternalsVisibleTo` hacia `TimeTracker.Infrastructure.Tests`. |
| 1.5 | `tests/TimeTracker.Infrastructure.Tests` (en la solución, dobles enlazados de `Application.Tests`, `FakeChatClient`, `CapturingLogger`, `ThrowingTimeEntryRepository`); `RepositoryConventionsTests` con el proyecto en `ProjectPaths` y `AiPackages_AreDeclaredOnlyInInfrastructureWithCentralVersion` (mutación temporal: un `PackageReference` de IA en Web la hace fallar). |
| 2.1 | `AssistantJson`, `ToolResults`, `DisplayNameSanitizer`, `ToolOutcome` y `AssistantLog` (`LoggerMessage`, solo metadatos). |
| 2.2-2.4 | `AssistantToolset` (`get_my_time_entries`, `get_team_members`, `get_team_member_time_entries`; las de equipo solo para `Manager`) y refactor (`SummarizeAsync`/`RunAsync` compartidos). |
| 3.1 | Puerta de unidad en verde (abajo). |

### Puerta de unidad (reales)

- `RESTORE` y `BUILD -warnaserror`: 0 advertencias, 0 errores.
- `TEST`: Domain 67, Application 195, Web 180, **Infrastructure.Tests 54 (nuevo)**, Architecture 77 (eran 74: +3), Api.IntegrationTests 326 (Docker): todas en verde.
- `COV-A`: líneas 100,0 %, ramas 99,3 % (mínimo 80). `COV-D`: 100,0 % / 100,0 %. Infrastructure sin umbral.
- `FORMAT`: sin cambios. `VULN`: sin paquetes vulnerables.

### Desviaciones y decisiones

1. **Tamaño**: el diff real es de 1.156 líneas (285 `src`, 854 pruebas, 17 props y `.sln`; sin docs de openspec) frente a las ≈ 265 previstas. Supera el umbral de 450: ver propuesta de corte en el informe del apply. Las pruebas de seguridad por herramienta (tabla de «no visibles», registro de auditoría sin datos, registro por rol) son la parte grande y están exigidas por las specs.
2. `AssistantToolset` es `public` y recibe `GetTimeEntriesUseCase`, `GetTeamMembersUseCase`, `GetTeamMemberTimeEntriesUseCase`, `ICurrentUser`, `TimeProvider` e `ILogger<AssistantToolset>`; `CreateTools()` devuelve las `AIFunction` del rol (PR 2b lo llamará una vez por petición). El resto de tipos nuevos son `internal` con `InternalsVisibleTo`.
3. Cualquier excepción inesperada dentro de una herramienta (salvo la cancelación) se convierte en `{ "error": "No se ha podido completar la consulta en este momento." }` (sin texto ni tipo) y se registra con resultado `error`. Por eso el límite de errores consecutivos del SDK solo actuará en fallos de enlace de argumentos.
4. `AssistantJson.Options` copia `AIJsonUtilities.DefaultOptions` (que indenta) y fija `WriteIndented = false` para gastar menos tokens.
5. Mensaje de fechas inválidas: «Las fechas deben tener el formato yyyy-MM-dd, por ejemplo 2026-10-18.»; los demás errores de fechas son los de `LocalDateRange`.
6. Orden de tareas: la referencia al paquete se añadió antes de la verificación 1.2 (no se puede restaurar sin declararlo); el resultado es el mismo.

### Para PR 2b

- `AssistantChatPipeline`: con `MaximumIterationsPerRequest` = N hay N + 1 llamadas como máximo y la última va sin herramientas; respuesta vacía o con llamada pendiente → texto fijo. La excepción relanzada por el SDK conserva el mensaje original: registrar solo `GetType().Name`.
- `AIFunction.JsonSerializerOptions` ya lleva `AssistantJson.Options`; el motor solo debe pasar `Tools = toolset.CreateTools()` en `ChatOptions`.
- `FakeChatClient` guioniza turnos con `Func<IReadOnlyList<ChatMessage>, ChatResponse>` y guarda `Calls` (mensajes y `ChatOptions`); para «modelo que siempre pide herramienta» basta pasar N veces el mismo turno. PR 3 lo enlazará desde `Api.IntegrationTests`.
- `TimeProvider` con `FakeTimeProvider` ya se usa en el arnés (`ToolHarness`), reutilizable para el tiempo máximo del motor.
- `docs/api/openapi.json` sigue regenerándose con ruido de saltos de línea tras compilar: restaurar con `git checkout`.
