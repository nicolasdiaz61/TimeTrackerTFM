# Especificación: time-tracking

## Purpose

Define las reglas de dominio del fichaje (`TimeEntry`, con tipos de intervalo `Work`, `Break` y `MealBreak`), los casos de uso `ClockIn`, `ClockOut`, `StartBreak`, `EndBreak` y `GetTimeEntries`, el contrato REST de `TimeTracker.Api` para fichar, pausar y consultar, la identidad del empleado (tomada del claim `sub` del token JWT mediante `HttpContextCurrentUser`, con todos los endpoints de fichaje autenticados) y el aislamiento entre empleados. Reutiliza `WorkedHours` (spec `worked-hours`). La autenticación se especifica en `authentication`; los roles y el acceso del responsable al equipo, en `team-access`.

Nombres fijados en el diseño: `TimeEntry.Start(employeeId, type, at)`, `Close`, `Pause`, `Resume`, `IsOpen`, `CalculateDuration()`, `TimeEntryType`, `TimeTrackingStatus`, casos de uso `StartBreakUseCase` y `EndBreakUseCase`; excepciones de dominio `TimeEntryAlreadyClosedException`, `ClockOutBeforeClockInException`, `NotWorkingException` y `NoOpenBreakException` y excepciones de Application `OpenTimeEntryAlreadyExistsException`, `NoOpenTimeEntryException` y `ConcurrentTimeEntryChangeException` (todas derivadas de `DomainRuleException`); el listado se devuelve como `{ "items": [...], "totalWorkedMinutes": n, "totalBreakMinutes": n, "totalMealBreakMinutes": n, "status": "..." }` y cada elemento lleva `type` y `durationMinutes` (antes `workedMinutes`; cambio `tipos-de-marcaje`, ADR 0014).

Decisiones aceptadas: los instantes se truncan a segundos enteros; los totales suman solo los intervalos cerrados devueltos (sin recortar a la ventana); el `201` de entrada no lleva cabecera `Location`.

Fuera de alcance: tabla `Employee`, atribución por día, saldos, horas extra, tope de entrada olvidada, edición o borrado de fichajes, paginación y zona horaria local en la API (la API habla UTC; la hora local la resuelve la Web, ADR 0016).

## ADDED Requirements

### Requirement: Inicio de un fichaje en el dominio

`TimeTracker.Domain` DEBE ofrecer `TimeEntry.Start(employeeId, type, at)` que crea un intervalo abierto (`ClockOutAt` nulo, `IsOpen` verdadero) del tipo indicado con un `Id` nuevo. `employeeId` NO DEBE ser `Guid.Empty` y `at` DEBE ser un instante UTC (offset cero); en otro caso DEBE lanzar `ArgumentException` o una derivada. `ClockInAt` DEBE truncarse a segundos enteros (se descarta la fracción de segundo). Un fichaje de entrada es un intervalo de tipo `Work`.

#### Scenario: Truncado a segundos
- DADO `at` = `2026-10-12T08:00:00.9876543Z`
- CUANDO se invoca `TimeEntry.Start(employeeId, Work, at)`
- ENTONCES `ClockInAt` es `2026-10-12T08:00:00Z`

#### Scenario: Fichaje abierto válido
- DADO un `employeeId` válido y `at` = `2026-10-12T08:00:00Z`
- CUANDO se invoca `TimeEntry.Start(employeeId, Work, at)`
- ENTONCES `ClockInAt` es `at`, `ClockOutAt` es nulo, `IsOpen` es verdadero y `Type` es `Work`

#### Scenario: Identificador de empleado vacío
- DADO `Guid.Empty` como `employeeId`
- CUANDO se invoca `TimeEntry.Start`
- ENTONCES se lanza `ArgumentException` o una derivada

#### Scenario: Instante que no es UTC
- DADO `at` = `2026-10-12T10:00:00+02:00`
- CUANDO se invoca `TimeEntry.Start`
- ENTONCES se lanza `ArgumentException` o una derivada

### Requirement: Cierre de un fichaje en el dominio

`TimeEntry.Close(at)` DEBE fijar `ClockOutAt` y dejar `IsOpen` en falso. `at` DEBE ser UTC y NO DEBE ser anterior a `ClockInAt`; `at` igual a `ClockInAt` es válido. Cerrar un fichaje ya cerrado DEBE lanzar una excepción de dominio tipada y NO DEBE modificar `ClockOutAt`. `ClockOutAt` DEBE truncarse a segundos enteros; el truncado a la baja nunca deja la salida por debajo de una entrada también truncada.

