# Especificación: assistant-settings

Cambio: `asistente-config-responsable`. Capacidad NUEVA; todos los requisitos son `ADDED`. Define la configuración efectiva del asistente (fila guardada sobre entorno), el catálogo de proveedores, la API y la página Web del Manager, la prueba de conexión, la auditoría, la concurrencia y la recarga en caliente. La protección de la clave se especifica en `assistant-credential-protection`. Las claves `Ai:*` del entorno definidas por el cambio `asistente-ia` (capacidad `assistant-configuration`, modificada por este cambio) conservan su significado como valores por defecto.

Nota de nombre: la capacidad se llama `assistant-settings` para no colisionar con `assistant-configuration` (claves `Ai:*` del entorno, de `asistente-ia`). Orden de archivo: archivar `asistente-ia` primero.

Notas de verificación:
- Las pruebas unitarias usan un `IChatClient` falso y nunca contactan con un proveedor real.
- El campo que indica si hay clave guardada se llama `credentialConfigured`; el contrato OpenAPI NO DEBE contener la cadena `apikey` (sin distinguir mayúsculas).
- «Manager» es el rol con la política de autorización de Manager; «Employee» es cualquier usuario autenticado sin ese rol.

Fuera de alcance: URL escritas por el Manager, lista de modelos permitidos, edición de cuotas, límites de tasa e iteraciones de herramientas, configuración por equipo, GitHub Models, OpenCode Zen, notificar a los usuarios del chat, exigir una prueba antes de guardar.

## ADDED Requirements

### Requirement: Acceso exclusivo del Manager

Los endpoints `GET`, `PUT` y `DELETE /api/asistente/configuracion` y `POST /api/asistente/configuracion/prueba` DEBEN exigir autenticación y la política de Manager. Todo Manager PUEDE ver y editar la configuración.

#### Scenario: Anónimo
- DADO una petición sin token a cualquiera de los cuatro endpoints
- CUANDO se envía
- ENTONCES responde `401`

#### Scenario: Employee
- DADO un usuario autenticado sin rol Manager
- CUANDO invoca cualquiera de los cuatro endpoints
- ENTONCES responde `403` y no se lee ni modifica ninguna configuración

#### Scenario: Manager
- DADO un usuario con rol Manager
- CUANDO invoca `GET /api/asistente/configuracion`
- ENTONCES responde `200`

### Requirement: Catálogo de proveedores y lista permitida

El servidor DEBE mantener un catálogo fijo de proveedores `Anthropic`, `OpenAI`, `Gemini` y `Ollama` con URL base fijas en código de servidor (excepto Ollama). El Manager NO DEBE poder escribir ninguna URL. El endpoint de `Ollama` DEBE provenir solo del entorno (`Ai:Endpoint`). La variable `Ai__AllowedProviders` DEBE limitar los proveedores seleccionables; sin ella, el único permitido es el proveedor del entorno. Un proveedor fuera del catálogo o fuera de la lista permitida DEBE rechazarse. `None` (deshabilitar) DEBE ser siempre seleccionable, esté o no en la lista. Un nombre desconocido en `Ai__AllowedProviders` (fuera del catálogo, incluido `None`) DEBE impedir el arranque con un mensaje que nombre `Ai:AllowedProviders`. Cada elemento de la lista de proveedores permitidos que devuelve la API DEBE ser un objeto con `provider`, `requiresCredential` y `supportsEffort`, de modo que la Web no duplique reglas.

#### Scenario: Proveedor permitido
- DADO `Ai__AllowedProviders=Anthropic,OpenAI`
- CUANDO el Manager guarda `OpenAI`
- ENTONCES se acepta

#### Scenario: Proveedor no permitido
- DADO `Ai__AllowedProviders=Anthropic` y un `PUT` con `Gemini`
- CUANDO se valida
- ENTONCES responde `400` con mensaje en español que nombra el campo `provider` y no se guarda nada

#### Scenario: Proveedor fuera del catálogo
- DADO un `PUT` con `GitHubModels`
- CUANDO se valida
- ENTONCES responde `400`

#### Scenario: Sin lista configurada
- DADO el entorno con `Ai__Provider=Ollama` y sin `Ai__AllowedProviders`
- CUANDO el Manager consulta la configuración
- ENTONCES los proveedores permitidos son únicamente `Ollama`

