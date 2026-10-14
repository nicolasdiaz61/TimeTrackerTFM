# Propuesta: tipos-de-marcaje

## Intención

Hoy el fichaje solo permite entrada y salida. Las personas también necesitan registrar **pausas** y **pausas de comida** sin que cuenten como tiempo trabajado, y los responsables deben verlas en la vista de su equipo.

## Alcance

Dentro:

- Dominio: `TimeEntryType` (`Work`, `Break`, `MealBreak`) y transiciones `Pause` y `Resume` sobre `TimeEntry`; nuevas excepciones de regla de negocio.
- Aplicación: casos de uso `StartBreak` (pausa o comida) y `EndBreak`; `ClockOut` cierra cualquier tipo abierto; el listado devuelve totales de trabajo, pausa y comida y el estado actual.
- Persistencia: columna `type` (texto), migración reversible `AddTimeEntryType` que deja los datos existentes como `Work`; transición atómica y token de concurrencia `xmin`.
- API: `POST /api/time-entries/start-break`, `start-meal-break` y `end-break`; `TimeEntryResponse` gana `type` y sustituye `workedMinutes` por `durationMinutes`; el listado gana `totalBreakMinutes`, `totalMealBreakMinutes` y `status`. El contrato OpenAPI se regenera.
- Web: botones según el estado, columna «Tipo» («Trabajo», «Pausa», «Comida»), totales de trabajado y de pausas y estado actual, también en `/equipo`.
- Documentación: README, ADR 0014 y fusión de las especificaciones.

Fuera: edición o borrado de marcajes, pausas con duración máxima o prevista, varias pausas simultáneas, zona horaria local, informes por día.

## Enfoque

Se mantiene el invariante «un único intervalo abierto por empleado». Cada intervalo tiene un tipo; pausar cierra el trabajo y abre la pausa, reanudar cierra la pausa y abre un trabajo nuevo, y salir cierra el intervalo abierto sea del tipo que sea. El tiempo trabajado es la suma de los intervalos `Work`. Detalle y alternativas en `design.md` y en el ADR 0014.

## Riesgos y reversión

- **Migración en producción con datos:** la columna se añade `NOT NULL DEFAULT 'Work'`, de modo que las filas existentes quedan como `Work` sin pérdida. Reversión: `dotnet ef database update AddUsers` elimina la columna y su restricción; las pausas ya registradas se convertirían en filas sin tipo (tiempo no distinguible), por lo que conviene revertir solo antes de que existan pausas reales.
- **Cambio de contrato (`workedMinutes` → `durationMinutes`):** la API y la Web se despliegan juntas desde el mismo repositorio; no hay otros consumidores.
- **Concurrencia entre transiciones:** mitigada con transacción única y `xmin`; un conflicto devuelve `409` con mensaje claro.
- **Límite de 50 filas:** cubre menos días; aceptado y documentado.

## Criterios de éxito

Ciclo entrada → pausa → reanudar → comida → reanudar → salida correcto de extremo a extremo; transiciones inválidas con `409` en español; totales coherentes; vista de responsable con pausas de su equipo; datos antiguos migrados como `Work`; build, formato, pruebas, cobertura (Domain y Application al 100 %) y contrato OpenAPI en verde.
