# Delta para authentication

Cambio: `asistente-ia`. Recoge dos decisiones del diseño (DD-1 y DD-3) que afectan a la capacidad vigente `authentication`: (1) `UseRateLimiter` pasa a ejecutarse DESPUÉS de `UseAuthorization`, para que la política `assistant` pueda particionar por el claim `sub`; (2) `ICurrentUser` expone además el rol del token. La política `login` (por IP, anónima) y sus requisitos observables no cambian. El pipeline pasa a ser: cabeceras de seguridad, manejador de excepciones, páginas de estado, CORS, autenticación, autorización, limitador de tasa, endpoints. Esto sustituye el orden vigente (limitador antes de la autenticación; motivo original: DD-21 archivada, que el `429` conserve CORS) y se documenta en el ADR 0017.

Notas de verificación:
- `LoginRateLimitTests` DEBEN seguir en verde sin modificar sus expectativas con el nuevo orden (diez intentos permitidos, undécimo `429`, correctos también cuentan, otras rutas sin límite, CORS en el `429`).
- En `WebApplicationFactory` la IP remota es nula y todas las peticiones comparten la misma partición `login`; el aislamiento entre IP distintas no se comprueba de forma automática (sin cambios respecto a la capacidad vigente).
- Las reglas de la política `assistant` (5 por minuto y por `sub`, tope diario de 50, valores configurables) pertenecen a `assistant-endpoint`; aquí solo se fija el orden del pipeline y el contrato del rol.

## ADDED Requirements

### Requirement: Orden del pipeline de seguridad

La API DEBE ejecutar el pipeline en este orden: cabeceras de seguridad, manejador de excepciones, páginas de estado (ProblemDetails), CORS, autenticación, autorización, limitador de tasa y endpoints. CORS DEBE ir antes del limitador y de la autenticación, de modo que las respuestas `401`, `403` y `429` conserven las cabeceras CORS de un origen permitido. El limitador de tasa DEBE ejecutarse después de la autorización, de modo que las políticas por usuario dispongan del principal autenticado. Una petición anónima a un endpoint protegido DEBE recibir `401` sin consumir permisos de ninguna política por usuario. Las respuestas `429` del limitador DEBEN seguir incluyendo las cabeceras de seguridad.

#### Scenario: Anónimo en endpoint con límite por usuario no consume cuota
- DADO muchas peticiones anónimas a `POST /api/asistente`
- CUANDO después un usuario autenticado envía su primera petición
- ENTONCES las anónimas recibieron `401` y la del usuario autenticado no recibe `429`

#### Scenario: 429 con CORS tras el nuevo orden
- DADO `Cors:AllowedOrigins` con `http://localhost:5172` y una petición con `Origin: http://localhost:5172` que recibe `429` (de login o del asistente)
- CUANDO se inspeccionan las cabeceras
- ENTONCES incluyen `Access-Control-Allow-Origin: http://localhost:5172` y las cuatro cabeceras de seguridad

#### Scenario: Preflight sin autenticación ni límite
- DADO `Origin: http://localhost:5172` permitido
- CUANDO se envía `OPTIONS /api/asistente` con `Access-Control-Request-Method: POST` y `Access-Control-Request-Headers: authorization,content-type`
- ENTONCES la respuesta permite el origen y las cabeceras, sin exigir token ni consumir cuota del limitador

### Requirement: Rol del usuario autenticado en el contexto de aplicación

El puerto `ICurrentUser` DEBE exponer, además del identificador del empleado, su rol (`Employee` o `Manager`), leído únicamente del claim `role` del token ya validado (sin traducción de nombres). El adaptador NO DEBE consultar la base de datos ni aceptar el rol desde un parámetro, cuerpo, cabecera o historial de la petición. Si el principal no está autenticado o el claim falta o no es un rol conocido, el adaptador DEBE lanzar una excepción y NO DEBE devolver ningún rol por defecto. El valor del rol NO DEBE aparecer en ningún parámetro de herramienta ni de caso de uso.

#### Scenario: El adaptador lee el claim role
- DADO un contexto HTTP con usuario autenticado cuyo claim `role` es `Manager`
- CUANDO se lee `HttpContextCurrentUser.Role`
- ENTONCES vale `Manager`

#### Scenario: Rol de empleado
- DADO un contexto HTTP con usuario autenticado cuyo claim `role` es `Employee`
- CUANDO se lee `HttpContextCurrentUser.Role`
- ENTONCES vale `Employee`

#### Scenario: Sin usuario autenticado
- DADO un contexto HTTP sin usuario autenticado
- CUANDO se lee `HttpContextCurrentUser.Role`
- ENTONCES se lanza una excepción y no se devuelve ningún rol

#### Scenario: Claim ausente o desconocido
- DADO un contexto HTTP autenticado sin claim `role`, y otro con `role` = `Admin`
- CUANDO se lee `HttpContextCurrentUser.Role`
- ENTONCES ambos lanzan una excepción (en la API real estos tokens ya se rechazan con `401` en la validación)

#### Scenario: El rol no se toma de la petición
- DADO un token de empleado y un cuerpo o cabecera que afirman `role` = `Manager`
- CUANDO se procesa la petición
- ENTONCES `ICurrentUser.Role` vale `Employee`

## MODIFIED Requirements

### Requirement: Límite de intentos de login

