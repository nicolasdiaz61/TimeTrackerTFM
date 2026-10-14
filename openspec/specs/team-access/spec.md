# Especificación: team-access

## Purpose

Define los roles (`Employee`, `Manager`), la cuenta de usuario en el dominio, la tabla `users`, el concepto de equipo (reportes directos por `ManagerId`) y el acceso de solo lectura del responsable a los fichajes de su equipo (incluidas pausas y comidas, con sus totales y el estado actual), con control por rol y por objeto, además de la siembra opcional de usuarios de demostración. Capacidad NUEVA; todos los requisitos son `ADDED`. Reutiliza el listado de fichajes de `time-tracking` y la autenticación de `authentication`.

Decisiones aceptadas (D6 a D11, D20 de la exploración; Q4, Q5, Q7, Q8, Q15): roles en inglés en el código; equipo = reportes directos (sin jerarquía transitiva); el responsable también ficha y solo lee los fichajes de su equipo; miembro ajeno e inexistente producen el mismo `404`; el parámetro de ruta se llama `memberId` y nunca `employeeId`; sin tablas de equipos ni de empleados.

Notas de verificación:
- «Respuesta idéntica» significa mismo estado, tipo de medio (`application/problem+json`, comparado sin parámetros), `type`, `title` y `detail`, salvo el campo `traceId`, que cambia en cada petición.
- El rol se lee del claim `role` del token (vigente hasta 30 minutos); un cambio de rol en base de datos no surte efecto hasta el siguiente login.
- Nombres fijados en el diseño: índice único `ux_users_email`, índice `ix_users_manager_id`, clave foránea `fk_users_users_manager_id` y restricciones `CHECK` `ck_users_role`, `ck_users_manager_not_self` y `ck_users_email_lowercase`; los escenarios se evalúan por la restricción y su código SQLSTATE.
- Un `POST` bajo `/api/team` recibe `405` con un cliente autenticado y `401` sin token (política de respaldo autenticada; ver `authentication`).

Fuera de alcance: jerarquías de más de un nivel, tablas de equipos o empleados, escritura o edición de fichajes ajenos, administración de usuarios (alta, baja, cambio de rol), FK de `time_entries.employee_id` a `users` y despliegue.

## ADDED Requirements

### Requirement: Roles de usuario

`TimeTracker.Domain` DEBE definir `UserRole` con exactamente los valores `Employee` y `Manager`. Un usuario con rol `Manager` DEBE poder fichar igual que un `Employee`.

#### Scenario: Valores del rol
- DADO la enumeración `UserRole`
- CUANDO se listan sus valores
- ENTONCES son exactamente `Employee` y `Manager`

#### Scenario: El responsable ficha
- DADO un token de `marta.responsable@example.test` (rol `Manager`)
- CUANDO envía `POST /api/time-entries/clock-in`
- ENTONCES recibe `201`

### Requirement: Cuenta de usuario en el dominio

`TimeTracker.Domain` DEBE ofrecer `UserAccount.Create(id, email, displayName, passwordHash, role, managerId)` como único modo de construir un usuario (constructor privado). DEBE rechazar con `ArgumentException` o una derivada: `id` igual a `Guid.Empty`; `email` en blanco, sin `@` o de más de 254 caracteres; `displayName` en blanco o de más de 100 caracteres; `passwordHash` en blanco; un `role` no definido en `UserRole`; un `managerId` igual a `Guid.Empty` o igual al propio `id`. El `email` DEBE guardarse sin espacios exteriores y en minúsculas (cultura invariante) y el `displayName` sin espacios exteriores. `managerId` nulo es válido.

#### Scenario: Usuario válido con correo normalizado
- DADO `email` = `  Marta.Responsable@Example.TEST ` y los demás datos válidos
- CUANDO se invoca `UserAccount.Create`
- ENTONCES `Email` es `marta.responsable@example.test` y el resto de propiedades conserva los valores dados

#### Scenario: Usuario sin responsable
- DADO `managerId` nulo
- CUANDO se invoca `UserAccount.Create`
- ENTONCES se crea el usuario con `ManagerId` nulo

