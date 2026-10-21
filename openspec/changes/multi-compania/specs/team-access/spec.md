# Delta para team-access

Cambio: `multi-compania`. Capacidad EXISTENTE (`openspec/specs/team-access/spec.md`). Contiene bloques `MODIFIED` (con el nombre exacto del requisito vigente) y `ADDED`.

> Aviso de delta que cambia escenarios vigentes:
> - «Tabla users con restricciones»: la clave foránea del responsable pasa de `manager_id` → `users(id)` a la compuesta `(company_id, manager_id)` → `users(company_id, id)`; se añaden `company_id` y `must_change_password`.
> - Reconciliación `ON DELETE`: la especificación vigente decía `ON DELETE RESTRICT` y el ADR 0011 y la migración generada usan `NO ACTION`. Se elige `NO ACTION` (no diferible) en ambas claves foráneas compuestas, por tres razones: (1) es lo que EF Core genera por defecto y lo que existe hoy en el esquema (`UserConfiguration.cs`: PostgreSQL rechaza el borrado con `23503`; `RESTRICT` lo rechazaría con `23001`, que cambiaría el código SQLSTATE observable de los escenarios vigentes); (2) al no ser diferibles, `NO ACTION` y `RESTRICT` protegen igual en este modelo (no hay cascada ni borrado de usuarios en este cambio); (3) evita una migración que solo cambia la acción referencial. El texto de la especificación se corrige a lo que el sistema hace.
> - «Siembra de usuarios de demostración»: los usuarios demo pasan a la compañía demo y la siembra no puede coexistir con el registro abierto.
> - La nota «Fuera de alcance: administración de usuarios (alta…)» y «FK de `time_entries.employee_id` a `users`» de la cabecera de la especificación vigente dejan de aplicar para el alta (ver `employee-management`) y la FK (ver `time-entries-persistence`); la baja y el cambio de rol siguen fuera de alcance.

## MODIFIED Requirements

### Requirement: Cuenta de usuario en el dominio

`TimeTracker.Domain` DEBE ofrecer `UserAccount.Create(id, companyId, email, displayName, passwordHash, role, managerId, mustChangePassword)` como único modo de construir un usuario (constructor privado). DEBE rechazar con `ArgumentException` o una derivada: `id` igual a `Guid.Empty`; `companyId` igual a `Guid.Empty`; `email` en blanco, sin `@` o de más de 254 caracteres; `displayName` en blanco o de más de 100 caracteres; `passwordHash` en blanco; un `role` no definido en `UserRole`; un `managerId` igual a `Guid.Empty` o igual al propio `id`. El `email` DEBE guardarse sin espacios exteriores y en minúsculas (cultura invariante) y el `displayName` sin espacios exteriores. `managerId` nulo es válido. `CompanyId` y `MustChangePassword` DEBEN exponerse como propiedades inmutables en Domain; el cambio de contraseña DEBE hacerse con una operación de dominio (`ChangePassword(hash)`) que reemplace el hash y ponga `MustChangePassword` en falso, y la regeneración de la contraseña temporal con otra (`RegenerateTemporaryPassword(hash)`) que reemplace el hash y mantenga `MustChangePassword` en `true`.
(Previously: `UserAccount.Create(id, email, displayName, passwordHash, role, managerId)`, sin compañía ni indicador de cambio de contraseña)

#### Scenario: Usuario válido con correo normalizado
- DADO `email` = `  Marta.Responsable@Example.TEST ` y los demás datos válidos
- CUANDO se invoca `UserAccount.Create`
- ENTONCES `Email` es `marta.responsable@example.test` y el resto de propiedades conserva los valores dados, incluidos `CompanyId` y `MustChangePassword`

#### Scenario: Usuario sin responsable
- DADO `managerId` nulo
- CUANDO se invoca `UserAccount.Create`
- ENTONCES se crea el usuario con `ManagerId` nulo

#### Scenario: Identificador vacío
- DADO `id` = `Guid.Empty`
- CUANDO se invoca `UserAccount.Create`
- ENTONCES se lanza `ArgumentException` o una derivada

