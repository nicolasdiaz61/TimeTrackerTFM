# Informe de archivo: tipos-de-marcaje

Fecha: 2026-10-14 · Fase: sdd-archive · Modo: openspec · Veredicto de verificación: **PASS WITH WARNINGS** (0 CRITICAL).

Destino: `openspec/changes/archive/2026-10-14-tipos-de-marcaje/`.

## Resumen

El fichaje admite pausas y pausas de comida. Cada intervalo de `TimeEntry` tiene un tipo (`Work`, `Break`, `MealBreak`); se conserva «un único intervalo abierto por empleado». Pausar cierra el trabajo y abre la pausa, terminar la pausa abre un trabajo nuevo y fichar la salida cierra el intervalo abierto de cualquier tipo. La API gana `start-break`, `start-meal-break` y `end-break`; `workedMinutes` pasa a `durationMinutes`; el listado devuelve totales de trabajo, pausas y comidas y el estado actual, también en la vista del responsable. La migración `AddTimeEntryType` deja los datos existentes como `Work` y es reversible. Decisiones y alternativas en `design.md` y en el ADR 0014.

Evidencia final (`verify-report.md`): 514/514 pruebas en verde, cobertura Domain y Application al 100 % (líneas y ramas), `build -warnaserror` y `format --verify-no-changes` limpios, sin paquetes vulnerables, comprobación real en navegador y migración real sobre datos antiguos con reversión.

Puerta de tareas: las 21 tareas de `tasks.md` están marcadas `[x]`.

## Trazabilidad

| Artefacto | Ruta (antes del movimiento) |
|---|---|
| Exploración | `openspec/changes/tipos-de-marcaje/exploration.md` |
| Propuesta | `openspec/changes/tipos-de-marcaje/proposal.md` |
| Especificaciones delta (4) | `openspec/changes/tipos-de-marcaje/specs/{time-tracking,time-entries-persistence,time-entries-ui,team-access}/spec.md` |
| Diseño | `openspec/changes/tipos-de-marcaje/design.md` |
| Tareas | `openspec/changes/tipos-de-marcaje/tasks.md` |
| Informe de verificación | `openspec/changes/tipos-de-marcaje/verify-report.md` |
| ADR | `docs/adr/0014-tipos-de-marcaje-pausas-y-comida.md` |

## Especificaciones fusionadas

| Dominio | Acción | Requisitos (antes → después) | Escenarios (antes → después) | Detalle |
|---|---|---|---|---|
| `time-tracking` | Actualizada | 11 → 18 | 52 → 83 | 7 ADDED (Tipos de intervalo, Pausa y Reanudación en el dominio, StartBreak, EndBreak, Cálculo de duración, Transición atómica y concurrente); 8 MODIFIED (Inicio de un fichaje, ClockIn, ClockOut, GetTimeEntries, Contrato REST, Errores 409, Aislamiento, Flujo completo); Purpose y nombres fijados actualizados |
| `time-entries-persistence` | Actualizada | 10 → 13 | 25 → 34 | 3 ADDED (Columna de tipo, Migración de datos existentes, Transición atómica en el repositorio); 1 MODIFIED (Modelo de datos); Purpose actualizado (el control de concurrencia ya no está fuera de alcance) |
| `time-entries-ui` | Actualizada | 5 → 6 | 17 → 23 | 1 ADDED (Totales de trabajo y de pausas); 3 MODIFIED (Página /fichajes, Botones de fichaje, Presentación de tiempos); Purpose actualizado |
| `team-access` | Actualizada | 12 → 12 | 63 → 65 | 1 MODIFIED (Fichajes de un miembro del equipo); Purpose actualizado |
| `worked-hours` y resto | Sin cambios | 0 delta | 0 delta | No participan |

Los MODIFIED reemplazan por completo al requisito del mismo nombre; el resto se conserva sin cambios. No hubo REMOVED ni RENAMED (no hay deltas destructivos). La fusión se hizo con un script que falla si un MODIFIED no existe o un ADDED ya existe.

## Fuera de los deltas

- `openspec/config.yaml`: se actualiza el estado del proyecto (ADR 0014; la concurrencia de la salida deja de estar diferida).
- `DeliveryArtifactsTests` pasa a exigir catorce ADR.

## Hallazgos abiertos del verify (ninguno bloqueante)

WARNING: W1 sin pruebas automáticas de interfaz en la Web; W2 el límite de 50 intervalos cubre menos días; W3 cambio de contrato `workedMinutes` → `durationMinutes`; W4 la reversión de la migración con pausas reales las confundiría con trabajo; W5 `DEFAULT 'Work'` solo en el esquema.

SUGGESTION: definir un desempate de orden para intervalos con el mismo `ClockInAt`.
