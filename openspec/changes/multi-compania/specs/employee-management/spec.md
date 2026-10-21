# Especificación: employee-management

Cambio: `multi-compania`. Capacidad NUEVA; todos los requisitos son `ADDED`. Define el alta de personas (Employees y Managers) por un Manager dentro de su propia compañía, la contraseña temporal de un solo uso, su regeneración antes del primer acceso, el cambio de contraseña obligatorio en el primer inicio de sesión, el endpoint de cambio de contraseña y el tope de personas por compañía. La política de contraseñas se especifica en `company-registration`; el token y la sesión, en `authentication`; las tablas, en `team-access` y `company-isolation`.

Decisiones firmes: alta por un Manager con contraseña temporal de un solo uso y cambio forzado en el primer inicio (`MustChangePassword`); un Manager puede crear otros Managers solo dentro de su compañía, sin jerarquías nuevas; la contraseña temporal solo la regenera el responsable directo de la persona (mínimo privilegio); topes por compañía configurables por entorno y no editables por el Manager.

Riesgo aceptado y documentado: la contraseña temporal no caduca (no hay caducidad por tiempo; el runbook prevé revisar periódicamente las cuentas con `must_change_password` antigua), y un token `mcp` ya emitido no se invalida al regenerar la contraseña temporal (caduca como máximo a los 30 minutos).

Supuestos de contrato fijados por esta especificación (el diseño puede ajustar nombres de ruta con nota en el diseño): `POST /api/employees` para el alta, `POST /api/employees/{memberId}/temporary-password` para regenerar la contraseña temporal y `POST /api/auth/change-password` para el cambio de contraseña; la contraseña temporal la genera el servidor y se devuelve una sola vez (en el alta o en la regeneración). Los límites de tasa de 30 peticiones por minuto del alta y de la regeneración y los 3 reintentos acotados del tope bajo concurrencia son valores propuestos por el diseño y adoptados aquí como parte del contrato.

Notas de verificación:
- «Respuesta idéntica» significa mismo estado, tipo de medio (`application/problem+json`, sin parámetros), `type`, `title` y `detail`, salvo `traceId`.
- Las contraseñas de las pruebas son valores inventados y reconocibles.

Fuera de alcance: baja, edición y cambio de rol de personas, restablecimiento de contraseña por correo y cualquier restablecimiento por un Manager distinto de la regeneración de la contraseña temporal antes del primer acceso (ver «Regenerar la contraseña temporal»), caducidad de la contraseña temporal, listado administrativo de todas las personas de la compañía, jerarquías de más de un nivel, importación masiva y supresión y exportación de datos (RGPD, posposición explícita).

## ADDED Requirements

### Requirement: Alta de personas por un Manager

`POST /api/employees` DEBE exigir autenticación y la política de Manager (sin token `401`; con rol distinto de `Manager` `403`, antes de evaluar el cuerpo). El cuerpo DEBE aceptar `displayName` (1 a 100 caracteres tras recortar), `email` (válido y normalizado según `UserAccount`), `role` (`Employee` o `Manager`; por defecto `Employee`) y `managerId` (opcional). La persona DEBE crearse siempre en la compañía del token, con `must_change_password` = `true`. `managerId` por defecto DEBE ser el Manager que crea; si se indica otro, DEBE ser un usuario con rol `Manager` de la misma compañía; un `managerId` de otra compañía y uno inexistente DEBEN producir exactamente la misma respuesta `400` con error en el campo `managerId`. La respuesta `201` DEBE incluir `id`, `displayName`, `email`, `role` y `temporaryPassword` (ver «Contraseña temporal de un solo uso») y NO DEBE incluir el hash ni la compañía. Las propiedades desconocidas (por ejemplo `companyId`, `passwordHash`) NO DEBEN tener efecto. Un correo ya existente en cualquier compañía DEBE responder `409` con un `detail` genérico que no indique la compañía ni el rol. El alta DEBE limitarse a 30 peticiones por minuto y por usuario autenticado (política `employee-create`; `429` al superarlo, con `Retry-After` y cabeceras CORS).

#### Scenario: Límite de tasa del alta
- DADO un Manager con 30 altas (correctas o no) en el último minuto
- CUANDO envía la siguiente
- ENTONCES recibe `429` con `Retry-After` y no se crea nada

#### Scenario: Manager da de alta a un Employee
- DADO un Manager de la compañía A autenticado
- CUANDO envía `POST /api/employees` con `displayName`, `email` y sin `role`
- ENTONCES recibe `201`, la persona es `Employee` de la compañía A con `manager_id` = el Manager, `must_change_password` = `true` y aparece en `GET /api/team/members` del Manager

