# Especificación: web-session

## Purpose

Define la sesión en `TimeTracker.Web` (Blazor WebAssembly): página `/login`, almacenamiento del token en `sessionStorage` con copia en memoria, estado de autenticación derivado de los claims sin validar la firma, rutas protegidas, envío del token a la API y redirección a `/login` ante un `401` o una expiración, menú según el rol, página `/equipo` solo para el responsable y cierre de sesión. Capacidad NUEVA; todos los requisitos son `ADDED`. La Web solo oculta interfaz: los permisos reales viven en la API (`authentication`, `team-access`).

Decisiones aceptadas (D16 a D19, Q3, Q4): token en `sessionStorage` con copia en memoria y riesgo XSS aceptado y documentado en el ADR 0012; `JwtAuthenticationStateProvider` decodifica el payload y comprueba `exp` pero no valida la firma; `BearerTokenHandler` compartido por los clientes de fichajes y de equipo (el cliente de autenticación no lo usa); roles en inglés en el código y textos de interfaz en español.

Verificación: sin bUnit ni E2E; la lógica de presentación y de estado sin interfaz (formato de duraciones, hora local, meses y franjas, aviso de sesión caducada, estado de autenticación) se cubre con xUnit en `TimeTracker.Web.Tests`; los escenarios de esta capacidad se verifican manualmente en el navegador (flujo de empleado y de responsable) y la evidencia se registra en `verify-report.md`. Solo la compilación, el formato y las pruebas de arquitectura de la Web (sin URL fija ni referencias de proyecto) son automáticos. Los escenarios que exigirían esperar 30 minutos se comprueban editando el token de `sessionStorage` con las herramientas del navegador.

Fuera de alcance: refresh token, renovación silenciosa, «recordarme», registro, recuperación de contraseña, alta o edición de usuarios, hosting estático de la Web con CSP y la Web en compose.

## ADDED Requirements

### Requirement: Página de inicio de sesión

