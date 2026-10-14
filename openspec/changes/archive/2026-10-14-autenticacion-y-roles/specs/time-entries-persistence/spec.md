# Delta para time-entries-persistence

Cambio: `autenticacion-y-roles`. Añade la migración `AddUsers` (tabla `users`, especificada en `team-access`) tras `InitialTimeEntries`, deja constancia de que `time_entries.employee_id` no tiene clave foránea hacia `users`, y sustituye en los escenarios de arranque la identidad provisional por `Jwt:SigningKey` y el token. El `Purpose` de la especificación vigente ya no es completo (solo habla de fichajes) y se ajusta al archivar.

Notas de verificación:
- Las pruebas de API con base de datos autentican con `TestTokens` y crean un usuario con correo único por prueba cuando necesitan filas en `users`.
- Los fichajes creados con la identidad provisional quedan huérfanos (sin fila en `users`); `docker compose down -v` los elimina.

## ADDED Requirements

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

## MODIFIED Requirements

### Requirement: Migraciones reales y sin cambios pendientes

El esquema DEBE crearse con migraciones de EF Core versionadas (`InitialTimeEntries` y `AddUsers`), con `IDesignTimeDbContextFactory` en `Infrastructure` y `dotnet-ef` como herramienta local. Las pruebas DEBEN usar `MigrateAsync()` y NO DEBEN usar `EnsureCreated`. El modelo NO DEBE tener cambios sin migrar. `AddUsers` DEBE aplicarse sobre una base que ya tiene `InitialTimeEntries` sin perder filas de `time_entries`. Los archivos de `Migrations` DEBEN tratarse como código generado sin romper `dotnet build -warnaserror` ni `dotnet format --verify-no-changes`.
(Previously: una sola migración inicial, sin `AddUsers`)

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
(Previously: los escenarios llamaban a `clock-in` sin autenticación y solo mencionaban `time_entries`)

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
(Previously: el escenario exigía `Identity:DevelopmentEmployeeId`; ahora exige `Jwt:SigningKey`)

#### Scenario: Salud sin base de datos
- DADO `ConnectionStrings:Default` apuntando a un servidor inexistente, la bandera de migraciones sin definir, `Seed:DemoUsers` sin definir, el entorno `Development` y `Jwt:SigningKey` y `Cors:AllowedOrigins` válidos
- CUANDO la API arranca y se envía `GET /health` sin cabecera `Authorization`
- ENTONCES responde `200` con `{"status":"Healthy"}`

### Requirement: Pruebas con Testcontainers

Las pruebas de integración con base de datos DEBEN usar PostgreSQL real mediante Testcontainers con la imagen `postgres:18-alpine` (la misma que en compose), con un contenedor por ejecución compartido entre pruebas (`ICollectionFixture`), y un identificador de empleado nuevo por prueba (y un correo único por prueba cuando se necesiten filas en `users`). Las pruebas de API DEBEN autenticarse con tokens fabricados por `TestTokens`. Requieren Docker en marcha: sin él, fallan con un mensaje claro y NO se omiten en silencio. La documentación (README) DEBE indicarlo.
(Previously: sin filas en `users` ni tokens de prueba)

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