#### Scenario: Identificador vacío
- DADO `id` = `Guid.Empty`
- CUANDO se invoca `UserAccount.Create`
- ENTONCES se lanza `ArgumentException` o una derivada

#### Scenario: Correo inválido
- DADO `email` en blanco, `sin-arroba` o de 255 caracteres
- CUANDO se invoca `UserAccount.Create` con cada uno
- ENTONCES cada llamada lanza `ArgumentException` o una derivada

#### Scenario: Nombre o hash no válidos
- DADO `displayName` en blanco, `displayName` de 101 caracteres, y por separado `passwordHash` en blanco
- CUANDO se invoca `UserAccount.Create`
- ENTONCES cada llamada lanza `ArgumentException` o una derivada

#### Scenario: Nombre con espacios exteriores
- DADO `displayName` = `  Marta Ejemplo  `
- CUANDO se invoca `UserAccount.Create`
- ENTONCES `DisplayName` es `Marta Ejemplo`

#### Scenario: Normalización del correo
- DADO el texto `  Luis.Empleado@Example.TEST `
- CUANDO se invoca `UserAccount.NormalizeEmail`
- ENTONCES devuelve `luis.empleado@example.test`

#### Scenario: Rol no definido
- DADO un `role` convertido desde un entero fuera de `UserRole`
- CUANDO se invoca `UserAccount.Create`
- ENTONCES se lanza `ArgumentException` o una derivada

#### Scenario: Responsable no válido
- DADO `managerId` = `Guid.Empty`, y por separado `managerId` igual a `id`
- CUANDO se invoca `UserAccount.Create`
- ENTONCES cada llamada lanza `ArgumentException` o una derivada

### Requirement: Tabla users con restricciones

La base DEBE tener la tabla `users` con `id` (uuid, clave primaria), `email`, `display_name`, `password_hash`, `role` (texto con el nombre del valor de `UserRole`) y `manager_id` (uuid, nulo), todas obligatorias salvo `manager_id`. DEBE existir un índice único sobre `email`, una clave foránea autorreferencial `manager_id` → `users(id)` con `ON DELETE RESTRICT`, una restricción `CHECK` `manager_id <> id` una `CHECK` que limite `role` a `Employee` y `Manager` y una `CHECK` que exija `email` en minúsculas (`email = lower(email)`). DEBE existir además un índice sobre `manager_id`. El mapeo DEBE ser Fluent en `Infrastructure`; `Domain` NO DEBE referenciar EF Core.

#### Scenario: Columnas
- DADO la base migrada
- CUANDO se consulta `information_schema.columns` para `users`
- ENTONCES existen las seis columnas indicadas y solo `manager_id` admite nulos

#### Scenario: Correo duplicado
- DADO un usuario con el correo `luis.empleado@example.test`
- CUANDO se inserta otra fila con el mismo correo
- ENTONCES PostgreSQL la rechaza con `23505`

#### Scenario: Responsable inexistente
- DADO una fila con `manager_id` que no existe en `users`
- CUANDO se inserta
- ENTONCES PostgreSQL la rechaza con `23503`

#### Scenario: Borrado de un responsable con reportes
- DADO `marta` con `luis` como reporte
- CUANDO se intenta borrar la fila de `marta`
- ENTONCES PostgreSQL lo rechaza con `23503` y `luis` conserva su `manager_id`

#### Scenario: Responsable de sí mismo
- DADO una fila con `manager_id` igual a su `id`
- CUANDO se inserta
- ENTONCES PostgreSQL la rechaza con `23514`

#### Scenario: Rol fuera de catálogo
- DADO una fila con `role` = `Admin`
- CUANDO se inserta
- ENTONCES PostgreSQL la rechaza con `23514`

#### Scenario: Correo con mayúsculas
- DADO una fila con `email` = `Luis.Empleado@example.test`
- CUANDO se inserta
- ENTONCES PostgreSQL la rechaza con `23514`

#### Scenario: Ida y vuelta de un usuario
- DADO un `UserAccount` de rol `Manager` guardado
- CUANDO se lee con un contexto nuevo
- ENTONCES `Id`, `Email`, `DisplayName`, `PasswordHash`, `Role` y `ManagerId` coinciden con los guardados