#### Scenario: Salida truncada a segundos
- DADO un fichaje abierto con `ClockInAt` = `2026-10-12T08:00:00Z`
- CUANDO se invoca `Close(2026-10-12T08:00:00.9Z)`
- ENTONCES no se lanza ninguna excepción y `ClockOutAt` es `2026-10-12T08:00:00Z`

#### Scenario: Cierre válido
- DADO un fichaje abierto con `ClockInAt` = `2026-10-12T08:00:00Z`
- CUANDO se invoca `Close(2026-10-12T16:00:00Z)`
- ENTONCES `ClockOutAt` es `2026-10-12T16:00:00Z` e `IsOpen` es falso

#### Scenario: Salida igual a la entrada
- DADO un fichaje abierto con `ClockInAt` = `2026-10-12T08:00:00Z`
- CUANDO se invoca `Close(2026-10-12T08:00:00Z)`
- ENTONCES no se lanza ninguna excepción

#### Scenario: Salida anterior a la entrada
- DADO un fichaje abierto con `ClockInAt` = `2026-10-12T08:00:00Z`
- CUANDO se invoca `Close(2026-10-12T07:59:59Z)`
- ENTONCES se lanza la excepción de dominio de salida inválida y el fichaje sigue abierto

#### Scenario: Cierre repetido
- DADO un fichaje ya cerrado a las `2026-10-12T16:00:00Z`
- CUANDO se invoca `Close(2026-10-12T17:00:00Z)`
- ENTONCES se lanza la excepción de dominio de fichaje cerrado y `ClockOutAt` conserva `2026-10-12T16:00:00Z`

#### Scenario: Salida que no es UTC
- DADO un fichaje abierto
- CUANDO se invoca `Close` con un instante de offset distinto de cero
- ENTONCES se lanza `ArgumentException` o una derivada

### Requirement: Cálculo de minutos trabajados truncados

`CalculateWorked()` DEBE devolver `null` si el fichaje está abierto y, si está cerrado, un `WorkedHours` con los minutos completos transcurridos entre `ClockInAt` y `ClockOutAt` **truncados** (nunca redondeados). El cálculo usa instantes absolutos, por lo que cruzar medianoche es correcto.

#### Scenario: Minutos exactos
- DADO un fichaje de `08:00:00Z` a `16:30:00Z`
- CUANDO se invoca `CalculateWorked()`
- ENTONCES `Minutes` es `510`

#### Scenario: Truncado hacia abajo
- DADO un fichaje de `08:00:00Z` a `08:01:59Z`
- CUANDO se invoca `CalculateWorked()`
- ENTONCES `Minutes` es `1`

#### Scenario: Menos de un minuto
- DADO un fichaje de `08:00:00Z` a `08:00:59Z`
- CUANDO se invoca `CalculateWorked()`
- ENTONCES `Minutes` es `0`

#### Scenario: Truncado con segundos sobrantes
- DADO un fichaje de `08:00:00Z` a `09:30:59Z`
- CUANDO se invoca `CalculateWorked()`
- ENTONCES `Minutes` es `90`

#### Scenario: Cruce de medianoche
- DADO un fichaje de `2026-10-12T22:00:00Z` a `2026-10-13T06:00:00Z`
- CUANDO se invoca `CalculateWorked()`
- ENTONCES `Minutes` es `480`

#### Scenario: Entrada abierta
- DADO un fichaje abierto
- CUANDO se invoca `CalculateWorked()`
- ENTONCES el resultado es `null`

### Requirement: Caso de uso ClockIn

`ClockIn` DEBE crear un intervalo `Work` abierto para el empleado devuelto por `ICurrentUser`, con `ClockInAt` igual al instante de `TimeProvider.GetUtcNow()` (truncado a segundos), y persistirlo mediante `ITimeEntryRepository`. Si el empleado ya tiene un intervalo abierto, de cualquier tipo, DEBE lanzar `OpenTimeEntryAlreadyExistsException` y NO DEBE crear otro. Dependiendo de `TimeProvider`, las pruebas unitarias NO DEBEN usar el reloj del sistema.

#### Scenario: Primera entrada
- DADO un empleado sin fichajes y un `FakeTimeProvider` en `2026-10-12T08:00:00Z`
- CUANDO se ejecuta `ClockIn`
- ENTONCES se guarda un intervalo `Work` abierto con `ClockInAt` = `2026-10-12T08:00:00Z` para ese empleado

