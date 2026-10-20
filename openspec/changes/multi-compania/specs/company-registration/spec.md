# Especificación: company-registration

Cambio: `multi-compania`. Capacidad NUEVA; todos los requisitos son `ADDED`. Define el autorregistro de un Manager que crea su compañía: bandera de apertura, código de invitación secreto, creación atómica de compañía y primer Manager, validación de entrada, política de contraseñas, unicidad de nombre y correo, límite de tasa por IP y privacidad del registro. El alta de personas posterior se especifica en `employee-management`; las tablas y el aislamiento, en `company-isolation`; las variables de entorno, en `startup-configuration`.

Decisiones firmes: sin correo (no hay verificación de correo ni proveedor de correo); código de invitación secreto definido en el entorno (`Registration__InviteCodes`); registro tras `Registration__Enabled`, apagada por defecto; una persona, una compañía; compañía nueva con el asistente deshabilitado.

Notas de verificación:
- «Respuesta idéntica» significa mismo estado, tipo de medio (`application/problem+json`, sin parámetros), `type`, `title` y `detail`, salvo `traceId`.
- En `WebApplicationFactory` la IP remota es nula y todas las peticiones comparten la misma partición del limitador; el aislamiento entre IP distintas se comprueba con `ForwardedHeaders` activo y cabeceras `X-Forwarded-For` distintas (ver `startup-configuration`).
- Los códigos de invitación y las contraseñas de las pruebas son valores inventados y reconocibles.

Fuera de alcance: verificación de correo, recuperación de contraseña, proveedor de correo, códigos de un solo uso gestionados en base de datos, panel de alta de compañías por una persona administradora de plataforma, baja o suspensión de compañías desde la interfaz y supresión y exportación de datos (RGPD, posposición explícita).

## ADDED Requirements

### Requirement: Registro cerrado por defecto

`POST /api/auth/register-company` DEBE existir solo con `Registration:Enabled` = `true` (valor por defecto `false`). Con la bandera apagada, el endpoint NO DEBE registrarse: la petición recibe la respuesta de una ruta inexistente (`401` sin token por la política de respaldo, `404` `application/problem+json` con un token vigente) y NO DEBE evaluarse ningún código ni consultarse la base de datos. Con la bandera encendida el endpoint DEBE ser anónimo.

#### Scenario: Bandera apagada sin token
- DADO la API sin `Registration__Enabled`
- CUANDO se envía `POST /api/auth/register-company` con un cuerpo completo y sin token
- ENTONCES recibe `401` y no se crea ninguna compañía ni usuario

#### Scenario: Bandera apagada con token
- DADO la bandera apagada y un token vigente
- CUANDO se envía la misma petición
- ENTONCES recibe `404` `application/problem+json`

#### Scenario: Bandera encendida
- DADO `Registration__Enabled=true`, un código de invitación válido definido y un cuerpo válido
- CUANDO se envía sin token
- ENTONCES la petición llega al caso de uso (no recibe `401` por falta de token)

#### Scenario: Valor por defecto
- DADO un entorno sin `Registration__Enabled`
- CUANDO se enlazan las opciones
- ENTONCES `Enabled` es `false`

### Requirement: Código de invitación secreto

El cuerpo DEBE incluir `inviteCode`. El código DEBE compararse con cada código de `Registration:InviteCodes` en tiempo constante (sin cortocircuito por longitud ni por posición del primer carácter distinto; p. ej. `CryptographicOperations.FixedTimeEquals` sobre resúmenes de igual longitud). El código DEBE comprobarse antes que cualquier otra regla del registro, de modo que un `409` por correo o nombre repetido solo pueda observarlo quien conoce un código válido. Un código ausente, en blanco, incorrecto o de más de 128 caracteres DEBE responder `403` `application/problem+json` con un `detail` genérico en español, sin indicar cuál fue la causa. Un código válido NO se consume: sigue siendo válido hasta que se retire del entorno (rotación por el entorno). El código NO DEBE aparecer en respuestas, registros, contrato OpenAPI ni mensajes de error.

#### Scenario: Código válido
- DADO `Registration__InviteCodes=codigo-prueba-0123456789` y un cuerpo válido con ese código
- CUANDO se envía el registro
- ENTONCES recibe `201`

#### Scenario: Código incorrecto, ausente o en blanco
- DADO un cuerpo con un código erróneo, otro sin `inviteCode` y otro en blanco
- CUANDO se envían
- ENTONCES las tres respuestas son `403` idénticas y no se crea nada

#### Scenario: Varios códigos
- DADO `Registration__InviteCodes` con dos códigos separados por comas
- CUANDO se registran dos compañías, cada una con un código distinto
- ENTONCES ambas se crean

#### Scenario: El código no se consume
- DADO un registro correcto con un código
- CUANDO otra persona registra otra compañía con el mismo código
- ENTONCES también recibe `201`

