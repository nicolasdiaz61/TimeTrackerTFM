# Delta para time-entries-persistence

Cambio: `multi-compania`. Capacidad EXISTENTE (`openspec/specs/time-entries-persistence/spec.md`). Contiene un bloque `MODIFIED` (columnas de `time_entries`), un bloque `REMOVED` y bloques `ADDED`. Cierra el hueco del ADR 0011: `time_entries.employee_id` pasa a tener clave foránea compuesta hacia `users`, con la compañía como parte de la clave.

> Aviso de delta que cambia escenarios vigentes: los escenarios «Sin FK en time_entries» y «Fichaje huérfano aceptado» del requisito `Sin clave foránea de fichajes a usuarios` dejan de valer y el requisito se retira. La nota de cabecera «Fuera de alcance: tabla `Employee` y claves foráneas de fichajes» deja de aplicar a esas claves.

Notas de verificación adicionales:
- Las pruebas con base de datos usan Testcontainers (`postgres:18-alpine`) y dos compañías A y B (ver `company-isolation`); cada prueba usa compañía, empleados y correos nuevos.
- El orden de las migraciones y el tratamiento de fichajes huérfanos en producción se especifican en `company-isolation` («Migración expandir/contraer con datos existentes»).

## MODIFIED Requirements

### Requirement: Modelo de datos con instantes UTC

La tabla `time_entries` DEBE almacenar `id` (uuid), `company_id` (uuid, obligatorio), `employee_id` (uuid), `type` (ver «Columna de tipo con valores acotados»), `clock_in_at` (`timestamp with time zone`, obligatorio) y `clock_out_at` (`timestamp with time zone`, nulo si abierta), con los nombres SQL explícitos en minúsculas y con guion bajo; no DEBE tener más columnas (el token de concurrencia es la columna de sistema `xmin`). DEBE existir una restricción `CHECK` `ck_time_entries_clock_out_after_clock_in` (`clock_out_at IS NULL OR clock_out_at >= clock_in_at`) como defensa en profundidad; la regla vive en Domain. El mapeo DEBE ser Fluent en `Infrastructure`; `Domain` NO DEBE referenciar EF Core. Un intervalo leído DEBE conservar el mismo instante con offset cero. Los instantes guardados ya están truncados a segundos (ver `time-tracking`), de modo que lo persistido coincide con lo devuelto por la API.
(Previously: las columnas eran exactamente `clock_in_at`, `clock_out_at`, `employee_id`, `id` y `type`)

#### Scenario: Tipos de columna
- DADO la base migrada
- CUANDO se consulta `information_schema.columns` para `time_entries`
- ENTONCES `clock_in_at` y `clock_out_at` son `timestamp with time zone`, solo `clock_out_at` admite nulos y las columnas son exactamente `clock_in_at`, `clock_out_at`, `company_id`, `employee_id`, `id` y `type`

#### Scenario: Restricción CHECK presente
- DADO la base migrada
- CUANDO se intenta insertar una fila con `clock_out_at` anterior a `clock_in_at`
- ENTONCES PostgreSQL la rechaza por la restricción `ck_time_entries_clock_out_after_clock_in`

#### Scenario: Ida y vuelta del instante
- DADO un intervalo con `ClockInAt` = `2026-10-12T08:00:00Z` guardado
- CUANDO se lee con un contexto nuevo de su compañía
- ENTONCES `ClockInAt` representa el mismo instante con `Offset` cero y `CompanyId` coincide con el guardado

## REMOVED Requirements

### Requirement: Sin clave foránea de fichajes a usuarios

Reason: el ADR 0011 fijó como disparador de revisión «al añadir alta y baja de usuarios»; el alta de usuarios existe desde este cambio (ver `employee-management` y `company-registration`) y el aislamiento entre compañías exige que el fichaje pertenezca a un empleado de su misma compañía. Lo sustituye el requisito «Clave foránea compuesta de fichajes a usuarios».
Migration: la migración de contraer (B, ver `company-isolation`) crea la clave foránea compuesta; los fichajes cuyo `employee_id` no exista en `users` impiden aplicarla y DEBEN resolverse antes en producción (comprobar el contenido de la base de datos de producción antes de desplegar).

