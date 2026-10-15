# Delta para time-entries-ui

Cambio: `fichajes-vista-mensual`. Las acciones de fichaje pasan al reproductor del Inicio; `/fichajes` es de consulta y muestra el historial por mes, un día por fila, con una barra de franjas en hora local. Se retiran los requisitos de la tabla plana en UTC, los botones de `/fichajes` y el formato «N minutos (h:mm)». Los dos requisitos modificados reemplazan por completo a los vigentes del mismo nombre.

## ADDED Requirements

### Requirement: Reproductor de fichaje en el Inicio

La ruta `/` DEBE mostrar, solo con sesión iniciada (empleado o responsable), un control tipo reproductor que consulta `GET /api/time-entries` con la ventana de hoy en hora local y ofrece únicamente las acciones válidas para el `status` recibido: `Idle` → «Iniciar jornada» (icono de reproducir); `Working` → «Pausa» (icono de pausa), «Comida» (icono de cubiertos) y «Finalizar jornada» (icono de detener); `OnBreak` y `OnMealBreak` → «Reanudar» (reproducir) y «Finalizar jornada». Cada botón invoca su endpoint (`clock-in`, `start-break`, `start-meal-break`, `end-break`, `clock-out`) y tras una respuesta correcta DEBE recargar el estado. Mientras haya una petición en curso, los botones DEBEN estar deshabilitados. Un `409` DEBE mostrarse con el mensaje de la API y recargar el estado. Un anónimo NO DEBE ver el reproductor, solo el texto con «Iniciar sesión». El reproductor DEBE mostrar un indicador de estado con texto («Sin fichar», «Trabajando», «En pausa», «En pausa de comida»), un cronómetro en vivo del tramo actual y el total trabajado hoy (hora local) y DEBE ser accesible: botones con `aria-label`, foco visible, objetivos táctiles de al menos 44 px, contraste suficiente, cronómetro con `role="timer"` y cambio de estado anunciado con `role="status"`.

#### Scenario: Sin fichar
- DADO un empleado sin intervalo abierto
- CUANDO abre `/`
- ENTONCES ve el estado «Sin fichar», un único botón «Iniciar jornada» y el cronómetro en cero

#### Scenario: Trabajando
- DADO un empleado sin fichar
- CUANDO pulsa «Iniciar jornada»
- ENTONCES el estado es «Trabajando», el cronómetro avanza cada segundo y se ofrecen «Pausa», «Comida» y «Finalizar jornada»

#### Scenario: Pausa y comida
- DADO un empleado trabajando
- CUANDO pulsa «Pausa» o «Comida»
- ENTONCES el estado es «En pausa» o «En pausa de comida», se ofrecen «Reanudar» y «Finalizar jornada» y el total trabajado hoy deja de avanzar

#### Scenario: Reanudar y finalizar
- DADO un empleado en pausa
- CUANDO pulsa «Reanudar» y después «Finalizar jornada»
- ENTONCES el estado vuelve a «Trabajando» y después a «Sin fichar»

#### Scenario: Estado obsoleto
- DADO un empleado que pulsa «Reanudar» cuando otra pestaña ya terminó la pausa
- CUANDO el servidor responde `409`
- ENTONCES se muestra el mensaje en español de la API y el reproductor se recarga con el estado real

#### Scenario: Jornada abierta de un día anterior
- DADO un empleado con un intervalo de trabajo abierto desde ayer
- CUANDO abre `/`
- ENTONCES el estado es «Trabajando» y el tramo actual cuenta desde la entrada de ayer

#### Scenario: Anónimo
- DADO ninguna sesión iniciada
- CUANDO se abre `/`
- ENTONCES no hay reproductor, se ve el texto con «Iniciar sesión» y no se observa ninguna petición a `/api/time-entries`

#### Scenario: Responsable
- DADO una sesión de responsable
- CUANDO abre `/`
- ENTONCES ve el mismo reproductor que un empleado, además del acceso a «Equipo»

### Requirement: Página /fichajes de consulta mensual

