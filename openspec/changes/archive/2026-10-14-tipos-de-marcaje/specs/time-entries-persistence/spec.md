# Delta para time-entries-persistence

Cambio: `tipos-de-marcaje`. La tabla `time_entries` gana la columna `type` (texto) mediante la migración `AddTimeEntryType`, reversible y que deja las filas existentes como `Work`. El repositorio gana `ReplaceOpenAsync` (transición atómica) y la fila usa `xmin` como token de concurrencia, lo que levanta el aplazamiento DD-9 de `fichaje-extremo-a-extremo` para todas las escrituras de intervalos. El `Purpose` vigente, que lista las migraciones y deja la concurrencia fuera de alcance, debe actualizarse al archivar.

## ADDED Requirements

### Requirement: Columna de tipo con valores acotados

La tabla `time_entries` DEBE tener la columna `type` (`character varying(16)`, obligatoria) que guarda el nombre del valor de `TimeEntryType` (`Work`, `Break`, `MealBreak`) y una restricción `CHECK` `ck_time_entries_type` que solo admite esos tres textos. La conversión DEBE ser explícita en el mapeo de `Infrastructure` (nombre del miembro, no su valor numérico), de modo que reordenar o ampliar la enumeración no altere lo ya guardado. Una fila insertada sin indicar `type` DEBE quedar como `Work` (valor por defecto de la base de datos, que conserva compatibilidad con inserciones anteriores a este cambio).

#### Scenario: Columna y restricción presentes
- DADO la base migrada
- CUANDO se consulta `information_schema.columns` y `pg_constraint`
- ENTONCES existe `type:character varying:NO` y la restricción `ck_time_entries_type`

#### Scenario: Valor desconocido rechazado
- DADO la base migrada
- CUANDO se inserta una fila con `type` = `Vacaciones`
- ENTONCES PostgreSQL la rechaza por `ck_time_entries_type`

#### Scenario: Valor por defecto
- DADO la base migrada
- CUANDO se inserta una fila sin indicar `type`
- ENTONCES la fila queda con `type` = `Work`

#### Scenario: Ida y vuelta del tipo
- DADO un intervalo `MealBreak` guardado mediante el repositorio
- CUANDO se lee con un contexto nuevo
- ENTONCES su `Type` es `MealBreak` y en la base está guardado como el texto `MealBreak`

### Requirement: Migración de datos existentes

La migración `AddTimeEntryType` DEBE añadir la columna con valor por defecto `Work` para que las filas existentes queden como `Work` sin pérdida de datos (ninguna otra columna cambia), y DEBE ser reversible: `Down` elimina la restricción y la columna. Aplicarla sobre una base creada con la migración anterior y con filas abiertas y cerradas DEBE conservar todos sus valores, y todas las filas DEBEN pasar a `Work`.

#### Scenario: Base con datos antiguos
- DADO una base migrada hasta `AddUsers` con una fila cerrada y una abierta
- CUANDO se aplica `AddTimeEntryType`
- ENTONCES ambas filas conservan `id`, `employee_id`, `clock_in_at` y `clock_out_at` y tienen `type` = `Work`

#### Scenario: Reversión
- DADO una base con `AddTimeEntryType` aplicada
- CUANDO se migra de vuelta a `AddUsers`
- ENTONCES `time_entries` no tiene la columna `type` ni la restricción y conserva sus filas

### Requirement: Transición atómica en el repositorio

`ITimeEntryRepository.ReplaceOpenAsync(closed, next)` DEBE persistir en una única transacción el cierre del intervalo `closed` y la inserción del intervalo abierto `next`, aplicando primero el cierre (el índice único parcial es no diferible). Si la base rechaza la inserción por el índice de intervalo abierto, o la fila cerrada fue modificada por otra operación (`xmin` distinto), DEBE lanzar `ConcurrentTimeEntryChangeException` y la transacción DEBE revertirse por completo.

#### Scenario: Cierre e inserción juntos
- DADO un `Work` abierto guardado
- CUANDO se invoca `ReplaceOpenAsync` con el `Work` cerrado y un `Break` nuevo
- ENTONCES la base contiene el `Work` cerrado y el `Break` abierto

#### Scenario: Reversión completa
- DADO un `Work` abierto y un `Break` nuevo con un identificador ya existente (fallo de inserción)
- CUANDO se invoca `ReplaceOpenAsync`
- ENTONCES el `Work` sigue abierto en la base de datos

#### Scenario: Modificación concurrente
- DADO un `Work` abierto cargado por dos contextos y cerrado por uno de ellos
- CUANDO el otro invoca `ReplaceOpenAsync` con su copia
- ENTONCES se lanza `ConcurrentTimeEntryChangeException` y no se inserta el intervalo nuevo

## MODIFIED Requirements

### Requirement: Modelo de datos con instantes UTC

La tabla `time_entries` DEBE almacenar `id` (uuid), `employee_id` (uuid), `type` (ver «Columna de tipo con valores acotados»), `clock_in_at` (`timestamp with time zone`, obligatorio) y `clock_out_at` (`timestamp with time zone`, nulo si abierta), con los nombres SQL explícitos en minúsculas y con guion bajo; no DEBE tener más columnas (el token de concurrencia es la columna de sistema `xmin`). DEBE existir una restricción `CHECK` `ck_time_entries_clock_out_after_clock_in` (`clock_out_at IS NULL OR clock_out_at >= clock_in_at`) como defensa en profundidad; la regla vive en Domain. El mapeo DEBE ser Fluent en `Infrastructure`; `Domain` NO DEBE referenciar EF Core. Un intervalo leído DEBE conservar el mismo instante con offset cero. Los instantes guardados ya están truncados a segundos (ver `time-tracking`), de modo que lo persistido coincide con lo devuelto por la API.

#### Scenario: Tipos de columna
- DADO la base migrada
- CUANDO se consulta `information_schema.columns` para `time_entries`
- ENTONCES `clock_in_at` y `clock_out_at` son `timestamp with time zone`, solo `clock_out_at` admite nulos y las columnas son exactamente `clock_in_at`, `clock_out_at`, `employee_id`, `id` y `type`

#### Scenario: Restricción CHECK presente
- DADO la base migrada
- CUANDO se intenta insertar una fila con `clock_out_at` anterior a `clock_in_at`
- ENTONCES PostgreSQL la rechaza por la restricción `ck_time_entries_clock_out_after_clock_in`

#### Scenario: Ida y vuelta del instante
- DADO un intervalo con `ClockInAt` = `2026-10-12T08:00:00Z` guardado
- CUANDO se lee con un contexto nuevo
- ENTONCES `ClockInAt` representa el mismo instante con `Offset` cero
