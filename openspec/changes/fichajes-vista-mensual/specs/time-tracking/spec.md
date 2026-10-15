# Delta para time-tracking

Cambio: `fichajes-vista-mensual`. El listado se puede acotar a una ventana `[from, to)` en UTC (la Web pide un mes natural en hora local) y el estado deja de depender de los elementos devueltos. Los requisitos modificados reemplazan por completo a los vigentes del mismo nombre.

## ADDED Requirements

### Requirement: Ventana de consulta del listado

`GET /api/time-entries` DEBE aceptar los parámetros de consulta opcionales `from` y `to`: instantes ISO 8601 (sin desfase se interpretan como UTC; con desfase se normalizan a UTC) que definen la ventana semiabierta `[from, to)`. Se envían ambos o ninguno. Con ventana, DEBE devolver los intervalos del empleado que **solapan** la ventana (`ClockInAt < to` y `ClockOutAt` nulo o `> from`), por `ClockInAt` descendente, con un tope de 1000 (`MaxWindowEntries`); sin ventana DEBE comportarse como antes (últimos 300). `TimeWindow` DEBE rechazar con un mensaje en español: `to` no posterior a `from`, una ventana de más de 32 días y un `from` posterior al instante actual del servidor (periodos futuros). La API DEBE responder `400` `application/problem+json` con título «Solicitud no válida» y el motivo en `detail` cuando falta uno de los dos, no se pueden leer o la ventana no es válida. Los totales son los de los intervalos cerrados devueltos (sin recortar a la ventana).

#### Scenario: Ventana de un mes
- DADO un empleado con intervalos en septiembre y octubre de 2026 y un instante actual de `2026-10-14T12:00:00Z`
- CUANDO envía `GET /api/time-entries?from=2026-09-30T22:00:00Z&to=2026-10-31T23:00:00Z`
- ENTONCES recibe solo los intervalos que solapan esa ventana, por `ClockInAt` descendente

#### Scenario: Límites semiabiertos y solape
- DADO un intervalo que acaba exactamente en `from`, otro que empieza exactamente en `to`, otro que empieza antes de `from` y acaba dentro, y otro que empieza dentro y acaba después de `to`
- CUANDO se consulta la ventana
- ENTONCES se devuelven los dos últimos y no los dos primeros

#### Scenario: Abierto anterior a la ventana
- DADO un empleado con un intervalo abierto desde el mes anterior
- CUANDO consulta el mes actual
- ENTONCES el intervalo abierto aparece en la ventana y `status` lo refleja

#### Scenario: Ventana inválida
- DADO un empleado autenticado
- CUANDO envía `from` sin `to`, fechas ilegibles, `to` anterior a `from`, una ventana de 40 días o un `from` futuro
- ENTONCES recibe `400` `application/problem+json` con el motivo en español

#### Scenario: Sin ventana
- DADO un empleado con 305 intervalos
- CUANDO envía `GET /api/time-entries` sin parámetros
- ENTONCES recibe los 300 más recientes, igual que antes

## MODIFIED Requirements

### Requirement: Caso de uso GetTimeEntries y total

`GetTimeEntries` DEBE devolver, para el empleado de `ICurrentUser`, sus intervalos ordenados por `ClockInAt` descendente: los últimos 300 (constante `GetTimeEntriesUseCase.MaxEntries`, no controlable por el cliente) si no recibe ventana, o los que solapan la ventana `TimeWindow` (hasta `MaxWindowEntries`, 1000) si la recibe. Cada intervalo lleva `type` y `durationMinutes` (nulo si está abierto). Los totales, siempre de los devueltos y solo de los **cerrados**, son `totalWorkedMinutes` (tipo `Work`), `totalBreakMinutes` (tipo `Break`) y `totalMealBreakMinutes` (tipo `MealBreak`). Un intervalo abierto NO DEBE sumar. `status` DEBE reflejar el intervalo abierto **del empleado, aunque no esté en la lista devuelta**: `Idle` si no hay ninguno, `Working` si es `Work`, `OnBreak` si es `Break` y `OnMealBreak` si es `MealBreak`. Sin intervalos, la lista es vacía, los totales son `0` y `status` es `Idle`.

#### Scenario: Totales por tipo
- DADO un empleado con trabajos cerrados de `60` y `30` minutos, una pausa de `15`, una comida de `45` y un trabajo abierto
- CUANDO se ejecuta `GetTimeEntries`
- ENTONCES `totalWorkedMinutes` es `90`, `totalBreakMinutes` es `15`, `totalMealBreakMinutes` es `45` y el abierto tiene `durationMinutes` nulo

#### Scenario: Estado según el intervalo abierto
- DADO un empleado cuyo intervalo abierto es `Work`, `Break` o `MealBreak`, o que no tiene ninguno
- CUANDO se ejecuta `GetTimeEntries`
- ENTONCES `status` es `Working`, `OnBreak`, `OnMealBreak` o `Idle`, respectivamente

#### Scenario: Estado con la ventana de un mes anterior
- DADO un empleado con un intervalo `Work` abierto desde hoy y una ventana de hace dos meses
- CUANDO se ejecuta `GetTimeEntries` con esa ventana
- ENTONCES la lista no incluye el abierto y `status` es `Working`

#### Scenario: Orden descendente
- DADO intervalos con entradas a las `08:00Z` de los días 10, 11 y 12
- CUANDO se ejecuta `GetTimeEntries`
- ENTONCES el primero es el del día 12 y el último el del día 10

#### Scenario: Sin fichajes
- DADO un empleado sin intervalos
- CUANDO se ejecuta `GetTimeEntries`
- ENTONCES la lista está vacía, los tres totales son `0` y `status` es `Idle`