La ruta `/fichajes` DEBE exigir una sesión iniciada; sin sesión DEBE navegar a `/login`. Con sesión DEBE mostrar el estado actual, un enlace «Ir al Inicio para fichar» (la página NO DEBE tener botones de fichar) y el historial del **mes** elegido (el actual al abrir) pidiendo `GET /api/time-entries?from=…&to=…` con el inicio del mes y el del mes siguiente en hora local convertidos a UTC. DEBE mostrar el nombre del mes en español («octubre de 2026»), botones «Mes anterior», «Mes siguiente» y «Mes actual», y NO DEBE permitir meses futuros: en el mes actual «Mes siguiente» y «Mes actual» están deshabilitados. DEBE mostrar los totales del mes (trabajado, pausas, comidas y días con fichajes) calculados sobre los tramos recortados por día. Un mes sin intervalos DEBE mostrar un estado vacío claro con el nombre del mes. El estado visible es el del servidor aunque el intervalo abierto no esté en el mes mostrado.

#### Scenario: Mes actual
- DADO un empleado con fichajes este mes
- CUANDO abre `/fichajes`
- ENTONCES ve el mes actual con una fila por día (el más reciente primero, sin días futuros), los totales del mes y «Mes siguiente» deshabilitado

#### Scenario: Mes anterior y vuelta
- DADO el mes actual visible
- CUANDO pulsa «Mes anterior» y después «Mes actual»
- ENTONCES ve el mes anterior completo, con el rótulo en español, y después el actual

#### Scenario: Mes vacío
- DADO un mes sin intervalos
- CUANDO se muestra
- ENTONCES aparece un mensaje de que no hay fichajes en ese mes y los totales son cero

#### Scenario: Sin sesión
- DADO ninguna sesión iniciada
- CUANDO se abre `/fichajes`
- ENTONCES se navega a `/login` y no se observa ninguna petición a `/api/time-entries`

#### Scenario: Solo los fichajes propios
- DADO las sesiones de `luis` y de `ana`, cada una con sus intervalos
- CUANDO cada una abre `/fichajes`
- ENTONCES cada vista contiene únicamente los intervalos de su titular

#### Scenario: Sin acciones de fichaje
- DADO un empleado sin fichar
- CUANDO abre `/fichajes`
- ENTONCES no hay botones «Iniciar jornada», «Pausa», «Comida», «Reanudar» ni «Finalizar jornada»; hay un enlace al Inicio

### Requirement: Barra de franjas por día

Cada día del mes DEBE mostrarse como una fila con la etiqueta del día (día de la semana y número, en español), una barra de franjas a escala sobre un eje horario común a todas las filas del mes y el total del día (trabajado, pausas y comidas). Las franjas son `Trabajo`, `Pausa` y `Comida` con colores distintos y una leyenda con texto. El eje DEBE ir de 06:00 a 22:00 como mínimo y ampliarse a horas enteras si algún tramo del mes empieza antes o acaba después. Un intervalo que cruza medianoche DEBE repartirse entre los días. El tramo abierto de hoy DEBE dibujarse hasta «ahora». Los días sin intervalos DEBEN atenuarse y los sábados y domingos DEBEN distinguirse. Cada franja DEBE ofrecer un tooltip con la hora de inicio y fin y la duración, y la barra DEBE tener un texto accesible con todas las franjas del día; bajo el gráfico DEBE haber un detalle desplegable con una tabla (día, tipo, inicio, fin, duración) para pantallas táctiles. La barra NO DEBE usar estilos en línea, librerías de gráficos ni recursos externos: DEBE funcionar con la CSP de la Web (`style-src 'self'`), con SVG de atributos numéricos y colores por clases CSS.

#### Scenario: Franjas a escala
- DADO un día con trabajo de 08:00 a 13:00, comida de 13:00 a 14:00 y trabajo de 14:00 a 17:00 (hora local)
- CUANDO se muestra
- ENTONCES hay tres franjas contiguas posicionadas según el eje del mes y el total del día es «7 h 0 min» de trabajo y «1 h» de comida

#### Scenario: Eje comparable
- DADO un mes cuyos tramos están entre 08:00 y 18:00
- CUANDO se muestra
- ENTONCES todas las filas usan el eje 06:00–22:00

#### Scenario: Eje ampliado
- DADO un tramo que acaba a las 23:30 algún día del mes
- CUANDO se muestra
- ENTONCES todas las filas usan un eje que llega hasta las 24:00

