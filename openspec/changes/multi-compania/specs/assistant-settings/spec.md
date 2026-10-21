# Delta para assistant-settings

Cambio: `multi-compania`. Capacidad EXISTENTE (`openspec/specs/assistant-settings/spec.md`). Contiene bloques `MODIFIED` (con el nombre exacto del requisito vigente) y `ADDED`. La configuración del asistente deja de ser una fila única de toda la instancia (ADR 0018/0019) y pasa a ser una fila por compañía; el ADR 0021 sustituye a esos ADR en lo de «fila única/toda la instancia».

> Aviso de delta que cambia escenarios vigentes:
> - «Consulta de la configuración efectiva» y «Deshabilitar, prioridad y restablecer»: el escenario «Solo entorno» y «Restablecer» dejan de valer para cualquier compañía: los valores del entorno (`Ai:*`, incluida `Ai__ApiKey`) solo se usan, de forma explícita, para la compañía demo. Una compañía sin fila tiene el asistente DESHABILITADO.
> - «Página Web de configuración»: el aviso «afecta a toda la empresa» pasa a nombrar la compañía; hay pruebas de arquitectura con literales fijados que deben actualizarse en el mismo cambio.
> - Sigue vigente que cualquier Manager de la misma compañía puede editar la configuración (riesgo aceptado, ahora acotado a la compañía).

Notas de verificación adicionales:
- «Compañía demo» es la compañía por defecto de identificador fijo (ver `company-isolation` y `demo-data`).
- Las pruebas usan dos compañías A y B (ver `company-isolation`) y un `IChatClient` falso.
- Para que una compañía pueda habilitar un proveedor, el entorno debe definir `Ai__AllowedProviders` (sin esa lista solo está permitido el proveedor del entorno y, si no hay ninguno, únicamente `None`).

## MODIFIED Requirements

### Requirement: Acceso exclusivo del Manager

Los endpoints `GET`, `PUT` y `DELETE /api/asistente/configuracion` y `POST /api/asistente/configuracion/prueba` DEBEN exigir autenticación y la política de Manager. Todo Manager PUEDE ver y editar la configuración de SU compañía y solo la de su compañía; la compañía sale del token (ver `company-isolation`) y NO existe ningún parámetro que la seleccione.
(Previously: «Todo Manager PUEDE ver y editar la configuración» sin acotar a una compañía)

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
- ENTONCES responde `200` con la configuración de su compañía

#### Scenario: Manager de otra compañía no ve la ajena
- DADO una configuración guardada por el Manager de A con modelo `m-a`
- CUANDO el Manager de B hace `GET`
- ENTONCES el cuerpo no contiene `m-a` ni ningún valor de la configuración de A

### Requirement: Consulta de la configuración efectiva

`GET /api/asistente/configuracion` DEBE devolver la configuración efectiva de la compañía del token en un objeto JSON (camelCase) con los campos `provider`, `model` (PUEDE ser nulo), `timeoutSeconds`, `maxOutputTokens` y `effort` (este último PUEDE ser nulo), `sources` (origen `env`, `stored` o `default` de cada valor; el de la clave PUEDE ser también `none`), `credentialConfigured` (booleano), `credentialStorageAvailable` (booleano: hay clave maestra utilizable), `allowedProviders`, `concurrencyToken`, `updatedAt`, `updatedBy` y `applied` (si la configuración guardada es la que usa esta instancia). NUNCA DEBE devolver la clave, ni cifrada ni en claro; PUEDE devolver como máximo los últimos 4 caracteres. `PUT` DEBE responder `200` con el mismo cuerpo que `GET` (con el token de concurrencia nuevo). Los valores del entorno `Ai:*` (proveedor, modelo, endpoint y `Ai:ApiKey`) DEBEN usarse solo para la compañía demo. Para cualquier otra compañía, `provider`, `model` y la clave provienen únicamente de su fila; sin fila, `provider` DEBE ser `None` (origen `default`), `model` nulo (origen `default`), `credentialConfigured` falso (origen `none`), y `timeoutSeconds` y `maxOutputTokens` toman los valores por defecto de plataforma (origen `env`). La respuesta NO DEBE contener el identificador de la compañía.
(Previously: sin fila, todos los valores tomaban el origen `env` para cualquier instalación, incluida la clave del entorno)

