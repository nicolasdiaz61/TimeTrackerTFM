# Especificación: assistant-web

Cambio: `asistente-ia`. Capacidad NUEVA; todos los requisitos son `ADDED`. Define la página `/asistente` de la Web, su enlace en el menú, el comportamiento de la conversación en memoria y los mensajes en español ante `400`, `401`, `429` y `503`. La Web solo presenta y llama a la API; no contiene reglas de negocio ni permisos (ver `web-session`).

Decisiones aceptadas: página propia, sin JS interop, historial solo en memoria del componente; respuesta como texto plano; cuando el asistente está deshabilitado (`Ai:Provider` ausente) la página sigue accesible y muestra un aviso claro construido a partir del `503` de la API, y el enlace del menú sigue visible.

Notas de verificación:
- La lógica de la conversación (historial, recorte, mapeo de errores a mensajes) vive en una clase pura probada en `TimeTracker.Web.Tests`; no hay bUnit ni pruebas E2E. El renderizado y el flujo con la API real se verifican a mano y se registran en `verify-report.md` con valores ficticios.
- El enlace del menú amplía el requisito «Menú según el rol» de `web-session` sin modificarlo: «Asistente» se añade a las entradas de sesión.

Fuera de alcance: streaming, persistencia de conversaciones, formato enriquecido (Markdown/HTML), panel flotante en Inicio.

## ADDED Requirements

### Requirement: Página /asistente para cualquier usuario autenticado

La Web DEBE ofrecer la ruta `/asistente` con un campo de mensaje, un botón de envío y la conversación en pantalla. DEBE exigir sesión (cualquier rol). Sin sesión DEBE navegar a `/login`. NO DEBE exigir el rol `Manager`.

#### Scenario: Sin sesión
- DADO ninguna sesión
- CUANDO se abre `/asistente`
- ENTONCES se navega a `/login`

#### Scenario: Empleado
- DADO una sesión de `luis.empleado@example.test`
- CUANDO se abre `/asistente`
- ENTONCES se muestra la página con el campo de mensaje

#### Scenario: Responsable
- DADO una sesión de `marta.responsable@example.test`
- CUANDO se abre `/asistente`
- ENTONCES se muestra la página con el campo de mensaje

### Requirement: Enlace en el menú para toda sesión

El menú DEBE mostrar el enlace «Asistente» con icono propio (SVG en línea, sin recursos externos) a todo usuario con sesión, empleado o responsable, y NO DEBE mostrarlo sin sesión. El enlace DEBE permanecer visible aunque el asistente esté deshabilitado en la API.

#### Scenario: Empleado y responsable
- DADO una sesión de empleado y otra de responsable
- CUANDO se revisa el menú de cada una
- ENTONCES ambas muestran «Asistente»

#### Scenario: Sin sesión
- DADO ninguna sesión
- CUANDO se carga la Web
- ENTONCES el menú no muestra «Asistente»

#### Scenario: Asistente deshabilitado
- DADO una API cuyo asistente está deshabilitado
- CUANDO se revisa el menú con sesión
- ENTONCES «Asistente» sigue visible

### Requirement: Envío y conversación en memoria

Al enviar, la Web DEBE llamar a `POST /api/asistente` con el mensaje y el historial de la conversación actual (solo turnos con rol `user` y `assistant`, hasta 10 turnos; si hay más, los más antiguos se descartan) y DEBE añadir a la pantalla el mensaje del usuario y la respuesta. Al construir la petición, el historial enviado DEBE limitarse además a un total de 8000 caracteres de texto (descartando los turnos más antiguos), y la conversación en pantalla a 500 turnos. Mientras se espera la respuesta DEBE mostrarse un indicador `role="status"` con el texto «Consultando…». El historial DEBE vivir solo en memoria del componente: NO DEBE guardarse en `sessionStorage`, `localStorage` ni en la URL, y se pierde al recargar. No DEBE enviarse un mensaje vacío o de solo espacios ni uno de más de 500 caracteres: el botón se desactiva o se muestra una indicación en español, sin llamar a la API. Mientras se espera la respuesta DEBE mostrarse un indicador de carga y DEBE impedirse un segundo envío.

#### Scenario: Primer mensaje
- DADO la página abierta y una API que responde `{ "reply": "Trabajó 8 horas." }`
- CUANDO se envía «¿Cuántas horas trabajé ayer?»
- ENTONCES la petición lleva `message` e `history` vacío, y se muestran el mensaje y la respuesta

