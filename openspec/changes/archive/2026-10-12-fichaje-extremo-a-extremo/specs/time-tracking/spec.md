# Especificación: time-tracking

## Purpose

Define las reglas de dominio del fichaje (`TimeEntry`), los casos de uso `ClockIn`, `ClockOut` y `GetTimeEntries`, el contrato REST de `TimeTracker.Api` para fichar y consultar, la identidad provisional del empleado y el aislamiento entre empleados. Capacidad NUEVA; todos los requisitos son `ADDED`. Reutiliza `WorkedHours` (spec `worked-hours`).

Nombres fijados en el diseño: `TimeEntry.Start`, `Close`, `IsOpen`, `CalculateWorked()`; excepciones de dominio `TimeEntryAlreadyClosedException` y `ClockOutBeforeClockInException` y excepciones de Application `OpenTimeEntryAlreadyExistsException` y `NoOpenTimeEntryException` (todas derivadas de `DomainRuleException`); el listado se devuelve como `{ "items": [...], "totalWorkedMinutes": n }`.

Decisiones aceptadas: los instantes se truncan a segundos enteros; `totalWorkedMinutes` suma solo los fichajes cerrados de los 50 devueltos; el `201` de entrada no lleva cabecera `Location`.

Fuera de alcance: autenticación real y roles, tabla `Employee`, atribución por día, saldos, horas extra, tope de entrada olvidada, edición o borrado de fichajes, paginación y zona horaria local.

## ADDED Requirements

### Requirement: Inicio de un fichaje en el dominio

`TimeTracker.Domain` DEBE ofrecer `TimeEntry.Start(employeeId, at)` que crea un fichaje abierto (`ClockOutAt` nulo, `IsOpen` verdadero) con un `Id` nuevo. `employeeId` NO DEBE ser `Guid.Empty` y `at` DEBE ser un instante UTC (offset cero); en otro caso DEBE lanzar `ArgumentException` o una derivada. `ClockInAt` DEBE truncarse a segundos enteros (se descarta la fracción de segundo).

#### Scenario: Truncado a segundos
- DADO `at` = `2026-10-12T08:00:00.9876543Z`
- CUANDO se invoca `TimeEntry.Start(employeeId, at)`
- ENTONCES `ClockInAt` es `2026-10-12T08:00:00Z`

#### Scenario: Fichaje abierto válido
- DADO un `employeeId` válido y `at` = `2026-10-12T08:00:00Z`
- CUANDO se invoca `TimeEntry.Start(employeeId, at)`
- ENTONCES `ClockInAt` es `at`, `ClockOutAt` es nulo e `IsOpen` es verdadero

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

`ClockIn` DEBE crear un fichaje abierto para el empleado devuelto por `ICurrentUser`, con `ClockInAt` igual al instante de `TimeProvider.GetUtcNow()` (truncado a segundos), y persistirlo mediante `ITimeEntryRepository`. Si el empleado ya tiene un fichaje abierto, DEBE lanzar `OpenTimeEntryAlreadyExistsException` y NO DEBE crear otro. Dependiendo de `TimeProvider`, las pruebas unitarias NO DEBEN usar el reloj del sistema.

#### Scenario: Primera entrada
- DADO un empleado sin fichajes y un `FakeTimeProvider` en `2026-10-12T08:00:00Z`
- CUANDO se ejecuta `ClockIn`
- ENTONCES se guarda un fichaje abierto con `ClockInAt` = `2026-10-12T08:00:00Z` para ese empleado

#### Scenario: Segunda entrada con una abierta
- DADO un empleado con un fichaje abierto
- CUANDO se ejecuta `ClockIn`
- ENTONCES se lanza `OpenTimeEntryAlreadyExistsException` y no se guarda ningún fichaje adicional

#### Scenario: Entrada tras cerrar la anterior
- DADO un empleado cuyo único fichaje está cerrado
- CUANDO se ejecuta `ClockIn`
- ENTONCES se guarda un nuevo fichaje abierto

