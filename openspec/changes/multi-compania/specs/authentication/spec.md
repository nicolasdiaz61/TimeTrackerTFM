# Delta para authentication

Cambio: `multi-compania`. Capacidad EXISTENTE (`openspec/specs/authentication/spec.md`). Contiene bloques `MODIFIED` (con el nombre exacto del requisito vigente) y `ADDED`.

> Aviso de delta que cambia escenarios vigentes:
> - La cabecera «Fuera de alcance: … registro público …» de la especificación vigente deja de aplicar: el registro público existe, cerrado por defecto (ver `company-registration`). Siguen fuera de alcance el refresh token, la 2FA, la recuperación de contraseña, la revocación de tokens y las cookies.
> - «Almacenamiento y verificación de contraseñas»: la contraseña en claro ya no es «nunca devuelta»: la contraseña temporal de un alta se devuelve una única vez (ver `employee-management`).
> - «Política de respaldo autenticada»: los endpoints anónimos dejan de ser exactamente tres cuando `Registration:Enabled` es `true`.

Notas de verificación adicionales:
- `TestTokens` DEBE fabricar tokens con `cid` (y opcionalmente `cname`, `mcp`) de forma independiente del emisor de producción; el PR de tokens y fábricas se separa del resto (ver propuesta).
- Los tokens vigentes sin `cid` quedan inválidos (`401`) al desplegar el cambio (caducan en 30 minutos de todos modos); es un fallo cerrado deliberado.

## MODIFIED Requirements

### Requirement: Almacenamiento y verificación de contraseñas

La contraseña DEBE almacenarse solo como hash PBKDF2 con sal aleatoria generado con `PasswordHasher` tras el puerto `IPasswordHasher` (Application) y su adaptador en `Api`. La contraseña en claro NO DEBE persistirse ni registrarse, y NO DEBE devolverse en ninguna respuesta, con una única excepción: la contraseña temporal generada por el servidor en el alta de una persona, que DEBE devolverse una sola vez en la respuesta del alta (ver `employee-management`). La verificación DEBE devolver falso, sin lanzar excepción, para una contraseña incorrecta.
(Previously: la contraseña en claro no debía devolverse en ninguna respuesta, sin excepciones)

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

#### Scenario: Única excepción de devolución
- DADO el contrato regenerado y las respuestas de un recorrido completo (registro, alta, login, cambio de contraseña)
- CUANDO se busca una contraseña en claro en las respuestas
- ENTONCES solo aparece `temporaryPassword` en la respuesta del alta y ninguna contraseña en ninguna otra

### Requirement: Política de respaldo autenticada

La API DEBE aplicar una política de respaldo que exige usuario autenticado a todo endpoint que no declare otra cosa. Solo PUEDEN ser anónimos `GET /health`, `POST /api/auth/login`, los endpoints de OpenAPI (solo en `Development`) y, únicamente con `Registration:Enabled` = `true`, `POST /api/auth/register-company`. Un endpoint nuevo sin atributos de autorización DEBE quedar protegido.
(Previously: solo `GET /health`, `POST /api/auth/login` y OpenAPI podían ser anónimos)

#### Scenario: Endpoints anónimos permitidos con el registro cerrado
- DADO el conjunto de endpoints registrados en `Development` con `Registration:Enabled` apagada
- CUANDO se enumeran los marcados como anónimos
- ENTONCES son exactamente `GET /health`, `POST /api/auth/login` y los de OpenAPI

#### Scenario: Endpoints anónimos permitidos con el registro abierto
- DADO el mismo conjunto con `Registration:Enabled` = `true`
- CUANDO se enumeran los marcados como anónimos
- ENTONCES son exactamente los tres anteriores y `POST /api/auth/register-company`

#### Scenario: Endpoints protegidos sin token
- DADO `GET /api/auth/me`, `POST /api/auth/change-password`, `POST /api/employees`, los tres endpoints de fichaje, `GET /api/team/members` y `GET /api/team/members/{memberId}/time-entries`
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

## ADDED Requirements

### Requirement: Compañía en el token y validación estricta de `cid`

El token emitido DEBE contener el claim `cid` (GUID de la compañía del usuario) y el claim `cname` (nombre visible de la compañía); si el usuario tiene el cambio de contraseña pendiente DEBE contener además `mcp` = `true` (ver `employee-management`). `RejectInvalidClaims` DEBE exigir un `cid` que sea un GUID distinto de `Guid.Empty` y rechazar con `401` los tokens sin `cid`, con `cid` no GUID o igual a `Guid.Empty`. Los claims se leen sin traducción de nombres. El nombre de la compañía NO DEBE figurar en ningún otro claim ni respuesta distinta del inicio de sesión, el registro, el cambio de contraseña y `GET /api/auth/me`. La compañía NO DEBE poder fijarse por la entrada del usuario (ver `company-isolation`).

#### Scenario: Claims de compañía
- DADO un login correcto de un usuario de la compañía A
- CUANDO se decodifica el token
- ENTONCES contiene `cid` = el GUID de A y `cname` = el nombre de A, y no contiene `mcp`

#### Scenario: Token sin cid
- DADO un token correctamente firmado y vigente sin claim `cid`
- CUANDO se envía a `GET /api/auth/me` y a `GET /api/time-entries`
- ENTONCES ambos reciben `401`

#### Scenario: cid no válido
- DADO un token correctamente firmado con `cid` = `no-es-un-guid`, y otro con `cid` = `00000000-0000-0000-0000-000000000000`
- CUANDO se envían a `GET /api/time-entries`
- ENTONCES ambos reciben `401`