La ruta `/login` DEBE ser accesible sin sesión y mostrar un formulario con correo y contraseña que invoque `POST /api/auth/login` mediante el cliente tipado. Con éxito DEBE guardar la sesión y navegar al Inicio (`/`, donde está el reproductor de fichaje), o a la ruta del parámetro `returnUrl` si es una ruta relativa segura (empieza por `/` y no por `//` ni `/\`); en otro caso DEBE ignorar `returnUrl`. Con un `401` DEBE mostrar un mensaje genérico en español que no distinga entre correo y contraseña; con un `429`, un mensaje en español que pida esperar; ante un fallo de red u otra respuesta, un mensaje genérico en español sin datos técnicos. Si el correo o la contraseña están vacíos NO DEBE enviar la petición y DEBE avisar en español.

#### Scenario: Inicio de sesión correcto
- DADO la API en marcha con los usuarios de demostración
- CUANDO se introduce `luis.empleado@example.test` y su contraseña de demostración en `/login`
- ENTONCES se navega a `/`, se ve el reproductor de fichaje y el menú muestra el nombre del usuario

#### Scenario: Regreso a la ruta de origen
- DADO `/login?returnUrl=/equipo` abierta
- CUANDO se inicia sesión correctamente como responsable
- ENTONCES se navega a `/equipo`

#### Scenario: Redirección abierta rechazada
- DADO `/login?returnUrl=//ejemplo.test` y, por separado, `/login?returnUrl=https://ejemplo.test`
- CUANDO se inicia sesión correctamente
- ENTONCES se navega a `/` y no se abandona el origen de la Web

#### Scenario: Credenciales erróneas
- DADO `/login` abierta
- CUANDO se envía una contraseña incorrecta y, por separado, un correo inexistente
- ENTONCES ambos casos muestran el mismo mensaje genérico en español, la página sigue en `/login` y el campo de contraseña queda vacío

#### Scenario: Demasiados intentos
- DADO 10 intentos de login fallidos en un minuto
- CUANDO se envía el undécimo
- ENTONCES se muestra un mensaje en español indicando que debe esperar y la pantalla sigue operativa

#### Scenario: API caída
- DADO la API detenida
- CUANDO se envía el formulario de `/login`
- ENTONCES se muestra un mensaje genérico en español sin trazas ni detalles técnicos

#### Scenario: Campos vacíos
- DADO `/login` con el correo en blanco
- CUANDO se pulsa el botón de entrar
- ENTONCES se muestra un aviso en español y no se observa ninguna petición a `/api/auth/login` en la red

### Requirement: Almacenamiento del token de sesión

La Web DEBE guardar el token en `sessionStorage` (no en `localStorage`, cookies ni URL) y mantener una copia en memoria para los envíos a la API. Cerrar la pestaña DEBE terminar la sesión; recargar la página DEBE conservarla. El token NO DEBE mostrarse en mensajes de la interfaz ni escribirse en la consola.

#### Scenario: Token en sessionStorage
- DADO un inicio de sesión correcto
- CUANDO se inspecciona el almacenamiento del navegador
- ENTONCES el token está en `sessionStorage` y no hay token en `localStorage` ni en cookies

#### Scenario: Recarga de la página
- DADO una sesión iniciada
- CUANDO se recarga `/fichajes`
- ENTONCES la sesión sigue activa sin volver a pedir credenciales

#### Scenario: Copia en memoria
- DADO una sesión iniciada en la que se borra la clave del token de `sessionStorage` con las herramientas del navegador
- CUANDO se pulsa «Fichar entrada» sin recargar
- ENTONCES la petición sigue llevando el token

#### Scenario: El token no se expone
- DADO una sesión iniciada
- CUANDO se revisan la barra de direcciones, los textos de la página y la consola
- ENTONCES el token no aparece en ninguno

### Requirement: Estado de autenticación por claims

El estado de autenticación DEBE derivarse del payload del token (claims `sub`, `name`, `email` y `role`) y de su `exp`, sin validar la firma, que solo valida la API. Un token ausente, mal formado o caducado DEBE interpretarse como no autenticado y eliminarse del almacenamiento, sin mostrar errores técnicos.

#### Scenario: Token vigente
- DADO un token vigente de `marta.responsable@example.test`
- CUANDO se carga la Web
- ENTONCES el usuario figura autenticado con su nombre y el rol `Manager`

#### Scenario: Token caducado al cargar
- DADO un token en `sessionStorage` cuyo `exp` ya pasó
- CUANDO se carga `/fichajes`
- ENTONCES se elimina el token y se navega a `/login`

#### Scenario: Token mal formado
- DADO un texto cualquiera guardado como token
- CUANDO se carga la Web
- ENTONCES el usuario figura no autenticado, se limpia el almacenamiento y no aparece ningún error técnico

#### Scenario: Firma alterada
- DADO un token con el payload legible y vigente pero con la firma modificada
- CUANDO se abre `/fichajes`
- ENTONCES la Web lo trata como autenticado, la API responde `401` y el usuario vuelve a `/login`

### Requirement: Rutas protegidas

`/fichajes` DEBE exigir sesión y `/equipo` DEBE exigir sesión con rol `Manager`. Sin sesión, el acceso a cualquiera de ellas DEBE navegar a `/login`. Un usuario autenticado sin el rol necesario DEBE ver un mensaje en español de falta de permiso y la Web NO DEBE llamar a `/api/team`. Ocultar o impedir la ruta en la Web no sustituye la autorización de la API.

#### Scenario: /fichajes sin sesión
- DADO ninguna sesión
- CUANDO se abre `/fichajes`
- ENTONCES se navega a `/login`

#### Scenario: /equipo sin sesión
- DADO ninguna sesión
- CUANDO se abre `/equipo`
- ENTONCES se navega a `/login`

#### Scenario: Empleado en /equipo
- DADO una sesión de `luis.empleado@example.test`
- CUANDO se abre `/equipo`
- ENTONCES se muestra un mensaje en español de falta de permiso y no se observa ninguna petición a `/api/team/…`

#### Scenario: Responsable en /equipo
- DADO una sesión de `marta.responsable@example.test`
- CUANDO se abre `/equipo`
- ENTONCES se muestra la página del equipo

### Requirement: Envío del token y gestión del 401

Todas las peticiones a la API (salvo el propio login) DEBEN llevar `Authorization: Bearer <token>` mediante un manejador común, y el token NO DEBE enviarse a ningún otro destino. Una respuesta `401` a una petición con sesión DEBE eliminar la sesión y navegar a `/login` conservando la ruta de origen en `returnUrl`. Un `401` del propio login NO DEBE provocar esa navegación (muestra el error de credenciales). Una respuesta `403` NO DEBE cerrar la sesión.

#### Scenario: Cabecera Bearer
- DADO una sesión iniciada
- CUANDO se abre `/fichajes`
- ENTONCES la petición `GET /api/time-entries` lleva `Authorization: Bearer` y ninguna petición a otro origen lleva esa cabecera

#### Scenario: Login sin cabecera
- DADO ninguna sesión
- CUANDO se envía el formulario de `/login`
- ENTONCES la petición `POST /api/auth/login` no lleva `Authorization`

#### Scenario: 401 con sesión
- DADO una sesión cuyo token fue alterado en su firma
- CUANDO se pulsa «Fichar entrada»
- ENTONCES la API responde `401`, se elimina la sesión y se navega a `/login` con `returnUrl` apuntando a `/fichajes`

#### Scenario: Expiración local
- DADO una sesión cuyo `exp` en el token almacenado se ha editado a un instante pasado
- CUANDO se navega a `/fichajes`
- ENTONCES se elimina el token y se navega a `/login` con `returnUrl` apuntando a `/fichajes`

#### Scenario: 403 no cierra la sesión
- DADO una sesión de empleado en la que se invoca `GET /api/team/members` desde las herramientas del navegador
- CUANDO la API responde `403`
- ENTONCES la sesión sigue activa y el menú no cambia

### Requirement: Menú según el rol

El menú DEBE mostrar la marca «TimeTracker» y el enlace «Inicio»; sin sesión, además solo el enlace de inicio de sesión; con sesión, «Fichajes», el nombre del usuario y «Cerrar sesión»; y «Equipo» solo si el rol es `Manager`. Cada entrada DEBE llevar un icono propio (SVG en línea, sin recursos externos), el nombre del usuario y «Cerrar sesión» DEBEN alinearse con el resto de entradas, y en pantallas estrechas el menú DEBE cerrarse al elegir una entrada. La plantilla (páginas de ejemplo y enlaces externos) NO DEBE aparecer en la interfaz.

#### Scenario: Sin sesión
- DADO ninguna sesión
- CUANDO se carga la Web
- ENTONCES el menú muestra «Inicio» y el enlace de inicio de sesión, y no muestra «Fichajes» ni «Equipo»

#### Scenario: Empleado
- DADO una sesión de `luis.empleado@example.test`
- CUANDO se revisa el menú
- ENTONCES muestra «Fichajes», su nombre y «Cerrar sesión», y no muestra «Equipo»

#### Scenario: Responsable
- DADO una sesión de `marta.responsable@example.test`
- CUANDO se revisa el menú
- ENTONCES muestra «Fichajes» y «Equipo»

### Requirement: Página Equipo

La ruta `/equipo` DEBE listar, con `GET /api/team/members`, los miembros del responsable (nombre visible) y permitir elegir uno para ver sus fichajes con `GET /api/team/members/{memberId}/time-entries`, con la misma vista mensual que `/fichajes` (navegación de mes, barras de franjas por día en hora local y totales del mes). Al cambiar de mes DEBE conservar el miembro elegido y al cambiar de miembro DEBE conservar el mes. NO DEBE ofrecer botones de fichar ni editar. Un equipo vacío DEBE mostrar un mensaje en español. Una respuesta `404` o `403` DEBE mostrarse como mensaje en español sin romper la pantalla.

#### Scenario: Lista de miembros
- DADO una sesión de `marta` cuyo equipo es `ana` y `luis`
- CUANDO se abre `/equipo`
- ENTONCES se listan `ana` y `luis` y no `pablo`

#### Scenario: Fichajes de un miembro
- DADO `luis` con fichajes en el mes actual
- CUANDO el responsable elige a `luis`
- ENTONCES ve las filas por día con las franjas del mes actual en hora local y los totales del mes

#### Scenario: Cambio de mes conservando al miembro
- DADO `luis` elegido y el mes actual visible
- CUANDO el responsable pulsa «Mes anterior»
- ENTONCES se muestra el mes anterior de `luis` y el selector sigue en `luis`

#### Scenario: Cambio de miembro
- DADO la vista de `luis` en un mes anterior
- CUANDO se elige a `ana`
- ENTONCES se muestran los fichajes de `ana` del mismo mes y solo los suyos

#### Scenario: Solo lectura
- DADO la página `/equipo`
- CUANDO se revisan sus controles
- ENTONCES no hay botones de fichar ni de editar

#### Scenario: Equipo vacío
- DADO un responsable sin reportes
- CUANDO abre `/equipo`
- ENTONCES se muestra un mensaje en español de que no tiene personas a su cargo

#### Scenario: Miembro que ya no es del equipo
- DADO un miembro elegido cuya relación se eliminó después de cargar la lista
- CUANDO la API responde `404`
- ENTONCES se muestra un mensaje en español y la pantalla sigue operativa

### Requirement: Aviso de sesión caducada

Cuando la sesión termina por un token caducado o por un `401` de la API y se navega a `/login`, la página DEBE mostrar un aviso en español («Su sesión ha caducado. Inicie sesión de nuevo.») sin conservarlo en la URL: el aviso viaja como estado en memoria, se muestra una sola vez y NO DEBE contener datos personales. NO DEBE aparecer en un inicio de sesión normal, ni tras el cierre voluntario de sesión, ni tras un inicio de sesión correcto. `returnUrl` y su validación contra redirecciones abiertas DEBEN conservarse.

#### Scenario: Token caducado
- DADO un token almacenado cuyo `exp` ya pasó
- CUANDO se abre `/fichajes`
- ENTONCES se navega a `/login?returnUrl=%2Ffichajes` y se muestra el aviso de sesión caducada

#### Scenario: 401 de la API
- DADO una sesión con la firma del token alterada
- CUANDO la API responde `401`
- ENTONCES se navega a `/login` con `returnUrl` y se muestra el aviso

#### Scenario: Sin aviso en un acceso normal
- DADO ninguna sesión previa, o el cierre voluntario con «Cerrar sesión»
- CUANDO se abre `/login`
- ENTONCES no se muestra el aviso

### Requirement: Página de inicio

La ruta `/` DEBE presentar TimeTracker en español: qué es la aplicación y, según la sesión, un enlace para iniciar sesión (anónimo) o, con sesión (empleado o responsable), el reproductor de fichaje (ver `time-entries-ui`), un acceso a «Fichajes» y, solo para el rol `Manager`, a «Equipo». La página no encontrada DEBE estar en español con un enlace al inicio, el documento DEBE declarar `lang="es"` y su título DEBE llevar la marca «TimeTracker».

#### Scenario: Inicio según la sesión
- DADO una sesión anónima, de empleado o de responsable
- CUANDO se abre `/`
- ENTONCES se ofrece iniciar sesión sin reproductor, o el reproductor y «Fichajes», o el reproductor, «Fichajes» y «Equipo», respectivamente

### Requirement: Cierre de sesión

La acción «Cerrar sesión» DEBE eliminar el token de `sessionStorage` y de la memoria, actualizar el estado de autenticación y navegar a `/login`. Después, las rutas protegidas DEBEN redirigir a `/login` y ninguna petición DEBE llevar el token anterior.

#### Scenario: Cierre de sesión
- DADO una sesión iniciada
- CUANDO se pulsa «Cerrar sesión»
- ENTONCES `sessionStorage` no contiene el token, se navega a `/login` y el menú vuelve al estado sin sesión

#### Scenario: Ruta protegida tras cerrar
- DADO la sesión cerrada
- CUANDO se vuelve atrás con el navegador o se abre `/fichajes`
- ENTONCES se navega a `/login` y la red no registra ninguna petición con `Authorization`

### Requirement: Textos en español y sin datos técnicos

Todos los textos de las páginas de sesión y de equipo DEBEN estar en español (registro neutro). Ningún mensaje DEBE mostrar trazas, nombres de excepciones, tokens ni identificadores internos.

#### Scenario: Textos en español
- DADO el recorrido de `/login`, `/fichajes` y `/equipo`
- CUANDO se revisan etiquetas, botones y mensajes
- ENTONCES todos están en español

#### Scenario: Error inesperado
- DADO una respuesta `500` de la API
- CUANDO ocurre en `/fichajes` o `/equipo`
- ENTONCES se muestra un mensaje genérico en español sin datos técnicos y la pantalla sigue operativa

### Requirement: Verificación y límites de la Web

La Web NO DEBE contener reglas de negocio ni permisos, NO DEBE referenciar proyectos de `src/` y NO DEBE tener URL fijas (`ApiBaseUrl` sigue en `wwwroot/appsettings.json`). La compilación, el formato y las pruebas de arquitectura de la Web DEBEN ser automáticos. El flujo manual de empleado y de responsable DEBE registrarse en `verify-report.md` con valores ficticios.

#### Scenario: Compilación y formato
- DADO `TimeTracker.Web` con el cambio aplicado
- CUANDO se ejecutan `dotnet build -warnaserror` y `dotnet format --verify-no-changes`
- ENTONCES ambos terminan con `0`

#### Scenario: Sin URL fija ni referencias
- DADO las pruebas de arquitectura `Web_DoesNotHardcodeApiUrl` y `Web_HasNoProjectReferences`
- CUANDO se ejecuta `dotnet test`
- ENTONCES pasan

#### Scenario: Flujo manual del empleado
- DADO la API y la Web en marcha con los usuarios de demostración y el origen `http://localhost:5172` permitido
- CUANDO `luis.empleado@example.test` inicia sesión, ficha entrada y salida, abre `/equipo` y cierra sesión
- ENTONCES el flujo funciona sin errores de CORS, ve solo sus fichajes, `/equipo` muestra falta de permiso y el resultado se registra en `verify-report.md`

#### Scenario: Flujo manual del responsable
- DADO el mismo entorno
- CUANDO `marta.responsable@example.test` inicia sesión, ficha, abre `/equipo`, elige a `luis` y a `ana`, y cierra sesión
- ENTONCES ve los fichajes de ambos en solo lectura, no ve a `pablo` y el resultado se registra en `verify-report.md`