#### Scenario: Más de 300 intervalos sin ventana
- DADO un empleado con 305 intervalos
- CUANDO se ejecuta `GetTimeEntries` sin ventana
- ENTONCES se devuelven 300, los más recientes por `ClockInAt`

#### Scenario: Total limitado a los 300 devueltos
- DADO un empleado con 305 intervalos `Work` cerrados de 10 minutos cada uno
- CUANDO se ejecuta `GetTimeEntries` sin ventana
- ENTONCES se devuelven 300 y `totalWorkedMinutes` es `3000`

#### Scenario: Con ventana solo los de la ventana
- DADO un empleado con intervalos dentro y fuera de la ventana
- CUANDO se ejecuta `GetTimeEntries` con la ventana
- ENTONCES solo se devuelven los que solapan y los totales suman solo los cerrados devueltos

### Requirement: Contrato REST de fichajes

La API DEBE exponer, todos con autenticación obligatoria y sin cuerpo: `POST /api/time-entries/clock-in` (`201` con `TimeEntryResponse`; `401`; `409` si hay un intervalo abierto), `POST /api/time-entries/clock-out` (`200`; `401`; `409` si no hay abierto), `POST /api/time-entries/start-break` y `POST /api/time-entries/start-meal-break` (`200` con el `TimeEntryResponse` de la pausa abierta; `401`; `409` si no se está trabajando), `POST /api/time-entries/end-break` (`200` con el `TimeEntryResponse` del trabajo reanudado; `401`; `409` si no hay pausa abierta) y `GET /api/time-entries` con los parámetros de consulta opcionales `from` y `to` (`200` con `{ items, totalWorkedMinutes, totalBreakMinutes, totalMealBreakMinutes, status }`; `400` ventana no válida; `401`). `TimeEntryResponse` DEBE tener `id`, `type` (texto `Work`, `Break` o `MealBreak`, declarado como enumeración en el contrato), `clockInAt`, `clockOutAt` (nulo si abierto) y `durationMinutes` (nulo si abierto); `status` es texto (`Idle`, `Working`, `OnBreak`, `OnMealBreak`). Los instantes DEBEN serializarse en ISO 8601 UTC con sufijo `Z` y declararse `format: date-time`. El `201` de `clock-in` NO DEBE incluir cabecera `Location`. Ningún endpoint acepta `employeeId`. El `clockInAt` devuelto por cada POST DEBE ser idéntico al que devuelve el `GET` posterior.

#### Scenario: Entrada correcta
- DADO un empleado autenticado sin intervalo abierto
- CUANDO envía `POST /api/time-entries/clock-in`
- ENTONCES recibe `201`, `application/json` y un cuerpo con `type` = `Work`, `clockOutAt` y `durationMinutes` nulos y `clockInAt` terminado en `Z`

#### Scenario: Entrada sin Location y coherente con el listado
- DADO un empleado autenticado sin intervalo abierto y un `FakeTimeProvider` en `2026-10-12T08:00:00.7Z`
- CUANDO envía `POST /api/time-entries/clock-in` y después `GET /api/time-entries`
- ENTONCES el `201` no incluye cabecera `Location` y su `clockInAt` (`2026-10-12T08:00:00Z`) es idéntico al del primer elemento del `GET`

#### Scenario: Salida correcta
- DADO un empleado autenticado con un `Work` abierto
- CUANDO envía `POST /api/time-entries/clock-out`
- ENTONCES recibe `200` y un cuerpo con `clockOutAt` no nulo y `durationMinutes` entero ≥ 0

#### Scenario: Minutos truncados en la salida
- DADO un empleado autenticado con entrada a las `08:00:00Z` y un `FakeTimeProvider` en `09:30:30Z`
- CUANDO envía `POST /api/time-entries/clock-out`
- ENTONCES `durationMinutes` es `90`

#### Scenario: Ciclo con pausas
- DADO un empleado autenticado sin intervalo abierto
- CUANDO envía `clock-in`, `start-break`, `end-break`, `start-meal-break`, `end-break` y `clock-out` y después `GET /api/time-entries`
- ENTONCES las respuestas son `201` y cinco `200`, el listado tiene 5 intervalos (`Work`, `Break`, `Work`, `MealBreak`, `Work`) todos cerrados, `status` es `Idle` y la suma de los tres totales es la suma de sus `durationMinutes`

#### Scenario: Estado en el listado
- DADO un empleado autenticado con una pausa abierta
- CUANDO envía `GET /api/time-entries`
- ENTONCES `status` es `OnBreak` y el primer elemento tiene `type` = `Break` y `durationMinutes` nulo

#### Scenario: Transiciones inválidas
- DADO un empleado autenticado sin intervalo abierto, y otro con una pausa abierta
- CUANDO el primero envía `start-break`, `start-meal-break` y `end-break`, y el segundo envía `start-break`
- ENTONCES las cuatro respuestas son `409`

#### Scenario: Token caducado
- DADO un token cuyo `exp` fue hace 2 minutos
- CUANDO envía `GET /api/time-entries`
- ENTONCES recibe `401` `application/problem+json`

#### Scenario: Contrato OpenAPI
- DADO el build de `TimeTracker.Api` ejecutado
- CUANDO se lee `docs/api/openapi.json`
- ENTONCES contiene `post` en `clock-in` (`201`, `401`, `409`), `clock-out`, `start-break`, `start-meal-break` y `end-break` (`200`, `401`, `409`) y `get` en `/api/time-entries` (`200`, `400`, `401`) con los parámetros `from` y `to` de tipo `date-time`; `TimeEntryResponse` declara `type` como enumeración de texto, `format: date-time` en `clockInAt` y `clockOutAt` y no declara `workedMinutes`