### Requirement: Caso de uso ClockOut

`ClockOut` DEBE cerrar el fichaje abierto del empleado de `ICurrentUser` con el instante de `TimeProvider.GetUtcNow()` (truncado a segundos) y persistir el cambio. Si no hay fichaje abierto, DEBE lanzar `NoOpenTimeEntryException`. Si el instante del reloj es anterior a la entrada, DEBE propagar `ClockOutBeforeClockInException` sin modificar el fichaje.

#### Scenario: Reloj anterior a la entrada
- DADO un empleado con un fichaje abierto desde `08:00:00Z` y un `FakeTimeProvider` en `07:00:00Z`
- CUANDO se ejecuta `ClockOut`
- ENTONCES se lanza `ClockOutBeforeClockInException` y el fichaje sigue abierto

#### Scenario: Salida con entrada abierta
- DADO un empleado con un fichaje abierto desde `08:00:00Z` y un `FakeTimeProvider` en `16:00:00Z`
- CUANDO se ejecuta `ClockOut`
- ENTONCES el fichaje queda cerrado en `16:00:00Z` y se persiste

#### Scenario: Salida sin entrada abierta
- DADO un empleado sin fichajes abiertos
- CUANDO se ejecuta `ClockOut`
- ENTONCES se lanza `NoOpenTimeEntryException`

### Requirement: Caso de uso GetTimeEntries y total

`GetTimeEntries` DEBE devolver, para el empleado de `ICurrentUser`, sus últimos 50 fichajes ordenados por `ClockInAt` descendente, con `workedMinutes` por fichaje (nulo si está abierto) y `totalWorkedMinutes` igual a la suma de los fichajes **cerrados** de esos mismos 50 devueltos (no del histórico completo). Un fichaje abierto NO DEBE sumar. Sin fichajes, la lista es vacía y el total es `0`.

#### Scenario: Total solo de cerrados
- DADO un empleado con fichajes cerrados de `60` y `30` minutos y uno abierto
- CUANDO se ejecuta `GetTimeEntries`
- ENTONCES `totalWorkedMinutes` es `90` y el abierto tiene `workedMinutes` nulo

#### Scenario: Orden descendente
- DADO fichajes con entradas a las `08:00Z` de los días 10, 11 y 12
- CUANDO se ejecuta `GetTimeEntries`
- ENTONCES el primero es el del día 12 y el último el del día 10

#### Scenario: Sin fichajes
- DADO un empleado sin fichajes
- CUANDO se ejecuta `GetTimeEntries`
- ENTONCES la lista está vacía y `totalWorkedMinutes` es `0`

#### Scenario: Más de 50 fichajes
- DADO un empleado con 55 fichajes
- CUANDO se ejecuta `GetTimeEntries`
- ENTONCES se devuelven 50, los más recientes por `ClockInAt`

#### Scenario: Total limitado a los 50 devueltos
- DADO un empleado con 55 fichajes cerrados de 10 minutos cada uno
- CUANDO se ejecuta `GetTimeEntries`
- ENTONCES se devuelven 50 y `totalWorkedMinutes` es `500`

### Requirement: Identidad provisional mediante ICurrentUser

`Application` DEBE definir el puerto `ICurrentUser` con `Guid EmployeeId`. Ningún endpoint, parámetro de ruta, consulta, cabecera ni cuerpo DEBE aceptar un `employeeId`; el empleado sale solo de `ICurrentUser`. En `Development`, `Api` DEBE ofrecer un adaptador que devuelve el valor de `Identity:DevelopmentEmployeeId`. Fuera de `Development` la API NO DEBE arrancar sin una identidad real (ver `startup-configuration`).

#### Scenario: El empleado sale de la configuración
- DADO `Identity:DevelopmentEmployeeId` = `A` en `Development`
- CUANDO se ejecuta `POST /api/time-entries/clock-in`
- ENTONCES el fichaje creado pertenece al empleado `A`

