# Especificación: authentication

## Purpose

Define la autenticación de `TimeTracker.Api`: inicio de sesión con correo y contraseña, almacenamiento seguro de contraseñas (PBKDF2 mediante `PasswordHasher`), emisión y validación estricta de un JWT HS256 de 30 minutos, consulta de la sesión (`GET /api/auth/me`), política de respaldo autenticada, límite de intentos de login, cabeceras de seguridad, errores de acceso como ProblemDetails con CORS y exposición del documento OpenAPI solo en `Development`. Capacidad NUEVA; todos los requisitos son `ADDED`. La autorización por rol y por objeto del equipo se especifica en `team-access`; la validación de arranque de `Jwt:SigningKey` y de `Seed:*`, en `startup-configuration`; la sesión en la Web, en `web-session`.

Decisiones aceptadas (D1 a D5, D12 a D15 de la exploración): implementación mínima propia sin ASP.NET Core Identity ni cookies; sin refresh token (desviación consciente de AGENTS §6, seguimiento en el ADR 0011); duración fija de 30 minutos; tolerancia de reloj de 30 segundos; comparación de tipos de medio solo por el tipo (`application/json`, `application/problem+json`), sin parámetros como `charset`.

Notas de verificación:
- En `WebApplicationFactory` la IP remota es nula y todas las peticiones comparten la misma partición del limitador; el aislamiento entre IP distintas y el restablecimiento de la ventana de un minuto no se comprueban de forma automática (el segundo exigiría esperar 60 s).
- La vigencia del token se evalúa con el `TimeProvider` de la aplicación (en pruebas, un `FakeTimeProvider`), no con el reloj del sistema; `TestTokens` fabrica los tokens de forma independiente del emisor de producción y con una vida larga por defecto, para que avanzar el reloj en las pruebas de fichaje no los caduque.
- Los cuerpos ProblemDetails incluyen un campo `traceId` distinto en cada petición; «cuerpo idéntico» significa idéntico salvo `traceId`.
- Las pruebas que pasen a la primera (p. ej. límite exacto de 10 intentos, tolerancia de 30 s) DEBEN registrarse como verdes a la primera, con su rojo demostrado por mutación o justificado.

Fuera de alcance: refresh token, 2FA, registro público, recuperación de contraseña, cierre de sesión en servidor o revocación de tokens, cookies, `ForwardedHeaders` (seguimiento antes de desplegar tras un proxy) y HSTS/HTTPS (TLS en el ingreso).

## ADDED Requirements

### Requirement: Almacenamiento y verificación de contraseñas

La contraseña DEBE almacenarse solo como hash PBKDF2 con sal aleatoria generado con `PasswordHasher` tras el puerto `IPasswordHasher` (Application) y su adaptador en `Api`. La contraseña en claro NO DEBE persistirse, registrarse ni devolverse en ninguna respuesta. La verificación DEBE devolver falso, sin lanzar excepción, para una contraseña incorrecta.

#### Scenario: El hash no contiene la contraseña
- DADO la contraseña ficticia `clave-de-prueba-123`
- CUANDO se genera su hash
- ENTONCES el hash no es vacío ni contiene la contraseña, y la verificación con esa contraseña es correcta

#### Scenario: Contraseña incorrecta
- DADO el hash de `clave-de-prueba-123`
- CUANDO se verifica con `otra-clave-456`
- ENTONCES el resultado es falso y no se lanza ninguna excepción

#### Scenario: Sal aleatoria
- DADO la misma contraseña hasheada dos veces
- CUANDO se comparan los dos hashes
- ENTONCES son distintos y ambos verifican la contraseña

### Requirement: Inicio de sesión

