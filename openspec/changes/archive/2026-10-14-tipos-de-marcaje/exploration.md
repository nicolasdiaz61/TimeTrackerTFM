# Exploración: tipos-de-marcaje

## Punto de partida

- Cada `TimeEntry` es un intervalo con entrada y salida opcional; el índice único parcial `ux_time_entries_employee_id_open` garantiza como máximo un intervalo abierto por empleado.
- `ClockIn`, `ClockOut` y `GetTimeEntries` (50 últimos) son los casos de uso; `GetTeamMemberTimeEntries` reutiliza el listado para el responsable.
- La Web (`/fichajes`, `/equipo`) comparte `TimeEntriesTable`; los botones están siempre activos y la decisión de validez es del servidor.
- Producción (Railway) ya tiene filas en `time_entries` sin concepto de tipo.

## Alternativas estudiadas

1. **Un intervalo por tipo con un único abierto (elegida).** Cada `TimeEntry` lleva un tipo (`Work`, `Break`, `MealBreak`). Pausar cierra el intervalo `Work` y abre uno de pausa; reanudar hace lo inverso. Conserva el índice único y todas las consultas; el tiempo trabajado es la suma de intervalos `Work`.
2. **Pausas como intervalos anidados dentro de un fichaje.** Requiere dos niveles (fichaje y pausas), permitir varios abiertos (entrada y pausa) y restar pausas del total: rompe el invariante y el índice, y complica la migración.
3. **Eventos (log de marcajes) y derivar intervalos.** Es más flexible, pero reescribe el modelo, la migración y todas las consultas para una necesidad que cubre la opción 1.

## Hallazgos que condicionan el diseño

- Pausar y reanudar tocan dos filas (cerrar una, abrir otra): deben ser atómicas, y con el índice único parcial el orden de las sentencias importa (primero cerrar, después insertar).
- El orden de dos transiciones concurrentes puede dejar un intervalo abierto tras una salida si no hay control de concurrencia de la fila: se activa `xmin` como token de concurrencia (antes diferido por DD-9 de `fichaje-extremo-a-extremo`).
- `workedMinutes` ya no describe bien la duración de una pausa: se renombra a `durationMinutes`.
- Con pausas, un día genera varios intervalos; el límite de 50 filas cubre menos días (riesgo aceptado, ver `design.md`).