#### Scenario: Anónimo y Employee
- DADO una petición sin token y otra con un token de `Employee`
- CUANDO envían el alta
- ENTONCES reciben `401` y `403` respectivamente y no se crea nada

#### Scenario: Compañía siempre la del token
- DADO un Manager de A y un cuerpo con `companyId` de la compañía B
- CUANDO envía el alta
- ENTONCES la persona se crea en A y B no cambia

#### Scenario: Responsable de otra compañía
- DADO un `managerId` de un Manager de B, y por separado un `managerId` inexistente
- CUANDO el Manager de A envía el alta con cada uno
- ENTONCES ambas respuestas son `400` idénticas con error en `managerId` y no se crea nada

#### Scenario: Responsable de la misma compañía
- DADO un `managerId` de otro Manager de A
- CUANDO el Manager de A envía el alta
- ENTONCES la persona se crea con ese responsable

#### Scenario: Correo ya existente
- DADO `luis@example.test` en la compañía B
- CUANDO el Manager de A da de alta ese correo
- ENTONCES recibe `409` con un `detail` que no nombra ninguna compañía y no se crea nada

#### Scenario: Datos inválidos
- DADO `displayName` en blanco, `email` sin `@` y `role` = `Admin`, cada uno por separado
- CUANDO se envían
- ENTONCES cada petición recibe `400` con el campo afectado y no se crea nada

#### Scenario: Sin hash ni compañía en la respuesta
- DADO un alta correcta
- CUANDO se inspecciona el cuerpo
- ENTONCES no contiene `passwordHash`, `companyId` ni el hash almacenado

### Requirement: Creación de otros Managers solo dentro de la compañía

Un Manager PUEDE crear personas con `role` = `Manager` únicamente en su propia compañía, sin introducir jerarquías nuevas: el equipo sigue siendo el de reportes directos (ver `team-access`). Un Manager nuevo NO DEBE adquirir acceso alguno a datos de otra compañía ni a la configuración del asistente de otra compañía; sí PUEDE ver y editar la configuración del asistente de la suya (riesgo aceptado y documentado: cualquier Manager de la compañía edita esa configuración).

#### Scenario: Nuevo Manager
- DADO un Manager de A que crea otro Manager
- CUANDO el nuevo Manager cambia su contraseña e inicia sesión
- ENTONCES su token tiene `role` = `Manager` y el `cid` de A

#### Scenario: Sin acceso a otra compañía
- DADO el nuevo Manager de A
- CUANDO envía `GET /api/team/members` y `GET /api/asistente/configuracion`
- ENTONCES recibe solo datos de A y nunca de B

#### Scenario: Sin jerarquía transitiva
- DADO un Manager `m1` que crea un Manager `m2` con `managerId` = `m1`, y `m2` que crea un Employee `e`
- CUANDO `m1` consulta su equipo
- ENTONCES contiene a `m2` y no a `e`

### Requirement: Contraseña temporal de un solo uso

El servidor DEBE generar la contraseña temporal con un generador criptográficamente seguro, de al menos 16 caracteres y que cumpla la política de contraseñas. DEBE devolverse una sola vez en la respuesta `201` del alta o en la `200` de la regeneración (ambas con `Cache-Control: no-store`) y almacenarse solo como hash. NO DEBE persistirse en claro, registrarse, aparecer en filas de auditoría ni devolverse en ninguna otra respuesta. Solo sirve para iniciar sesión y cambiarla (ver «Cambio de contraseña obligatorio»); tras el cambio, la contraseña temporal DEBE dejar de ser válida.

#### Scenario: Cumple la política
- DADO 50 altas consecutivas
- CUANDO se leen las contraseñas temporales devueltas
- ENTONCES todas tienen al menos 16 caracteres, cumplen la política y son distintas entre sí

#### Scenario: Solo como hash
- DADO un alta correcta
- CUANDO se lee `password_hash`
- ENTONCES no contiene la contraseña temporal y la verifica

#### Scenario: Una sola vez
- DADO un alta correcta y un registrador que captura los mensajes
- CUANDO se inspeccionan el registro y las demás respuestas de la API
- ENTONCES la contraseña temporal solo aparece en la respuesta `201` del alta (o, tras regenerarla, en la `200` de la regeneración)