#### Scenario: Intervalo que cruza medianoche
- DADO un intervalo de trabajo de 22:00 a 02:00
- CUANDO se muestra el mes
- ENTONCES el primer día tiene un tramo de 22:00 a 24:00 y el siguiente de 00:00 a 02:00

#### Scenario: Tramo abierto de hoy
- DADO un empleado trabajando desde las 09:00 locales
- CUANDO abre `/fichajes`
- ENTONCES la fila de hoy dibuja la franja hasta la hora actual y el total de hoy incluye ese tramo

#### Scenario: Días sin fichajes y fines de semana
- DADO un mes con días sin intervalos y fines de semana
- CUANDO se muestra
- ENTONCES esos días aparecen atenuados y los fines de semana se distinguen de los laborables

#### Scenario: Texto accesible y tooltip
- DADO un día con franjas
- CUANDO se pasa el ratón por una franja o se lee la barra con un lector de pantalla
- ENTONCES se oye o lee el tipo, la hora de inicio y fin y la duración («Comida de 13:00 a 14:00, 1 hora»)

#### Scenario: Compatible con la CSP
- DADO la imagen de la Web servida con su CSP (`style-src 'self'`)
- CUANDO se abre `/fichajes`
- ENTONCES las franjas se pintan con su color y la consola no muestra violaciones de CSP

### Requirement: Presentación en hora local

Los días y las horas DEBEN mostrarse en la zona horaria del navegador, con formato `HH:mm` y días agrupados por fecha local, respetando el cambio de hora de marzo y octubre. Los límites del mes que se envían a la API DEBEN ser las 00:00 locales del primer día del mes y del primer día del mes siguiente, en UTC. La zona horaria se obtiene del navegador mediante un script propio del mismo origen (`js/local-time.js`); la lógica de cálculo NO DEBE depender de una base de datos de zonas en WebAssembly. Las duraciones DEBEN formatearse en español con plurales correctos: en forma corta («7 h 30 min», «45 min», «1 h») en la interfaz y en forma hablada («1 hora y 5 minutos», «2 horas», «1 minuto») en los textos accesibles. Los nombres de mes y de día de la semana DEBEN estar en español.

#### Scenario: Hora local de España
- DADO un navegador en `Europe/Madrid` y un intervalo con `clockInAt` = `2026-10-12T06:00:00Z`
- CUANDO se muestra
- ENTONCES aparece el día 12 a las `08:00`

#### Scenario: Límites del mes con cambio de hora
- DADO un navegador en `Europe/Madrid` y octubre de 2026
- CUANDO se pide el mes
- ENTONCES `from` = `2026-09-30T22:00:00Z` y `to` = `2026-10-31T23:00:00Z`

#### Scenario: Plurales
- DADO duraciones de 0, 1, 60, 65 y 150 minutos
- CUANDO se formatean
- ENTONCES la forma corta es «0 min», «1 min», «1 h», «1 h 5 min» y «2 h 30 min», y la hablada «0 minutos», «1 minuto», «1 hora», «1 hora y 5 minutos» y «2 horas y 30 minutos»

## MODIFIED Requirements

### Requirement: Mensajes de error en español

Una respuesta `409` DEBE mostrar al usuario el `detail` en español del ProblemDetails sin recargar la página ni romper la pantalla. Una respuesta `401` NO DEBE mostrarse como error de la pantalla: DEBE eliminar la sesión y navegar a `/login` (ver `web-session`). Un fallo de red o una respuesta inesperada DEBE mostrar un mensaje genérico en español sin trazas ni datos técnicos. Esto aplica al reproductor del Inicio, a `/fichajes` y a `/equipo`.

#### Scenario: Doble entrada
- DADO un empleado con un fichaje abierto desde otra pestaña y el reproductor mostrando «Iniciar jornada»
- CUANDO pulsa «Iniciar jornada»
- ENTONCES se muestra el mensaje en español de la API y el reproductor se recarga con el estado real

#### Scenario: Salida sin entrada
- DADO un empleado sin fichaje abierto y el reproductor mostrando «Finalizar jornada» por un estado obsoleto
- CUANDO pulsa «Finalizar jornada»
- ENTONCES se muestra el mensaje en español de la API