#### Scenario: Compañía demo solo con entorno
- DADO que la compañía demo no tiene fila guardada y `Ai__Provider=Ollama`
- CUANDO el Manager demo hace `GET`
- ENTONCES cada valor tiene origen `env` y `credentialConfigured` refleja si el entorno tiene clave

#### Scenario: Compañía nueva sin configuración
- DADO una compañía no demo sin fila guardada y un entorno con `Ai__Provider=Anthropic` y `Ai__ApiKey` definidos
- CUANDO su Manager hace `GET`
- ENTONCES `provider` es `None` (origen `default`), `model` es nulo, `credentialConfigured` es `false` con origen `none`, y el cuerpo no contiene el valor de la clave del entorno

#### Scenario: Valores guardados
- DADO una fila guardada de la compañía con modelo `m-1`
- CUANDO el Manager hace `GET`
- ENTONCES el modelo es `m-1` con origen `stored`

#### Scenario: Campos de la respuesta
- DADO una configuración cualquiera
- CUANDO el Manager hace `GET`
- ENTONCES el cuerpo contiene `provider`, `model`, `timeoutSeconds`, `maxOutputTokens`, `effort`, `sources`, `credentialConfigured`, `credentialStorageAvailable`, `allowedProviders`, `concurrencyToken`, `updatedAt`, `updatedBy` y `applied`, y ninguna propiedad `companyId`

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

### Requirement: Deshabilitar, prioridad y restablecer

La configuración efectiva de una compañía DEBE resolverse así: para la compañía demo, fila guardada sobre entorno; para cualquier otra compañía, solo la fila guardada (sin fila, el asistente está deshabilitado). Un proveedor guardado `None` DEBE deshabilitar el asistente de esa compañía aunque el entorno defina uno (el endpoint del chat responde `503` a sus usuarios). `DELETE /api/asistente/configuracion` DEBE borrar la fila de la compañía del token y registrar auditoría; DEBE responder `204` incluso cuando no existe fila, y `409` sin borrar ni auditar si otro Manager de la misma compañía modificó la fila entre su lectura y el borrado. Tras borrar, la compañía demo vuelve a los valores del entorno y cualquier otra compañía queda con el asistente deshabilitado. La configuración de otra compañía NO DEBE verse afectada. Si la tabla no existe, DEBE resolverse como «sin fila».
(Previously: sin fila o con la tabla ausente se usaban siempre los valores del entorno; `DELETE` restablecía los valores del entorno para toda la instancia)

#### Scenario: Guardado gana al entorno en la demo
- DADO `Ai__Model=env-m` y una fila de la compañía demo con modelo `db-m`
- CUANDO se resuelve la configuración efectiva de la demo
- ENTONCES el modelo es `db-m`

#### Scenario: Deshabilitar
- DADO el entorno con proveedor activo y una fila de la compañía con proveedor `None`
- CUANDO un usuario de esa compañía envía un mensaje al chat
- ENTONCES responde `503`

#### Scenario: Restablecer en la demo
- DADO una fila guardada de la compañía demo
- CUANDO su Manager invoca `DELETE`
- ENTONCES responde `204`, la fila no existe y el siguiente `GET` muestra origen `env`

#### Scenario: Restablecer en otra compañía
- DADO una fila guardada de la compañía A (no demo)
- CUANDO su Manager invoca `DELETE`
- ENTONCES responde `204`, la fila no existe, el siguiente `GET` muestra `provider` = `None` con origen `default` y el chat de A responde `503`