#### Scenario: Nombre desconocido en la lista
- DADO `Ai__AllowedProviders=Anthropic,GitHubModels`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Ai:AllowedProviders` y no imprime el valor

#### Scenario: None siempre seleccionable
- DADO `Ai__AllowedProviders=Anthropic` y un `PUT` con `provider` = `None`
- CUANDO se valida
- ENTONCES se acepta y el asistente queda deshabilitado

#### Scenario: Elementos de la lista permitida
- DADO `Ai__AllowedProviders=Anthropic,Ollama`
- CUANDO el Manager hace `GET`
- ENTONCES cada elemento de `allowedProviders` es un objeto con `provider`, `requiresCredential` (`true` para `Anthropic`, `false` para `Ollama`) y `supportsEffort`

#### Scenario: Sin URL del Manager
- DADO un `PUT` con un campo de URL o endpoint
- CUANDO se procesa
- ENTONCES se rechaza con `400`, no se guarda nada y ninguna URL escrita por el Manager se usa en una llamada saliente

### Requirement: Consulta de la configuración efectiva

`GET /api/asistente/configuracion` DEBE devolver la configuración efectiva en un objeto JSON (camelCase) con los campos `provider`, `model`, `timeoutSeconds`, `maxOutputTokens` y `effort` (este último PUEDE ser nulo), `sources` (origen `env` o `stored` de cada valor; el de la clave PUEDE ser también `none`), `credentialConfigured` (booleano), `credentialStorageAvailable` (booleano: hay clave maestra utilizable), `allowedProviders`, `concurrencyToken`, `updatedAt`, `updatedBy` y `applied` (si la configuración guardada es la que usa esta instancia). NUNCA DEBE devolver la clave, ni cifrada ni en claro; PUEDE devolver como máximo los últimos 4 caracteres. `PUT` DEBE responder `200` con el mismo cuerpo que `GET` (con el token de concurrencia nuevo).

#### Scenario: Solo entorno
- DADO que no existe fila guardada y `Ai__Provider=Ollama`
- CUANDO el Manager hace `GET`
- ENTONCES cada valor tiene origen `env` y `credentialConfigured` refleja si el entorno tiene clave

#### Scenario: Valores guardados
- DADO una fila guardada con modelo `m-1`
- CUANDO el Manager hace `GET`
- ENTONCES el modelo es `m-1` con origen `stored`

#### Scenario: Campos de la respuesta
- DADO una configuración cualquiera
- CUANDO el Manager hace `GET`
- ENTONCES el cuerpo contiene `provider`, `model`, `timeoutSeconds`, `maxOutputTokens`, `effort`, `sources`, `credentialConfigured`, `credentialStorageAvailable`, `allowedProviders`, `concurrencyToken`, `updatedAt`, `updatedBy` y `applied`

#### Scenario: Respuesta del guardado
- DADO un `PUT` válido
- CUANDO se procesa
- ENTONCES responde `200` con un cuerpo equivalente al de un `GET` posterior

#### Scenario: Almacenamiento de clave no disponible
- DADO que no hay clave maestra utilizable
- CUANDO el Manager hace `GET`
- ENTONCES `credentialStorageAvailable` es `false`

#### Scenario: Configuración aplicada
- DADO una fila guardada válida y permitida que la instancia usa
- CUANDO el Manager hace `GET`
- ENTONCES `applied` es `true`; si la fila no se aplica (por ejemplo, su proveedor ya no está permitido), `applied` es `false`

#### Scenario: Sin clave en la respuesta
- DADO una clave guardada con valor reconocible `clave-prueba-123456`
- CUANDO el Manager hace `GET`
- ENTONCES el cuerpo no contiene `clave-prueba-123456` ni su forma cifrada

### Requirement: Guardado con validación

`PUT /api/asistente/configuracion` DEBE aceptar únicamente las propiedades `provider`, `model`, `timeoutSeconds`, `maxOutputTokens`, `effort`, `credential` y `concurrencyToken`; cualquier propiedad desconocida (por ejemplo `url` o `endpoint`) DEBE responder `400`. DEBE validar: `provider` en el catálogo y en la lista permitida (o `None` para deshabilitar); `model` texto libre de 1 a 100 caracteres con un juego de caracteres restringido (letras ASCII, dígitos y `. _ - : / @`); `timeoutSeconds` de 5 a 55; `maxOutputTokens` de 64 a 4096 (extremos incluidos); `effort` opcional con valores `low`, `medium` o `high`, solo si el proveedor lo admite (el servidor no conoce las capacidades de cada modelo). Los errores DEBEN responder `400` con mensajes en español, nombrando el campo y la razón, sin valores secretos. Una clave nueva es opcional: si se omite, se conserva la guardada.

#### Scenario: Guardado válido
- DADO un `PUT` con proveedor permitido, modelo, `timeoutSeconds` 25, `maxOutputTokens` 800 y token de concurrencia vigente
- CUANDO se procesa
- ENTONCES responde `200`, queda guardado y se registra una fila de auditoría

#### Scenario: Extremos numéricos
- DADO `timeoutSeconds` 4 o 56, o `maxOutputTokens` 63 o 4097, cada uno por separado
- CUANDO se valida
- ENTONCES responde `400` con el campo afectado; con 5, 55, 64 y 4096 se acepta

#### Scenario: Modelo inválido
- DADO `model` vacío, de 101 caracteres o con caracteres fuera del juego permitido (por ejemplo `<script>`)
- CUANDO se valida
- ENTONCES responde `400` con un mensaje en español que nombra `model`

#### Scenario: Modelo con caracteres permitidos
- DADO `model` con letras ASCII, dígitos y `. _ - : / @` (por ejemplo `a1.b_c-d:e/f@g`)
- CUANDO se valida
- ENTONCES se acepta; con una letra no ASCII (por ejemplo `ñ`) responde `400`

#### Scenario: Esfuerzo
- DADO `effort` = `extreme`, o `effort` = `high` con un proveedor que no lo admite
- CUANDO se valida
- ENTONCES responde `400`; con `low`, `medium` o `high` en un proveedor que lo admite se acepta

#### Scenario: Esfuerzo evaluado por proveedor
- DADO `effort` = `high` con un proveedor que lo admite y un modelo cualquiera
- CUANDO se valida
- ENTONCES se acepta (el servidor no rechaza por modelo; si el modelo no lo admite, el rechazo lo observa la prueba de conexión como `rejected`)

#### Scenario: Propiedad desconocida
- DADO un `PUT` con una propiedad no listada (por ejemplo `url`)
- CUANDO se procesa
- ENTONCES responde `400` y no se guarda nada

#### Scenario: Mantener la clave
- DADO una clave guardada y un `PUT` sin clave nueva
- CUANDO se procesa
- ENTONCES `credentialConfigured` sigue siendo `true`

### Requirement: Concurrencia optimista

`PUT` DEBE incluir el token de concurrencia (basado en `xmin`) obtenido en la lectura y DEBE responder `409` si no coincide con la fila actual, sin modificar nada.

#### Scenario: Token obsoleto
- DADO dos Managers que leyeron la misma versión y el primero guardó
- CUANDO el segundo envía su `PUT`
- ENTONCES responde `409` con mensaje en español y la configuración del primero se conserva

#### Scenario: Primera creación
- DADO que no existe fila
- CUANDO el Manager guarda con el token de «sin fila»
- ENTONCES se crea; si otro la creó antes, responde `409`

### Requirement: Deshabilitar, prioridad y restablecer

La configuración efectiva DEBE resolverse como fila guardada sobre entorno. Un proveedor guardado `None` DEBE deshabilitar el asistente aunque el entorno defina uno (el endpoint del chat responde `503`). `DELETE /api/asistente/configuracion` DEBE borrar la fila y restablecer los valores del entorno, y registrar auditoría; DEBE responder `204` incluso cuando no existe fila. Sin fila, o si la tabla no existe, DEBEN usarse los valores del entorno.

#### Scenario: Guardado gana al entorno
- DADO `Ai__Model=env-m` y una fila con modelo `db-m`
- CUANDO se resuelve la configuración efectiva
- ENTONCES el modelo es `db-m`

#### Scenario: Deshabilitar
- DADO el entorno con proveedor activo y una fila con proveedor `None`
- CUANDO un usuario envía un mensaje al chat
- ENTONCES responde `503`

#### Scenario: Restablecer
- DADO una fila guardada
- CUANDO el Manager invoca `DELETE`
- ENTONCES responde `204`, la fila no existe y el siguiente `GET` muestra origen `env`

#### Scenario: Restablecer sin fila
- DADO que no existe fila
- CUANDO el Manager invoca `DELETE`
- ENTONCES responde `204` y no se inserta ninguna fila de auditoría

#### Scenario: Tabla ausente
- DADO una base de datos sin la tabla de configuración
- CUANDO se resuelve la configuración efectiva
- ENTONCES se usan los valores del entorno sin error

### Requirement: Prueba de conexión

`POST /api/asistente/configuracion/prueba` DEBE probar los valores candidatos enviados en el cuerpo sin guardarlos; los campos ausentes DEBEN tomarse de la configuración efectiva y un cuerpo vacío prueba la efectiva. El candidato DEBE cumplir las reglas de validación del guardado: si no es válido, responde `400` con errores por campo. El límite es de 3 peticiones por minuto y usuario (`429` al superarlo). Una prueba que se completa DEBE responder `200` con `{ "result", "message" }`, donde `result` es una de las categorías fijas `ok`, `authentication`, `timeout`, `unreachable`, `model-not-found` o `rejected`, y `message` un texto fijo en español. `rejected` indica que el proveedor rechazó los parámetros (modelo o esfuerzo no admitido; respuesta 400/422 del proveedor). `unreachable` abarca cualquier otro fallo sin categoría propia (429, 5xx, red u otros). La respuesta NO DEBE incluir texto, URL ni cabeceras del proveedor ni la clave.

#### Scenario: Conexión correcta
- DADO un `IChatClient` falso que responde bien
- CUANDO el Manager invoca la prueba
- ENTONCES responde `200` y el resultado es `ok`

#### Scenario: Categorías de error
- DADO fallos simulados de autenticación, tiempo agotado, red y modelo inexistente
- CUANDO se invoca la prueba en cada caso
- ENTONCES el resultado es `authentication`, `timeout`, `unreachable` y `model-not-found` respectivamente, con su mensaje fijo en español

#### Scenario: Parámetros rechazados por el proveedor
- DADO un fallo simulado con estado HTTP 400 o 422 del proveedor (por ejemplo, esfuerzo no admitido por el modelo)
- CUANDO se invoca la prueba
- ENTONCES el resultado es `rejected` con su mensaje fijo en español

#### Scenario: Otros fallos
- DADO fallos simulados con estado 429 o 5xx, o un error de red
- CUANDO se invoca la prueba en cada caso
- ENTONCES el resultado es `unreachable`

#### Scenario: Candidato no guardado y valores ausentes
- DADO una prueba con solo `model` distinto del guardado
- CUANDO se invoca
- ENTONCES se prueba con ese modelo y el resto de los valores efectivos, y nada se guarda

#### Scenario: Candidato inválido
- DADO una prueba con `timeoutSeconds` 4
- CUANDO se invoca
- ENTONCES responde `400` con el campo `timeoutSeconds` y no se contacta con el proveedor

#### Scenario: Sin eco del proveedor
- DADO un fallo cuyo mensaje contiene `https://api.ejemplo.test/v1` y `clave-prueba-123456`
- CUANDO se invoca la prueba
- ENTONCES la respuesta no contiene ninguno de los dos textos