#### Scenario: Orden de validación
- DADO un correo ya registrado y un código incorrecto
- CUANDO se envía el registro
- ENTONCES recibe `403` y no `409` (no se revela si el correo existe)

#### Scenario: Comparación en tiempo constante
- DADO el código de la aplicación que compara el código de invitación
- CUANDO se revisa
- ENTONCES usa una comparación de tiempo constante y no `==`, `Equals` ni `string.Compare`

#### Scenario: El código no se filtra
- DADO un registrador que captura los mensajes y un código reconocible
- CUANDO se envían registros válidos e inválidos y se descarga el contrato OpenAPI
- ENTONCES ni las respuestas, ni los registros, ni el contrato contienen ese valor

### Requirement: Creación atómica de la compañía y su primer Manager

Un registro válido DEBE crear, en una única transacción, la compañía (`Active`) y su primer usuario con rol `Manager`, sin responsable, con `must_change_password` = `false` y con la contraseña almacenada solo como hash. DEBE responder `201` con `application/json` y el mismo cuerpo que el inicio de sesión (`{ accessToken, expiresAt, user }`, ver `authentication`), de modo que la persona queda con sesión iniciada. Si cualquier paso falla, NO DEBE quedar ni compañía ni usuario. NO DEBE enviarse ningún correo ni exigirse verificación de correo. La compañía nueva NO DEBE tener fila de configuración del asistente (asistente deshabilitado; ver `assistant-settings`).

#### Scenario: Registro correcto
- DADO un cuerpo válido con `companyName`, `managerName`, `email`, `password` e `inviteCode`
- CUANDO se envía
- ENTONCES recibe `201`, existe la compañía y un único usuario `Manager` de esa compañía sin `manager_id`, y el token lleva su `cid`

#### Scenario: Contraseña como hash
- DADO el registro correcto
- CUANDO se lee `password_hash`
- ENTONCES no contiene la contraseña y la verifica

#### Scenario: Atomicidad
- DADO un fallo simulado al insertar el usuario
- CUANDO se envía un registro válido
- ENTONCES no existe la compañía ni ningún usuario nuevo

#### Scenario: Asistente deshabilitado en la compañía nueva
- DADO una compañía recién registrada y un entorno con proveedor de IA de plataforma configurado
- CUANDO su Manager envía `POST /api/asistente` con un cuerpo válido
- ENTONCES recibe `503` con el aviso de asistente deshabilitado y no se invoca ningún modelo

#### Scenario: Sin correo
- DADO el código de registro
- CUANDO se revisa su composición
- ENTONCES no depende de ningún puerto de correo y la respuesta no indica ninguna verificación pendiente

### Requirement: Validación de la entrada del registro

El cuerpo DEBE tener `companyName` de 1 a 100 caracteres tras recortar espacios, `managerName` de 1 a 100 caracteres (mismas reglas que `displayName`), `email` válido según las reglas de `UserAccount` (hasta 254 caracteres, normalizado) y `password` según «Política de contraseñas». Una entrada inválida DEBE responder `400` `application/problem+json` con errores por campo en español, sin reproducir la contraseña ni el código. Un cuerpo ausente o ilegible DEBE responder `400`. La validación de campos DEBE ejecutarse después del código de invitación y antes de cualquier escritura. Las propiedades desconocidas (por ejemplo `companyId` o `role`) NO DEBEN tener efecto.

#### Scenario: Campos inválidos
- DADO `companyName` en blanco, otro de 101 caracteres, `managerName` en blanco y `email` sin `@`, cada uno por separado
- CUANDO se envían con un código válido
- ENTONCES cada petición recibe `400` con el campo afectado y no se crea nada

#### Scenario: Sin eco de la contraseña
- DADO una contraseña de 5 caracteres reconocible
- CUANDO se envía
- ENTONCES la respuesta `400` nombra el campo `password` y no contiene ese valor

#### Scenario: Propiedades desconocidas sin efecto
- DADO un cuerpo válido con `role` = `Employee` y `companyId` = una compañía existente
- CUANDO se envía
- ENTONCES se crea una compañía nueva y el usuario es `Manager`

#### Scenario: Cuerpo ilegible
- DADO una petición sin cuerpo y otra con JSON roto
- CUANDO se envían con la bandera encendida
- ENTONCES ambas reciben `400`

### Requirement: Política de contraseñas

Toda contraseña nueva (registro, alta de personas con contraseña temporal, cambio de contraseña) DEBE tener entre 12 y 128 caracteres y NO DEBE someterse a reglas de composición (mayúsculas, dígitos o símbolos). Los espacios NO DEBEN recortarse ni normalizarse. El inicio de sesión conserva su comprobación existente (máximo 128, ver `authentication`) y NO exige el mínimo de 12 a usuarios ya existentes.