#### Scenario: Compañía vacía
- DADO `companyId` = `Guid.Empty`
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

#### Scenario: Cambio de contraseña en el dominio
- DADO un usuario con `MustChangePassword` = `true`
- CUANDO se aplica la operación de cambio de contraseña con un hash nuevo no vacío
- ENTONCES `PasswordHash` es el nuevo, `MustChangePassword` es `false` y un hash en blanco lanza `ArgumentException` o una derivada sin modificar el usuario

#### Scenario: Regeneración de la contraseña temporal en el dominio
- DADO un usuario con `MustChangePassword` = `true`
- CUANDO se aplica `RegenerateTemporaryPassword` con un hash nuevo no vacío
- ENTONCES `PasswordHash` es el nuevo, `MustChangePassword` sigue en `true` y un hash en blanco lanza `ArgumentException` o una derivada sin modificar el usuario

### Requirement: Tabla users con restricciones

La base DEBE tener la tabla `users` con `id` (uuid, clave primaria), `company_id` (uuid), `email`, `display_name`, `password_hash`, `role` (texto con el nombre del valor de `UserRole`), `manager_id` (uuid, nulo) y `must_change_password` (booleano, valor por defecto `false`), todas obligatorias salvo `manager_id`. DEBE existir un índice único global sobre `email` (el correo es único en toda la base, no por compañía), un índice único `(company_id, id)`, una clave foránea de `company_id` hacia `companies(id)`, una clave foránea autorreferencial compuesta `(company_id, manager_id)` → `users(company_id, id)` con `NO ACTION` (no diferible; `MATCH SIMPLE`, de modo que un `manager_id` nulo no exige nada), una restricción `CHECK` `manager_id <> id`, una `CHECK` que limite `role` a `Employee` y `Manager` y una `CHECK` que exija `email` en minúsculas (`email = lower(email)`). DEBE existir además un índice sobre `(company_id, manager_id)`. El token de concurrencia de la fila es la columna de sistema `xmin` (no añade columnas; lo usan el cambio de contraseña y la regeneración de la contraseña temporal, ver `employee-management`). El mapeo DEBE ser Fluent en `Infrastructure`; `Domain` NO DEBE referenciar EF Core.
(Previously: columnas `id`, `email`, `display_name`, `password_hash`, `role`, `manager_id`; clave foránea simple `manager_id` → `users(id)` con `ON DELETE RESTRICT`; índice sobre `manager_id`)

#### Scenario: Columnas
- DADO la base migrada
- CUANDO se consulta `information_schema.columns` para `users`
- ENTONCES existen las ocho columnas indicadas, solo `manager_id` admite nulos y `must_change_password` tiene valor por defecto `false`

#### Scenario: Correo duplicado
- DADO un usuario con el correo `luis.empleado@example.test`
- CUANDO se inserta otra fila con el mismo correo, de la misma o de otra compañía
- ENTONCES PostgreSQL la rechaza con `23505`

#### Scenario: Responsable inexistente
- DADO una fila con `manager_id` que no existe en `users`
- CUANDO se inserta
- ENTONCES PostgreSQL la rechaza con `23503`

#### Scenario: Responsable de otra compañía
- DADO un Manager de la compañía B
- CUANDO se inserta un usuario de la compañía A con ese `manager_id`
- ENTONCES PostgreSQL la rechaza con `23503`

#### Scenario: Borrado de un responsable con reportes
- DADO `marta` con `luis` como reporte
- CUANDO se intenta borrar la fila de `marta`
- ENTONCES PostgreSQL lo rechaza con `23503` y `luis` conserva su `manager_id`

#### Scenario: Acción referencial NO ACTION
- DADO la base migrada
- CUANDO se consultan las claves foráneas de `users` en `pg_constraint`
- ENTONCES la autorreferencial compuesta tiene `confdeltype` = `a` (`NO ACTION`) y no es diferible

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