#### Scenario: Segunda entrada con una abierta
- DADO un empleado con un intervalo abierto
- CUANDO se ejecuta `ClockIn`
- ENTONCES se lanza `OpenTimeEntryAlreadyExistsException` y no se guarda ningún intervalo adicional

#### Scenario: Entrada con una pausa abierta
- DADO un empleado con un `Break` abierto
- CUANDO se ejecuta `ClockIn`
- ENTONCES se lanza `OpenTimeEntryAlreadyExistsException`

#### Scenario: Entrada tras cerrar la anterior
- DADO un empleado cuyo único intervalo está cerrado
- CUANDO se ejecuta `ClockIn`
- ENTONCES se guarda un nuevo intervalo `Work` abierto

### Requirement: Caso de uso ClockOut

`ClockOut` DEBE cerrar el intervalo abierto del empleado de `ICurrentUser`, sea `Work`, `Break` o `MealBreak`, con el instante de `TimeProvider.GetUtcNow()` (truncado a segundos) y persistir el cambio; no abre ningún intervalo nuevo. Si no hay intervalo abierto, DEBE lanzar `NoOpenTimeEntryException`. Si el instante del reloj es anterior al inicio, DEBE propagar `ClockOutBeforeClockInException` sin modificar el intervalo.

#### Scenario: Reloj anterior a la entrada
- DADO un empleado con un intervalo abierto desde `08:00:00Z` y un `FakeTimeProvider` en `07:00:00Z`
- CUANDO se ejecuta `ClockOut`
- ENTONCES se lanza `ClockOutBeforeClockInException` y el intervalo sigue abierto

#### Scenario: Salida con entrada abierta
- DADO un empleado con un `Work` abierto desde `08:00:00Z` y un `FakeTimeProvider` en `16:00:00Z`
- CUANDO se ejecuta `ClockOut`
- ENTONCES el intervalo queda cerrado en `16:00:00Z` y se persiste

#### Scenario: Salida durante una pausa
- DADO un empleado con un `MealBreak` abierto desde `13:00:00Z` y un reloj en `13:30:00Z`
- CUANDO se ejecuta `ClockOut`
- ENTONCES la pausa queda cerrada a las `13:30:00Z`, no se abre ningún intervalo y el empleado queda sin intervalo abierto

#### Scenario: Salida sin entrada abierta
- DADO un empleado sin intervalos abiertos
- CUANDO se ejecuta `ClockOut`
- ENTONCES se lanza `NoOpenTimeEntryException`

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

### Requirement: Aislamiento entre empleados

Un empleado NO DEBE ver, cerrar, pausar ni reanudar los intervalos de otro, ni ser bloqueado por ellos. La condición «un solo intervalo abierto» se aplica por empleado.

#### Scenario: Listado propio
- DADO los empleados `A` y `B`, y `B` con un intervalo
- CUANDO `A` consulta `GET /api/time-entries`
- ENTONCES la lista de `A` no contiene intervalos de `B`

#### Scenario: Entradas abiertas simultáneas de empleados distintos
- DADO `A` con un intervalo abierto
- CUANDO `B` ejecuta `clock-in`
- ENTONCES `B` recibe `201`

#### Scenario: No se puede cerrar el fichaje ajeno
- DADO `A` con un intervalo abierto y `B` sin intervalos abiertos
- CUANDO `B` ejecuta `clock-out`
- ENTONCES `B` recibe `409` y el intervalo de `A` sigue abierto

#### Scenario: No se puede pausar ni reanudar por otro
- DADO `A` con un `Work` abierto y `B` con una pausa abierta
- CUANDO `B` ejecuta `start-break` y `A` ejecuta `end-break`
- ENTONCES ambos reciben `409` y los intervalos de ambos siguen como estaban

#### Scenario: Pausa sin tocar al otro empleado
- DADO `A` y `B` con un `Work` abierto cada uno
- CUANDO `A` ejecuta `start-break`
- ENTONCES `A` pasa a estar en pausa y el `Work` de `B` sigue abierto

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
- ENTONCES contiene `post` en `clock-in` (`201`, `401`, `409`), `clock-out`, `start-break`, `start-meal-break` y `end-break` (`200`, `401`, `409`) y `get` en `/api/time-entries` (`200`, `400`, `401`) con los parámetros de consulta `from` y `to`; `TimeEntryResponse` declara `type` como enumeración de texto, `format: date-time` en `clockInAt` y `clockOutAt` y no declara `workedMinutes`

