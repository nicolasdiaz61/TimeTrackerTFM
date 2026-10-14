# Informe de verificación: tipos-de-marcaje

Fecha: 2026-10-14 · Rama `feat/tipos-de-marcaje` (desde `origin/main`) · Modo: openspec · Strict TDD: sí · Veredicto: **PASS WITH WARNINGS** (0 CRITICAL).

## Resultados automáticos

| Comprobación | Resultado |
|---|---|
| `dotnet build TimeTracker.sln -c Release --no-restore -warnaserror` | 0 advertencias, 0 errores |
| `dotnet format TimeTracker.sln --verify-no-changes --no-restore` | Sin cambios pendientes |
| `dotnet test TimeTracker.sln -c Release --no-build --collect:"XPlat Code Coverage"` | **514/514** en verde: Domain 67, Application 66, Web 35, Arquitectura 74, Integración 272 (PostgreSQL 18 real con Testcontainers) |
| `check-coverage.ps1` Domain (100 %) | Líneas 100,0 %, ramas 100,0 % |
| `check-coverage.ps1` Application (umbral CI 80 %; objetivo 100 %) | Líneas 100,0 %, ramas 100,0 % |
| `dotnet list package --vulnerable --include-transitive` | Ningún proyecto con paquetes vulnerables |
| Contrato OpenAPI (`OpenApiContractTests`, `docs/api/openapi.json` regenerado en el build) | Verde: tres operaciones nuevas con `200/401/409`, `type` y `status` como enumeraciones, `workedMinutes` ausente, `durationMinutes` presente |

Strict TDD: cada unidad empezó en rojo (el código de producción no compilaba o fallaba la prueba) y terminó en verde: Domain (`TimeEntryTests`), Application (`StartBreakUseCaseTests`, `EndBreakUseCaseTests`, `TimeEntryListResultTests`), persistencia (`MigrationTests`, `TimeEntryRepositoryTests`), API (`BreakEndpointsTests`, `TeamEndpointsTests`) y Web (`TimeEntryPresentationTests`).

## Matriz de especificaciones

| Requisito (spec) | Evidencia |
|---|---|
| Tipos de intervalo; Inicio de un fichaje en el dominio (time-tracking) | `Start_WithEachType_CreatesOpenEntryOfThatType`, `Start_WithUndefinedType_Throws`, resto de `Start_*` |
| Pausa / Reanudación en el dominio | `Pause_*` (desde trabajo, desde pausa, tipo inválido, reloj anterior, cerrado) y `Resume_*` (pausa, comida, trabajo, reloj anterior) |
| Cálculo de duración | `CalculateDuration_*` (pausa, abierto, truncado, medianoche) |
| Caso de uso StartBreak / EndBreak | `StartBreakUseCaseTests`, `EndBreakUseCaseTests` (atómico, sin abierto, ya en pausa, ajeno, reloj anterior) |
| ClockIn / ClockOut con pausas | `ClockIn_WithOpenBreak_*`, `ClockOut_DuringBreak_*` (unitarias) y `ClockIn_DuringBreak_Returns409`, `ClockOut_DuringBreak_*` (API) |
| GetTimeEntries: totales por tipo y estado | `TimeEntryListResultTests`, `GetTeamMemberTimeEntriesUseCaseTests`, `FullCycle_*` (API) |
| Contrato REST y errores 409 | `BreakEndpointsTests` (200, 409 «No está trabajando» y «No hay ninguna pausa abierta», 401 sin token) |
| Aislamiento entre empleados | `Employees_CannotPauseOrResumeForEachOther`, `StartBreak_OfOneEmployee_DoesNotTouchAnotherEmployeeWork` |
| Transición atómica y concurrente | `ReplaceOpenAsync_*`, `UpdateAsync_WhenAnotherContextChangedTheEntry_*`, `StartBreak_TenParallel_*`, `ClockOutAndStartBreak_InParallel_*` |
| Columna de tipo con valores acotados (persistencia) | `Check_RejectsUnknownType`, `Check_AcceptsEachKnownType`, `Insert_WithoutType_DefaultsToWork`, `AddAsync_StoresTypeAsReadableText_AndReadsItBack`, `Columns_*` |
| Migración de datos existentes | `AddTimeEntryType_OnDatabaseWithOldEntries_KeepsThemAsWork`, `AddTimeEntryType_Down_*`, y la comprobación real descrita abajo |
| Vista de equipo con pausas (team-access) | `MemberEntries_IncludeBreaksWithTypeTotalsAndStatus`, `MemberEntries_BreaksOfAForeignMember_Return404`, `Team_HasNoWriteEndpointsForBreaks_*` |
| UI: botones por estado, tipo, totales (time-entries-ui) | `TimeEntryPresentationTests` (lógica pura) y verificación manual del navegador (abajo) |

## Verificación manual de la Web (navegador real)

Entorno: API y PostgreSQL con `docker compose -p tt-marcaje` y un `.env` ficticio fuera del repositorio (sin usar el de la raíz), Web con `dotnet run --launch-profile http` (puerto 5172) y Chrome headless controlado por un script Node con el protocolo DevTools. Datos de ejemplo (`example.test`). Al terminar se ejecutó `down -v`.

