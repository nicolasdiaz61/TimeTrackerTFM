# Especificación: time-entries-persistence

## Purpose

Define el almacenamiento de fichajes en PostgreSQL con EF Core: modelo de datos, índices, migraciones reales (`InitialTimeEntries`, `AddUsers`, con la tabla `users` especificada en `team-access`, y `AddTimeEntryType`, que añade el tipo de intervalo dejando los datos existentes como `Work`), aplicación opcional de migraciones al arrancar (antes de la siembra de demostración), la ausencia deliberada de clave foránea de `time_entries.employee_id` a `users` (ADR 0011), traducción del conflicto de entrada abierta duplicada, la transición atómica de pausa y reanudación con token de concurrencia `xmin` y pruebas de integración con Testcontainers y tokens de prueba.

Fuera de alcance: tabla `Employee` y claves foráneas de fichajes, datos de ejemplo de fichajes, copias de seguridad, datos reales (el control de concurrencia con `xmin`, diferido en `fichaje-extremo-a-extremo`, se incorpora en `tipos-de-marcaje`, ADR 0014).

## ADDED Requirements

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

### Requirement: Índices de consulta y de entrada abierta única

La base DEBE tener un índice `ix_time_entries_employee_id_clock_in_at` sobre `(employee_id, clock_in_at DESC)` y un índice **único parcial** `ux_time_entries_employee_id_open` sobre `employee_id` con filtro `clock_out_at IS NULL`. Dos filas abiertas del mismo empleado NO DEBEN poder coexistir; empleados distintos sí.

#### Scenario: Índices presentes
- DADO la base migrada
- CUANDO se consulta `pg_indexes` para `time_entries`
- ENTONCES existe `ix_time_entries_employee_id_clock_in_at` cuya definición contiene `clock_in_at DESC` y `ux_time_entries_employee_id_open`, `UNIQUE`, cuya definición contiene `WHERE (clock_out_at IS NULL)`

#### Scenario: Segunda fila abierta rechazada
- DADO un empleado con una fila abierta
- CUANDO se inserta otra fila abierta del mismo empleado
- ENTONCES PostgreSQL la rechaza con `23505`

#### Scenario: Fila cerrada no bloquea
- DADO un empleado con una fila cerrada
- CUANDO se inserta una fila abierta del mismo empleado
- ENTONCES la inserción se acepta

### Requirement: Traducción de conflicto de entrada duplicada

`Infrastructure` DEBE traducir la violación `23505` del índice único de entrada abierta (identificada por el nombre de la restricción) a `OpenTimeEntryAlreadyExistsException`. Otros errores de base de datos, incluidas otras violaciones únicas, NO DEBEN traducirse a esa excepción.

#### Scenario: Conflicto traducido
- DADO un empleado con una fila abierta
- CUANDO el repositorio añade otra abierta del mismo empleado
- ENTONCES se lanza `OpenTimeEntryAlreadyExistsException`

### Requirement: Repositorio acotado por empleado

El repositorio DEBE devolver el fichaje abierto de un empleado y sus últimos 50 fichajes por `ClockInAt` descendente, siempre filtrados por `EmployeeId`.

#### Scenario: Últimos 50
- DADO un empleado con 55 fichajes y otro con 3
- CUANDO se solicita el listado del primero
- ENTONCES devuelve 50, los más recientes en orden descendente, sin filas del segundo

#### Scenario: Entrada abierta ajena no se devuelve
- DADO un fichaje abierto de `B`
- CUANDO se busca el abierto de `A`
- ENTONCES el resultado es nulo

### Requirement: Migraciones reales y sin cambios pendientes

El esquema DEBE crearse con migraciones de EF Core versionadas (`InitialTimeEntries` y `AddUsers`), con `IDesignTimeDbContextFactory` en `Infrastructure` y `dotnet-ef` como herramienta local. Las pruebas DEBEN usar `MigrateAsync()` y NO DEBEN usar `EnsureCreated`. El modelo NO DEBE tener cambios sin migrar. `AddUsers` DEBE aplicarse sobre una base que ya tiene `InitialTimeEntries` sin perder filas de `time_entries`. Los archivos de `Migrations` DEBEN tratarse como código generado sin romper `dotnet build -warnaserror` ni `dotnet format --verify-no-changes`.

#### Scenario: Sin cambios pendientes
- DADO el contexto y las migraciones
- CUANDO se invoca `HasPendingModelChanges()` sobre una base migrada
- ENTONCES devuelve `false`

#### Scenario: Migración sobre base vacía
- DADO una base PostgreSQL vacía
- CUANDO se ejecuta `MigrateAsync()`
- ENTONCES se aplican `InitialTimeEntries` y `AddUsers` en ese orden y existen las tablas `time_entries` y `users`, los índices y las restricciones de ambas

#### Scenario: AddUsers sobre una base con fichajes
- DADO una base migrada solo hasta `InitialTimeEntries` con un fichaje guardado
- CUANDO se aplica `AddUsers`
- ENTONCES existe la tabla `users` y el fichaje sigue presente con los mismos valores

#### Scenario: Herramienta local
- DADO `.config/dotnet-tools.json`
- CUANDO se lee
- ENTONCES declara `dotnet-ef` en la versión `10.0.12`

### Requirement: Bandera de migraciones al arrancar

