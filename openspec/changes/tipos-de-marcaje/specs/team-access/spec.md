# Delta para team-access

Cambio: `tipos-de-marcaje`. La vista del responsable muestra también las pausas y comidas de su equipo, con los totales por tipo y el estado actual de cada miembro. Sigue siendo de solo lectura: ninguna operación de pausa ni reanudación existe bajo `/api/team`.

## MODIFIED Requirements

### Requirement: Fichajes de un miembro del equipo

`GET /api/team/members/{memberId}/time-entries` DEBE devolver `200` con la misma forma y reglas que `GET /api/time-entries` (`{ items, totalWorkedMinutes, totalBreakMinutes, totalMealBreakMinutes, status }`, últimos 50 intervalos por `ClockInAt` descendente, con `type` por intervalo, totales de los cerrados de esos 50) para el miembro indicado, solo si es reporte directo del responsable autenticado. Es de solo lectura: la API NO DEBE ofrecer ninguna operación de escritura bajo `/api/team`.

#### Scenario: Fichajes del miembro
- DADO `luis`, reporte de `marta`, con trabajos cerrados de 60 y 30 minutos, una pausa de 15, una comida de 45 y un trabajo abierto
- CUANDO `marta` envía `GET /api/team/members/{luis}/time-entries`
- ENTONCES recibe `200`, `totalWorkedMinutes` = `90`, `totalBreakMinutes` = `15`, `totalMealBreakMinutes` = `45`, `status` = `Working`, el abierto con `durationMinutes` nulo y los instantes terminados en `Z`

#### Scenario: Pausas visibles para el responsable
- DADO `luis`, reporte de `marta`, con una pausa de comida abierta
- CUANDO `marta` consulta sus fichajes
- ENTONCES el primer elemento tiene `type` = `MealBreak` y `status` es `OnMealBreak`

#### Scenario: Más de 50 intervalos
- DADO `luis` con 55 intervalos `Work` cerrados de 10 minutos
- CUANDO `marta` consulta sus fichajes
- ENTONCES recibe los 50 más recientes y `totalWorkedMinutes` = `500`

#### Scenario: Miembro sin fichajes
- DADO `ana`, reporte de `marta`, sin intervalos
- CUANDO `marta` consulta sus fichajes
- ENTONCES recibe `200` con `items` vacío, los tres totales en `0` y `status` = `Idle`

#### Scenario: Solo los fichajes del miembro
- DADO intervalos de `luis`, de `ana` y de la propia `marta`
- CUANDO `marta` consulta los fichajes de `luis`
- ENTONCES la respuesta solo contiene los de `luis`

#### Scenario: Solo lectura
- DADO un token de `marta` y `luis` con un intervalo abierto
- CUANDO se envía `POST /api/team/members/{luis}/time-entries`, `POST /api/team/members/{luis}/start-break` o `POST /api/team/members/{luis}/end-break`
- ENTONCES ninguna crea ni modifica nada (`405` o `404`) y el intervalo de `luis` sigue abierto

#### Scenario: Escritura sin token
- DADO una petición sin cabecera `Authorization`
- CUANDO se envía `POST /api/team/members/{luis}/time-entries`
- ENTONCES recibe `401` y no se crea ni se modifica ningún intervalo

#### Scenario: Las pausas de un ajeno no se exponen
- DADO `ana`, que no es reporte de `marta`, con una pausa abierta
- CUANDO `marta` consulta los fichajes de `ana`
- ENTONCES recibe `404`, idéntico al de un miembro inexistente