#### Scenario: No guarda
- DADO una prueba con valores distintos de los guardados
- CUANDO termina
- ENTONCES la fila guardada, la auditoría y la configuración efectiva no cambian

#### Scenario: Límite de tasa
- DADO un Manager que ya hizo 3 pruebas en el último minuto
- CUANDO hace una cuarta
- ENTONCES responde `429`

### Requirement: Auditoría de solo inserción

Cada guardado, restablecimiento y cambio de clave DEBE insertar una fila de auditoría con quién, cuándo, la acción y los valores previo y nuevo no secretos. La auditoría NO DEBE contener la clave (ni en claro ni cifrada). Las filas NO DEBEN modificarse ni borrarse desde la aplicación.

#### Scenario: Registro de un cambio
- DADO un Manager que cambia el modelo de `a` a `b`
- CUANDO se guarda
- ENTONCES existe una fila con su identificador, la hora, la acción y los valores `a` y `b`

#### Scenario: Cambio de clave
- DADO un `PUT` con clave nueva
- CUANDO se guarda
- ENTONCES la fila de auditoría indica que la clave cambió sin contener su valor

#### Scenario: Solo inserción
- DADO el código de la aplicación
- CUANDO se buscan operaciones de actualización o borrado sobre la auditoría
- ENTONCES no existen