`POST /api/auth/login` DEBE limitarse a 10 peticiones por minuto y por IP de origen (ventana fija), contando todos los intentos, correctos o no. La política `login` es anónima y se particiona por IP, por lo que su semántica NO cambia al ejecutarse el limitador después de la autorización (ver «Orden del pipeline de seguridad»). La petición undécima dentro de la ventana DEBE recibir `429` `application/problem+json`, con `status` = `429`, `title` «Demasiadas solicitudes», `detail` en español y cabecera `Retry-After`. Las demás rutas NO DEBEN estar sujetas a la política `login`; las políticas por usuario de otros endpoints (p. ej. `assistant`) NO DEBEN afectar al login ni a la inversa. La respuesta `429` DEBE conservar las cabeceras CORS de un origen permitido.
(Previously: el requisito no precisaba el orden del pipeline; el limitador se ejecutaba antes de la autenticación y solo existía la política `login`. Contenido observable sin cambios: 10 por minuto y por IP, undécimo `429`, CORS en el `429`.)

#### Scenario: Diez intentos permitidos
- DADO una API recién arrancada
- CUANDO se envían 10 logins con contraseña errónea desde la misma IP
- ENTONCES ninguno recibe `429` (todos `401`)

#### Scenario: Undécimo intento
- DADO 10 intentos de login ya realizados en el minuto
- CUANDO se envía el undécimo
- ENTONCES recibe `429` `application/problem+json` con `status` = `429` y cabecera `Retry-After`

#### Scenario: Los intentos correctos también cuentan
- DADO 10 logins correctos en el minuto
- CUANDO se envía otro login con credenciales correctas
- ENTONCES recibe `429`

#### Scenario: Otras rutas sin límite
- DADO un login que ya recibió `429`
- CUANDO se envían 20 `GET /api/auth/me` con un token vigente
- ENTONCES todos reciben `200`

#### Scenario: CORS en el 429
- DADO `Cors:AllowedOrigins` con `http://localhost:5172` y un login que recibe `429` con `Origin: http://localhost:5172`
- CUANDO se inspeccionan las cabeceras
- ENTONCES incluyen `Access-Control-Allow-Origin: http://localhost:5172`

#### Scenario: Login anónimo sigue limitado por IP con el nuevo orden
- DADO el limitador ejecutado después de la autorización y un login anónimo (sin token)
- CUANDO se envían 11 logins desde la misma IP
- ENTONCES el undécimo recibe `429` y los diez primeros no (la política `login` no depende del principal)

#### Scenario: El límite del asistente no cuenta logins
- DADO un usuario que agotó su límite por minuto del asistente
- CUANDO se envía un login
- ENTONCES no recibe `429` por ese motivo

### Requirement: Validación estricta del token

La API DEBE aceptar solo tokens con firma válida HS256 con la clave configurada, emisor `timetracker-api` y audiencia `timetracker-web`, no caducados ni aún no vigentes (con tolerancia máxima de reloj de 30 segundos, evaluada con el `TimeProvider` de la aplicación), con un `sub` que sea un GUID distinto de `Guid.Empty` y con un `role` que sea `Employee` o `Manager`. DEBE rechazar con `401` los tokens ausentes, caducados, con `nbf` futuro, alterados, firmados con otro algoritmo (incluido `none`) o con otra clave, de otro emisor o audiencia, con un `sub` no válido o con un `role` desconocido. Los claims DEBEN leerse sin traducción de nombres (`sub`, `role`). La cabecera `WWW-Authenticate` del `401` NO DEBE incluir `error_description`. Los claims `sub` y `role` ya validados son la única fuente de la identidad y del rol que `ICurrentUser` entrega a Application (ver «Rol del usuario autenticado en el contexto de aplicación»).
(Previously: el requisito no mencionaba que el rol validado alimenta `ICurrentUser`; las reglas de validación y los escenarios no cambian.)

#### Scenario: Token válido
- DADO un token vigente de un empleado
- CUANDO envía `GET /api/auth/me` con `Authorization: Bearer <token>`
- ENTONCES recibe `200`

#### Scenario: Sin token
- DADO una petición sin cabecera `Authorization`
- CUANDO envía `GET /api/auth/me`
- ENTONCES recibe `401`

#### Scenario: Token caducado
- DADO un token cuyo `exp` fue hace 2 minutos
- CUANDO envía `GET /api/auth/me`
- ENTONCES recibe `401`

#### Scenario: Tolerancia de reloj
- DADO un token cuyo `exp` fue hace 10 segundos
- CUANDO envía `GET /api/auth/me`
- ENTONCES recibe `200`

#### Scenario: Token alterado
- DADO un token de empleado en el que se sustituye el payload por otro con `role` = `Manager` conservando la firma original
- CUANDO envía `GET /api/team/members`
- ENTONCES recibe `401`

#### Scenario: Rol desconocido
- DADO un token correctamente firmado y vigente con `role` = `Admin`
- CUANDO envía `GET /api/auth/me`
- ENTONCES recibe `401`

#### Scenario: Token alterado en el endpoint del asistente
- DADO un token de empleado con el payload sustituido por otro con `role` = `Manager` y la firma original
- CUANDO envía `POST /api/asistente`
- ENTONCES recibe `401` y no se ofrece ninguna herramienta de equipo

(Los demás escenarios vigentes del requisito —algoritmo distinto, `sub` no válido, emisor o audiencia ajenos, `nbf` futuro, caducidad según el reloj de la aplicación y sin `error_description`— se conservan sin cambios.)
