# Tareas: datos-demo-mes

Cada tarea de implementación empieza por su prueba (rojo), sigue con el código mínimo (verde) y termina con la limpieza (refactor). Marcar `[x]` al completar.

## 1. Application: tiempo y generador

- [ ] 1.1 `SpainTime` (invierno, verano, cambios de marzo y octubre, fecha local desde UTC).
- [ ] 1.2 `DemoRandom` (SplitMix64) y modelo de perfil (`DemoBreakPlan`, `DemoDayPlan`, `DemoScheduleProfile`).
- [ ] 1.3 `DemoScheduleGenerator` (determinismo, sin solapes, orden de tipos, fines de semana, ausencias, pausa que no cabe, UTC verano/invierno).
- [ ] 1.4 `DemoScheduleProfiles` (cuatro perfiles distintos entre sí; miércoles de Ana; Pablo sin comida).

## 2. Application: caso de uso

- [ ] 2.1 `IDemoTimeEntryStore`, `DemoEmployee`, `DemoSeedResult` y `SeedDemoTimeEntriesUseCase` (primera siembra, reejecución, ventana móvil, día con fichajes previos, hoy libre) con almacén en memoria.
- [ ] 2.2 `GetTimeEntriesUseCase.MaxEntries` = 300 (adaptar las pruebas de 50).

## 3. Infrastructure

- [ ] 3.1 Pruebas de integración de `DemoTimeEntryStore` (rango y atomicidad) y su implementación y registro.

## 4. Api y configuración

- [ ] 4.1 `SeedOptions.DemoData`, validador (requiere `DemoUsers`) y pruebas de arranque (no booleano, sin usuarios, válido).
- [ ] 4.2 `DemoDataSeeder`, perfiles en `DemoUsers` y registro tras `DemoUsersSeeder`; pruebas de integración (siembra, idempotencia, convivencia, equipo, desactivada, registro).
- [ ] 4.3 Adaptar las pruebas de 50 a 300 en integración; confirmar que `openapi.json` no cambia.

## 5. Documentación y cierre

- [ ] 5.1 README (`Seed__DemoData`) y ADR 0015; ajustar la prueba que cuenta los ADR.
- [ ] 5.2 Verificación (build, formato, pruebas, cobertura, paquetes), comprobación real en navegador, `verify-report.md`.
- [ ] 5.3 Fusión de especificaciones y archivo (`archive-report.md`).
