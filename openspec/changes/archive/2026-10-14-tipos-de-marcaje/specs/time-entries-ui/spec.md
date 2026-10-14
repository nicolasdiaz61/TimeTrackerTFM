# Delta para time-entries-ui

Cambio: `tipos-de-marcaje`. La pantalla `/fichajes` muestra el estado actual, solo las acciones válidas para ese estado, una columna «Tipo» y totales de trabajo, pausa y comida. La Web no aplica reglas de negocio: el estado y los totales vienen de la API; la correspondencia estado → botones es solo presentación y la validez real la sigue decidiendo el servidor (un `409` se muestra tal cual). Se reemplaza el requisito de que los botones permanezcan siempre activos.

Verificación: sin bUnit ni E2E; los escenarios visuales se verifican manualmente (y con Chrome automatizado) y se registran en `verify-report.md`. La lógica de presentación pura (`TimeEntryPresentation`) tiene pruebas unitarias en `TimeTracker.Web.Tests`.

## MODIFIED Requirements

### Requirement: Página /fichajes con listado y total

La ruta `/fichajes` DEBE exigir una sesión iniciada; sin sesión DEBE navegar a `/login`. Con sesión, DEBE mostrar el estado actual, una tabla con los intervalos devueltos por `GET /api/time-entries` (tipo, entrada, salida, minutos) en el orden recibido y los totales `totalWorkedMinutes`, `totalBreakMinutes` y `totalMealBreakMinutes` (cada uno en minutos y en formato h:mm, con «1 minuto» en singular). Sin intervalos DEBE mostrar un mensaje indicando que no hay fichajes y totales `0`. La página DEBERÍA enlazarse desde el menú de navegación.

#### Scenario: Carga inicial con datos
- DADO una sesión de un empleado con intervalos cerrados de trabajo, pausa y comida y uno abierto
- CUANDO se abre `/fichajes`
- ENTONCES la tabla muestra todos en el orden de la API y los tres totales coinciden con los de la API

#### Scenario: Sin fichajes
- DADO una sesión de un empleado sin intervalos
- CUANDO se abre `/fichajes`
- ENTONCES aparece un mensaje de que no hay fichajes, los totales son `0` y el estado es «Sin fichar»

#### Scenario: Sin sesión
- DADO ninguna sesión iniciada
- CUANDO se abre `/fichajes`
- ENTONCES se navega a `/login` y no se observa ninguna petición a `/api/time-entries`

#### Scenario: Solo los fichajes propios
- DADO las sesiones de `luis` y de `ana`, cada una con sus intervalos
- CUANDO cada una abre `/fichajes`
- ENTONCES cada tabla contiene únicamente los intervalos de su titular

### Requirement: Botones de fichaje

La página DEBE mostrar únicamente las acciones válidas para el `status` recibido: `Idle` → «Fichar entrada»; `Working` → «Iniciar pausa», «Pausa de comida» y «Fichar salida»; `OnBreak` y `OnMealBreak` → «Terminar pausa» y «Fichar salida». Cada botón invoca su endpoint (`clock-in`, `start-break`, `start-meal-break`, `end-break`, `clock-out`) y tras una respuesta correcta DEBE recargar el listado y con él el estado y los botones. Mientras haya una petición en curso, los botones DEBEN estar deshabilitados. Un `409` DEBE mostrarse con el mensaje de la API y recargar el listado (el estado pudo cambiar en otra pestaña).

#### Scenario: Fichar entrada
- DADO un empleado sin intervalo abierto
- CUANDO se abre `/fichajes`
- ENTONCES solo se ve «Fichar entrada» y, al pulsarlo, aparece una fila «Trabajo» con salida «en curso» y el estado es «Trabajando»

#### Scenario: Iniciar pausa
- DADO un empleado trabajando
- CUANDO pulsa «Iniciar pausa»
- ENTONCES el estado es «En pausa», solo se ven «Terminar pausa» y «Fichar salida», y hay una fila «Pausa» en curso encima de una fila «Trabajo» cerrada

#### Scenario: Pausa de comida
- DADO un empleado trabajando
- CUANDO pulsa «Pausa de comida»
- ENTONCES el estado es «En pausa de comida» y hay una fila «Comida» en curso

#### Scenario: Terminar pausa
- DADO un empleado en pausa
- CUANDO pulsa «Terminar pausa»
- ENTONCES el estado vuelve a «Trabajando» y hay una fila «Trabajo» en curso

#### Scenario: Fichar salida
- DADO un empleado con un intervalo abierto de cualquier tipo
- CUANDO pulsa «Fichar salida»
- ENTONCES el intervalo muestra su hora de salida y sus minutos, el estado es «Sin fichar» y solo se ve «Fichar entrada»

#### Scenario: Estado obsoleto
- DADO un empleado que pulsa «Terminar pausa» cuando otra pestaña ya la terminó
- CUANDO el servidor responde `409`
- ENTONCES se muestra el mensaje en español de la API y el listado se recarga con el estado real

### Requirement: Presentación de tiempos en UTC y «en curso»

Las horas DEBEN mostrarse en UTC con el formato `yyyy-MM-dd HH:mm 'UTC'` con cultura invariante, es decir, con la etiqueta «UTC» junto a cada valor; NO DEBEN convertirse a hora local. Un intervalo abierto DEBE mostrar «en curso» en la salida y NO DEBE mostrar minutos ni sumar a ningún total. Los minutos se muestran tal como los devuelve la API (truncados). La columna «Tipo» DEBE mostrar «Trabajo» para `Work`, «Pausa» para `Break` y «Comida» para `MealBreak`; un valor desconocido se muestra tal cual, sin romper la tabla.

#### Scenario: Hora etiquetada
- DADO un intervalo con `clockInAt` = `2026-10-12T08:00:00Z`
- CUANDO se muestra en la tabla
- ENTONCES se lee `2026-10-12 08:00 UTC`, sin desplazamiento de zona

#### Scenario: Intervalo en curso
- DADO un intervalo abierto
- CUANDO se muestra en la tabla
- ENTONCES su salida dice «en curso», sin minutos, y ningún total lo incluye

#### Scenario: Etiquetas de tipo
- DADO intervalos de tipo `Work`, `Break` y `MealBreak`
- CUANDO se muestran en la tabla
- ENTONCES la columna «Tipo» dice «Trabajo», «Pausa» y «Comida»

## ADDED Requirements

### Requirement: Totales de trabajo y de pausas

Bajo la tabla DEBE mostrarse «Total trabajado», «Total en pausas» y «Total en comidas», cada uno con el formato `WorkedTimeFormatter.FormatTotal` («1 minuto (0:01)», «90 minutos (1:30)»). El estado se muestra con texto en español («Sin fichar», «Trabajando», «En pausa», «En pausa de comida»).

#### Scenario: Plurales correctos
- DADO totales de `1`, `0` y `90` minutos
- CUANDO se muestran
- ENTONCES se leen «1 minuto (0:01)», «0 minutos (0:00)» y «90 minutos (1:30)»