#### Scenario: Restablecer sin fila
- DADO que la compañía no tiene fila
- CUANDO su Manager invoca `DELETE`
- ENTONCES responde `204` y no se inserta ninguna fila de auditoría

#### Scenario: Restablecer con token obsoleto
- DADO que el Manager A1 inicia `DELETE` y otro Manager de la misma compañía guarda una configuración nueva entre la lectura de la fila y el borrado
- CUANDO se ejecuta el borrado con el token leído por A1
- ENTONCES responde `409`, la fila nueva se conserva y no se inserta ninguna fila de auditoría

#### Scenario: Borrar solo la propia
- DADO filas de A y de B y un `DELETE` del Manager de A
- CUANDO termina
- ENTONCES la fila de B sigue presente con el mismo `xmin` y no hay auditoría nueva de B

#### Scenario: Tabla ausente
- DADO una base de datos sin la tabla de configuración
- CUANDO se resuelve la configuración efectiva de la compañía demo
- ENTONCES se usan los valores del entorno sin error (y la de cualquier otra compañía queda deshabilitada)

### Requirement: Auditoría de solo inserción

Cada guardado, restablecimiento y cambio de clave DEBE insertar una fila de auditoría con la compañía (`company_id` de la compañía del token), quién, cuándo, la acción y los valores previo y nuevo no secretos. La auditoría NO DEBE contener la clave (ni en claro ni cifrada). Las filas NO DEBEN modificarse ni borrarse desde la aplicación, y los triggers de solo inserción DEBEN seguir activos tras la migración a `company_id` (ver `company-isolation`). Las filas de auditoría de una compañía NO DEBEN ser visibles ni accesibles desde otra.
(Previously: la auditoría no registraba compañía)

#### Scenario: Registro de un cambio
- DADO un Manager de A que cambia el modelo de `a` a `b`
- CUANDO se guarda
- ENTONCES existe una fila con su identificador, la hora, la acción, los valores `a` y `b` y el `company_id` de A

#### Scenario: Cambio de clave
- DADO un `PUT` con clave nueva
- CUANDO se guarda
- ENTONCES la fila de auditoría indica que la clave cambió sin contener su valor

#### Scenario: Solo inserción
- DADO el código de la aplicación
- CUANDO se buscan operaciones de actualización o borrado sobre la auditoría
- ENTONCES no existen

#### Scenario: Trigger activo
- DADO la base migrada
- CUANDO se intenta `UPDATE` o `DELETE` sobre `assistant_settings_audit`
- ENTONCES PostgreSQL lo rechaza

#### Scenario: Auditoría por compañía
- DADO un guardado del Manager de A y otro del Manager de B
- CUANDO se consultan las filas de auditoría con el contexto de A
- ENTONCES solo se obtiene la fila de A

### Requirement: Página Web de configuración

La Web DEBE ofrecer `/asistente/configuracion` solo a Managers (enlace de menú y página protegida; un Employee es redirigido o ve acceso denegado). La página DEBE mostrar el aviso de protección de datos (los datos personales se envían a un tercero con la clave del Manager) y el aviso de alcance que NOMBRA la compañía, con el texto «Este cambio afecta a todas las personas de la compañía {nombre}», donde `{nombre}` es el nombre de la compañía tomado de la sesión (`companyName` del login o de `me`; ver `authentication`); la Web NO DEBE consultar otro endpoint para obtenerlo. DEBE renderizar todo texto como texto plano, NO DEBE usar almacenamiento del navegador para la configuración ni la clave, y DEBE vaciar el campo de la clave tras guardar. La página del chat NO DEBE cambiar para el resto de usuarios. La página NO DEBE afirmar que la configuración vale para toda la instancia o toda la empresa.
(Previously: el aviso decía que el cambio afecta a toda la empresa, sin nombrar la compañía)