#### Scenario: Límites
- DADO contraseñas de 11, 12, 128 y 129 caracteres
- CUANDO se validan
- ENTONCES las de 11 y 129 se rechazan y las de 12 y 128 se aceptan

#### Scenario: Sin reglas de composición
- DADO una contraseña de 12 letras minúsculas sin dígitos ni símbolos
- CUANDO se valida
- ENTONCES se acepta

#### Scenario: Espacios conservados
- DADO una contraseña de 12 caracteres con espacios exteriores
- CUANDO se guarda y se verifica
- ENTONCES se verifica con los espacios y no sin ellos

#### Scenario: Usuario previo con contraseña corta
- DADO un usuario existente cuyo hash corresponde a una contraseña de 8 caracteres
- CUANDO inicia sesión con ella
- ENTONCES el login es correcto (la política solo afecta a contraseñas nuevas)

### Requirement: Nombre de compañía y correo únicos

El nombre de compañía DEBE ser único según `name_normalized` (solo duplicado exacto tras normalizar espacios y mayúsculas; sin plegar acentos); un nombre repetido DEBE responder `409` `application/problem+json` con un `detail` en español propio de ese caso. El correo es único en toda la base: uno ya registrado DEBE responder `409` con un `detail` genérico que NO indique la compañía ni el rol de la persona. La enumeración de correos mediante este `409` es un riesgo residual aceptado y documentado (se mitiga con el código de invitación, la bandera y el límite por IP). Ante una carrera de dos registros iguales, exactamente uno DEBE tener éxito y el otro recibir `409`, sin `500`.

#### Scenario: Nombre repetido
- DADO la compañía «Acme Norte»
- CUANDO se registra «  acme   NORTE  » con un código válido
- ENTONCES recibe `409` y no se crea nada

#### Scenario: Nombre distinto por acento
- DADO la compañía «Acme Norte»
- CUANDO se registra «Ácme Norte»
- ENTONCES recibe `201`

#### Scenario: Correo ya registrado
- DADO `luis@example.test` en cualquier compañía
- CUANDO se registra una compañía nueva con ese correo
- ENTONCES recibe `409` con un `detail` que no nombra ninguna compañía y no se crea la compañía

#### Scenario: Carrera de registros iguales
- DADO dos registros simultáneos con el mismo correo y el mismo nombre
- CUANDO terminan
- ENTONCES hay exactamente un `201` y un `409` y existe una sola compañía

### Requirement: Límite de tasa por IP del registro

`POST /api/auth/register-company` DEBE limitarse a 5 peticiones por hora y por IP de origen (ventana fija, política `register-company` anónima y particionada por IP), contando todos los intentos, correctos o no, incluidos los rechazados por código. La petición excedida DEBE recibir `429` `application/problem+json` con `title` «Demasiadas solicitudes», `detail` en español, cabecera `Retry-After` y las cabeceras CORS y de seguridad. La IP DEBE ser la del cliente (con `ForwardedHeaders` activo según el ADR 0013). Las demás políticas (login, asistente) NO DEBEN afectar al registro ni a la inversa.

#### Scenario: Cinco intentos permitidos
- DADO la bandera encendida
- CUANDO se envían 5 registros con código erróneo desde la misma IP
- ENTONCES ninguno recibe `429` (todos `403`)

#### Scenario: Sexto intento
- DADO 5 intentos ya realizados en la hora
- CUANDO se envía el sexto
- ENTONCES recibe `429` con `Retry-After` y no se evalúa el código

#### Scenario: IP distinta tras el proxy
- DADO `ForwardedHeaders:Enabled` = `true` y 5 intentos de una IP
- CUANDO llega un intento con otra IP en `X-Forwarded-For`
- ENTONCES no recibe `429`

#### Scenario: Independiente del login
- DADO el registro limitado
- CUANDO se envía un login
- ENTONCES no recibe `429` por ese motivo

#### Scenario: CORS en el 429
- DADO un origen permitido y un registro que recibe `429`
- CUANDO se inspeccionan las cabeceras
- ENTONCES incluyen `Access-Control-Allow-Origin` y las cuatro cabeceras de seguridad

### Requirement: Privacidad del registro

El servicio DEBE registrar únicamente metadatos del registro (resultado: creado, rechazado por código, por validación, por duplicado o por límite; instante; identificador de la compañía creada). NO DEBE registrar contraseñas, códigos de invitación, correos ni nombres de personas o de compañías.

#### Scenario: Metadatos presentes
- DADO un registrador que captura los mensajes y un registro correcto
- CUANDO termina
- ENTONCES existe un registro con el resultado y el identificador de la compañía

#### Scenario: Sin datos personales
- DADO el mismo registrador, un correo, un nombre y una contraseña reconocibles
- CUANDO se envían registros correctos e incorrectos
- ENTONCES ningún mensaje contiene esos valores ni el código de invitación
