# Delta para team-access

Cambio: `datos-demo-mes`. La vista del responsable usa el mismo límite del listado (300). El requisito modificado reemplaza por completo al vigente del mismo nombre.

## MODIFIED Requirements

### Requirement: Fichajes de un miembro del equipo

`GET /api/team/members/{memberId}/time-entries` DEBE devolver `200` con la misma forma y reglas que `GET /api/time-entries` (`{ items, totalWorkedMinutes, totalBreakMinutes, totalMealBreakMinutes, status }`, últimos 300 intervalos por `ClockInAt` descendente, con `type` por intervalo, totales de los cerrados de esos 300) para el miembro indicado, solo si es reporte directo del responsable autenticado. Es de solo lectura: la API NO DEBE ofrecer ninguna operación de escritura bajo `/api/team`.

#### Scenario: Fichajes del miembro
- DADO `luis`, reporte de `marta`, con trabajos cerrados de 60 y 30 minutos, una pausa de 15, una comida de 45 y un trabajo abierto
- CUANDO `marta` envía `GET /api/team/members/{luis}/time-entries`
- ENTONCES recibe `200`, `totalWorkedMinutes` = `90`, `totalBreakMinutes` = `15`, `totalMealBreakMinutes` = `45`, `status` = `Working`, el abierto con `durationMinutes` nulo y los instantes terminados en `Z`

#### Scenario: Pausas visibles para el responsable
- DADO `luis`, reporte de `marta`, con una pausa de comida abierta
- CUANDO `marta` consulta sus fichajes
- ENTONCES el primer elemento tiene `type` = `MealBreak` y `status` es `OnMealBreak`

#### Scenario: Más de 300 intervalos
- DADO `luis` con 305 intervalos `Work` cerrados de 10 minutos
- CUANDO `marta` consulta sus fichajes
- ENTONCES recibe los 300 más recientes y `totalWorkedMinutes` = `3000`

#### Scenario: Miembro sin fichajes
- DADO `ana`, reporte de `marta`, sin intervalos
- CUANDO `marta` consulta sus fichajes
- ENTONCES recibe `200` con `items` vacío, los tres totales en `0` y `status` = `Idle`