### Requirement: Errores 409 como ProblemDetails en español

Toda excepción derivada de `DomainRuleException` (`OpenTimeEntryAlreadyExistsException`, `NoOpenTimeEntryException`, `ClockOutBeforeClockInException`, `TimeEntryAlreadyClosedException`, `NotWorkingException`, `NoOpenBreakException` y `ConcurrentTimeEntryChangeException`) DEBE traducirse en un único punto (`IExceptionHandler`) a una respuesta `409` de tipo de medio `application/problem+json` (RFC 9457), con `status` = `409` y `title` y `detail` en español (registro neutro). La respuesta NO DEBE incluir trazas de pila, nombres de excepciones ni identificadores de empleado. Cualquier otra excepción DEBE producir un `500` `application/problem+json` genérico (`title` «Se produjo un error inesperado», sin `detail`) sin mensaje ni traza de la excepción, también en `Development`.

#### Scenario: Doble entrada
- DADO un empleado con un intervalo abierto
- CUANDO envía `POST /api/time-entries/clock-in`
- ENTONCES recibe `409`, `application/problem+json` y `status` = `409`, con `detail` no vacío en español

#### Scenario: Salida sin entrada
- DADO un empleado sin intervalo abierto
- CUANDO envía `POST /api/time-entries/clock-out`
- ENTONCES recibe `409`, `application/problem+json` y `detail` no vacío en español

#### Scenario: Pausa sin estar trabajando
- DADO un empleado sin intervalo abierto, o ya en pausa
- CUANDO envía `POST /api/time-entries/start-break`
- ENTONCES recibe `409` con `title` «No está trabajando»

#### Scenario: Terminar una pausa inexistente
- DADO un empleado que está trabajando
- CUANDO envía `POST /api/time-entries/end-break`
- ENTONCES recibe `409` con `title` «No hay ninguna pausa abierta»

#### Scenario: Títulos en español
- DADO un `409` por entrada duplicada y otro por salida sin entrada
- CUANDO se inspeccionan sus `title`
- ENTONCES son «Ya existe un fichaje abierto» y «No hay ningún fichaje abierto», respectivamente

#### Scenario: Error inesperado
- DADO un repositorio que lanza una excepción con el texto «texto-sensible» y su traza
- CUANDO se envía cualquier petición de fichaje
- ENTONCES recibe `500` `application/problem+json` sin «texto-sensible» ni traza

#### Scenario: Sin datos internos
- DADO cualquier respuesta `409` de fichajes
- CUANDO se inspecciona el cuerpo
- ENTONCES no contiene `Exception`, `at TimeTracker` ni el `employeeId`

### Requirement: Flujo completo y pruebas del caso de uso

DEBE existir una prueba de integración de extremo a extremo (API y PostgreSQL real) del ciclo entrada, pausa, reanudar, comida, reanudar, salida con listados intermedios, y las reglas de Domain y Application DEBEN estar cubiertas por pruebas unitarias con `FakeTimeProvider`. La cobertura de líneas y de ramas de Domain DEBE ser del 100 % y la de Application del 100 % de líneas (umbral mínimo exigido por CI: 80 %).

#### Scenario: Flujo de extremo a extremo
- DADO la API en memoria con PostgreSQL real y un empleado nuevo
- CUANDO ejecuta el ciclo completo con pausas y consulta el listado en los pasos relevantes
- ENTONCES el `status` y los totales son coherentes con los intervalos devueltos

#### Scenario: Cobertura por niveles
- DADO `dotnet test` con cobertura
- CUANDO se evalúan los informes
- ENTONCES Domain muestra `line-rate` y `branch-rate` = `1` y Application muestra `line-rate` = `1`

### Requirement: Tipos de intervalo

`TimeTracker.Domain` DEBE definir `TimeEntryType` con los valores `Work`, `Break` y `MealBreak`. `TimeEntry` DEBE exponer su `Type`, fijado al crearlo e inmutable. `TimeEntry.Start(employeeId, type, at)` DEBE aceptar cualquier valor definido de `TimeEntryType` y lanzar `ArgumentOutOfRangeException` para un valor no definido. Las reglas de `employeeId` y de instante UTC de «Inicio de un fichaje en el dominio» siguen vigentes.

#### Scenario: Cada tipo se crea abierto
- DADO un `employeeId` válido y `at` UTC
- CUANDO se invoca `TimeEntry.Start` con `Work`, `Break` y `MealBreak`
- ENTONCES cada intervalo queda abierto y su `Type` es el pedido