#### Scenario: API caída
- DADO la API detenida
- CUANDO se abre `/`, `/fichajes` o se pulsa un botón
- ENTONCES se muestra un mensaje genérico en español y la pantalla sigue operativa

#### Scenario: Sesión caducada en un botón
- DADO una sesión cuyo token fue rechazado por la API
- CUANDO se pulsa «Iniciar jornada» y la API responde `401`
- ENTONCES se elimina la sesión, se navega a `/login` y no se muestra un mensaje de error técnico

#### Scenario: Ventana rechazada
- DADO una respuesta `400` de la API a una ventana de fechas
- CUANDO se muestra
- ENTONCES aparece el motivo en español de la API y la pantalla sigue operativa

### Requirement: Cliente tipado y URL base configurable

La Web DEBE acceder a la API mediante `TimeEntriesApiClient` tipado, con la URL base tomada de `ApiBaseUrl` en `wwwroot/appsettings.json`, sin URL fijas en el código. Cada petición a la API DEBE llevar el token de sesión como `Authorization: Bearer` (ver `web-session`). El listado se pide con `from` y `to` en UTC con sufijo `Z`. Los DTO se duplican en Web; NO DEBE haber `ProjectReference` hacia proyectos de `src/`. La Web NO DEBE enviar ningún `employeeId`. Si `ApiBaseUrl` falta o no es una URI absoluta `http`/`https`, la Web DEBE fallar al arrancar con un mensaje en español que nombre `ApiBaseUrl`.

#### Scenario: ApiBaseUrl ausente o inválida
- DADO `ApiBaseUrl` ausente o con el valor `no-es-una-url`
- CUANDO arranca la Web
- ENTONCES el arranque falla con una `InvalidOperationException` cuyo mensaje, en español, contiene `ApiBaseUrl`

#### Scenario: URL base
- DADO `wwwroot/appsettings.json` con `ApiBaseUrl` = `http://localhost:5274`
- CUANDO la página llama a la API
- ENTONCES las peticiones van a esa base

#### Scenario: Sin referencias de proyecto
- DADO `TimeTracker.Web.csproj`
- CUANDO se ejecuta `dotnet list TimeTracker.Web reference`
- ENTONCES no lista ningún proyecto de `src/`

#### Scenario: Token en las peticiones
- DADO una sesión iniciada
- CUANDO se abre `/`, se pulsa «Iniciar jornada» y se abre `/fichajes`
- ENTONCES las peticiones a `http://localhost:5274/api/time-entries…` llevan `Authorization: Bearer`, el listado lleva `from` y `to` y ninguna incluye `employeeId` en ruta, consulta, cabecera ni cuerpo

#### Scenario: Flujo de navegador registrado
- DADO la API (en `Development`, con `ConnectionStrings__Default`, `Jwt__SigningKey`, `Seed__DemoUsers=true`, `Seed__DemoPassword`, `Seed__DemoData=true` y `Database__ApplyMigrationsOnStartup=true`, o con `docker compose up`) y la Web en marcha, y el origen de la Web (p. ej. `Cors__AllowedOrigins__0=http://localhost:5172`) permitido en `Cors:AllowedOrigins`
- CUANDO se inicia sesión con un usuario de demostración, se ficha con el reproductor y se revisa el mes en `/fichajes`
- ENTONCES el flujo funciona sin errores de CORS ni de CSP y se registra en `verify-report.md`

## REMOVED Requirements

### Requirement: Página /fichajes con listado y total

Sustituido por «Página /fichajes de consulta mensual».

### Requirement: Botones de fichaje

Sustituido por «Reproductor de fichaje en el Inicio»; `/fichajes` ya no tiene botones.

### Requirement: Presentación de tiempos en UTC y «en curso»

Sustituido por «Presentación en hora local» y «Barra de franjas por día» (el tramo abierto se dibuja hasta «ahora»).

### Requirement: Totales de trabajo y de pausas

Sustituido por los totales del mes de «Página /fichajes de consulta mensual» y los del día de «Barra de franjas por día»; `WorkedTimeFormatter` desaparece.