`POST /api/auth/login` DEBE ser anónimo y aceptar un cuerpo JSON con `email` y `password`. Con credenciales correctas DEBE responder `200` con `application/json` y un cuerpo `{ "accessToken", "expiresAt", "user" }`, donde `expiresAt` es el instante de caducidad en ISO 8601 UTC con sufijo `Z` y `user` contiene `id`, `email`, `displayName` y `role`. El correo DEBE normalizarse (espacios exteriores eliminados, minúsculas) antes de buscarlo. La respuesta NO DEBE contener el hash ni la contraseña. Un `email` o una `password` ausentes o en blanco, un correo de más de 254 caracteres o una contraseña de más de 128 NO DEBEN provocar ninguna consulta de usuarios ni verificación de contraseña y DEBEN producir la misma respuesta `401` que unas credenciales erróneas (ver «Credenciales no válidas indistinguibles»). Solo un cuerpo ausente o ilegible (JSON roto) DEBE responder `400` `application/problem+json`.

#### Scenario: Inicio de sesión correcto
- DADO el usuario `luis.empleado@example.test` existente (sembrado con la contraseña de `Seed:DemoPassword` o creado en la prueba) y una petición sin cabecera `Authorization`
- CUANDO envía `POST /api/auth/login` con su correo y contraseña correctos
- ENTONCES recibe `200`, `application/json`, `accessToken` no vacío, `expiresAt` terminado en `Z` y `user.role` = `Employee`

#### Scenario: Correo normalizado
- DADO el usuario `luis.empleado@example.test`
- CUANDO envía el login con `  Luis.Empleado@Example.TEST  ` y la contraseña correcta
- ENTONCES recibe `200` y `user.email` es `luis.empleado@example.test`

#### Scenario: Sin hash en la respuesta
- DADO un login correcto
- CUANDO se inspecciona el cuerpo
- ENTONCES no contiene `passwordHash`, el hash almacenado ni la contraseña enviada

#### Scenario: Cuerpo incompleto
- DADO un cuerpo con `email` en blanco, otro con `password` ausente, otro con un correo de 255 caracteres y otro con una contraseña de 129
- CUANDO se envían a `POST /api/auth/login`
- ENTONCES todos reciben el mismo `401` `application/problem+json` que unas credenciales erróneas y no se emite ningún token

#### Scenario: Entrada inválida sin consulta
- DADO un `LoginCommand` con correo en blanco, correo de 255 caracteres o contraseña de 129 caracteres, un repositorio de usuarios falso y un `IPasswordHasher` falso
- CUANDO se ejecuta `LoginUseCase`
- ENTONCES el resultado es nulo y ni el repositorio ni el hasher reciben ninguna llamada

#### Scenario: Cuerpo ausente o ilegible
- DADO una petición `POST /api/auth/login` sin cuerpo, y otra con un JSON roto
- CUANDO se envían
- ENTONCES ambas reciben `400` `application/problem+json` y no se emite ningún token

### Requirement: Credenciales no válidas indistinguibles

Un correo inexistente y una contraseña errónea DEBEN producir exactamente la misma respuesta `401` `application/problem+json`, con el mismo `title` («Credenciales no válidas»), `detail` (en español, genérico) y `type`, sin indicar cuál de los dos datos falló. Con un correo inexistente (y forma válida), el caso de uso DEBE invocar la verificación de contraseña una vez con hash nulo, de modo que el adaptador la haga contra un hash ficticio y se iguale el trabajo realizado. Un hash almacenado ilegible NO DEBE producir un `500`: DEBE tratarse como credenciales no válidas.

#### Scenario: Correo inexistente
- DADO que `nadie@example.test` no existe
- CUANDO envía el login con ese correo
- ENTONCES recibe `401` `application/problem+json`

#### Scenario: Contraseña errónea
- DADO el usuario `luis.empleado@example.test`
- CUANDO envía el login con una contraseña incorrecta
- ENTONCES recibe `401` `application/problem+json`

#### Scenario: Respuestas idénticas
- DADO un login con correo inexistente y otro con contraseña errónea
- CUANDO se comparan ambas respuestas
- ENTONCES coinciden estado, tipo de medio, `type`, `title` y `detail` (salvo `traceId`)