#### Scenario: Inválida tras el cambio
- DADO una persona que cambió su contraseña
- CUANDO inicia sesión con la temporal
- ENTONCES recibe el `401` genérico de credenciales no válidas

### Requirement: Regenerar la contraseña temporal

`POST /api/employees/{memberId}/temporary-password` DEBE exigir autenticación y la política de Manager (sin token `401`; con rol distinto de `Manager` `403`). Solo PUEDE regenerar la contraseña temporal de una persona que sea reporte directo suyo (la misma relación que compone su equipo, ver `team-access`; mínimo privilegio), y solo mientras esa persona tenga `must_change_password` = `true` (aún no ha cambiado la contraseña temporal; es el criterio de «antes del primer acceso»). Un `memberId` de otra compañía, uno inexistente y el de una persona de su compañía que no sea reporte directo suyo (por ejemplo, de otro Manager) DEBEN producir exactamente la misma respuesta `404` `application/problem+json` (ver `company-isolation`). Si la persona ya cambió su contraseña, DEBE responder `409` sin modificar nada. La actualización del hash DEBE protegerse con el token de concurrencia de la fila del usuario (`xmin`): si la fila cambió entre su lectura y el guardado (por ejemplo, la persona cambió su contraseña entre tanto), DEBE responder `409` sin modificar nada y sin pisar la contraseña elegida por la persona. Con éxito, el servidor DEBE generar una contraseña temporal nueva con las mismas reglas de «Contraseña temporal de un solo uso», sustituir el hash (manteniendo `must_change_password` = `true`) y responder `200` con `{ temporaryPassword }` y `Cache-Control: no-store`, una sola vez; la contraseña anterior DEBE dejar de ser válida. La operación DEBE limitarse a 30 peticiones por minuto y por usuario autenticado (política propia; `429` al superarlo, con `Retry-After` y cabeceras CORS). DEBE registrar únicamente metadatos (instante, resultado, identificadores del Manager y de la persona) y NUNCA la contraseña, ni anterior ni nueva, en registros ni en filas de auditoría. La petición NO DEBE aceptar compañía ni contraseña del cliente.

#### Scenario: Regeneración correcta
- DADO una persona de la compañía A con cambio pendiente y su Manager de A
- CUANDO el Manager envía `POST /api/employees/{memberId}/temporary-password`
- ENTONCES recibe `200` con `temporaryPassword` y `Cache-Control: no-store`, la contraseña nueva cumple la política (16 caracteres o más), `must_change_password` sigue en `true`, la persona inicia sesión con la nueva y ya no con la anterior

#### Scenario: Persona que ya cambió su contraseña
- DADO una persona con `must_change_password` = `false`
- CUANDO su Manager envía la regeneración
- ENTONCES recibe `409` y el hash no cambia

#### Scenario: Persona de otra compañía o inexistente
- DADO una persona con cambio pendiente de la compañía B y, por separado, un `memberId` inexistente
- CUANDO el Manager de A envía la regeneración con cada uno
- ENTONCES ambas respuestas son `404` idénticas (salvo `traceId`) y el hash de la persona de B no cambia

#### Scenario: Manager que no es el responsable directo
- DADO una persona con cambio pendiente cuyo responsable es el Manager `m1` de la compañía A y otro Manager `m2` de la misma compañía A
- CUANDO `m2` envía la regeneración para esa persona
- ENTONCES recibe el mismo `404` que para un `memberId` inexistente y el hash no cambia

#### Scenario: Carrera con el cambio de contraseña de la persona
- DADO una persona con cambio pendiente cuyo Manager ha leído su fila para regenerar y que cambia su contraseña antes de que el Manager guarde
- CUANDO el Manager guarda la contraseña temporal nueva
- ENTONCES recibe `409`, el hash conserva la contraseña elegida por la persona y `must_change_password` sigue en `false`

#### Scenario: Anónimo y Employee
- DADO una petición sin token y otra con un token de `Employee`
- CUANDO envían la regeneración
- ENTONCES reciben `401` y `403` respectivamente y no cambia ningún hash

#### Scenario: Una sola vez y sin registro
- DADO una regeneración correcta y un registrador que captura los mensajes
- CUANDO se inspeccionan el registro y las demás respuestas de la API
- ENTONCES la contraseña nueva solo aparece en la respuesta `200` de la regeneración y ningún mensaje contiene la contraseña anterior ni la nueva

#### Scenario: Límite de tasa de la regeneración
- DADO un Manager con 30 peticiones de regeneración en el último minuto
- CUANDO envía otra
- ENTONCES recibe `429` con `Retry-After` y no cambia ningún hash