#### Scenario: Un employeeId del cliente se ignora
- DADO `Identity:DevelopmentEmployeeId` = `A`
- CUANDO se envía `POST /api/time-entries/clock-in?employeeId=B` (con `B` ≠ `A`)
- ENTONCES el fichaje creado pertenece a `A` y ninguno a `B`

#### Scenario: Una cabecera de empleado se ignora
- DADO `Identity:DevelopmentEmployeeId` = `A`
- CUANDO se envía `POST /api/time-entries/clock-in` con la cabecera `X-Employee-Id: B` (con `B` ≠ `A`)
- ENTONCES el fichaje creado pertenece a `A` y ninguno a `B`

#### Scenario: El contrato no expone employeeId
- DADO `docs/api/openapi.json`
- CUANDO se revisan los parámetros y esquemas de los tres endpoints de fichaje
- ENTONCES ninguno contiene una propiedad o parámetro `employeeId`

### Requirement: Aislamiento entre empleados

Un empleado NO DEBE ver, cerrar ni ser bloqueado por los fichajes de otro. La condición «una sola entrada abierta» se aplica por empleado.

#### Scenario: Listado propio
- DADO los empleados `A` y `B`, y `B` con un fichaje
- CUANDO `A` consulta `GET /api/time-entries`
- ENTONCES la lista de `A` no contiene fichajes de `B`

#### Scenario: Entradas abiertas simultáneas de empleados distintos
- DADO `A` con un fichaje abierto
- CUANDO `B` ejecuta `clock-in`
- ENTONCES `B` recibe `201`

#### Scenario: No se puede cerrar el fichaje ajeno
- DADO `A` con un fichaje abierto y `B` sin fichajes abiertos
- CUANDO `B` ejecuta `clock-out`
- ENTONCES `B` recibe `409` y el fichaje de `A` sigue abierto

#### Scenario: Salida sin tocar al otro empleado
- DADO `A` y `B` con un fichaje abierto cada uno
- CUANDO `A` ejecuta `clock-out`
- ENTONCES el fichaje de `A` queda cerrado y el de `B` sigue abierto

### Requirement: Contrato REST de fichajes

La API DEBE exponer: `POST /api/time-entries/clock-in` (`201` con `TimeEntryResponse`; `409` si hay una abierta), `POST /api/time-entries/clock-out` (`200` con `TimeEntryResponse`; `409` si no hay abierta) y `GET /api/time-entries` (`200` con `{ items, totalWorkedMinutes }`, donde `items` es una lista de `TimeEntryResponse`). `TimeEntryResponse` DEBE tener `id`, `clockInAt`, `clockOutAt` (nulo si abierta) y `workedMinutes` (nulo si abierta). Los instantes DEBEN serializarse en ISO 8601 UTC con sufijo `Z` (nunca `+00:00`) y declararse `format: date-time` en el contrato. El `201` de `clock-in` NO DEBE incluir cabecera `Location` (no existe consulta por identificador). Los POST NO DEBEN requerir cuerpo. El `clockInAt` del `201` DEBE ser idéntico al que devuelve el `GET` posterior.

#### Scenario: Entrada correcta
- DADO un empleado sin fichaje abierto
- CUANDO envía `POST /api/time-entries/clock-in`
- ENTONCES recibe `201`, `application/json` y un cuerpo con `clockOutAt` y `workedMinutes` nulos y `clockInAt` terminado en `Z`

#### Scenario: Entrada sin Location y coherente con el listado
- DADO un empleado sin fichaje abierto y un `FakeTimeProvider` en `2026-10-12T08:00:00.7Z`
- CUANDO envía `POST /api/time-entries/clock-in` y después `GET /api/time-entries`
- ENTONCES el `201` no incluye cabecera `Location` y su `clockInAt` (`2026-10-12T08:00:00Z`) es idéntico al del primer elemento del `GET`