`Database:ApplyMigrationsOnStartup` DEBE valer `false` por defecto. Con `true`, la API DEBE aplicar las migraciones pendientes mediante un servicio alojado en su fase de arranque (`StartingAsync`), después de la validación de opciones y antes de que el servidor empiece a escuchar; si la migración falla, el proceso NO DEBE arrancar. Con `false`, NO DEBE tocar el esquema. Esto se cumple también con `WebApplicationFactory`. La siembra de demostración (`team-access`) DEBE ejecutarse después de aplicar las migraciones.

#### Scenario: Bandera activada
- DADO una base vacía, `Database:ApplyMigrationsOnStartup=true`, el entorno `Development` y el resto de la configuración obligatoria válida (incluida `Jwt:SigningKey`)
- CUANDO la API arranca
- ENTONCES las tablas `time_entries` y `users` existen y `POST /api/time-entries/clock-in` con un token válido responde `201`

#### Scenario: Bandera por defecto
- DADO una base vacía, la bandera sin definir, el entorno `Development` y el resto de la configuración obligatoria válida
- CUANDO la API arranca
- ENTONCES la API arranca y la base sigue sin las tablas `time_entries` y `users`

#### Scenario: Migración fallida impide el arranque
- DADO `Database:ApplyMigrationsOnStartup=true` y una cadena de conexión sintácticamente válida sin servidor accesible
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla y no se atiende ninguna petición

### Requirement: Conexión perezosa y salud independiente

Con una cadena de conexión sintácticamente válida pero sin servidor accesible, la API DEBE arrancar y `GET /health` DEBE responder `200` sin necesidad de token, porque `/health` no consulta la base de datos. Esto permite el uso de una cadena ficticia en `WebApplicationFactory` y en la exportación OpenAPI en build (que fuerza el entorno `Development` con valores ficticios; ver `solution-architecture`). La siembra de demostración desactivada (valor por defecto) no accede a la base de datos.

#### Scenario: Salud sin base de datos
- DADO `ConnectionStrings:Default` apuntando a un servidor inexistente, la bandera de migraciones sin definir, `Seed:DemoUsers` sin definir, el entorno `Development` y `Jwt:SigningKey` y `Cors:AllowedOrigins` válidos
- CUANDO la API arranca y se envía `GET /health` sin cabecera `Authorization`
- ENTONCES responde `200` con `{"status":"Healthy"}`

### Requirement: Carrera de doble fichaje

Ante varios `clock-in` simultáneos del mismo empleado, exactamente uno DEBE tener éxito y el resto DEBEN fallar con el conflicto de entrada abierta; al final DEBE existir una sola fila abierta.

#### Scenario: Carrera en el repositorio
- DADO un empleado sin fichajes y 10 inserciones abiertas lanzadas en paralelo
- CUANDO terminan
- ENTONCES una tiene éxito, nueve lanzan `OpenTimeEntryAlreadyExistsException` y hay una sola fila abierta

#### Scenario: Carrera a través de la API
- DADO un empleado nuevo y 10 `POST /api/time-entries/clock-in` en paralelo
- CUANDO terminan
- ENTONCES hay exactamente un `201`, nueve `409` y el listado muestra un solo fichaje

### Requirement: Pruebas con Testcontainers

Las pruebas de integración con base de datos DEBEN usar PostgreSQL real mediante Testcontainers con la imagen `postgres:18-alpine` (la misma que en compose), con un contenedor por ejecución compartido entre pruebas (`ICollectionFixture`), y un identificador de empleado nuevo por prueba (y un correo único por prueba cuando se necesiten filas en `users`). Las pruebas de API DEBEN autenticarse con tokens fabricados por `TestTokens`. Requieren Docker en marcha: sin él, fallan con un mensaje claro y NO se omiten en silencio. La documentación (README) DEBE indicarlo.

#### Scenario: Imagen fija
- DADO la fixture de PostgreSQL
- CUANDO se revisa su código y `docker-compose.yml`
- ENTONCES ambos usan `postgres:18-alpine`

#### Scenario: Suite verde con Docker
- DADO Docker en marcha
- CUANDO se ejecuta `dotnet test`
- ENTONCES pasan las pruebas de repositorio de fichajes y de usuarios, carrera de entrada, API de extremo a extremo, equipo y migraciones

#### Scenario: Correos únicos por prueba
- DADO dos pruebas que crean usuarios en la misma base compartida
- CUANDO se ejecutan en paralelo
- ENTONCES ninguna falla por correo duplicado (`23505`)

#### Scenario: README documenta el requisito
- DADO `README.md`
- CUANDO se busca la sección de pruebas
- ENTONCES indica que las pruebas de integración requieren Docker

### Requirement: Sin clave foránea de fichajes a usuarios

`time_entries.employee_id` NO DEBE tener clave foránea hacia `users` en este cambio. Un fichaje cuyo `employee_id` no existe en `users` DEBE poder almacenarse (decisión registrada en el ADR 0011 con su disparador de revisión).

#### Scenario: Sin FK en time_entries
- DADO la base migrada
- CUANDO se consultan las restricciones de `time_entries` en `pg_constraint`
- ENTONCES no existe ninguna de tipo clave foránea y sí siguen la clave primaria y `ck_time_entries_clock_out_after_clock_in`

#### Scenario: Fichaje huérfano aceptado
- DADO un `employee_id` que no existe en `users`
- CUANDO se inserta un fichaje con ese `employee_id`
- ENTONCES PostgreSQL lo acepta

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
