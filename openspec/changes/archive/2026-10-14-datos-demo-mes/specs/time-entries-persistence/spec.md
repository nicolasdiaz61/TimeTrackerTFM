# Delta para time-entries-persistence

Cambio: `datos-demo-mes`. El repositorio devuelve hasta el límite que le pide el caso de uso (300) y se añade el almacén de la siembra de demostración.

## MODIFIED Requirements

### Requirement: Repositorio acotado por empleado

El repositorio DEBE devolver el fichaje abierto de un empleado y los últimos fichajes que se le pidan (el listado usa 300) por `ClockInAt` descendente, siempre filtrados por `EmployeeId`.

#### Scenario: Últimos N
- DADO un empleado con 55 fichajes y otro con 3
- CUANDO se solicitan los 50 últimos del primero
- ENTONCES devuelve 50, los más recientes en orden descendente, sin filas del segundo

#### Scenario: Entrada abierta ajena no se devuelve
- DADO un fichaje abierto de `B`
- CUANDO se busca el abierto de `A`
- ENTONCES el resultado es nulo

## ADDED Requirements

### Requirement: Almacén de la siembra de fichajes de demostración

`DemoTimeEntryStore` DEBE implementar `IDemoTimeEntryStore`: `ListStartsBetweenAsync` devuelve los `ClockInAt` de un empleado en un rango `[desde, hasta)` y `AddRangeAsync` persiste una colección de intervalos con una única llamada a `SaveChanges` (todo o nada).

#### Scenario: Inicios en el rango
- DADO un empleado con intervalos dentro y fuera del rango y otro empleado con intervalos dentro
- CUANDO se listan los inicios del primero
- ENTONCES solo se devuelven los suyos dentro del rango, incluido el límite inferior y excluido el superior

#### Scenario: Inserción atómica
- DADO una colección de intervalos cerrados con una fila inválida para la base
- CUANDO se intenta persistir
- ENTONCES falla y no queda ninguna fila de la colección
