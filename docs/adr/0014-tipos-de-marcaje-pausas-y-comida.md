# ADR 0014: Tipos de marcaje, pausas y pausas de comida

- Estado: Aceptado
- Fecha: 2026-10-14

## Contexto

El fichaje solo permitía entrada y salida. Hay que registrar también pausas y pausas de comida sin que cuenten como tiempo trabajado, mostrarlas al responsable y migrar los datos de producción (intervalos sin tipo) sin pérdida. El modelo previo garantiza «un único intervalo abierto por empleado» con un índice único parcial.

## Decisión

- **Un intervalo por tipo, un único abierto.** Cada `TimeEntry` tiene un `TimeEntryType` inmutable (`Work`, `Break`, `MealBreak`). Pausar cierra el `Work` abierto y abre un intervalo de pausa; terminar la pausa cierra la pausa y abre un `Work`; fichar la salida cierra el abierto sea del tipo que sea. El tiempo trabajado es la suma de intervalos `Work`; pausas y comidas se totalizan aparte. Las reglas de transición viven en el dominio (`Pause`, `Resume`) y fallan con `NotWorkingException` y `NoOpenBreakException` (409 en español).
- **Tipo guardado como texto**, `varchar(16)` con los nombres de miembro y una restricción `CHECK`. Es legible en consultas y soporte y estable si se reordena o amplía la enumeración, a diferencia de un `smallint`, que depende del orden de declaración. El coste es de unos bytes por fila.
- **Migración `AddTimeEntryType`:** columna `NOT NULL DEFAULT 'Work'` más la restricción; las filas existentes quedan como `Work` sin reescritura de la tabla. `Down` elimina ambas. El valor por defecto se conserva en la base de datos y no se declara en el modelo (EF omitiría el valor por defecto del CLR en los `INSERT`).
- **Endpoints dedicados** (`start-break`, `start-meal-break`, `end-break`, además de `clock-in` y `clock-out`) en lugar de un `POST /transition` con cuerpo: sin cuerpo que validar, sin pares de transición inválidos que rechazar, coherentes con los actuales y con autorización y contrato por endpoint.
- **Transición atómica con concurrencia optimista.** `ReplaceOpenAsync` cierra e inserta en una transacción (primero el cierre, porque el índice parcial no es diferible) y la fila usa `xmin` como token de concurrencia; un conflicto produce `ConcurrentTimeEntryChangeException` (409). Levanta el aplazamiento DD-9 de `fichaje-extremo-a-extremo`, que dejaba la salida sin control de concurrencia: con dos filas por operación una pausa y una salida simultáneas podrían dejar un intervalo abierto tras la salida.
- **Contrato:** `TimeEntryResponse` gana `type` y renombra `workedMinutes` a `durationMinutes` (ya no es siempre trabajo); el listado gana `totalBreakMinutes`, `totalMealBreakMinutes` y `status` (`Idle`, `Working`, `OnBreak`, `OnMealBreak`), calculado en Application. La Web traduce tipo y estado a texto en español y elige los botones válidos; la validez real la decide el servidor.

## Consecuencias

- Un día de trabajo con pausas genera varios intervalos `Work`; el límite de 50 intervalos por consulta cubre menos días (aceptado, se revisará con los informes por día).
- El cambio de nombre `workedMinutes` → `durationMinutes` rompe a cualquier consumidor externo; en este repositorio API y Web se despliegan juntas.
- Revertir la migración tras registrar pausas reales las convertiría en filas indistinguibles del trabajo: conviene revertir solo antes de que existan.
- La concurrencia de `clock-out` también queda cubierta (409 ante modificación simultánea).

## Alternativas

- **Pausas anidadas dentro de un fichaje:** exigen varios intervalos abiertos (entrada y pausa), restar pausas del total y rompen el índice único.
- **Registro de eventos con intervalos derivados:** flexible, pero reescribe el modelo, la migración y todas las consultas.
- **`smallint` con conversión explícita:** más compacto pero opaco y dependiente del orden de la enumeración.
- **Endpoint único de transición:** menos rutas, pero cuerpo, validación adicional y un contrato menos descriptivo.
