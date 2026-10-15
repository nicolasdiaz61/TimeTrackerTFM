# Delta para team-access

Cambio: `fichajes-vista-mensual`. Los fichajes de un miembro admiten la misma ventana `[from, to)` que los propios. El requisito modificado reemplaza por completo al vigente del mismo nombre.

## MODIFIED Requirements

### Requirement: Fichajes de un miembro del equipo

`GET /api/team/members/{memberId}/time-entries` DEBE devolver `200` con la misma forma y reglas que `GET /api/time-entries` (`{ items, totalWorkedMinutes, totalBreakMinutes, totalMealBreakMinutes, status }`, intervalos por `ClockInAt` descendente con `type` por intervalo, totales de los cerrados devueltos) para el miembro indicado, solo si es reporte directo del responsable autenticado. Acepta los mismos parámetros opcionales `from` y `to` (ventana `[from, to)` en UTC con las mismas reglas y los mismos `400` que los fichajes propios; sin ellos, los últimos 300). `status` es el del intervalo abierto del miembro aunque no esté en la ventana. La ventana se valida antes de resolver al miembro, de modo que un `400` nunca distingue entre miembros propios y ajenos y el `404` de un miembro ajeno, inexistente o el propio responsable sigue siendo idéntico. Es de solo lectura: la API NO DEBE ofrecer ninguna operación de escritura bajo `/api/team`.

#### Scenario: Fichajes del miembro
- DADO `luis`, reporte de `marta`, con trabajos cerrados de 60 y 30 minutos, una pausa de 15, una comida de 45 y un trabajo abierto
- CUANDO `marta` envía `GET /api/team/members/{luis}/time-entries`
- ENTONCES recibe `200`, `totalWorkedMinutes` = `90`, `totalBreakMinutes` = `15`, `totalMealBreakMinutes` = `45`, `status` = `Working`, el abierto con `durationMinutes` nulo y los instantes terminados en `Z`

#### Scenario: Pausas visibles para el responsable
- DADO `luis`, reporte de `marta`, con una pausa de comida abierta
- CUANDO `marta` consulta sus fichajes
- ENTONCES el primer elemento tiene `type` = `MealBreak` y `status` es `OnMealBreak`

#### Scenario: Más de 300 intervalos sin ventana
- DADO `luis` con 305 intervalos `Work` cerrados de 10 minutos
- CUANDO `marta` consulta sus fichajes sin ventana
- ENTONCES recibe los 300 más recientes y `totalWorkedMinutes` = `3000`

#### Scenario: Ventana mensual de un miembro
- DADO `luis`, reporte de `marta`, con intervalos en dos meses
- CUANDO `marta` envía `GET /api/team/members/{luis}/time-entries?from=…&to=…` con la ventana de uno de ellos
- ENTONCES recibe solo los intervalos de `luis` que solapan esa ventana

#### Scenario: Ventana inválida
- DADO un responsable autenticado
- CUANDO consulta a un miembro con un `from` futuro o sin `to`
- ENTONCES recibe `400` en español, tanto si el miembro es suyo como si no

#### Scenario: Miembro ajeno con ventana
- DADO un miembro de otro responsable
- CUANDO `marta` consulta sus fichajes con una ventana válida
- ENTONCES recibe el mismo `404` que sin ventana

#### Scenario: Miembro sin fichajes
- DADO `ana`, reporte de `marta`, sin intervalos
- CUANDO `marta` consulta sus fichajes
- ENTONCES recibe `200` con `items` vacío, los tres totales en `0` y `status` = `Idle`