#### Scenario: Verificación ficticia
- DADO un `IPasswordHasher` falso que cuenta llamadas y un correo inexistente de forma válida
- CUANDO se ejecuta `LoginUseCase`
- ENTONCES `Verify` se llama exactamente una vez con hash nulo, el resultado es nulo y no se emite token

#### Scenario: Hash almacenado ilegible
- DADO un usuario cuyo `password_hash` no es un hash válido
- CUANDO envía el login con cualquier contraseña
- ENTONCES recibe el mismo `401` genérico y no un `500`

### Requirement: Token de acceso JWT

El token emitido DEBE ser un JWT firmado con HS256 que contenga los claims `sub` (id del usuario), `name` (nombre visible), `email` (en minúsculas), `role` (`Employee` o `Manager`), `jti` (único por token), `iat`, `nbf` y `exp`, con emisor (`iss`) `timetracker-api` y audiencia (`aud`) `timetracker-web`. `exp` DEBE ser `iat` + 30 minutos. `expiresAt` de la respuesta DEBE corresponder a `exp`. El token NO DEBE contener hash ni contraseña.

#### Scenario: Claims del token
- DADO un login correcto de `marta.responsable@example.test` (responsable)
- CUANDO se decodifica el token
- ENTONCES contiene `sub` (GUID del usuario), `name`, `email`, `role` = `Manager`, `jti`, `iat`, `nbf`, `exp`, `iss` = `timetracker-api` y `aud` = `timetracker-web`, y la cabecera indica `alg` = `HS256`

#### Scenario: Vigencia de 30 minutos
- DADO un `FakeTimeProvider` en `2026-10-12T08:00:00Z`
- CUANDO se emite un token
- ENTONCES `exp` es `2026-10-12T08:30:00Z` y `expiresAt` es `2026-10-12T08:30:00Z`

#### Scenario: Identificador único por token
- DADO dos logins correctos consecutivos del mismo usuario
- CUANDO se comparan los tokens
- ENTONCES sus `jti` son distintos

### Requirement: Validación estricta del token

La API DEBE aceptar solo tokens con firma válida HS256 con la clave configurada, emisor `timetracker-api` y audiencia `timetracker-web`, no caducados ni aún no vigentes (con tolerancia máxima de reloj de 30 segundos, evaluada con el `TimeProvider` de la aplicación), con un `sub` que sea un GUID distinto de `Guid.Empty` y con un `role` que sea `Employee` o `Manager`. DEBE rechazar con `401` los tokens ausentes, caducados, con `nbf` futuro, alterados, firmados con otro algoritmo (incluido `none`) o con otra clave, de otro emisor o audiencia, con un `sub` no válido o con un `role` desconocido. Los claims DEBEN leerse sin traducción de nombres (`sub`, `role`). La cabecera `WWW-Authenticate` del `401` NO DEBE incluir `error_description`. Los claims `sub` y `role` ya validados son la única fuente de la identidad y del rol que `ICurrentUser` entrega a Application (ver «Rol del usuario autenticado en el contexto de aplicación»).

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

#### Scenario: Algoritmo distinto
- DADO un token firmado con HS512 con la misma clave, y otro con `alg` = `none` sin firma
- CUANDO se envían a `GET /api/auth/me`
- ENTONCES ambos reciben `401`

#### Scenario: Sub no válido
- DADO un token correctamente firmado cuyo `sub` es `no-es-un-guid`, y otro con `sub` = `00000000-0000-0000-0000-000000000000`
- CUANDO se envían a `GET /api/time-entries`
- ENTONCES ambos reciben `401`

#### Scenario: Emisor o audiencia ajenos
- DADO un token correctamente firmado con un emisor distinto de `timetracker-api`, y otro con una audiencia distinta de `timetracker-web`
- CUANDO se envían a `GET /api/auth/me`
- ENTONCES ambos reciben `401`

#### Scenario: Token aún no vigente
- DADO un token correctamente firmado cuyo `nbf` es 5 minutos posterior al reloj de la aplicación
- CUANDO envía `GET /api/auth/me`
- ENTONCES recibe `401`