## ADDED Requirements

### Requirement: Clave foránea compuesta de fichajes a usuarios

`time_entries` DEBE tener una clave foránea compuesta `(company_id, employee_id)` → `users(company_id, id)` con `NO ACTION` no diferible (`MATCH SIMPLE`; ambas columnas son obligatorias, de modo que la clave siempre se comprueba), además de la clave foránea de `company_id` hacia `companies(id)`. Un fichaje cuyo `employee_id` no existe en `users`, o cuyo `company_id` difiere del de su empleado, NO DEBE poder almacenarse (`23503`). Las restricciones, índices y el índice único parcial de entrada abierta por empleado vigentes DEBEN conservarse.

#### Scenario: FK compuesta presente
- DADO la base migrada
- CUANDO se consultan las restricciones de `time_entries` en `pg_constraint`
- ENTONCES existe una clave foránea compuesta sobre `(company_id, employee_id)` hacia `users`, y siguen la clave primaria y `ck_time_entries_clock_out_after_clock_in`

#### Scenario: Fichaje huérfano rechazado
- DADO un `employee_id` que no existe en `users`
- CUANDO se inserta un fichaje con ese `employee_id`
- ENTONCES PostgreSQL lo rechaza con `23503`

#### Scenario: Compañía distinta a la del empleado
- DADO el empleado `e` de la compañía A
- CUANDO se inserta un fichaje con `employee_id` = `e` y el `company_id` de la compañía B
- ENTONCES PostgreSQL lo rechaza con `23503`

#### Scenario: Fichaje válido
- DADO el empleado `e` de la compañía A
- CUANDO se inserta un fichaje con su `employee_id` y `company_id` de A
- ENTONCES se acepta

#### Scenario: Índice de entrada abierta conservado
- DADO un empleado con una fila abierta
- CUANDO se inserta otra fila abierta del mismo empleado
- ENTONCES PostgreSQL la rechaza con `23505` por `ux_time_entries_employee_id_open` (el conflicto se sigue traduciendo a `OpenTimeEntryAlreadyExistsException`)

### Requirement: Repositorio de fichajes acotado por compañía

`ITimeEntryRepository` y `IDemoTimeEntryStore` DEBEN operar siempre sobre la compañía del contexto (filtro global; ver `company-isolation`) además del filtro por `EmployeeId`: el fichaje abierto de un empleado, los últimos fichajes, `ReplaceOpenAsync`, `ListStartsBetweenAsync` y `AddRangeAsync` NO DEBEN leer ni escribir filas de otra compañía, aunque el `EmployeeId` coincida con un identificador de otra compañía. Un contexto sin compañía NO DEBE devolver ninguna fila (falla cerrado). Un fichaje nuevo DEBE persistirse con el `CompanyId` del empleado dueño.

#### Scenario: Últimos N de la compañía
- DADO un empleado de A con 5 fichajes y otro de B con 3
- CUANDO se solicitan los últimos de un `EmployeeId` de B con el contexto de A
- ENTONCES el resultado está vacío

#### Scenario: Abierto ajeno por compañía
- DADO un fichaje abierto del empleado `e` de B
- CUANDO se busca el abierto de `e` con el contexto de A
- ENTONCES el resultado es nulo

#### Scenario: Sin compañía en el contexto
- DADO un contexto sin compañía y fichajes en la base
- CUANDO se consulta cualquier operación de lectura del repositorio
- ENTONCES no se devuelve ninguna fila y no se lanza error

#### Scenario: Transición atómica en la compañía
- DADO un `Work` abierto de A
- CUANDO se invoca `ReplaceOpenAsync` con el `Work` cerrado y un `Break` nuevo
- ENTONCES ambos intervalos tienen `company_id` de A

#### Scenario: Siembra acotada
- DADO una colección de intervalos de la compañía demo
- CUANDO se persiste con `AddRangeAsync` y se listan los inicios con el contexto de otra compañía
- ENTONCES la lista está vacía y los intervalos conservan el `company_id` de la demo