### Requirement: Repositorio de usuarios

`IUserRepository` (Application) DEBE ofrecer: buscar por correo (normalizado), listar los reportes directos de un responsable ordenados por `DisplayName` ascendente y acotados por un límite recibido, `FindDirectReportAsync(managerId, memberId)` que devuelve el usuario solo si `memberId` es reporte directo de `managerId` (y nulo en otro caso, incluido el propio responsable) y añadir un usuario. El listado NO DEBE incluir al propio responsable ni a reportes de sus reportes.

#### Scenario: Búsqueda por correo
- DADO el usuario `luis.empleado@example.test`
- CUANDO se busca por `Luis.Empleado@Example.Test`
- ENTONCES se devuelve ese usuario; con `nadie@example.test` el resultado es nulo

#### Scenario: Reportes directos ordenados
- DADO `marta` con reportes `luis` y `ana`, `pablo` sin responsable y un reporte `carmen` de `ana`
- CUANDO se listan los reportes directos de `marta`
- ENTONCES se devuelven `ana` y `luis`, en ese orden, sin `marta`, `pablo` ni `carmen`

#### Scenario: Miembro del equipo
- DADO `luis` reporte de `marta` y `pablo` sin responsable
- CUANDO se invoca `FindDirectReportAsync` con `(marta, luis)`, con `(marta, pablo)` y con `(marta, marta)`
- ENTONCES el primero devuelve a `luis` y los otros dos nulo

#### Scenario: Límite del listado
- DADO un responsable con 3 reportes directos
- CUANDO se listan sus reportes con límite 2
- ENTONCES se devuelven solo 2, los primeros por `DisplayName` ascendente

### Requirement: Definición de equipo

El equipo de un responsable DEBE ser el conjunto de usuarios cuyo `ManagerId` es el id de ese responsable. No DEBE haber jerarquía transitiva: los reportes de un reporte no pertenecen al equipo del responsable superior. Un responsable sin reportes tiene un equipo vacío.

#### Scenario: Equipo con reportes
- DADO `marta` (`Manager`) con `luis` y `ana` como reportes directos
- CUANDO se ejecuta `GetTeamMembersUseCase` para `marta`
- ENTONCES devuelve `luis` y `ana`

#### Scenario: Sin jerarquía transitiva
- DADO `marta` responsable de `diego` (`Manager`), y `diego` responsable de `carmen`
- CUANDO se ejecuta `GetTeamMembersUseCase` para `marta`
- ENTONCES devuelve solo a `diego`

#### Scenario: Equipo vacío
- DADO un responsable sin reportes
- CUANDO se ejecuta `GetTeamMembersUseCase`
- ENTONCES devuelve una lista vacía

### Requirement: Listado del equipo

`GET /api/team/members` DEBE devolver `200` `application/json` con `{ "items": [ { "id", "displayName" } ] }` con los reportes directos del responsable autenticado (identificado por el claim `sub`), ordenados por `displayName` y con un máximo de 100. La respuesta NO DEBE contener el correo, el hash, el rol ni el `managerId`. Un responsable sin reportes DEBE recibir `items` vacío.

#### Scenario: Equipo de Marta
- DADO `marta` con `luis` y `ana` como reportes y `pablo` sin responsable
- CUANDO `marta` envía `GET /api/team/members`
- ENTONCES recibe `200` con `ana` y `luis`, y sin `pablo`

#### Scenario: Sin datos sensibles
- DADO el listado devuelto
- CUANDO se inspecciona el cuerpo
- ENTONCES no contiene `email`, `passwordHash`, `role` ni `managerId`

#### Scenario: Máximo de 100 miembros
- DADO un responsable con 101 reportes directos
- CUANDO se ejecuta `GetTeamMembersUseCase` (y al invocar el listado en la API)
- ENTONCES el caso de uso pide el listado al repositorio con límite 100 y la respuesta no supera los 100 miembros

#### Scenario: Equipo ajeno no visible
- DADO `diego` (`Manager`) con otro reporte distinto
- CUANDO `diego` envía `GET /api/team/members`
- ENTONCES su lista no contiene a `luis` ni a `ana`

