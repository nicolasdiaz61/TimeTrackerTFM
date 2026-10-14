# Delta para time-entries-ui

Cambio: `autenticacion-y-roles`. `/fichajes` pasa a exigir sesión; un `401` lleva a `/login`; las peticiones llevan el token. La sesión, el manejador del token, `/login` y `/equipo` se especifican en `web-session`. El `Purpose` de la especificación vigente («Fuera de alcance: autenticación») queda obsoleto y se ajusta al archivar.

Verificación: sin bUnit ni E2E; los escenarios de esta capacidad se verifican manualmente en el navegador y la evidencia se registra en `verify-report.md`. Compilación y formato sí son automáticos.

## MODIFIED Requirements

### Requirement: Página /fichajes con listado y total

La ruta `/fichajes` DEBE exigir una sesión iniciada; sin sesión DEBE navegar a `/login`. Con sesión, DEBE mostrar una tabla con los fichajes devueltos por `GET /api/time-entries` (entrada, salida, minutos trabajados) en el orden recibido y el total `totalWorkedMinutes` (en minutos y en formato h:mm). Sin fichajes DEBE mostrar un mensaje indicando que no hay fichajes y total `0`. La página DEBERÍA enlazarse desde el menú de navegación.
(Previously: accesible sin sesión)

#### Scenario: Carga inicial con datos
- DADO una sesión de un empleado con dos fichajes cerrados y uno abierto
- CUANDO se abre `/fichajes`
- ENTONCES la tabla muestra los tres en el orden de la API y el total coincide con el valor de la API

#### Scenario: Sin fichajes
- DADO una sesión de un empleado sin fichajes
- CUANDO se abre `/fichajes`
- ENTONCES aparece un mensaje de que no hay fichajes y el total es `0`

#### Scenario: Sin sesión
- DADO ninguna sesión iniciada
- CUANDO se abre `/fichajes`
- ENTONCES se navega a `/login` y no se observa ninguna petición a `/api/time-entries`

#### Scenario: Solo los fichajes propios
- DADO las sesiones de `luis` y de `ana`, cada una con sus fichajes
- CUANDO cada una abre `/fichajes`
- ENTONCES cada tabla contiene únicamente los fichajes de su titular

### Requirement: Mensajes de error en español

Una respuesta `409` DEBE mostrar al usuario el `detail` en español del ProblemDetails sin recargar la página ni romper la pantalla. Una respuesta `401` NO DEBE mostrarse como error de la pantalla: DEBE eliminar la sesión y navegar a `/login` (ver `web-session`). Un fallo de red o una respuesta inesperada DEBE mostrar un mensaje genérico en español sin trazas ni datos técnicos.
(Previously: sin tratamiento del `401`)

#### Scenario: Doble entrada
- DADO un empleado con un fichaje abierto
- CUANDO pulsa «Fichar entrada»
- ENTONCES se muestra el mensaje en español de la API y el listado no cambia

#### Scenario: Salida sin entrada
- DADO un empleado sin fichaje abierto
- CUANDO pulsa «Fichar salida»
- ENTONCES se muestra el mensaje en español de la API

#### Scenario: API caída
- DADO la API detenida
- CUANDO se abre `/fichajes` o se pulsa un botón
- ENTONCES se muestra un mensaje genérico en español y la pantalla sigue operativa

#### Scenario: Sesión caducada en un botón
- DADO una sesión cuyo token fue rechazado por la API
- CUANDO se pulsa «Fichar entrada» y la API responde `401`
- ENTONCES se elimina la sesión, se navega a `/login` y no se muestra un mensaje de error técnico en `/fichajes`

### Requirement: Cliente tipado y URL base configurable

La Web DEBE acceder a la API mediante `TimeEntriesApiClient` tipado, con la URL base tomada de `ApiBaseUrl` en `wwwroot/appsettings.json`, sin URL fijas en el código. Cada petición a la API DEBE llevar el token de sesión como `Authorization: Bearer` (ver `web-session`). Los DTO se duplican en Web; NO DEBE haber `ProjectReference` hacia proyectos de `src/`. La Web NO DEBE enviar ningún `employeeId`. Si `ApiBaseUrl` falta o no es una URI absoluta `http`/`https`, la Web DEBE fallar al arrancar con un mensaje en español que nombre `ApiBaseUrl`.
(Previously: sin token; el escenario de navegador usaba la identidad provisional)

#### Scenario: ApiBaseUrl ausente o inválida
- DADO `ApiBaseUrl` ausente o con el valor `no-es-una-url`
- CUANDO arranca la Web
- ENTONCES el arranque falla con una `InvalidOperationException` cuyo mensaje, en español, contiene `ApiBaseUrl`

#### Scenario: URL base
- DADO `wwwroot/appsettings.json` con `ApiBaseUrl` = `http://localhost:5274`
- CUANDO la página llama a la API
- ENTONCES las peticiones van a esa base

#### Scenario: Sin referencias de proyecto
- DADO `TimeTracker.Web.csproj`
- CUANDO se ejecuta `dotnet list TimeTracker.Web reference`
- ENTONCES no lista ningún proyecto de `src/`

#### Scenario: Token en las peticiones
- DADO una sesión iniciada
- CUANDO se abre `/fichajes` y se pulsa «Fichar entrada»
- ENTONCES las peticiones a `http://localhost:5274/api/time-entries…` llevan `Authorization: Bearer` y ninguna incluye `employeeId` en ruta, consulta, cabecera ni cuerpo

#### Scenario: Flujo de navegador registrado
- DADO la API (en `Development`, con `ConnectionStrings__Default`, `Jwt__SigningKey`, `Seed__DemoUsers=true`, `Seed__DemoPassword` y `Database__ApplyMigrationsOnStartup=true`, o con `docker compose up`) y la Web en marcha, y el origen de la Web (p. ej. `Cors__AllowedOrigins__0=http://localhost:5172`) permitido en `Cors:AllowedOrigins`
- CUANDO se inicia sesión con un usuario de demostración y se ficha entrada, salida y se revisa el total en `/fichajes`
- ENTONCES el flujo funciona sin errores de CORS (incluidos los POST, ahora con cabecera `Authorization`) y se registra en `verify-report.md`