#### Scenario: Acceso de Employee
- DADO un Employee autenticado
- CUANDO abre `/asistente/configuracion`
- ENTONCES no ve el formulario y el menú no muestra el enlace

#### Scenario: Avisos con el nombre de la compañía
- DADO un Manager de la compañía «Acme Norte» en la página
- CUANDO se muestra el formulario
- ENTONCES se ven el aviso de compartición de datos y el aviso «Este cambio afecta a todas las personas de la compañía Acme Norte»

#### Scenario: Nombre como texto plano
- DADO una compañía llamada `<b>x</b>`
- CUANDO se muestra el aviso
- ENTONCES el nombre se ve literalmente, sin interpretar HTML

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

## ADDED Requirements

### Requirement: Una configuración por compañía

`assistant_settings` DEBE contener a lo sumo una fila por compañía (clave primaria `company_id`; ver `company-isolation`) y el token de concurrencia (`xmin`) DEBE ser el de la fila de la compañía. `GET`, `PUT`, `DELETE` y la prueba de conexión DEBEN actuar solo sobre la fila de la compañía del token; NO DEBE existir ningún parámetro, cuerpo o cabecera que seleccione otra compañía. Un `PUT` de A NO DEBE leer, sobrescribir ni cambiar el `xmin` de la fila de B.

#### Scenario: Cada compañía su fila
- DADO los Managers de A y de B
- CUANDO cada uno guarda su configuración con un modelo distinto
- ENTONCES existen dos filas, cada una con su `company_id`, y cada `GET` devuelve el modelo propio

#### Scenario: PUT no toca la otra fila
- DADO filas de A y de B y el `xmin` de la fila de B anotado
- CUANDO el Manager de A hace `PUT`
- ENTONCES el `xmin` y el contenido de la fila de B no cambian

#### Scenario: Token de concurrencia ajeno
- DADO el `concurrencyToken` leído por el Manager de B y un `PUT` del Manager de A con ese token
- CUANDO se procesa
- ENTONCES responde `409` y no cambia ninguna fila

#### Scenario: Primera creación independiente
- DADO que A ya tiene fila y B no
- CUANDO el Manager de B guarda con el token de «sin fila»
- ENTONCES se crea la fila de B sin `409`

#### Scenario: Clave de A no usable por B
- DADO una clave guardada por A y la compañía B sin fila
- CUANDO el Manager de B hace `GET` y envía la prueba de conexión sin clave candidata
- ENTONCES `credentialConfigured` es `false` y la prueba no usa la clave de A (responde `400` en `provider`)

### Requirement: Recarga en caliente por compañía

El runtime del asistente DEBE mantener una instantánea de configuración por compañía (construida de forma perezosa en la primera pregunta de la compañía y sustituida cuando cambia la versión `xmin` de su fila), y la instantánea de una compañía NUNCA DEBE atender a otra. Un sondeo global DEBE leer todas las parejas `(company_id, xmin)` en una consulta y recargar solo las compañías cuya versión cambió, conservando la gracia y la regla «conservar la última configuración válida» por compañía. Tras guardar o restablecer, la instancia local DEBE aplicar el cambio de esa compañía de inmediato; las demás instancias, en unos 30 segundos. El número de instantáneas cargadas DEBE estar acotado (`Ai:MaxLoadedCompanies`, ver `assistant-configuration`), expulsando las de compañías menos usadas y liberando sus recursos (pipeline y cliente HTTP) al terminar las peticiones en curso. Una compañía sin fila no mantiene instantánea de proveedor (asistente deshabilitado). Un fallo transitorio al cargar la instantánea de una compañía (base de datos caída o agotamiento de tiempo) NO DEBE cachearse: la petición que lo sufre recibe la respuesta genérica de asistente no disponible y la siguiente petición de esa compañía DEBE reintentar la carga. Si la primera fila que se lee de una compañía (sin instantánea válida previa) es inválida, no se puede descifrar o tiene un proveedor no permitido, esa compañía DEBE quedar con el asistente deshabilitado; NUNCA DEBE recurrirse a la configuración o la clave de plataforma, ni siquiera para la compañía demo.