### Requirement: Cambio de contraseña obligatorio en el primer inicio de sesión

Mientras `must_change_password` sea `true`, el inicio de sesión DEBE emitir un token con el claim `mcp` = `true` y `user.mustChangePassword` = `true` en la respuesta, y la API DEBE rechazar con `403` `application/problem+json` (`title` «Acceso denegado») toda petición autenticada con ese claim salvo `GET /api/auth/me` y `POST /api/auth/change-password`. El claim `mcp` DEBE ser la única fuente de esta decisión (sin consultar la base de datos por petición). Un token sin `mcp` o con `mcp` distinto de `true` DEBE tratarse como sin cambio pendiente. El asistente NO DEBE responder a una persona con cambio pendiente.

#### Scenario: Primer inicio de sesión
- DADO una persona recién dada de alta
- CUANDO inicia sesión con la contraseña temporal
- ENTONCES recibe `200` con `user.mustChangePassword` = `true` y el token lleva `mcp` = `true`

#### Scenario: Acceso restringido
- DADO el token con `mcp` = `true`
- CUANDO envía `GET /api/time-entries`, `GET /api/team/members` y `POST /api/asistente`
- ENTONCES las tres reciben `403` `application/problem+json` y no se ejecuta ningún caso de uso

#### Scenario: Endpoints permitidos
- DADO el mismo token
- CUANDO envía `GET /api/auth/me` y `POST /api/auth/change-password`
- ENTONCES no reciben `403` por este motivo

#### Scenario: Persona sin cambio pendiente
- DADO un usuario con `must_change_password` = `false`
- CUANDO inicia sesión
- ENTONCES el token no lleva `mcp` y `user.mustChangePassword` es `false`

### Requirement: Endpoint de cambio de contraseña

`POST /api/auth/change-password` DEBE exigir autenticación (sin token `401`) y aceptar `currentPassword` y `newPassword`. Antes de cambiar nada DEBE comprobar que la compañía del usuario está `Active` (si no, el mismo `401` genérico que el inicio de sesión, ver `authentication`). DEBE verificar `currentPassword` contra el hash del usuario del token; si no coincide, DEBE responder `400` con error en el campo `currentPassword` y NO DEBE cambiar nada. `newPassword` DEBE cumplir la política de contraseñas y ser distinta de `currentPassword` (`400` con error en `newPassword`). La actualización DEBE protegerse con el token de concurrencia de la fila del usuario (`xmin`): si la fila cambió entre su lectura y el guardado (por ejemplo, un Manager regeneró la contraseña temporal entre tanto), DEBE responder `409` sin modificar nada. Con éxito DEBE actualizar el hash, poner `must_change_password` = `false` y responder `200` con el mismo cuerpo que el inicio de sesión (`{ accessToken, expiresAt, user }`) con un token nuevo sin `mcp`. NO DEBE aceptar identificadores de persona ni de compañía del cliente. DEBE limitarse a 5 peticiones por minuto y por usuario autenticado (política `change-password`; `429` al superarlo, con `Retry-After` y cabeceras CORS). Ni la contraseña actual ni la nueva DEBEN aparecer en respuestas, registros ni en el contrato OpenAPI como valores.

#### Scenario: Cambio correcto
- DADO una persona con cambio pendiente y su contraseña temporal
- CUANDO envía `POST /api/auth/change-password` con una `newPassword` válida
- ENTONCES recibe `200` con un token sin `mcp`, `must_change_password` pasa a `false` y puede usar `GET /api/time-entries`

#### Scenario: Contraseña actual incorrecta
- DADO una persona autenticada
- CUANDO envía una `currentPassword` errónea
- ENTONCES recibe `400` con el campo `currentPassword`, el hash no cambia y `must_change_password` no cambia

#### Scenario: Nueva contraseña inválida
- DADO una `newPassword` de 11 caracteres, otra de 129 y otra igual a la actual
- CUANDO se envían por separado
- ENTONCES cada petición recibe `400` con el campo `newPassword` y nada cambia

#### Scenario: Sin token
- DADO una petición sin cabecera `Authorization`
- CUANDO se envía
- ENTONCES recibe `401`

#### Scenario: Compañía suspendida
- DADO una persona con sesión vigente cuya compañía está `Suspended`
- CUANDO envía un cambio de contraseña correcto
- ENTONCES recibe el `401` genérico, no se emite token y el hash no cambia