#### Scenario: Salida correcta
- DADO un empleado con un fichaje abierto
- CUANDO envía `POST /api/time-entries/clock-out`
- ENTONCES recibe `200` y un cuerpo con `clockOutAt` no nulo y `workedMinutes` entero ≥ 0

#### Scenario: Minutos truncados en la salida
- DADO un empleado con entrada a las `08:00:00Z` y un `FakeTimeProvider` en `09:30:30Z`
- CUANDO envía `POST /api/time-entries/clock-out`
- ENTONCES `workedMinutes` es `90`

#### Scenario: Listado con total
- DADO un empleado con un fichaje cerrado de 60 minutos exactos y uno abierto
- CUANDO envía `GET /api/time-entries`
- ENTONCES recibe `200`, `totalWorkedMinutes` = `60` y el abierto con `workedMinutes` nulo

#### Scenario: Contrato OpenAPI
- DADO el build de `TimeTracker.Api` ejecutado
- CUANDO se lee `docs/api/openapi.json`
- ENTONCES contiene `post` en `/api/time-entries/clock-in` (respuesta `201`), `post` en `/api/time-entries/clock-out` (respuesta `200`) y `get` en `/api/time-entries`, la respuesta `409` de ambos POST y `format: date-time` en `clockInAt` y `clockOutAt`

### Requirement: Errores 409 como ProblemDetails en español

Toda excepción derivada de `DomainRuleException` (`OpenTimeEntryAlreadyExistsException`, `NoOpenTimeEntryException`, `ClockOutBeforeClockInException`, `TimeEntryAlreadyClosedException`) DEBE traducirse en un único punto (`IExceptionHandler`) a una respuesta `409` de tipo de medio `application/problem+json` (RFC 9457), con `status` = `409` y `title` y `detail` en español (registro neutro). La respuesta NO DEBE incluir trazas de pila, nombres de excepciones ni identificadores de empleado. Cualquier otra excepción DEBE producir un `500` `application/problem+json` genérico (`title` «Se produjo un error inesperado», sin `detail`) sin mensaje ni traza de la excepción, también en `Development`.

#### Scenario: Doble entrada
- DADO un empleado con un fichaje abierto
- CUANDO envía `POST /api/time-entries/clock-in`
- ENTONCES recibe `409`, `application/problem+json` y `status` = `409`, con `detail` no vacío en español

#### Scenario: Salida sin entrada
- DADO un empleado sin fichaje abierto
- CUANDO envía `POST /api/time-entries/clock-out`
- ENTONCES recibe `409`, `application/problem+json` y `detail` no vacío en español

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

DEBE existir una prueba de integración de extremo a extremo (API y PostgreSQL real) del flujo entrada, listado, salida, listado, y las reglas de Domain y Application DEBEN estar cubiertas por pruebas unitarias con `FakeTimeProvider`. La cobertura de líneas y ramas de `TimeTracker.Domain` DEBE ser 100 % y la de `TimeTracker.Application` ≥ 80 %. Las pruebas de borde que pasan a la primera (p. ej. salida igual a entrada, 59 segundos) DEBEN registrarse como verdes a la primera, con su rojo demostrado por mutación o justificado.

#### Scenario: Flujo de extremo a extremo
- DADO la API en memoria con PostgreSQL real y un empleado nuevo
- CUANDO ejecuta `clock-in`, `GET`, `clock-out` y `GET`
- ENTONCES las respuestas son `201`, `200` (con un abierto), `200` y `200` (con uno cerrado y total coherente con `workedMinutes`)

#### Scenario: Cobertura por niveles
- DADO `dotnet test` con cobertura
- CUANDO se evalúan los informes
- ENTONCES Domain muestra `line-rate` y `branch-rate` = `1` en el informe de `TimeTracker.Domain.Tests` y Application ≥ 80 % en el de `TimeTracker.Application.Tests`