### Requirement: Recarga en caliente

Tras guardar o restablecer, la instancia local DEBE aplicar la nueva configuración de inmediato; las demás instancias DEBEN aplicarla en unos 30 segundos mediante sondeo de la versión. Ante una caída de la base de datos o una fila guardada inválida DEBE mantenerse la última configuración válida. Una fila guardada cuyo proveedor ya no está en la lista permitida DEBE tratarse como inválida (se conserva la última configuración válida o la del entorno y `GET` devuelve `applied` = `false`). Las peticiones en curso NO DEBEN interrumpirse: usan la configuración con la que empezaron y los recursos anteriores se liberan al terminar.

#### Scenario: Aplicación local inmediata
- DADO un chat funcionando con el modelo `a`
- CUANDO el Manager guarda el modelo `b`
- ENTONCES el siguiente mensaje usa `b` sin reiniciar

#### Scenario: Otra instancia
- DADO dos instancias y un guardado en la primera
- CUANDO transcurre el intervalo de sondeo (≈30 s)
- ENTONCES la segunda usa la nueva configuración

#### Scenario: Base de datos caída
- DADO una configuración válida cargada y un fallo de lectura de la base de datos en el sondeo
- CUANDO el chat recibe un mensaje
- ENTONCES se usa la última configuración válida y no hay error