#### Scenario: Caducidad según el reloj de la aplicación
- DADO un token obtenido con un login y un `FakeTimeProvider` avanzado 31 minutos
- CUANDO envía `GET /api/auth/me`
- ENTONCES recibe `401`

#### Scenario: Sin detalle del error en la cabecera
- DADO una petición con un token caducado
- CUANDO se inspecciona `WWW-Authenticate` de la respuesta `401`
- ENTONCES contiene `Bearer` y no contiene `error_description`

### Requirement: Firma con la clave configurada

Los tokens DEBEN firmarse y validarse con `Jwt:SigningKey` (validada al arrancar según `startup-configuration`). La clave NO DEBE aparecer en respuestas, en el documento OpenAPI ni en los registros.

#### Scenario: Token firmado con otra clave
- DADO un token vigente firmado con una clave distinta de 32 bytes
- CUANDO envía `GET /api/auth/me`
- ENTONCES recibe `401`

#### Scenario: La clave no se filtra
- DADO una `Jwt:SigningKey` de prueba reconocible y un registrador que captura los mensajes
- CUANDO se arranca la API, se hace un login, una petición con `401` y se descarga el documento OpenAPI
- ENTONCES ni las respuestas ni los registros contienen el valor de la clave

### Requirement: Consulta de la sesión actual

`GET /api/auth/me` DEBE devolver `200` con `id`, `email`, `displayName` y `role` tomados únicamente de los claims del token, sin consultar la base de datos. Sin token DEBE responder `401`.

#### Scenario: Datos del token
- DADO un token de `luis.empleado@example.test` con rol `Employee`
- CUANDO envía `GET /api/auth/me`
- ENTONCES el cuerpo contiene su `id`, `email`, `displayName` y `role` = `Employee`

#### Scenario: Sin base de datos
- DADO `ConnectionStrings:Default` apuntando a un servidor inexistente y un token vigente
- CUANDO envía `GET /api/auth/me`
- ENTONCES recibe `200`

#### Scenario: Rol de responsable
- DADO un token con `role` = `Manager`
- CUANDO envía `GET /api/auth/me`
- ENTONCES `role` es `Manager`

### Requirement: Límite de intentos de login

`POST /api/auth/login` DEBE limitarse a 10 peticiones por minuto y por IP de origen (ventana fija), contando todos los intentos, correctos o no. La política `login` es anónima y se particiona por IP, por lo que su semántica NO cambia al ejecutarse el limitador después de la autorización (ver «Orden del pipeline de seguridad»). La petición undécima dentro de la ventana DEBE recibir `429` `application/problem+json`, con `status` = `429`, `title` «Demasiadas solicitudes», `detail` en español y cabecera `Retry-After`. Las demás rutas NO DEBEN estar sujetas a la política `login`; las políticas por usuario de otros endpoints (p. ej. `assistant`) NO DEBEN afectar al login ni a la inversa. La respuesta `429` DEBE conservar las cabeceras CORS de un origen permitido.

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

### Requirement: Cabeceras de seguridad

Todas las respuestas de la API, incluidos los errores `401`, `404`, `429` y `500`, DEBEN incluir `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer` y un `Content-Security-Policy` restrictivo (sin `unsafe-inline` ni comodín `*`). Las respuestas bajo `/api` DEBEN incluir además `Cache-Control: no-store`.

#### Scenario: Cabeceras en una respuesta correcta
- DADO la API en marcha
- CUANDO se envía `GET /health`
- ENTONCES la respuesta incluye las cuatro cabeceras indicadas

#### Scenario: Cabeceras en errores
- DADO una petición sin token a `GET /api/auth/me`, un login que recibe `429`, una ruta autenticada inexistente con token vigente (`404`) y una petición que provoca un error interno (`500`)
- CUANDO se inspeccionan las respuestas `401`, `429`, `404` y `500`
- ENTONCES todas incluyen las cuatro cabeceras

