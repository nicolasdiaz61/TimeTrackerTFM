# Informe de verificación: datos-demo-mes

Fecha: 2026-10-14 · Rama `feat/datos-demo-mes` (desde `feat/tipos-de-marcaje`) · Modo: openspec · Strict TDD: sí · Veredicto: **PASS** (0 CRITICAL).

## Resultados automáticos

| Comprobación | Resultado |
|---|---|
| `dotnet build TimeTracker.sln -c Release -warnaserror` | 0 advertencias, 0 errores |
| `dotnet format TimeTracker.sln --verify-no-changes --no-restore` | Sin cambios pendientes |
| `dotnet test` completo | **579/579** en verde: Domain 67, Application 114, Web 35, Arquitectura 74, Integración 289 (PostgreSQL 18 con Testcontainers) |
| `check-coverage.ps1` Domain y Application (100 %) | Líneas 100,0 %, ramas 100,0 % en ambos |
| `dotnet list package --vulnerable --include-transitive` | Ningún proyecto con paquetes vulnerables |
| `docs/api/openapi.json` | Sin cambios |

## Comprobación real (API + PostgreSQL con Compose, Web y Chrome headless)

- Con `Seed__DemoData=true`: 80 días-empleado creados y 338 intervalos; log solo con contadores.
- Reinicio de la API: 0 días creados, 80 omitidos, 0 intervalos (sin duplicados).
- Convivencia: un fichaje previo de Luis un día laborable; tras reiniciar queda solo ese fichaje ese día y se crean los demás.
- Historial visible y variado por usuario (Marta 110 intervalos, Luis 94, Ana 72, Pablo 60); totales de trabajo, pausas y comidas coherentes; Pablo sin comida; Ana con miércoles de teletrabajo con pausas y sin comida; la responsable ve a Luis y Ana y no a Pablo.
- Límite 50: con el límite anterior el mes habría quedado truncado a ~8 días; con 300 se ve completo.

## Matriz de especificaciones

Cada requisito de `demo-data`, `startup-configuration`, `time-tracking`, `team-access` y `time-entries-persistence` del delta tiene pruebas: `SpainTimeTests`, `DemoScheduleGeneratorTests`, `SeedDemoTimeEntriesUseCaseTests`, `DemoTimeEntryStoreTests`, `DemoDataSeederTests`, `SeedStartupTests`, `GetTimeEntriesUseCaseTests`, `TeamEndpointsTests`, `TimeEntryEndpointsTests`.

## Advertencias

- La Web muestra los instantes en UTC (comportamiento previo), no en hora local.
- Sin candado entre instancias concurrentes (documentado en el ADR 0015).