| Escenario | Resultado observado |
|---|---|
| Estado inicial sin fichaje | «Sin fichar», solo «Fichar entrada» |
| Entrada | «Trabajando»; botones «Iniciar pausa», «Pausa de comida», «Fichar salida» |
| Iniciar pausa | «En pausa»; solo «Terminar pausa» y «Fichar salida»; fila «Pausa» en curso sobre una fila «Trabajo» cerrada |
| Terminar pausa, Pausa de comida, terminar | «Trabajando» → «En pausa de comida» (fila «Comida») → «Trabajando» |
| Salida | «Sin fichar»; cinco intervalos (Trabajo, Pausa, Trabajo, Comida, Trabajo) todos cerrados |
| Estado obsoleto (la pausa se termina por la API y la UI pulsa «Terminar pausa») | Mensaje «Solo puede terminar una pausa si hay una en curso.» y la pantalla se recarga con «Trabajando» y los botones correctos |
| Transiciones inválidas por API | Pausa o comida sin estar trabajando: `409` «No está trabajando»; terminar pausa estando trabajando o sin intervalo: `409` «No hay ninguna pausa abierta»; salida sin entrada: `409` «No hay ningún fichaje abierto» |
| Totales con tiempos realistas (ciclo 08:00 a 17:00 con una pausa de 15 min y una comida de 45) | Trabajo 480 min en el día (8:00; 960 con la fila antigua de 480), «Total en pausas: 15 minutos (0:15)», «Total en comidas: 45 minutos (0:45)» |
| Responsable (Marta) en `/equipo` | Con Luis en pausa ve «En pausa», filas «Pausa» y «Comida» y los tres totales; con Ana ve su fila abierta y «Trabajando» |
| Marta intenta operar sobre su equipo por API | `404` en `POST /api/team/members/{id}/start-break` y en un miembro ajeno (Pablo): idéntico al de un miembro inexistente |
| Aislamiento | Ana solo ve su fila; `end-break` sin pausa propia da `409`; Luis y Ana no se afectan |
| Consola | Solo las respuestas `409` provocadas a propósito; sin excepciones ni errores de CORS |
| Móvil 390 px | Botones en línea con salto, tabla sin desbordar, fecha y hora de cada intervalo sin partirse (se corrigió tras la primera captura) |

Capturas revisadas (carpeta del scratchpad de sesión, no versionada): `01-inicial`, `02-trabajando`, `03-en-pausa`, `05-en-comida`, `06-estado-obsoleto`, `11-ciclo-completo`, `12-movil-390`, `13-luis-en-pausa-movil`, `14-equipo-luis`, `15-equipo-ana`, `16-equipo-movil-390`.

Defectos hallados y corregidos por la comprobación visual:

1. El indicador de estado mostraba el texto literal «list.Status»: el parámetro se pasaba como `Status="list.Status"` en lugar de `Status="@list.Status"`. Los 514 tests no lo detectaron (la Web no tiene bUnit).
2. En móvil las fechas se partían por el guion («2026-10-/13»): fecha y hora se envuelven ahora en trozos sin salto.

## Migración sobre datos antiguos

- Prueba real (no solo de test): se creó la base con `dotnet ef database update AddUsers` (esquema anterior), se insertaron dos intervalos (uno cerrado y uno abierto) con SQL sin columna `type`, y se arrancó la API con `Database__ApplyMigrationsOnStartup=true`. Resultado: ambas filas con `type = Work`, la abierta sigue abierta y `__EFMigrationsHistory` contiene `AddTimeEntryType`.
- Reversión real: `dotnet ef database update AddUsers` eliminó la columna conservando las 9 filas; `database update` la volvió a aplicar (todas `Work`).
- En PostgreSQL 18 la columna con valor por defecto constante no reescribe la tabla.

## Hallazgos

WARNING:

- **W1.** La Web no tiene pruebas automáticas de interfaz (sin bUnit ni E2E; decisión previa del proyecto): el defecto 1 solo se vio en el navegador. Recomendación para un cambio posterior: bUnit para `TrackingStatusBadge` y `TimeEntries`.
- **W2.** El límite de 50 intervalos por consulta cubre menos días con pausas (un día con dos pausas son 5 intervalos). Aceptado; revisar al añadir informes por día.
- **W3.** Cambio de contrato `workedMinutes` → `durationMinutes` (rompe a consumidores externos; no hay otros en el repositorio). La API y la Web deben desplegarse juntas.
- **W4.** Revertir la migración tras registrar pausas reales las convertiría en filas sin tipo distinguible (documentado en el ADR 0014 y en la propuesta).
- **W5.** El valor por defecto de la base de datos (`DEFAULT 'Work'`) no figura en el modelo de EF; es deliberado (DD-5) pero es una diferencia entre modelo y esquema.

SUGGESTION:

- El desempate de dos intervalos con el mismo `ClockInAt` (pausa en el mismo segundo que la entrada) no está definido en el orden del listado; solo afecta al orden visual, no al estado ni a los totales.

## Tareas

Las 21 tareas de `tasks.md` están completadas (marcadas `[x]`).