#### Scenario: Conflicto de concurrencia
- DADO una persona cuyo Manager regenera su contraseña temporal entre la lectura de su fila y el guardado de su cambio
- CUANDO se guarda el cambio de la persona
- ENTONCES recibe `409`, no se pisa la contraseña temporal nueva y `must_change_password` no cambia

#### Scenario: Límite de tasa
- DADO un usuario con 5 intentos en el último minuto
- CUANDO envía el sexto
- ENTONCES recibe `429` con `Retry-After`

#### Scenario: Sin eco de contraseñas
- DADO un registrador y contraseñas reconocibles
- CUANDO se envían cambios correctos e incorrectos
- ENTONCES ni las respuestas ni los registros contienen esos valores

#### Scenario: Sin identificadores del cliente
- DADO un cuerpo con `userId` de otra persona y `companyId`
- CUANDO se envía
- ENTONCES el cambio se aplica solo al usuario del token y el resto no cambia

### Requirement: Tope de personas por compañía

El número de usuarios de una compañía (Employees y Managers) NO DEBE superar `Company:MaxEmployees` (configurable por entorno; ver `startup-configuration`; el Manager NO puede editarlo). Un alta que lo superaría DEBE responder `409` `application/problem+json` con un `detail` en español propio de este caso (distinto del de correo repetido) y no crear nada. El tope DEBE respetarse bajo concurrencia: con el tope a una persona de ser alcanzado y varias altas simultáneas, exactamente una DEBE tener éxito. La implementación PUEDE reintentar el alta ante un conflicto de serialización de la base de datos, con un máximo de 3 reintentos; agotados sin resolverse, el resultado DEBE seguir respetando el tope (nunca más usuarios que el tope) y NO DEBE ser un `500` con datos internos. El tope de una compañía NO DEBE afectar a otra.

#### Scenario: Tope alcanzado
- DADO `Company__MaxEmployees=3` y una compañía con 3 usuarios
- CUANDO su Manager da de alta a otra persona
- ENTONCES recibe `409` con el `detail` del tope y no se crea nada

#### Scenario: Tope en el límite
- DADO `Company__MaxEmployees=3` y una compañía con 2 usuarios
- CUANDO se da de alta a una persona
- ENTONCES se crea y el recuento es 3

#### Scenario: Carrera en el último puesto
- DADO una compañía a un usuario del tope y 5 altas simultáneas con correos distintos
- CUANDO terminan
- ENTONCES hay exactamente un `201`, cuatro `409` y el recuento no supera el tope

#### Scenario: Otra compañía no afectada
- DADO la compañía A en el tope y la compañía B por debajo
- CUANDO el Manager de B da de alta a una persona
- ENTONCES recibe `201`

#### Scenario: El Manager no edita el tope
- DADO el contrato OpenAPI
- CUANDO se buscan operaciones que cambien el tope
- ENTONCES no existe ninguna

### Requirement: Contrato OpenAPI del alta y del cambio de contraseña

`docs/api/openapi.json` DEBE incluir `post` en `/api/employees` (respuestas `201`, `400`, `401`, `403`, `409` y `429`), `post` en `/api/employees/{memberId}/temporary-password` (`200`, `401`, `403`, `404`, `409` y `429`) y `post` en `/api/auth/change-password` (`200`, `400`, `401`, `403`, `409`, `429`), con la exigencia Bearer, y NO DEBE contener parámetros ni propiedades `companyId`, `passwordHash` ni `employeeId` en esas operaciones (el `memberId` de la ruta de regeneración sí es válido). Los esquemas de respuesta del alta y de la regeneración DEBEN ser los únicos que contienen `temporaryPassword`.

#### Scenario: Rutas y respuestas
- DADO el contrato regenerado
- CUANDO se leen ambas rutas
- ENTONCES existen solo `post`, con Bearer y las respuestas indicadas

#### Scenario: Ruta de regeneración
- DADO el contrato regenerado
- CUANDO se lee `/api/employees/{memberId}/temporary-password`
- ENTONCES existe solo `post`, con Bearer y las respuestas `200`, `401`, `403`, `404`, `409` y `429`

#### Scenario: Sin campos prohibidos
- DADO el contrato regenerado
- CUANDO se buscan `companyId`, `passwordHash` y `employeeId` en esas operaciones
- ENTONCES no aparecen

#### Scenario: Esquemas con contraseña temporal
- DADO el contrato regenerado
- CUANDO se busca `temporaryPassword`
- ENTONCES aparece solo en los esquemas de respuesta del alta y de la regeneración
