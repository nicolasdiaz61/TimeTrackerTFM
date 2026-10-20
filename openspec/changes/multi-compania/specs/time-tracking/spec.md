# Delta para time-tracking

Cambio: `multi-compania`. Capacidad EXISTENTE (`openspec/specs/time-tracking/spec.md`). NOTA DE ALCANCE: `time-tracking` no figura en la lista de capacidades de la propuesta; este delta mínimo existe porque `CompanyId` explícito en `TimeEntry` cambia la firma de `TimeEntry.Start` descrita en dos requisitos vigentes. Contiene bloques `MODIFIED` (nombres exactos de los requisitos vigentes) y `ADDED`. El resto de `time-tracking` no cambia.

> Aviso de delta que cambia escenarios vigentes: las firmas `TimeEntry.Start(employeeId, type, at)` pasan a `TimeEntry.Start(companyId, employeeId, type, at)`.

## MODIFIED Requirements

### Requirement: Inicio de un fichaje en el dominio

`TimeTracker.Domain` DEBE ofrecer `TimeEntry.Start(companyId, employeeId, type, at)` que crea un intervalo abierto (`ClockOutAt` nulo, `IsOpen` verdadero) del tipo indicado con un `Id` nuevo y la compañía indicada (`CompanyId`, inmutable). `companyId` y `employeeId` NO DEBEN ser `Guid.Empty` y `at` DEBE ser un instante UTC (offset cero); en otro caso DEBE lanzar `ArgumentException` o una derivada. `ClockInAt` DEBE truncarse a segundos enteros (se descarta la fracción de segundo). Un fichaje de entrada es un intervalo de tipo `Work`.
(Previously: `TimeEntry.Start(employeeId, type, at)`, sin compañía)

#### Scenario: Truncado a segundos
- DADO `at` = `2026-10-12T08:00:00.9876543Z`
- CUANDO se invoca `TimeEntry.Start(companyId, employeeId, Work, at)`
- ENTONCES `ClockInAt` es `2026-10-12T08:00:00Z`

#### Scenario: Fichaje abierto válido
- DADO un `companyId` y un `employeeId` válidos y `at` = `2026-10-12T08:00:00Z`
- CUANDO se invoca `TimeEntry.Start(companyId, employeeId, Work, at)`
- ENTONCES `ClockInAt` es `at`, `ClockOutAt` es nulo, `IsOpen` es verdadero, `Type` es `Work` y `CompanyId` es `companyId`

#### Scenario: Identificador de empleado vacío
- DADO `Guid.Empty` como `employeeId`
- CUANDO se invoca `TimeEntry.Start`
- ENTONCES se lanza `ArgumentException` o una derivada

#### Scenario: Compañía vacía
- DADO `Guid.Empty` como `companyId`
- CUANDO se invoca `TimeEntry.Start`
- ENTONCES se lanza `ArgumentException` o una derivada

#### Scenario: Instante que no es UTC
- DADO `at` = `2026-10-12T10:00:00+02:00`
- CUANDO se invoca `TimeEntry.Start`
- ENTONCES se lanza `ArgumentException` o una derivada

### Requirement: Tipos de intervalo

`TimeTracker.Domain` DEBE definir `TimeEntryType` con los valores `Work`, `Break` y `MealBreak`. `TimeEntry` DEBE exponer su `Type`, fijado al crearlo e inmutable. `TimeEntry.Start(companyId, employeeId, type, at)` DEBE aceptar cualquier valor definido de `TimeEntryType` y lanzar `ArgumentOutOfRangeException` para un valor no definido. Las reglas de `companyId`, `employeeId` y de instante UTC de «Inicio de un fichaje en el dominio» siguen vigentes.
(Previously: `TimeEntry.Start(employeeId, type, at)`)

#### Scenario: Cada tipo se crea abierto
- DADO un `companyId` y un `employeeId` válidos y `at` UTC
- CUANDO se invoca `TimeEntry.Start` con `Work`, `Break` y `MealBreak`
- ENTONCES cada intervalo queda abierto y su `Type` es el pedido

#### Scenario: Tipo no definido
- DADO el valor numérico `99` convertido a `TimeEntryType`
- CUANDO se invoca `TimeEntry.Start`
- ENTONCES se lanza `ArgumentOutOfRangeException`

## ADDED Requirements

### Requirement: La compañía del fichaje sale del token y se hereda

Los casos de uso `ClockIn`, `StartBreak`, `EndBreak` y `ClockOut` DEBEN crear los intervalos con la compañía de `ICurrentUser.CompanyId` (ver `authentication`), nunca de un parámetro. `TimeEntry.Pause` y la reanudación DEBEN crear el intervalo nuevo con el mismo `CompanyId` y el mismo `EmployeeId` del intervalo que cierran. Los endpoints de fichaje NO DEBEN aceptar `companyId` de ningún origen (ruta, consulta, cabecera ni cuerpo).

#### Scenario: Entrada con la compañía del token
- DADO un empleado de A autenticado
- CUANDO envía `POST /api/time-entries/clock-in`
- ENTONCES el intervalo creado tiene el `CompanyId` de A

#### Scenario: Pausa hereda la compañía
- DADO un `Work` abierto de A
- CUANDO se invoca `Pause(Break, at)`
- ENTONCES el `Break` devuelto tiene el mismo `CompanyId` y `EmployeeId` que el `Work`

#### Scenario: companyId del cliente ignorado
- DADO un empleado de A y `POST /api/time-entries/clock-in?companyId={B}` con cabecera `X-Company-Id: {B}`
- CUANDO se procesa
- ENTONCES el fichaje creado pertenece a A y B no gana ninguno

#### Scenario: Contrato sin companyId
- DADO el contrato regenerado
- CUANDO se buscan las operaciones de `/api/time-entries`
- ENTONCES ninguna contiene `companyId` en parámetros ni esquemas