#### Scenario: Tipo no definido
- DADO el valor numérico `99` convertido a `TimeEntryType`
- CUANDO se invoca `TimeEntry.Start`
- ENTONCES se lanza `ArgumentOutOfRangeException`

### Requirement: Pausa de un intervalo de trabajo en el dominio

`TimeEntry.Pause(breakType, at)` DEBE cerrar el intervalo de trabajo en `at` y devolver un intervalo nuevo, abierto, del mismo empleado, de tipo `breakType` (`Break` o `MealBreak`) y que empieza en `at`. Si el intervalo no es de tipo `Work`, DEBE lanzar `NotWorkingException` sin modificarlo. Si `breakType` es `Work` o un valor no definido, DEBE lanzar `ArgumentOutOfRangeException` sin modificarlo. Los errores de `Close` (ya cerrado, instante anterior a la entrada, instante no UTC) se propagan sin crear nada.

#### Scenario: Pausa desde trabajo
- DADO un intervalo `Work` abierto desde `08:00:00Z`
- CUANDO se invoca `Pause(Break, 10:00:00Z)`
- ENTONCES el trabajo queda cerrado a las `10:00:00Z` y se devuelve un intervalo `Break` abierto desde `10:00:00Z` del mismo empleado

#### Scenario: Pausa de comida
- DADO un intervalo `Work` abierto
- CUANDO se invoca `Pause(MealBreak, ...)`
- ENTONCES el intervalo devuelto es de tipo `MealBreak`

#### Scenario: Pausa desde una pausa
- DADO un intervalo `Break` abierto
- CUANDO se invoca `Pause(MealBreak, ...)`
- ENTONCES se lanza `NotWorkingException` y el intervalo sigue abierto

#### Scenario: Tipo de pausa no válido
- DADO un intervalo `Work` abierto
- CUANDO se invoca `Pause(Work, ...)` o `Pause` con un valor no definido
- ENTONCES se lanza `ArgumentOutOfRangeException` y el intervalo sigue abierto

#### Scenario: Pausa con reloj anterior a la entrada
- DADO un intervalo `Work` abierto desde `08:00:00Z`
- CUANDO se invoca `Pause(Break, 07:00:00Z)`
- ENTONCES se lanza `ClockOutBeforeClockInException` y el intervalo sigue abierto

### Requirement: Reanudación tras una pausa en el dominio

`TimeEntry.Resume(at)` DEBE cerrar el intervalo de pausa (`Break` o `MealBreak`) en `at` y devolver un intervalo `Work` nuevo, abierto, del mismo empleado, que empieza en `at`. Si el intervalo es de tipo `Work`, DEBE lanzar `NoOpenBreakException` sin modificarlo. Los errores de `Close` se propagan sin crear nada.

#### Scenario: Reanudar desde pausa
- DADO un intervalo `Break` abierto desde `10:00:00Z`
- CUANDO se invoca `Resume(10:15:00Z)`
- ENTONCES la pausa queda cerrada a las `10:15:00Z` y se devuelve un `Work` abierto desde `10:15:00Z`

#### Scenario: Reanudar desde comida
- DADO un intervalo `MealBreak` abierto
- CUANDO se invoca `Resume`
- ENTONCES se devuelve un `Work` abierto

#### Scenario: Reanudar estando trabajando
- DADO un intervalo `Work` abierto
- CUANDO se invoca `Resume`
- ENTONCES se lanza `NoOpenBreakException` y el intervalo sigue abierto

### Requirement: Caso de uso StartBreak

`StartBreak` DEBE, para el empleado de `ICurrentUser` y con el instante de `TimeProvider.GetUtcNow()`, cerrar su intervalo `Work` abierto y abrir un intervalo del tipo de pausa pedido (`Break` o `MealBreak`), persistiendo ambos cambios de forma atómica mediante `ITimeEntryRepository.ReplaceOpenAsync`. Si no hay intervalo abierto o el abierto no es `Work`, DEBE lanzar `NotWorkingException` y NO DEBE persistir nada. Devuelve el intervalo de pausa abierto.

#### Scenario: Iniciar pausa
- DADO un empleado con un `Work` abierto desde `08:00:00Z` y un reloj en `10:00:00Z`
- CUANDO se ejecuta `StartBreak` con `Break`
- ENTONCES el `Work` queda cerrado a las `10:00:00Z` y existe un `Break` abierto desde `10:00:00Z`, persistidos en una única operación

