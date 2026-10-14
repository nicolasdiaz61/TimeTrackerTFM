# Tareas: tipos-de-marcaje

Cada tarea de implementación empieza por su prueba (rojo), sigue con el código mínimo (verde) y termina con la limpieza (refactor). Marcar `[x]` al completar.

## 1. Dominio

- [x] 1.1 Pruebas y código de `TimeEntryType` y `TimeEntry.Start(employeeId, type, at)` (tipos válidos, tipo no definido); adaptar las pruebas existentes de `Start`.
- [x] 1.2 `CalculateDuration()` sustituye a `CalculateWorked()` (pausa, abierto, truncado, medianoche).
- [x] 1.3 `NotWorkingException`, `NoOpenBreakException` y `TimeEntry.Pause` (desde `Work`, desde pausa, tipo inválido, reloj anterior, cerrado).
- [x] 1.4 `TimeEntry.Resume` (desde pausa y comida, desde `Work`, errores de `Close`).

## 2. Aplicación

- [x] 2.1 `TimeEntryResult`, `TimeEntryListResult` (totales por tipo, `TimeTrackingStatus`); adaptar `GetTimeEntries` y `GetTeamMemberTimeEntries`.
- [x] 2.2 `ITimeEntryRepository.ReplaceOpenAsync`, `ConcurrentTimeEntryChangeException` y el repositorio en memoria de pruebas.
- [x] 2.3 `StartBreakUseCase` (pausa y comida, sin abierto, ya en pausa, ajeno).
- [x] 2.4 `EndBreakUseCase` (pausa, comida, trabajando, sin abierto).
- [x] 2.5 `ClockIn` con pausa abierta y `ClockOut` con pausa abierta (pruebas adicionales).

## 3. Persistencia

- [x] 3.1 Pruebas de integración: columna `type`, CHECK, valor por defecto, ida y vuelta, migración sobre datos antiguos y reversión.
- [x] 3.2 Mapeo (`HasConversion<string>`, `xmin`), migración `AddTimeEntryType` y snapshot.
- [x] 3.3 `ReplaceOpenAsync` con transacción (atomicidad, reversión, concurrencia) y traducción de errores.

## 4. API y contrato

- [x] 4.1 Pruebas de endpoints (`start-break`, `start-meal-break`, `end-break`, ciclo completo, `409`, aislamiento, 401, paralelismo, equipo con pausas).
- [x] 4.2 Endpoints, `TimeEntryResponse` (con `type`, `durationMinutes`), `TimeEntryListResponse` y registro de casos de uso.
- [x] 4.3 Regenerar `docs/api/openapi.json` y ampliar `OpenApiContractTests` (`AuthorizationCoverageTests` ya cubre los nuevos endpoints: solo tres rutas pueden ser anónimas).

## 5. Web

- [x] 5.1 Pruebas y código de `TimeEntryPresentation` (etiquetas de tipo y estado, acciones por estado, valores desconocidos).
- [x] 5.2 DTO, cliente tipado, `TimeEntriesTable` y `TimeEntries.razor` (botones según estado, tipo, totales y estado); `/equipo` reutiliza la tabla.
- [x] 5.3 Ajustar las pruebas de convenciones si procede (no fue necesario tocar `RepositoryConventionsTests`; sí `DeliveryArtifactsTests`, que cuenta catorce ADR).

## 6. Documentación y cierre

- [x] 6.1 README y ADR 0014.
- [x] 6.2 Verificación completa: build con `-warnaserror`, `dotnet format`, pruebas, cobertura, auditoría de paquetes, E2E visual en navegador y migración real; `verify-report.md`.
- [x] 6.3 Fusionar las especificaciones delta, actualizar `openspec/config.yaml` y archivar el cambio (`archive-report.md`).
