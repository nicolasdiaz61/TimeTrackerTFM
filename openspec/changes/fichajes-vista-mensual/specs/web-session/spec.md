# Delta para web-session

Cambio: `fichajes-vista-mensual`. El Inicio incorpora el reproductor de fichaje y `/equipo` usa la vista mensual en hora local. Los requisitos modificados reemplazan por completo a los vigentes del mismo nombre.

## MODIFIED Requirements

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

### Requirement: Página de inicio

La ruta `/` DEBE presentar TimeTracker en español: qué es la aplicación y, según la sesión, un enlace para iniciar sesión (anónimo) o, con sesión (empleado o responsable), el reproductor de fichaje (ver `time-entries-ui`), un acceso a «Fichajes» y, solo para el rol `Manager`, a «Equipo». La página no encontrada DEBE estar en español con un enlace al inicio, el documento DEBE declarar `lang="es"` y su título DEBE llevar la marca «TimeTracker».

#### Scenario: Inicio según la sesión
- DADO una sesión anónima, de empleado o de responsable
- CUANDO se abre `/`
- ENTONCES se ofrece iniciar sesión sin reproductor, o el reproductor y «Fichajes», o el reproductor, «Fichajes» y «Equipo», respectivamente