#### Scenario: Instantánea por compañía
- DADO A con proveedor y modelo propios y B con otros, ambos con un `IChatClient` falso
- CUANDO un usuario de A y otro de B preguntan
- ENTONCES cada pregunta usa el modelo y la clave de su compañía y nunca los de la otra

#### Scenario: Guardar en A no recarga B
- DADO instantáneas cargadas de A y de B
- CUANDO el Manager de A guarda un modelo nuevo
- ENTONCES la siguiente pregunta de A usa el modelo nuevo y la instantánea de B no se reconstruye

#### Scenario: Sondeo global
- DADO dos instancias y un guardado de B en la primera
- CUANDO transcurre el intervalo de sondeo
- ENTONCES la segunda recarga solo la instantánea de B y el sondeo ha leído las versiones de todas las compañías en una consulta

#### Scenario: Fila inválida de una compañía
- DADO una fila de B que no supera la validación o no se puede descifrar
- CUANDO el sondeo la lee
- ENTONCES B conserva su última configuración válida, A no se ve afectada y se registra un aviso sin secretos

#### Scenario: Primera fila inválida
- DADO una compañía B sin instantánea previa cuya fila no supera la validación o no se puede descifrar, y un entorno con proveedor y clave de plataforma
- CUANDO un usuario de B pregunta
- ENTONCES recibe `503` (asistente deshabilitado para B), no se usa la clave ni el proveedor de plataforma y no se invoca ningún modelo

#### Scenario: Fallo transitorio de lectura no se cachea
- DADO una base de datos que falla al leer la fila de la compañía A en la primera pregunta
- CUANDO el usuario de A pregunta de nuevo con la base de datos ya disponible
- ENTONCES la carga se reintenta, la instantánea de A se construye desde su fila y la pregunta recibe respuesta

#### Scenario: Tope y expulsión
- DADO `Ai__MaxLoadedCompanies=2` y preguntas de tres compañías con configuración
- CUANDO la tercera pregunta
- ENTONCES hay como máximo dos instantáneas cargadas, la menos usada se expulsa y una pregunta posterior de la expulsada la reconstruye desde su fila

#### Scenario: Petición en curso al expulsar
- DADO una respuesta en curso de la compañía expulsada
- CUANDO se expulsa su instantánea
- ENTONCES la respuesta termina sin error y los recursos se liberan después

### Requirement: Prueba de conexión acotada a la compañía

La prueba de conexión DEBE tomar los campos ausentes del cuerpo de la configuración efectiva de la compañía del token (ver «Consulta de la configuración efectiva») y NUNCA DEBE usar una clave guardada por otra compañía ni la clave de plataforma `Ai:ApiKey` salvo en la compañía demo. El límite de 3 peticiones por minuto sigue siendo por usuario.

#### Scenario: Prueba sin clave en una compañía nueva
- DADO una compañía no demo sin fila y un entorno con `Ai__ApiKey`
- CUANDO su Manager envía la prueba con `provider` = `Anthropic`, un modelo y sin clave candidata
- ENTONCES responde `400` con el error en `credential` y no se contacta con el proveedor

#### Scenario: Prueba en la demo con la clave de plataforma
- DADO la compañía demo, un entorno con `Ai__Provider=Anthropic` y `Ai__ApiKey`, y un `IChatClient` falso
- CUANDO su Manager envía la prueba sin cuerpo
- ENTONCES se usa la clave del entorno y responde `200` con el resultado de la prueba

#### Scenario: Clave candidata propia
- DADO una prueba del Manager de A con clave candidata
- CUANDO se invoca
- ENTONCES se usa esa clave y nunca la guardada por B