#### Scenario: Sin caché bajo /api
- DADO un token vigente
- CUANDO se envían `GET /api/auth/me` y una petición sin token a la misma ruta
- ENTONCES la `200` y la `401` incluyen `Cache-Control: no-store`

#### Scenario: CSP restrictiva
- DADO cualquier respuesta de la API
- CUANDO se lee `Content-Security-Policy`
- ENTONCES existe, no es vacía y no contiene `unsafe-inline` ni `*`

### Requirement: Política de respaldo autenticada

La API DEBE aplicar una política de respaldo que exige usuario autenticado a todo endpoint que no declare otra cosa. Solo PUEDEN ser anónimos `GET /health`, `POST /api/auth/login` y los endpoints de OpenAPI (solo en `Development`). Un endpoint nuevo sin atributos de autorización DEBE quedar protegido.

#### Scenario: Endpoints anónimos permitidos
- DADO el conjunto de endpoints registrados en `Development`
- CUANDO se enumeran los marcados como anónimos
- ENTONCES son exactamente `GET /health`, `POST /api/auth/login` y los de OpenAPI

#### Scenario: Endpoints protegidos sin token
- DADO `GET /api/auth/me`, los tres endpoints de fichaje, `GET /api/team/members` y `GET /api/team/members/{memberId}/time-entries`
- CUANDO se envían sin cabecera `Authorization`
- ENTONCES todos reciben `401`

#### Scenario: Endpoint nuevo sin atributos
- DADO un endpoint de prueba mapeado sin atributos de autorización
- CUANDO se envía sin token
- ENTONCES recibe `401`

#### Scenario: Ruta inexistente o método no permitido sin token
- DADO una petición sin cabecera `Authorization`
- CUANDO se envía `GET /ruta-que-no-existe` y `POST /health`
- ENTONCES ambas reciben `401` (efecto aceptado de la política de respaldo); con un token vigente reciben `404` y `405` respectivamente, como `application/problem+json`

#### Scenario: Salud anónima
- DADO una petición sin token
- CUANDO se envía `GET /health`
- ENTONCES recibe `200`

### Requirement: Documento OpenAPI solo en Development

El documento OpenAPI en tiempo de ejecución DEBE publicarse anónimamente solo en `Development`. En cualquier otro entorno la ruta NO DEBE existir (una petición sin token recibe `401` por la política de respaldo; con un token vigente, `404`).

#### Scenario: Development
- DADO el entorno `Development`
- CUANDO se envía `GET /openapi/v1.json` sin token
- ENTONCES recibe `200`

#### Scenario: Production
- DADO el entorno `Production` con configuración completa
- CUANDO se envía `GET /openapi/v1.json` con un token vigente, y otra vez sin token
- ENTONCES la primera recibe `404` y la segunda `401`; en ningún caso se sirve el documento

### Requirement: Errores de acceso como ProblemDetails con CORS

Las respuestas `401` (sin token o token inválido), `403`, `404` y `405` generadas sin cuerpo DEBEN ser `application/problem+json` con `status` y un `title` en español («Se requiere autenticación», «Acceso denegado», «Recurso no encontrado» y «Método no permitido» respectivamente), sin `detail`, trazas ni datos internos; el `401` de login (con `detail`) y los `409` conservan su propio cuerpo. La `429` lleva `title` «Demasiadas solicitudes». La `401` DEBE incluir `WWW-Authenticate: Bearer`. Las respuestas `401`, `403` y `429` DEBEN conservar las cabeceras CORS de un origen permitido. El preflight de un origen permitido con `Access-Control-Request-Headers: authorization` DEBE aceptarse, sin credenciales (`Access-Control-Allow-Credentials` ausente).

#### Scenario: 401 como ProblemDetails
- DADO una petición sin token a `GET /api/time-entries`
- CUANDO se inspecciona la respuesta
- ENTONCES es `401`, `application/problem+json`, `status` = `401`, con `title` en español y sin `detail`, y cabecera `WWW-Authenticate` que contiene `Bearer`

