# Delta para web-session

Cambio: `multi-compania`. Capacidad EXISTENTE (`openspec/specs/web-session/spec.md`). Contiene bloques `MODIFIED` (con el nombre exacto del requisito vigente) y `ADDED`. La Web sigue sin contener reglas de negocio ni permisos (los reales viven en la API): solo presenta y llama a la API.

> Aviso de delta que cambia escenarios vigentes:
> - La cabecera «Fuera de alcance: … registro, … alta o edición de usuarios …» de la especificación vigente deja de aplicar al registro de compañías y al alta de personas; siguen fuera de alcance la recuperación de contraseña, la edición y baja de personas y el «recordarme».
> - «Página de inicio de sesión»: con cambio de contraseña pendiente, el inicio de sesión navega a la página de cambio de contraseña en lugar de al Inicio o al `returnUrl`.
> - «Menú según el rol»: se añade el nombre de la compañía y las entradas nuevas; con cambio pendiente el menú se reduce.
> - «Estado de autenticación por claims»: la Web lee además `cname` y `mcp`.

Notas de verificación adicionales: igual que la especificación vigente (sin bUnit ni E2E; la lógica sin interfaz se cubre con xUnit en `TimeTracker.Web.Tests` y los escenarios de interfaz se verifican manualmente con evidencia en `verify-report.md`, con valores ficticios). Los textos de la interfaz DEBEN estar en español neutro.

## MODIFIED Requirements

### Requirement: Página de inicio de sesión

