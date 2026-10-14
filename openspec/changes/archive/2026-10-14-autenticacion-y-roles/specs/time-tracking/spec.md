# Delta para time-tracking

Cambio: `autenticacion-y-roles`. La identidad del empleado deja de ser provisional: sale del token (`sub`) mediante `HttpContextCurrentUser`, y los tres endpoints de fichaje exigen autenticación. Se retira el requisito «Identidad provisional mediante ICurrentUser» (delta destructivo: desaparecen los escenarios de `Identity:DevelopmentEmployeeId`; las cabeceras y parámetros `employeeId` siguen ignorados). El `Purpose` de la especificación vigente (identidad provisional) queda obsoleto y debe actualizarse al archivar.

## ADDED Requirements

### Requirement: Identidad del empleado desde el token

`Application` DEBE conservar el puerto `ICurrentUser` con `Guid EmployeeId`, sin cambios. `Api` DEBE ofrecer `HttpContextCurrentUser`, que devuelve el claim `sub` del usuario autenticado. Los tres endpoints de fichaje DEBEN exigir autenticación. Ningún endpoint, parámetro de ruta, consulta, cabecera ni cuerpo DEBE aceptar un `employeeId`; el empleado sale solo del token. Si no hay usuario autenticado, el adaptador NO DEBE devolver un empleado (lanza una excepción; defensa en profundidad, inalcanzable a través del pipeline).

#### Scenario: El empleado sale del token
- DADO un token de `luis` y otro de `ana`
- CUANDO `luis` envía `POST /api/time-entries/clock-in` y después ambos envían `GET /api/time-entries`
- ENTONCES el listado de `luis` contiene el fichaje creado y el de `ana` no

#### Scenario: Un employeeId del cliente se ignora
- DADO un token de `luis`
- CUANDO envía `POST /api/time-entries/clock-in?employeeId=<id de ana>`
- ENTONCES el fichaje creado pertenece a `luis` y ninguno a `ana`

#### Scenario: Una cabecera de empleado se ignora
- DADO un token de `luis`
- CUANDO envía `POST /api/time-entries/clock-in` con la cabecera `X-Employee-Id: <id de ana>`
- ENTONCES el fichaje creado pertenece a `luis` y ninguno a `ana`

#### Scenario: Sin token
- DADO una petición sin cabecera `Authorization`
- CUANDO se envían `POST /api/time-entries/clock-in`, `POST /api/time-entries/clock-out` y `GET /api/time-entries`
- ENTONCES las tres reciben `401` y no se crea ni se modifica ningún fichaje

#### Scenario: El adaptador lee el claim sub
- DADO un contexto HTTP con usuario autenticado cuyo claim `sub` es `A`
- CUANDO se lee `HttpContextCurrentUser.EmployeeId`
- ENTONCES vale `A`

#### Scenario: El adaptador sin usuario autenticado
- DADO un contexto HTTP sin usuario autenticado
- CUANDO se lee `HttpContextCurrentUser.EmployeeId`
- ENTONCES se lanza una excepción y no se devuelve ningún empleado

#### Scenario: El contrato no expone employeeId
- DADO `docs/api/openapi.json`
- CUANDO se revisan los parámetros y esquemas de los tres endpoints de fichaje
- ENTONCES ninguno contiene una propiedad o parámetro `employeeId`

## REMOVED Requirements

### Requirement: Identidad provisional mediante ICurrentUser

(Reason: la identidad provisional de `Development` (`Identity:DevelopmentEmployeeId`, adaptador de desarrollo y su validador) se sustituye por la autenticación real con JWT; el ADR 0009 pasa a «Sustituido por 0011».)
(Migration: ver el requisito «Identidad del empleado desde el token» de esta especificación y las capacidades `authentication` y `startup-configuration`; los escenarios que ignoran `employeeId` y `X-Employee-Id` se conservan allí con el token como origen de la identidad. Los fichajes creados con la identidad provisional quedan huérfanos y `docker compose down -v` los elimina.)

## MODIFIED Requirements

### Requirement: Contrato REST de fichajes

La API DEBE exponer, todos con autenticación obligatoria: `POST /api/time-entries/clock-in` (`201` con `TimeEntryResponse`; `401` sin token válido; `409` si hay una abierta), `POST /api/time-entries/clock-out` (`200` con `TimeEntryResponse`; `401`; `409` si no hay abierta) y `GET /api/time-entries` (`200` con `{ items, totalWorkedMinutes }`, donde `items` es una lista de `TimeEntryResponse`; `401`). `TimeEntryResponse` DEBE tener `id`, `clockInAt`, `clockOutAt` (nulo si abierta) y `workedMinutes` (nulo si abierta). Los instantes DEBEN serializarse en ISO 8601 UTC con sufijo `Z` (nunca `+00:00`) y declararse `format: date-time` en el contrato. El `201` de `clock-in` NO DEBE incluir cabecera `Location` (no existe consulta por identificador). Los POST NO DEBEN requerir cuerpo. El `clockInAt` del `201` DEBE ser idéntico al que devuelve el `GET` posterior.
(Previously: sin autenticación y sin respuesta `401` declarada)

#### Scenario: Entrada correcta
- DADO un empleado autenticado sin fichaje abierto
- CUANDO envía `POST /api/time-entries/clock-in`
- ENTONCES recibe `201`, tipo de medio `application/json` y un cuerpo con `clockOutAt` y `workedMinutes` nulos y `clockInAt` terminado en `Z`

#### Scenario: Entrada sin Location y coherente con el listado
- DADO un empleado autenticado sin fichaje abierto y un `FakeTimeProvider` en `2026-10-12T08:00:00.7Z`
- CUANDO envía `POST /api/time-entries/clock-in` y después `GET /api/time-entries`
- ENTONCES el `201` no incluye cabecera `Location` y su `clockInAt` (`2026-10-12T08:00:00Z`) es idéntico al del primer elemento del `GET`

#### Scenario: Salida correcta
- DADO un empleado autenticado con un fichaje abierto
- CUANDO envía `POST /api/time-entries/clock-out`
- ENTONCES recibe `200` y un cuerpo con `clockOutAt` no nulo y `workedMinutes` entero ≥ 0

#### Scenario: Minutos truncados en la salida
- DADO un empleado autenticado con entrada a las `08:00:00Z` y un `FakeTimeProvider` en `09:30:30Z`
- CUANDO envía `POST /api/time-entries/clock-out`
- ENTONCES `workedMinutes` es `90`

#### Scenario: Listado con total
- DADO un empleado autenticado con un fichaje cerrado de 60 minutos exactos y uno abierto
- CUANDO envía `GET /api/time-entries`
- ENTONCES recibe `200`, `totalWorkedMinutes` = `60` y el abierto con `workedMinutes` nulo

#### Scenario: Token caducado
- DADO un token cuyo `exp` fue hace 2 minutos
- CUANDO envía `GET /api/time-entries`
- ENTONCES recibe `401` `application/problem+json`

#### Scenario: Contrato OpenAPI
- DADO el build de `TimeTracker.Api` ejecutado
- CUANDO se lee `docs/api/openapi.json`
- ENTONCES contiene `post` en `/api/time-entries/clock-in` (respuestas `201`, `401` y `409`), `post` en `/api/time-entries/clock-out` (`200`, `401` y `409`) y `get` en `/api/time-entries` (`200` y `401`), y `format: date-time` en `clockInAt` y `clockOutAt`