#### Scenario: Segundo mensaje con historial
- DADO una conversación con un intercambio completo
- CUANDO se envía un segundo mensaje
- ENTONCES `history` contiene el turno `user` y el turno `assistant` anteriores, y ningún otro rol

#### Scenario: Historial acotado
- DADO una conversación con más de 10 turnos previos
- CUANDO se envía otro mensaje
- ENTONCES `history` contiene como máximo 10 turnos, los más recientes

#### Scenario: Historial acotado por caracteres
- DADO una conversación cuyos últimos turnos suman más de 8000 caracteres
- CUANDO se envía otro mensaje
- ENTONCES `history` suma como máximo 8000 caracteres y conserva los turnos más recientes

#### Scenario: Mensaje vacío o demasiado largo
- DADO el campo vacío, con solo espacios, o con más de 500 caracteres
- CUANDO se intenta enviar
- ENTONCES no se llama a la API y se indica el motivo en español

#### Scenario: Carga y doble envío
- DADO una petición en curso
- CUANDO se intenta enviar de nuevo
- ENTONCES se muestra el indicador de carga y no se emite una segunda petición

#### Scenario: Sin persistencia local
- DADO una conversación en curso
- CUANDO se inspeccionan `sessionStorage`, `localStorage` y la URL
- ENTONCES no contienen el texto de la conversación

### Requirement: Respuesta como texto plano

La respuesta del asistente y el mensaje del usuario DEBEN mostrarse como texto plano, sin interpretarse como HTML, Markdown ni `MarkupString`. La Web NO DEBE ejecutar ni enlazar contenido de la respuesta.

#### Scenario: HTML en la respuesta
- DADO una respuesta `<script>alert(1)</script><b>hola</b>`
- CUANDO se muestra
- ENTONCES el texto aparece literal y no se crea ningún elemento `script` ni `b`

#### Scenario: Salto de línea
- DADO una respuesta con saltos de línea
- CUANDO se muestra
- ENTONCES los saltos se conservan como texto sin interpretar marcado

### Requirement: Mensajes en español para errores de la API

La Web DEBE mostrar un mensaje en español, sin datos técnicos, para cada error de `POST /api/asistente`. Para `400`, `429` y `503` DEBE mostrar el `detail` en español que devuelve la API (textos fijos y sin datos técnicos, garantizados por `assistant-endpoint`); si el cuerpo no trae `detail` utilizable, DEBE usar un mensaje propio de respaldo: `400` (mensaje no válido, que se revise el texto), `429` (demasiadas consultas o tope diario alcanzado) y `503` (asistente no disponible). En un `429` con `Retry-After` DEBE indicarse además cuándo reintentar. Cualquier otro fallo o error de red (incluido `500`) DEBE mostrar un error genérico propio. La Web NO DEBE reproducir el cuerpo de ninguna otra respuesta, ni trazas, nombres de proveedor, claves ni identificadores. Un `401` DEBE seguir el flujo común de `web-session` (cerrar sesión y navegar a `/login` con `returnUrl`). Tras cualquier error la página DEBE seguir operativa; el turno pendiente se retira de la conversación pero el texto del usuario permanece en el campo para no obligar a reescribirlo.

#### Scenario: 429 con Retry-After
- DADO una respuesta `429` con `Retry-After: 30`
- CUANDO se muestra el error
- ENTONCES el mensaje está en español (el `detail` de la API), indica que se espere unos segundos antes de reintentar y la página sigue operativa

#### Scenario: 429 sin Retry-After
- DADO una respuesta `429` sin cabecera `Retry-After`
- CUANDO se muestra el error
- ENTONCES se muestra el mensaje de demasiadas consultas sin tiempo de espera

#### Scenario: 503 con detalle de la API
- DADO una respuesta `503` con `detail` «El asistente no está disponible en este momento. Inténtelo de nuevo más tarde.»
- CUANDO se muestra el error
- ENTONCES se muestra ese `detail` como aviso

#### Scenario: 503 sin detalle utilizable
- DADO una respuesta `503` sin `detail`
- CUANDO se muestra el error
- ENTONCES se muestra el mensaje propio de la Web de asistente no disponible

#### Scenario: Otros cuerpos no se reproducen
- DADO una respuesta `500` con un cuerpo que contiene texto reconocible
- CUANDO se muestra el error
- ENTONCES el mensaje es el genérico de la Web y no contiene ese texto