La ruta `/login` DEBE ser accesible sin sesión y mostrar un formulario con correo y contraseña que invoque `POST /api/auth/login` mediante el cliente tipado. Con éxito DEBE guardar la sesión y, si el usuario tiene el cambio de contraseña pendiente (`mustChangePassword`), navegar a `/cambiar-contrasena` (conservando un `returnUrl` seguro); en otro caso, navegar al Inicio (`/`, donde está el reproductor de fichaje) o a la ruta del parámetro `returnUrl` si es una ruta relativa segura (empieza por `/` y no por `//` ni `/\`); en otro caso DEBE ignorar `returnUrl`. Con un `401` DEBE mostrar un mensaje genérico en español que no distinga entre correo y contraseña (tampoco por compañía); con un `429`, un mensaje en español que pida esperar; ante un fallo de red u otra respuesta, un mensaje genérico en español sin datos técnicos. Si el correo o la contraseña están vacíos NO DEBE enviar la petición y DEBE avisar en español.
(Previously: tras un inicio de sesión correcto siempre navegaba al Inicio o al `returnUrl`)

#### Scenario: Inicio de sesión correcto
- DADO la API en marcha con los usuarios de demostración
- CUANDO se introduce `luis.empleado@example.test` y su contraseña de demostración en `/login`
- ENTONCES se navega a `/`, se ve el reproductor de fichaje y el menú muestra el nombre del usuario

#### Scenario: Cambio de contraseña pendiente
- DADO una persona recién dada de alta con su contraseña temporal
- CUANDO inicia sesión correctamente en `/login`
- ENTONCES se navega a `/cambiar-contrasena` y no al Inicio

#### Scenario: Regreso a la ruta de origen
- DADO `/login?returnUrl=/equipo` abierta
- CUANDO se inicia sesión correctamente como responsable sin cambio pendiente
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

### Requirement: Estado de autenticación por claims

El estado de autenticación DEBE derivarse del payload del token (claims `sub`, `name`, `email`, `role`, `cname` y `mcp`) y de su `exp`, sin validar la firma, que solo valida la API. El nombre de la compañía (`cname`) y el indicador de cambio pendiente (`mcp` = `true`) DEBEN estar disponibles para la interfaz; la Web NO DEBE usar el claim `cid` para ninguna decisión ni mostrarlo, y NO DEBE enviar ningún identificador de compañía a la API. Un token ausente, mal formado o caducado DEBE interpretarse como no autenticado y eliminarse del almacenamiento, sin mostrar errores técnicos.
(Previously: solo `sub`, `name`, `email`, `role` y `exp`)

#### Scenario: Token vigente
- DADO un token vigente de `marta.responsable@example.test` con `cname` = «Demo»
- CUANDO se carga la Web
- ENTONCES el usuario figura autenticado con su nombre, el rol `Manager` y la compañía «Demo»

#### Scenario: Cambio pendiente
- DADO un token vigente con `mcp` = `true`
- CUANDO se carga la Web
- ENTONCES el estado indica cambio de contraseña pendiente

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

#### Scenario: Sin identificador de compañía
- DADO el código de la Web
- CUANDO se buscan usos del claim `cid` y de `companyId` en peticiones
- ENTONCES no existen

### Requirement: Menú según el rol

El menú DEBE mostrar la marca «TimeTracker» y el enlace «Inicio»; sin sesión, además solo el enlace de inicio de sesión; con sesión, «Fichajes», el nombre del usuario junto al nombre de su compañía, «Cambiar contraseña» y «Cerrar sesión»; y «Equipo» y «Alta de personas» solo si el rol es `Manager`. Con el cambio de contraseña pendiente, el menú DEBE mostrar únicamente la marca, «Cambiar contraseña» y «Cerrar sesión». Cada entrada DEBE llevar un icono propio (SVG en línea, sin recursos externos), el nombre del usuario y «Cerrar sesión» DEBEN alinearse con el resto de entradas, y en pantallas estrechas el menú DEBE cerrarse al elegir una entrada. La plantilla (páginas de ejemplo y enlaces externos) NO DEBE aparecer en la interfaz. El nombre de la compañía se muestra como texto plano. No hay enlace público a `/registro`.
(Previously: sin nombre de compañía, sin «Cambiar contraseña» ni «Alta de personas», y sin menú reducido)

#### Scenario: Sin sesión
- DADO ninguna sesión
- CUANDO se carga la Web
- ENTONCES el menú muestra «Inicio» y el enlace de inicio de sesión, y no muestra «Fichajes», «Equipo», «Alta de personas» ni un enlace a `/registro`

#### Scenario: Empleado
- DADO una sesión de `luis.empleado@example.test`
- CUANDO se revisa el menú
- ENTONCES muestra «Fichajes», su nombre con la compañía, «Cambiar contraseña» y «Cerrar sesión», y no muestra «Equipo» ni «Alta de personas»

#### Scenario: Responsable
- DADO una sesión de `marta.responsable@example.test`
- CUANDO se revisa el menú
- ENTONCES muestra «Fichajes», «Equipo» y «Alta de personas»

#### Scenario: Cambio de contraseña pendiente
- DADO una sesión con `mcp` = `true`
- CUANDO se revisa el menú
- ENTONCES muestra solo la marca, «Cambiar contraseña» y «Cerrar sesión»

#### Scenario: Nombre de compañía como texto plano
- DADO una compañía llamada `<b>x</b>`
- CUANDO se revisa el menú
- ENTONCES se ve literalmente, sin interpretar HTML

## ADDED Requirements

### Requirement: Página /registro de compañía

La ruta `/registro` DEBE ser accesible sin sesión y mostrar un formulario con nombre de la compañía, nombre de la persona, correo, contraseña, confirmación de contraseña y código de invitación, que invoque `POST /api/auth/register-company` mediante el cliente tipado (sin `Authorization`). Con `201` DEBE guardar la sesión y navegar al Inicio. DEBE mostrar en español los errores por campo del `400`, un mensaje genérico de código no válido ante `403`, el aviso propio ante `409` (nombre o correo repetido, con el `detail` de la API), la petición de esperar ante `429` y un mensaje «El registro no está disponible» ante `401` o `404` (registro cerrado), sin disparar el flujo de cierre de sesión. La contraseña y el código DEBEN vaciarse tras cada respuesta, no DEBEN guardarse en `localStorage`, `sessionStorage` ni en la URL y se renderizan siempre como texto plano. Que la confirmación coincida PUEDE comprobarse en la Web (presentación); la política de contraseñas la impone la API. La Web NO DEBE conocer los códigos ni la bandera del servidor.

#### Scenario: Registro correcto
- DADO la API con el registro abierto y un código válido
- CUANDO se rellena y envía el formulario
- ENTONCES se guarda la sesión, se navega a `/` y el menú muestra el nombre de la persona y el de la nueva compañía

#### Scenario: Registro cerrado
- DADO la API con el registro cerrado (respuesta `401` o `404`)
- CUANDO se envía el formulario
- ENTONCES se muestra «El registro no está disponible», no se navega a `/login` y la página sigue operativa

#### Scenario: Código no válido
- DADO una respuesta `403`
- CUANDO se muestra el error
- ENTONCES se ve un mensaje genérico en español sin indicar qué falló y la contraseña y el código quedan vacíos

#### Scenario: Duplicados
- DADO una respuesta `409` con `detail` en español
- CUANDO se muestra el error
- ENTONCES se muestra ese `detail` y el formulario conserva nombre de compañía, nombre y correo

#### Scenario: Errores por campo
- DADO una respuesta `400` con errores en `password` y `email`
- CUANDO se muestran
- ENTONCES cada error aparece junto a su campo, en español

#### Scenario: Límite de tasa
- DADO una respuesta `429` con `Retry-After`
- CUANDO se muestra el error
- ENTONCES se pide esperar y la pantalla sigue operativa

#### Scenario: Sin almacenamiento del navegador
- DADO el código de la página
- CUANDO se buscan usos de `localStorage` o `sessionStorage` para la contraseña o el código
- ENTONCES no existen y la URL no contiene ninguno de los dos valores

### Requirement: Página /cambiar-contrasena

La ruta `/cambiar-contrasena` DEBE exigir sesión (sin sesión navega a `/login`) y mostrar un formulario con contraseña actual, contraseña nueva y confirmación, que invoque `POST /api/auth/change-password`. Con `200` DEBE sustituir la sesión por el token nuevo y navegar a un `returnUrl` seguro o al Inicio. DEBE mostrar en español los errores del `400` por campo y la petición de esperar ante `429`. Con el cambio pendiente (`mcp`), TODA ruta protegida distinta de esta DEBE redirigir a `/cambiar-contrasena` sin llamar a la API (la API responde `403` a esas rutas) y la página DEBE indicar que es obligatorio cambiar la contraseña temporal antes de continuar. Los campos de contraseña DEBEN vaciarse tras cada respuesta y NO DEBEN guardarse en el navegador.

#### Scenario: Cambio obligatorio
- DADO una sesión con `mcp` = `true`
- CUANDO se abre `/fichajes`
- ENTONCES se navega a `/cambiar-contrasena` y no se observa ninguna petición a `/api/time-entries`

#### Scenario: Cambio correcto
- DADO la sesión con cambio pendiente y una contraseña nueva válida
- CUANDO se envía el formulario
- ENTONCES se guarda el token nuevo, el menú vuelve al estado completo y se navega al Inicio

#### Scenario: Contraseña actual incorrecta
- DADO una respuesta `400` con error en `currentPassword`
- CUANDO se muestra
- ENTONCES el error aparece junto al campo en español y los campos de contraseña quedan vacíos

#### Scenario: Sin sesión
- DADO ninguna sesión
- CUANDO se abre `/cambiar-contrasena`
- ENTONCES se navega a `/login`

#### Scenario: Sin almacenamiento del navegador
- DADO el código de la página
- CUANDO se buscan usos de `localStorage` o `sessionStorage` para las contraseñas
- ENTONCES no existen

### Requirement: Página /equipo/alta para dar de alta personas

La ruta `/equipo/alta` DEBE exigir sesión con rol `Manager` (sin sesión navega a `/login`; un usuario sin el rol ve un mensaje en español de falta de permiso y la Web NO llama a `/api/employees`). El formulario DEBE pedir nombre, correo, rol (`Employee` o `Manager`) y, opcionalmente, responsable (elegido de la lista de `GET /api/team/members`), e invocar `POST /api/employees`. Con `201` DEBE mostrar la contraseña temporal devuelta UNA SOLA VEZ, como texto plano, con el aviso en español de que debe entregarse a la persona ahora y de que no se volverá a mostrar y de que deberá cambiarla en su primer inicio de sesión; la contraseña temporal NO DEBE guardarse en `localStorage`, `sessionStorage`, la URL ni el estado persistente, y DEBE desaparecer al cerrar el aviso o navegar a otra ruta. DEBE mostrar en español los errores por campo del `400`, el `409` por correo repetido o por tope de la compañía (con el `detail` de la API) y los `403`/`401` según el flujo común. NO DEBE ofrecer baja ni edición.

#### Scenario: Alta correcta
- DADO un Manager en `/equipo/alta`
- CUANDO envía nombre y correo válidos
- ENTONCES se muestra la contraseña temporal con el aviso de que no se volverá a mostrar

#### Scenario: La contraseña temporal desaparece
- DADO la contraseña temporal mostrada
- CUANDO se cierra el aviso o se navega a `/equipo`
- ENTONCES ya no está en pantalla ni en el almacenamiento del navegador

#### Scenario: Empleado en /equipo/alta
- DADO una sesión de `luis.empleado@example.test`
- CUANDO se abre `/equipo/alta`
- ENTONCES se muestra un mensaje en español de falta de permiso y no se observa ninguna petición a `/api/employees`

#### Scenario: Correo repetido o tope
- DADO una respuesta `409` con `detail` en español
- CUANDO se muestra el error
- ENTONCES se muestra ese `detail` y la pantalla sigue operativa

#### Scenario: Responsable elegido de la compañía
- DADO un Manager con reportes
- CUANDO abre el formulario
- ENTONCES la lista de responsables contiene solo personas devueltas por `GET /api/team/members` y la Web no envía ningún identificador de compañía

#### Scenario: Solo alta
- DADO la página `/equipo/alta`
- CUANDO se revisan sus controles
- ENTONCES no hay controles de baja ni de edición de personas

### Requirement: Textos de las páginas nuevas en español y sin datos técnicos

Todos los textos de `/registro`, `/cambiar-contrasena` y `/equipo/alta` DEBEN estar en español (registro neutro). Ningún mensaje DEBE mostrar trazas, nombres de excepciones, tokens, el código de invitación, identificadores internos ni identificadores de compañía. La lógica de presentación sin interfaz (mapeo de estados HTTP a mensajes, validación de coincidencia de la confirmación, redirección por cambio pendiente) DEBE estar en clases puras cubiertas con pruebas unitarias en `TimeTracker.Web.Tests` que no llaman a la API real.

#### Scenario: Textos en español
- DADO el recorrido de las tres páginas
- CUANDO se revisan etiquetas, botones y mensajes
- ENTONCES todos están en español

#### Scenario: Error inesperado
- DADO una respuesta `500` en cualquiera de las tres páginas
- CUANDO se muestra el error
- ENTONCES se muestra un mensaje genérico en español sin datos técnicos y la pantalla sigue operativa

#### Scenario: Pruebas unitarias de la lógica
- DADO `TimeTracker.Web.Tests`
- CUANDO se ejecuta `dotnet test`
- ENTONCES pasan las pruebas del mapeo de `400`, `403`, `404`, `409`, `429` y error genérico de las tres páginas y de la redirección por cambio pendiente

#### Scenario: Flujo manual
- DADO la API y la Web en marcha con el registro abierto y dos compañías
- CUANDO se registra una compañía, se da de alta a un empleado, el empleado cambia su contraseña temporal y cada Manager abre `/equipo`
- ENTONCES cada uno ve solo a las personas de su compañía y el resultado se registra en `verify-report.md` con valores ficticios