#### Scenario: Equipo vacío
- DADO un responsable sin reportes
- CUANDO envía `GET /api/team/members`
- ENTONCES recibe `200` con `items` vacío

### Requirement: Fichajes de un miembro del equipo

`GET /api/team/members/{memberId}/time-entries` DEBE devolver `200` con la misma forma y reglas que `GET /api/time-entries` (`{ items, totalWorkedMinutes, totalBreakMinutes, totalMealBreakMinutes, status }`, últimos 50 intervalos por `ClockInAt` descendente, con `type` por intervalo, totales de los cerrados de esos 50) para el miembro indicado, solo si es reporte directo del responsable autenticado. Es de solo lectura: la API NO DEBE ofrecer ninguna operación de escritura bajo `/api/team`.

#### Scenario: Fichajes del miembro
- DADO `luis`, reporte de `marta`, con trabajos cerrados de 60 y 30 minutos, una pausa de 15, una comida de 45 y un trabajo abierto
- CUANDO `marta` envía `GET /api/team/members/{luis}/time-entries`
- ENTONCES recibe `200`, `totalWorkedMinutes` = `90`, `totalBreakMinutes` = `15`, `totalMealBreakMinutes` = `45`, `status` = `Working`, el abierto con `durationMinutes` nulo y los instantes terminados en `Z`

#### Scenario: Pausas visibles para el responsable
- DADO `luis`, reporte de `marta`, con una pausa de comida abierta
- CUANDO `marta` consulta sus fichajes
- ENTONCES el primer elemento tiene `type` = `MealBreak` y `status` es `OnMealBreak`

#### Scenario: Más de 50 intervalos
- DADO `luis` con 55 intervalos `Work` cerrados de 10 minutos
- CUANDO `marta` consulta sus fichajes
- ENTONCES recibe los 50 más recientes y `totalWorkedMinutes` = `500`

#### Scenario: Miembro sin fichajes
- DADO `ana`, reporte de `marta`, sin intervalos
- CUANDO `marta` consulta sus fichajes
- ENTONCES recibe `200` con `items` vacío, los tres totales en `0` y `status` = `Idle`

#### Scenario: Solo los fichajes del miembro
- DADO intervalos de `luis`, de `ana` y de la propia `marta`
- CUANDO `marta` consulta los fichajes de `luis`
- ENTONCES la respuesta solo contiene los de `luis`

#### Scenario: Solo lectura
- DADO un token de `marta` y `luis` con un intervalo abierto
- CUANDO se envía `POST /api/team/members/{luis}/time-entries`, `POST /api/team/members/{luis}/start-break` o `POST /api/team/members/{luis}/end-break`
- ENTONCES ninguna crea ni modifica nada (`405` o `404`) y el intervalo de `luis` sigue abierto

#### Scenario: Escritura sin token
- DADO una petición sin cabecera `Authorization`
- CUANDO se envía `POST /api/team/members/{luis}/time-entries`
- ENTONCES recibe `401` y no se crea ni se modifica ningún intervalo

#### Scenario: Las pausas de un ajeno no se exponen
- DADO `ana`, que no es reporte de `marta`, con una pausa abierta
- CUANDO `marta` consulta los fichajes de `ana`
- ENTONCES recibe `404`, idéntico al de un miembro inexistente

### Requirement: Acceso del equipo solo para el rol Manager

Los endpoints bajo `/api/team` DEBEN exigir el rol `Manager` en el token. Sin token DEBEN responder `401`; con un token cuyo rol no sea `Manager` DEBEN responder `403`, antes de evaluar cualquier objeto (el rol se comprueba antes que el miembro, de modo que un empleado no puede averiguar si un id existe).

#### Scenario: Empleado en el listado
- DADO un token de `luis.empleado@example.test` (`Employee`)
- CUANDO envía `GET /api/team/members`
- ENTONCES recibe `403`

#### Scenario: Empleado en los fichajes de un miembro
- DADO un token de `luis` (`Employee`)
- CUANDO envía `GET /api/team/members/{ana}/time-entries`, y también con un `memberId` inexistente
- ENTONCES ambas respuestas son `403`