#### Scenario: Fila inválida
- DADO una fila guardada que no supera la validación o no se puede descifrar
- CUANDO el sondeo la lee
- ENTONCES se conserva la última configuración válida y se registra un aviso sin secretos

#### Scenario: Proveedor ya no permitido
- DADO una fila guardada con proveedor `Gemini` y `Ai__AllowedProviders` que ya no lo incluye
- CUANDO se carga la configuración
- ENTONCES la fila se trata como inválida, se conserva la última configuración válida (o la del entorno) y `GET` devuelve `applied` = `false`

#### Scenario: Petición en curso
- DADO una respuesta del chat en curso
- CUANDO se guarda una configuración nueva
- ENTONCES la respuesta en curso termina con la configuración anterior sin error

### Requirement: Página Web de configuración

La Web DEBE ofrecer `/asistente/configuracion` solo a Managers (enlace de menú y página protegida; un Employee es redirigido o ve acceso denegado). La página DEBE mostrar el aviso de protección de datos (los datos personales se envían a un tercero con la clave del Manager) y el aviso de que el cambio afecta a toda la empresa. DEBE renderizar todo texto como texto plano, NO DEBE usar almacenamiento del navegador para la configuración ni la clave, y DEBE vaciar el campo de la clave tras guardar. La página del chat NO DEBE cambiar para el resto de usuarios.

#### Scenario: Acceso de Employee
- DADO un Employee autenticado
- CUANDO abre `/asistente/configuracion`
- ENTONCES no ve el formulario y el menú no muestra el enlace

#### Scenario: Avisos
- DADO un Manager en la página
- CUANDO se muestra el formulario
- ENTONCES se ven el aviso de compartición de datos y el de alcance a toda la empresa

#### Scenario: Clave vaciada
- DADO un Manager que introdujo una clave y guarda
- CUANDO el guardado termina
- ENTONCES el campo de la clave queda vacío y solo se muestra «clave configurada»

#### Scenario: Sin almacenamiento del navegador
- DADO el código de la página
- CUANDO se buscan usos de `localStorage` o `sessionStorage`
- ENTONCES no existen

#### Scenario: Texto plano
- DADO un modelo guardado con valor `<b>x</b>`
- CUANDO se muestra
- ENTONCES se ve literalmente como texto, sin interpretar HTML

#### Scenario: Chat sin cambios
- DADO un Employee
- CUANDO usa `/asistente`
- ENTONCES su experiencia es la misma que antes del cambio
