# Delta para time-tracking

Cambio: `datos-demo-mes`. El límite del listado pasa de 50 a 300 intervalos: un mes de fichajes con pausas (5 a 7 intervalos por día) supera los 50 y la demostración quedaría truncada a la última semana y media. El requisito modificado reemplaza por completo al vigente del mismo nombre.

## MODIFIED Requirements

### Requirement: Caso de uso GetTimeEntries y total

`GetTimeEntries` DEBE devolver, para el empleado de `ICurrentUser`, sus últimos 300 intervalos (constante `GetTimeEntriesUseCase.MaxEntries`, no controlable por el cliente) ordenados por `ClockInAt` descendente, con `type` y `durationMinutes` por intervalo (nulo si está abierto) y estos totales, siempre de los 300 devueltos y solo de los **cerrados**: `totalWorkedMinutes` (tipo `Work`), `totalBreakMinutes` (tipo `Break`) y `totalMealBreakMinutes` (tipo `MealBreak`). Un intervalo abierto NO DEBE sumar. `status` DEBE reflejar el intervalo abierto: `Idle` si no hay ninguno, `Working` si es `Work`, `OnBreak` si es `Break` y `OnMealBreak` si es `MealBreak`. Sin intervalos, la lista es vacía, los totales son `0` y `status` es `Idle`.

#### Scenario: Totales por tipo
- DADO un empleado con trabajos cerrados de `60` y `30` minutos, una pausa de `15`, una comida de `45` y un trabajo abierto
- CUANDO se ejecuta `GetTimeEntries`
- ENTONCES `totalWorkedMinutes` es `90`, `totalBreakMinutes` es `15`, `totalMealBreakMinutes` es `45` y el abierto tiene `durationMinutes` nulo

#### Scenario: Estado según el intervalo abierto
- DADO un empleado cuyo intervalo abierto es `Work`, `Break` o `MealBreak`, o que no tiene ninguno
- CUANDO se ejecuta `GetTimeEntries`
- ENTONCES `status` es `Working`, `OnBreak`, `OnMealBreak` o `Idle`, respectivamente

#### Scenario: Orden descendente
- DADO intervalos con entradas a las `08:00Z` de los días 10, 11 y 12
- CUANDO se ejecuta `GetTimeEntries`
- ENTONCES el primero es el del día 12 y el último el del día 10

#### Scenario: Sin fichajes
- DADO un empleado sin intervalos
- CUANDO se ejecuta `GetTimeEntries`
- ENTONCES la lista está vacía, los tres totales son `0` y `status` es `Idle`

#### Scenario: Más de 300 intervalos
- DADO un empleado con 305 intervalos
- CUANDO se ejecuta `GetTimeEntries`
- ENTONCES se devuelven 300, los más recientes por `ClockInAt`

#### Scenario: Total limitado a los 300 devueltos
- DADO un empleado con 305 intervalos `Work` cerrados de 10 minutos cada uno
- CUANDO se ejecuta `GetTimeEntries`
- ENTONCES se devuelven 300 y `totalWorkedMinutes` es `3000`