#### Scenario: Sin token
- DADO una petición sin cabecera `Authorization`
- CUANDO envía `GET /api/team/members`
- ENTONCES recibe `401`

#### Scenario: Responsable permitido
- DADO un token de `marta` (`Manager`)
- CUANDO envía `GET /api/team/members`
- ENTONCES recibe `200`

#### Scenario: Rol desconocido
- DADO un token correctamente firmado con `role` = `Admin`
- CUANDO envía `GET /api/team/members`
- ENTONCES recibe `401` (el token se rechaza en la validación, antes de evaluar la política de rol)

### Requirement: Control por objeto con 404 idéntico

Si `memberId` no es reporte directo del responsable autenticado, la API DEBE responder `404` `application/problem+json` (`title` «Recurso no encontrado», sin `detail`), con la misma respuesta para un miembro de otro responsable, un usuario sin responsable, el propio responsable y un id inexistente. Un valor que no sea GUID no coincide con la ruta y también recibe `404` (con token de `Manager`; sin token recibe `401`). `GetTeamMemberTimeEntriesUseCase` DEBE devolver «no encontrado» sin consultar los fichajes en los casos de miembro ajeno, sin responsable, propio e inexistente.

#### Scenario: Miembro de otro responsable
- DADO `luis` reporte de `marta` y un token de `diego`
- CUANDO `diego` envía `GET /api/team/members/{luis}/time-entries`
- ENTONCES recibe `404` `application/problem+json`

#### Scenario: Miembro inexistente
- DADO un `memberId` GUID que no existe
- CUANDO `marta` envía `GET /api/team/members/{memberId}/time-entries`
- ENTONCES recibe `404` `application/problem+json`

#### Scenario: Ajeno e inexistente indistinguibles
- DADO una respuesta por miembro ajeno y otra por miembro inexistente
- CUANDO se comparan
- ENTONCES coinciden estado, tipo de medio, `type`, `title` y `detail` (salvo `traceId`)

#### Scenario: El propio responsable
- DADO el `id` de `marta` como `memberId`
- CUANDO `marta` envía `GET /api/team/members/{marta}/time-entries`
- ENTONCES recibe `404`

#### Scenario: Identificador que no es GUID
- DADO `memberId` = `abc`
- CUANDO `marta` envía `GET /api/team/members/abc/time-entries`
- ENTONCES recibe `404`

#### Scenario: Caso de uso sin consulta de fichajes
- DADO un repositorio de usuarios falso donde `FindDirectReportAsync` devuelve nulo y un repositorio de fichajes falso
- CUANDO se ejecuta `GetTeamMemberTimeEntriesUseCase`
- ENTONCES el resultado es «no encontrado» y el repositorio de fichajes no recibe ninguna llamada

### Requirement: Un empleado nunca ve datos ajenos

Ningún endpoint DEBE devolver fichajes de otra persona a quien no sea su responsable directo. `GET /api/time-entries`, `clock-in` y `clock-out` DEBEN operar siempre sobre la persona del token, también para un responsable, y NO DEBEN aceptar `memberId` ni `employeeId` de ningún origen (ruta, consulta, cabecera ni cuerpo).

#### Scenario: Empleado solo ve los suyos
- DADO `luis` y `ana` con fichajes cada uno
- CUANDO `luis` envía `GET /api/time-entries`
- ENTONCES la lista contiene solo fichajes de `luis`

#### Scenario: El responsable no ve los del equipo en su listado
- DADO `marta` con un fichaje y `luis` con otro
- CUANDO `marta` envía `GET /api/time-entries`
- ENTONCES la lista contiene solo el de `marta`

#### Scenario: Parámetros de empleado ignorados
- DADO `luis` autenticado
- CUANDO envía `GET /api/time-entries?memberId={ana}&employeeId={ana}`
- ENTONCES la lista contiene solo fichajes de `luis`

#### Scenario: El responsable no ficha por otro
- DADO `marta` autenticada
- CUANDO envía `POST /api/time-entries/clock-in?memberId={luis}`
- ENTONCES el fichaje creado pertenece a `marta` y `luis` no gana ninguno