#### Scenario: Iniciar pausa de comida
- DADO un empleado con un `Work` abierto
- CUANDO se ejecuta `StartBreak` con `MealBreak`
- ENTONCES el nuevo intervalo abierto es `MealBreak`

#### Scenario: Sin intervalo abierto
- DADO un empleado sin intervalos abiertos
- CUANDO se ejecuta `StartBreak`
- ENTONCES se lanza `NotWorkingException` y no se persiste nada

#### Scenario: Ya está en pausa
- DADO un empleado con un `Break` abierto
- CUANDO se ejecuta `StartBreak`
- ENTONCES se lanza `NotWorkingException`, el `Break` sigue abierto y no se persiste nada

#### Scenario: Pausa de otro empleado
- DADO que solo otro empleado tiene un `Work` abierto
- CUANDO se ejecuta `StartBreak`
- ENTONCES se lanza `NotWorkingException` y el intervalo ajeno no se modifica

### Requirement: Caso de uso EndBreak

`EndBreak` DEBE cerrar el intervalo de pausa (`Break` o `MealBreak`) abierto del empleado de `ICurrentUser` y abrir un intervalo `Work` con el mismo instante de `TimeProvider.GetUtcNow()`, de forma atómica mediante `ITimeEntryRepository.ReplaceOpenAsync`. Si no hay intervalo abierto o el abierto es `Work`, DEBE lanzar `NoOpenBreakException` y NO DEBE persistir nada. Devuelve el intervalo `Work` abierto.

#### Scenario: Terminar pausa
- DADO un empleado con un `Break` abierto desde `10:00:00Z` y un reloj en `10:15:00Z`
- CUANDO se ejecuta `EndBreak`
- ENTONCES el `Break` queda cerrado a las `10:15:00Z` y existe un `Work` abierto desde `10:15:00Z`

#### Scenario: Terminar pausa de comida
- DADO un empleado con un `MealBreak` abierto
- CUANDO se ejecuta `EndBreak`
- ENTONCES se abre un `Work`

#### Scenario: Sin pausa abierta
- DADO un empleado con un `Work` abierto, o sin intervalos abiertos
- CUANDO se ejecuta `EndBreak`
- ENTONCES se lanza `NoOpenBreakException` y no se persiste nada

### Requirement: Cálculo de duración de un intervalo

`CalculateDuration()` DEBE devolver `null` si el intervalo está abierto y, si está cerrado, un `WorkedHours` con los minutos completos transcurridos entre `ClockInAt` y `ClockOutAt` **truncados**, sea cual sea el tipo. Sustituye a `CalculateWorked()`; las reglas de truncado y de cruce de medianoche de «Cálculo de minutos trabajados truncados» siguen vigentes. Que la duración cuente como tiempo trabajado o como pausa depende solo del `Type`.

#### Scenario: Duración de una pausa
- DADO un intervalo `Break` de `10:00:00Z` a `10:15:59Z`
- CUANDO se invoca `CalculateDuration()`
- ENTONCES `Minutes` es `15`

#### Scenario: Intervalo abierto
- DADO un intervalo `MealBreak` abierto
- CUANDO se invoca `CalculateDuration()`
- ENTONCES el resultado es `null`

### Requirement: Transición atómica y concurrente

Pausar y reanudar DEBEN cerrar un intervalo y abrir otro en una única transacción: o se persisten los dos cambios o ninguno. Si otra operación modificó el intervalo abierto entre su lectura y su cierre, o ya existe otro intervalo abierto del empleado, DEBE lanzarse `ConcurrentTimeEntryChangeException` (`409`, título «El fichaje ha cambiado») sin persistir nada. Dos pausas simultáneas del mismo empleado DEBEN dar como máximo una respuesta correcta.

#### Scenario: Atomicidad
- DADO un empleado con un `Work` abierto y un fallo al insertar el intervalo nuevo
- CUANDO se ejecuta `ReplaceOpenAsync`
- ENTONCES el `Work` sigue abierto en la base de datos

#### Scenario: Pausas simultáneas
- DADO un empleado con un `Work` abierto
- CUANDO se envían diez `start-break` en paralelo
- ENTONCES exactamente una responde `200`, las demás `409`, y existe un único intervalo abierto de tipo `Break`

#### Scenario: Salida simultánea a una pausa
- DADO un empleado con un `Work` abierto
- CUANDO se envían a la vez `clock-out` y `start-break`
- ENTONCES nunca queda un intervalo abierto tras una respuesta correcta de `clock-out`

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