#### Scenario: 400
- DADO una respuesta `400` con `detail` en español
- CUANDO se muestra el error
- ENTONCES se muestra ese `detail`; sin `detail`, se indica en español que el mensaje no es válido

#### Scenario: Mensaje conservado tras el error
- DADO un envío que recibe `429`, `503` o un fallo de red
- CUANDO se muestra el error
- ENTONCES el turno pendiente no queda en la conversación, el texto sigue en el campo y no se incluye en el historial del siguiente envío

#### Scenario: Fallo de red o 500
- DADO un error de red, o una respuesta `500`
- CUANDO se muestra el error
- ENTONCES se muestra un mensaje genérico en español sin datos técnicos

#### Scenario: 401
- DADO una sesión cuyo token fue alterado
- CUANDO el envío recibe `401`
- ENTONCES se elimina la sesión y se navega a `/login` con `returnUrl` apuntando a `/asistente`

### Requirement: Aviso claro con el asistente deshabilitado

Cuando la API responde `503` al enviar, la página `/asistente` DEBE seguir accesible y DEBE mostrar un aviso persistente, visible y claro en español, construido con el `detail` del `503` de la API (con asistente deshabilitado: «El asistente no está disponible en este entorno.»; con proveedor caído: «El asistente no está disponible en este momento. Inténtelo de nuevo más tarde.»), distinto del error genérico. La página NO DEBE ocultarse ni redirigir y el enlace del menú NO DEBE ocultarse. El aviso se construye solo a partir del `503` de la API; la Web NO DEBE conocer la configuración del servidor ni consultar un endpoint de estado previo (el aviso aparece al primer `503`).

#### Scenario: Asistente deshabilitado
- DADO una API sin `Ai:Provider`
- CUANDO un usuario autenticado abre `/asistente` y envía un mensaje
- ENTONCES la página sigue visible, el enlace «Asistente» del menú sigue visible y se muestra el aviso «El asistente no está disponible en este entorno.»

#### Scenario: Aviso sin datos técnicos
- DADO el mismo caso
- CUANDO se revisa el aviso
- ENTONCES no menciona proveedor, configuración, claves ni detalles técnicos

#### Scenario: Recuperación
- DADO el aviso mostrado y después una respuesta `200`
- CUANDO se envía un nuevo mensaje
- ENTONCES la respuesta se muestra y el aviso desaparece

### Requirement: Textos en español y sin reglas de negocio

Todos los textos de la página DEBEN estar en español (registro neutro). La Web NO DEBE contener reglas de negocio ni permisos del asistente, NO DEBE llamar al proveedor de IA ni conocer su clave, y NO DEBE tener URL fijas: usa la `ApiBaseUrl` existente. La página NO DEBE afirmar ni ocultar capacidades según el rol: la autorización la impone la API.

#### Scenario: Textos
- DADO el recorrido de `/asistente`
- CUANDO se revisan etiquetas, botones, indicadores y mensajes
- ENTONCES todos están en español

#### Scenario: Sin llamadas al proveedor
- DADO la red registrada al usar `/asistente`
- CUANDO se revisan los destinos de las peticiones
- ENTONCES la única petición del asistente es `POST /api/asistente` a `ApiBaseUrl` con `Authorization: Bearer`

#### Scenario: Arquitectura de la Web
- DADO las pruebas `Web_DoesNotHardcodeApiUrl` y `Web_HasNoProjectReferences`
- CUANDO se ejecuta `dotnet test`
- ENTONCES pasan

### Requirement: Lógica de conversación probada sin navegador

La lógica de conversación (alta de turnos, recorte del historial a 10 turnos, validación de longitud del mensaje, mapeo de estados HTTP a mensajes) DEBE estar en una clase pura sin dependencia del navegador, cubierta con pruebas unitarias en `TimeTracker.Web.Tests` que NO llaman a la API real ni a un modelo.

#### Scenario: Pruebas unitarias de la lógica
- DADO `TimeTracker.Web.Tests`
- CUANDO se ejecuta `dotnet test`
- ENTONCES pasan las pruebas de recorte del historial, validación de longitud y mapeo de `400`, `429` (con y sin `Retry-After`), `503` y error genérico

#### Scenario: Flujo manual
- DADO la API y la Web en marcha con datos de demostración y el asistente habilitado
- CUANDO `luis.empleado@example.test` y `marta.responsable@example.test` preguntan por sus horas, y `marta` por un reporte
- ENTONCES las respuestas son coherentes con `/fichajes` y `/equipo` y el resultado se registra en `verify-report.md`