### Requirement: Contrato OpenAPI del equipo

El contrato DEBE incluir `get` en `/api/team/members` y `get` en `/api/team/members/{memberId}/time-entries` y ninguna otra operación bajo `/api/team`. El parámetro de ruta DEBE llamarse `memberId` (tipo `uuid`). El documento NO DEBE contener ningún parámetro ni propiedad `employeeId`. Las respuestas declaradas DEBEN ser `200`, `401` y `403` para el listado, y `200`, `401`, `403` y `404` para los fichajes del miembro.

#### Scenario: Rutas y parámetro
- DADO `docs/api/openapi.json` generado
- CUANDO se leen las rutas bajo `/api/team`
- ENTONCES existen solo las dos indicadas, el parámetro se llama `memberId` y no hay operaciones distintas de `get`

#### Scenario: Respuestas declaradas
- DADO el contrato generado
- CUANDO se revisan las respuestas de ambas rutas
- ENTONCES coinciden con las indicadas

#### Scenario: Sin employeeId
- DADO el contrato completo
- CUANDO se busca el texto `employeeId`
- ENTONCES no aparece en ningún parámetro ni esquema

### Requirement: Siembra de usuarios de demostración

Con `Seed:DemoUsers` = `true`, la API DEBE crear, en el arranque, tras aplicar las migraciones y antes de atender peticiones, estos usuarios inventados: `marta.responsable@example.test` (`Manager`), `luis.empleado@example.test` y `ana.empleada@example.test` (`Employee`, con `ManagerId` = el id de Marta) y `pablo.empleado@example.test` (`Employee`, sin responsable), todos con la contraseña `Seed:DemoPassword` almacenada como hash (el valor de demostración se documenta solo en el README: `.env.example` la deja vacía y quien quiera usar la siembra la rellena con esa contraseña). La siembra DEBE ejecutarse después de todas las migraciones al arrancar y antes de que el servidor atienda peticiones; con la bandera activa y la base no disponible, el arranque DEBE fallar. La siembra DEBE ser idempotente por correo: un usuario ya existente NO DEBE modificarse ni duplicarse y los vínculos DEBEN apuntar al id vigente de Marta. Con `Seed:DemoUsers` = `false` (valor por defecto) NO DEBE crear ningún usuario. La contraseña NO DEBE registrarse.

#### Scenario: Siembra activada
- DADO una base migrada vacía y `Seed:DemoUsers` = `true` con una contraseña de al menos 12 caracteres
- CUANDO la API arranca
- ENTONCES existen los cuatro usuarios con los roles y responsables indicados

#### Scenario: Contraseña como hash
- DADO la base sembrada
- CUANDO se leen los `password_hash`
- ENTONCES ninguno contiene la contraseña y cada uno la verifica

#### Scenario: Idempotente
- DADO la siembra ya ejecutada
- CUANDO la API arranca otra vez con la bandera activa
- ENTONCES siguen existiendo cuatro usuarios, con los mismos ids y sin error

#### Scenario: Usuario previo conservado
- DADO `marta.responsable@example.test` ya existente con otro hash
- CUANDO se ejecuta la siembra
- ENTONCES se crean los otros tres, `luis` y `ana` apuntan al id de la `marta` existente y su hash no cambia

#### Scenario: Siembra desactivada por defecto
- DADO una base migrada vacía y `Seed:DemoUsers` sin definir
- CUANDO la API arranca
- ENTONCES la tabla `users` sigue vacía

#### Scenario: Login y equipo con usuarios sembrados
- DADO la base sembrada
- CUANDO `marta` inicia sesión y envía `GET /api/team/members`
- ENTONCES recibe a `ana` y `luis`, y no a `pablo`

#### Scenario: Base no disponible con la siembra activa
- DADO `Seed:DemoUsers` = `true` con una contraseña válida y una cadena de conexión sin servidor accesible
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla y no se atiende ninguna petición

#### Scenario: La contraseña no se registra
- DADO un registrador que captura los mensajes y una contraseña de demostración reconocible
- CUANDO la API arranca con la siembra activa
- ENTONCES ningún mensaje contiene esa contraseña