#### Scenario: Compañía obligatoria
- DADO una fila sin `company_id`
- CUANDO se inserta
- ENTONCES PostgreSQL la rechaza con `23502`

#### Scenario: Ida y vuelta de un usuario
- DADO un `UserAccount` de rol `Manager` con `MustChangePassword` = `true` guardado
- CUANDO se lee con un contexto nuevo de su compañía
- ENTONCES `Id`, `CompanyId`, `Email`, `DisplayName`, `PasswordHash`, `Role`, `ManagerId` y `MustChangePassword` coinciden con los guardados

#### Scenario: Token de concurrencia de la fila
- DADO dos contextos que leen la misma fila de `users` y uno de ellos guarda un cambio de contraseña
- CUANDO el otro intenta guardar otro cambio sobre la fila leída
- ENTONCES el segundo guardado falla por concurrencia (se traduce en `409`) y la fila conserva el primer cambio

### Requirement: Siembra de usuarios de demostración

Con `Seed:DemoUsers` = `true`, la API DEBE crear, en el arranque, tras aplicar las migraciones y antes de atender peticiones, estos usuarios inventados dentro de la compañía demo (identificador fijo de la compañía por defecto; la siembra la localiza o la crea de forma idempotente): `marta.responsable@example.test` (`Manager`), `luis.empleado@example.test` y `ana.empleada@example.test` (`Employee`, con `ManagerId` = el id de Marta) y `pablo.empleado@example.test` (`Employee`, sin responsable), todos con `must_change_password` = `false` y la contraseña `Seed:DemoPassword` almacenada como hash (el valor de demostración se documenta solo en el README: `.env.example` la deja vacía y quien quiera usar la siembra la rellena con esa contraseña). La siembra DEBE ejecutarse después de todas las migraciones al arrancar y antes de que el servidor atienda peticiones; con la bandera activa y la base no disponible, el arranque DEBE fallar. La siembra DEBE ser idempotente por correo: un usuario ya existente en la compañía demo NO DEBE modificarse ni duplicarse y los vínculos DEBEN apuntar al id vigente de Marta; un correo demo que ya exista en OTRA compañía DEBE impedir el arranque con un mensaje claro (sin la contraseña). Con `Seed:DemoUsers` = `false` (valor por defecto) NO DEBE crear ningún usuario. La siembra NO PUEDE coexistir con el registro abierto: con `Seed:DemoUsers` = `true` y `Registration:Enabled` = `true` el arranque DEBE fallar (ver `startup-configuration`). La contraseña NO DEBE registrarse. Los usuarios demo NO DEBEN poder ver datos de ninguna otra compañía ni a la inversa.
(Previously: los usuarios se creaban sin compañía; no había restricción frente al registro)

#### Scenario: Siembra activada
- DADO una base migrada vacía y `Seed:DemoUsers` = `true` con una contraseña de al menos 12 caracteres
- CUANDO la API arranca
- ENTONCES existen los cuatro usuarios con los roles y responsables indicados, todos en la compañía demo

#### Scenario: Compañía demo creada una sola vez
- DADO la siembra ejecutada dos veces
- CUANDO se cuentan las compañías con el identificador de la compañía por defecto
- ENTONCES existe exactamente una

#### Scenario: Contraseña como hash
- DADO la base sembrada
- CUANDO se leen los `password_hash`
- ENTONCES ninguno contiene la contraseña y cada uno la verifica

#### Scenario: Idempotente
- DADO la siembra ya ejecutada
- CUANDO la API arranca otra vez con la bandera activa
- ENTONCES siguen existiendo cuatro usuarios, con los mismos ids y sin error

#### Scenario: Usuario previo conservado
- DADO `marta.responsable@example.test` ya existente en la compañía demo con otro hash
- CUANDO se ejecuta la siembra
- ENTONCES se crean los otros tres, `luis` y `ana` apuntan al id de la `marta` existente y su hash no cambia

#### Scenario: Correo demo en otra compañía
- DADO `luis.empleado@example.test` existente en una compañía distinta de la demo
- CUANDO se ejecuta la siembra
- ENTONCES el arranque falla con un mensaje claro que no contiene la contraseña y no se modifica ninguna compañía

