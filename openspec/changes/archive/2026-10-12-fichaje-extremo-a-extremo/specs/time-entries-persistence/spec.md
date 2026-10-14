# Especificación: time-entries-persistence

## Purpose

Define el almacenamiento de fichajes en PostgreSQL con EF Core: modelo de datos, índices, migraciones reales, aplicación opcional de migraciones al arrancar, traducción del conflicto de entrada abierta duplicada y pruebas de integración con Testcontainers. Capacidad NUEVA; todos los requisitos son `ADDED`.

Fuera de alcance: tabla `Employee` y claves foráneas, datos de ejemplo, copias de seguridad, datos reales y control de concurrencia de la salida (`xmin`; diferido a seguimiento, ver `design.md` DD-9).

## ADDED Requirements

### Requirement: Modelo de datos con instantes UTC

La tabla `time_entries` DEBE almacenar `id` (uuid), `employee_id` (uuid), `clock_in_at` (`timestamp with time zone`, obligatorio) y `clock_out_at` (`timestamp with time zone`, nulo si abierta), con los nombres SQL explícitos en minúsculas y con guion bajo. DEBE existir una restricción `CHECK` `ck_time_entries_clock_out_after_clock_in` (`clock_out_at IS NULL OR clock_out_at >= clock_in_at`) como defensa en profundidad; la regla vive en Domain. El mapeo DEBE ser Fluent en `Infrastructure`; `Domain` NO DEBE referenciar EF Core. Un fichaje leído DEBE conservar el mismo instante con offset cero. Los instantes guardados ya están truncados a segundos (ver `time-tracking`), de modo que lo persistido coincide con lo devuelto en el `201`.

#### Scenario: Tipos de columna
- DADO la base migrada
- CUANDO se consulta `information_schema.columns` para `time_entries`
- ENTONCES `clock_in_at` y `clock_out_at` son `timestamp with time zone` y solo `clock_out_at` admite nulos

#### Scenario: Restricción CHECK presente
- DADO la base migrada
- CUANDO se intenta insertar una fila con `clock_out_at` anterior a `clock_in_at`
- ENTONCES PostgreSQL la rechaza por la restricción `ck_time_entries_clock_out_after_clock_in`

#### Scenario: Ida y vuelta del instante
- DADO un fichaje con `ClockInAt` = `2026-10-12T08:00:00Z` guardado
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

El esquema DEBE crearse con migraciones de EF Core versionadas (migración inicial incluida), con `IDesignTimeDbContextFactory` en `Infrastructure` y `dotnet-ef` como herramienta local. Las pruebas DEBEN usar `MigrateAsync()` y NO DEBEN usar `EnsureCreated`. El modelo NO DEBE tener cambios sin migrar. Los archivos de `Migrations` DEBEN tratarse como código generado sin romper `dotnet build -warnaserror` ni `dotnet format --verify-no-changes`.

#### Scenario: Sin cambios pendientes
- DADO el contexto y las migraciones
- CUANDO se invoca `HasPendingModelChanges()` sobre una base migrada
- ENTONCES devuelve `false`

#### Scenario: Migración sobre base vacía
- DADO una base PostgreSQL vacía
- CUANDO se ejecuta `MigrateAsync()`
- ENTONCES se aplica la migración inicial `InitialTimeEntries` y existen la tabla, los dos índices y la restricción `CHECK`

#### Scenario: Herramienta local
- DADO `.config/dotnet-tools.json`
- CUANDO se lee
- ENTONCES declara `dotnet-ef` en la versión `10.0.12`

### Requirement: Bandera de migraciones al arrancar

`Database:ApplyMigrationsOnStartup` DEBE valer `false` por defecto. Con `true`, la API DEBE aplicar las migraciones pendientes mediante un servicio alojado en su fase de arranque (`StartingAsync`), después de la validación de opciones y antes de que el servidor empiece a escuchar; si la migración falla, el proceso NO DEBE arrancar. Con `false`, NO DEBE tocar el esquema. Esto se cumple también con `WebApplicationFactory`.

#### Scenario: Bandera activada
- DADO una base vacía, `Database:ApplyMigrationsOnStartup=true`, el entorno `Development` y el resto de la configuración obligatoria válida
- CUANDO la API arranca
- ENTONCES la tabla `time_entries` existe y `POST /api/time-entries/clock-in` responde `201`

#### Scenario: Bandera por defecto
- DADO una base vacía, la bandera sin definir, el entorno `Development` y el resto de la configuración obligatoria válida
- CUANDO la API arranca
- ENTONCES la API arranca y la base sigue sin la tabla `time_entries`

#### Scenario: Migración fallida impide el arranque
- DADO `Database:ApplyMigrationsOnStartup=true` y una cadena de conexión sintácticamente válida sin servidor accesible
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla y no se atiende ninguna petición

### Requirement: Conexión perezosa y salud independiente

Con una cadena de conexión sintácticamente válida pero sin servidor accesible, la API DEBE arrancar y `GET /health` DEBE responder `200`, porque `/health` no consulta la base de datos. Esto permite el uso de una cadena ficticia en `WebApplicationFactory` y en la exportación OpenAPI en build (que fuerza el entorno `Development` con valores ficticios; ver `solution-architecture`).

#### Scenario: Salud sin base de datos
- DADO `ConnectionStrings:Default` apuntando a un servidor inexistente, la bandera de migraciones sin definir, el entorno `Development` e `Identity:DevelopmentEmployeeId` y `Cors:AllowedOrigins` válidos
- CUANDO la API arranca y se envía `GET /health`
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

Las pruebas de integración con base de datos DEBEN usar PostgreSQL real mediante Testcontainers con la imagen `postgres:18-alpine` (la misma que en compose), con un contenedor por ejecución compartido entre pruebas (`ICollectionFixture`), y un `EmployeeId` nuevo por prueba. Requieren Docker en marcha: sin él, fallan con un mensaje claro y NO se omiten en silencio. La documentación (README) DEBE indicarlo.

#### Scenario: Imagen fija
- DADO la fixture de PostgreSQL
- CUANDO se revisa su código y `docker-compose.yml`
- ENTONCES ambos usan `postgres:18-alpine`

#### Scenario: Suite verde con Docker
- DADO Docker en marcha
- CUANDO se ejecuta `dotnet test`
- ENTONCES pasan las pruebas de repositorio, carrera de entrada, API de extremo a extremo y migraciones

#### Scenario: README documenta el requisito
- DADO `README.md`
- CUANDO se busca la sección de pruebas
- ENTONCES indica que las pruebas de integración requieren Docker