#### Scenario: Token alterado con otro cid
- DADO un token de un empleado de A al que se sustituye el payload por otro con el `cid` de B conservando la firma original
- CUANDO se envía a `GET /api/time-entries`
- ENTONCES recibe `401` y no se devuelve ningún dato de B

#### Scenario: Token con cid correcto
- DADO un token vigente con `cid` válido
- CUANDO envía `GET /api/auth/me`
- ENTONCES recibe `200`

### Requirement: Compañía y estado de contraseña en la sesión

La respuesta de inicio de sesión (`POST /api/auth/login`), la del registro, la del cambio de contraseña y `GET /api/auth/me` DEBEN incluir en `user` el nombre de la compañía (`companyName`) y `mustChangePassword` (booleano). `GET /api/auth/me` DEBE tomar ambos únicamente de los claims `cname` y `mcp` del token, sin consultar la base de datos. La respuesta NO DEBE contener el identificador de la compañía (ninguna propiedad `companyId`) ni el hash.

#### Scenario: Login con compañía
- DADO un usuario de la compañía «Acme Norte» sin cambio pendiente
- CUANDO inicia sesión
- ENTONCES `user.companyName` = «Acme Norte», `user.mustChangePassword` = `false` y el cuerpo no contiene ningún identificador de compañía

#### Scenario: me desde los claims
- DADO `ConnectionStrings:Default` apuntando a un servidor inexistente y un token vigente con `cname` = «Acme Norte» y sin `mcp`
- CUANDO envía `GET /api/auth/me`
- ENTONCES recibe `200` con `companyName` = «Acme Norte» y `mustChangePassword` = `false`

#### Scenario: me con cambio pendiente
- DADO un token vigente con `mcp` = `true`
- CUANDO envía `GET /api/auth/me`
- ENTONCES recibe `200` con `mustChangePassword` = `true`

#### Scenario: Nombre solo donde corresponde
- DADO el contrato regenerado
- CUANDO se buscan las respuestas que contienen `companyName`
- ENTONCES aparece solo en las respuestas de login, registro, cambio de contraseña y `me`

### Requirement: Compañía del usuario autenticado en el contexto de aplicación

El puerto `ICurrentUser` DEBE exponer, además del identificador del empleado y su rol, el identificador de su compañía (`CompanyId`), leído únicamente del claim `cid` del token ya validado (sin traducción de nombres). El adaptador NO DEBE consultar la base de datos ni aceptar la compañía desde un parámetro, cuerpo, cabecera o historial de la petición. Si el principal no está autenticado o el claim falta, no es un GUID o es `Guid.Empty`, el adaptador DEBE lanzar una excepción y NO DEBE devolver ninguna compañía por defecto.

#### Scenario: El adaptador lee el claim cid
- DADO un contexto HTTP con usuario autenticado cuyo claim `cid` es el GUID de A
- CUANDO se lee `HttpContextCurrentUser.CompanyId`
- ENTONCES vale el GUID de A

#### Scenario: Sin usuario autenticado
- DADO un contexto HTTP sin usuario autenticado
- CUANDO se lee `HttpContextCurrentUser.CompanyId`
- ENTONCES se lanza una excepción y no se devuelve ninguna compañía

#### Scenario: Claim ausente o inválido
- DADO un contexto autenticado sin `cid`, otro con `cid` no GUID y otro con `Guid.Empty`
- CUANDO se lee `HttpContextCurrentUser.CompanyId`
- ENTONCES los tres lanzan una excepción (en la API real estos tokens ya se rechazan con `401`)

#### Scenario: La compañía no se toma de la petición
- DADO un token de la compañía A y un cuerpo, consulta o cabecera que afirman la compañía B
- CUANDO se procesa la petición
- ENTONCES `ICurrentUser.CompanyId` vale el de A

### Requirement: Contrato OpenAPI del registro y de la sesión de compañía

Con `Registration:Enabled` = `true` (el contrato se genera con la bandera encendida; ver `solution-architecture`), `docs/api/openapi.json` DEBE incluir `post` en `/api/auth/register-company` (respuestas `201`, `400`, `403`, `409`, `429`; operación anónima con `security: []`) y `post` en `/api/auth/change-password` (ver `employee-management`). El documento NO DEBE contener `companyId`, `inviteCode` con valores de ejemplo ni ningún código real, y NO DEBE contener `passwordHash`. El esquema de petición del registro DEBE contener `companyName`, `managerName`, `email`, `password` e `inviteCode`. El documento regenerado en el build DEBE ser idéntico al versionado.

#### Scenario: Ruta del registro
- DADO el build ejecutado
- CUANDO se lee `docs/api/openapi.json`
- ENTONCES contiene `post` en `/api/auth/register-company` con las respuestas indicadas y `security: []`

#### Scenario: Esquema de petición
- DADO el documento regenerado
- CUANDO se revisa el esquema de la petición del registro
- ENTONCES contiene exactamente las cinco propiedades indicadas y ninguna `companyId`

#### Scenario: Sin datos prohibidos
- DADO el documento regenerado
- CUANDO se buscan `companyId` y `passwordHash`
- ENTONCES no aparece ninguno

#### Scenario: Contrato sincronizado
- DADO `dotnet build src/TimeTracker.Api --no-incremental`
- CUANDO termina
- ENTONCES el SHA-256 de `docs/api/openapi.json` coincide con el de antes del build