#### Scenario: 403 como ProblemDetails
- DADO un token de empleado
- CUANDO envía `GET /api/team/members`
- ENTONCES recibe `403` `application/problem+json` con `status` = `403` y `title` en español

#### Scenario: CORS en 401 y 403
- DADO `Origin: http://localhost:5172` permitido
- CUANDO se reciben una `401` (sin token) y una `403` (empleado en `/api/team/members`)
- ENTONCES ambas incluyen `Access-Control-Allow-Origin: http://localhost:5172`

#### Scenario: Preflight con Authorization
- DADO `Origin: http://localhost:5172` permitido
- CUANDO se envía `OPTIONS /api/time-entries` con `Access-Control-Request-Method: GET` y `Access-Control-Request-Headers: authorization`
- ENTONCES la respuesta permite el origen y la cabecera `authorization`, y no incluye `Access-Control-Allow-Credentials`

### Requirement: Contrato OpenAPI de autenticación

`docs/api/openapi.json` DEBE incluir `post` en `/api/auth/login` (respuestas `200`, `400`, `401`, `429`) y `get` en `/api/auth/me` (`200`, `401`). Las operaciones protegidas DEBEN declarar la respuesta `401`. El documento DEBERÍA declarar un esquema de seguridad `http` `bearer` (`bearerFormat` `JWT`) aplicado a las operaciones protegidas y no al login (elemento recortable). El documento NO DEBE contener `passwordHash` ni `employeeId`. El documento regenerado en el build DEBE ser idéntico al versionado.

#### Scenario: Rutas de autenticación
- DADO el build de `TimeTracker.Api` ejecutado
- CUANDO se lee `docs/api/openapi.json`
- ENTONCES contiene `post` en `/api/auth/login` con las respuestas `200`, `400`, `401` y `429`, y `get` en `/api/auth/me` con `200` y `401`

#### Scenario: Esquema Bearer
- DADO el documento regenerado
- CUANDO se revisa `components.securitySchemes`
- ENTONCES existe un esquema `http` `bearer` y `/api/time-entries` lo requiere, mientras `/api/auth/login` y `/health` no (declaran `security: []`)

#### Scenario: Sin datos prohibidos
- DADO el documento regenerado
- CUANDO se busca el texto `passwordHash` y `employeeId`
- ENTONCES no aparece ninguno

#### Scenario: Contrato sincronizado
- DADO `dotnet build src/TimeTracker.Api --no-incremental`
- CUANDO termina
- ENTONCES el SHA-256 de `docs/api/openapi.json` coincide con el de antes del build

### Requirement: Pruebas de seguridad y cobertura

DEBEN existir pruebas automáticas de los casos `200`, `401`, `403`, `404` y `429` anteriores, con `TestTokens` para fabricar tokens (válido, caducado, alterado, otro algoritmo, otra clave) y con PostgreSQL real (Testcontainers) para las pruebas con filas en `users`. `IdentityStartupTests` DEBE sustituirse por `JwtStartupTests`. La cobertura de líneas y ramas de `TimeTracker.Domain` DEBE ser 100 % y la de `TimeTracker.Application` ≥ 80 %.

#### Scenario: Pruebas de arranque de JWT
- DADO `dotnet test`
- CUANDO se listan las pruebas de `TimeTracker.Api.IntegrationTests`
- ENTONCES existe `JwtStartupTests` y no existe `IdentityStartupTests`

#### Scenario: Cobertura por niveles
- DADO `dotnet test` con cobertura y `scripts/check-coverage.ps1`
- CUANDO se evalúan los informes
- ENTONCES Domain muestra `line-rate` y `branch-rate` = `1` y Application ≥ 80 %

#### Scenario: Pruebas verdes a la primera declaradas
- DADO una prueba de límite que pasa en su primera ejecución (p. ej. 10 intentos sin `429`, tolerancia de 30 s)
- CUANDO se revisa `tasks.md` y `verify-report.md`
- ENTONCES figura como verde a la primera con su rojo demostrado por mutación o justificado

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