#### Scenario: Siembra desactivada por defecto
- DADO una base migrada vacía y `Seed:DemoUsers` sin definir
- CUANDO la API arranca
- ENTONCES la tabla `users` sigue vacía

#### Scenario: Siembra con registro abierto
- DADO `Seed__DemoUsers=true`, `Seed__DemoPassword` válida y `Registration__Enabled=true`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla antes de sembrar y no se crea ningún usuario

#### Scenario: Login y equipo con usuarios sembrados
- DADO la base sembrada
- CUANDO `marta` inicia sesión y envía `GET /api/team/members`
- ENTONCES recibe a `ana` y `luis`, y no a `pablo`

#### Scenario: Demo aislada de otra compañía
- DADO la base sembrada y una compañía B con un Manager y dos Employees
- CUANDO `marta` envía `GET /api/team/members` y el Manager de B envía el suyo
- ENTONCES cada lista contiene solo personas de su compañía

#### Scenario: Base no disponible con la siembra activa
- DADO `Seed:DemoUsers` = `true` con una contraseña válida y una cadena de conexión sin servidor accesible
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla y no se atiende ninguna petición

#### Scenario: La contraseña no se registra
- DADO un registrador que captura los mensajes y una contraseña de demostración reconocible
- CUANDO la API arranca con la siembra activa
- ENTONCES ningún mensaje contiene esa contraseña

## ADDED Requirements

### Requirement: Equipo y fichajes acotados a la compañía

El equipo de un Manager DEBE componerse solo de usuarios de su misma compañía: `GET /api/team/members`, `GET /api/team/members/{memberId}/time-entries`, `IUserRepository` (búsqueda de reportes directos, `FindDirectReportAsync` y nombre visible por identificador) y los casos de uso del equipo DEBEN operar siempre sobre la compañía del token (ver `company-isolation`) y componerse solo de los reportes directos del Manager del token. Un `memberId` de otra compañía DEBE producir exactamente la misma respuesta que uno inexistente (ver «Control por objeto con 404 idéntico»). El rol y el acceso a `/api/team` siguen según «Acceso del equipo solo para el rol Manager». La búsqueda de usuario por correo para el inicio de sesión es global (uso de la lista cerrada de `IgnoreQueryFilters`).

#### Scenario: Equipo solo de la compañía
- DADO un Manager de A con dos reportes y un Manager de B con dos reportes
- CUANDO el Manager de A envía `GET /api/team/members`
- ENTONCES recibe sus dos reportes y ninguna persona de B

#### Scenario: Miembro de otra compañía indistinguible del inexistente
- DADO `ana` de la compañía B
- CUANDO el Manager de A envía `GET /api/team/members/{ana}/time-entries`, y otra vez con un GUID inexistente
- ENTONCES ambas respuestas son `404` `application/problem+json` idénticas (salvo `traceId`)

#### Scenario: Fichajes de un miembro de la compañía
- DADO `luis`, reporte de `marta`, ambos de la misma compañía y con fichajes
- CUANDO `marta` envía `GET /api/team/members/{luis}/time-entries`
- ENTONCES recibe `200` con los fichajes de `luis` y ninguno de otra compañía

#### Scenario: Nombre visible de otra compañía
- DADO un identificador de usuario de la compañía B
- CUANDO el repositorio busca el nombre visible con un contexto de la compañía A
- ENTONCES el resultado es nulo

#### Scenario: Reporte directo con responsable de otra compañía imposible
- DADO un intento de crear un usuario de A cuyo `manager_id` sea de B
- CUANDO se guarda
- ENTONCES falla con `23503` y no existe ningún equipo que cruce compañías

#### Scenario: Sin escritura bajo /api/team
- DADO el contrato regenerado
- CUANDO se leen las operaciones bajo `/api/team`
- ENTONCES siguen existiendo solo las dos operaciones `get` vigentes (el alta de personas vive en `/api/employees`)
