# Diseño: tipos-de-marcaje

## Resumen

Cada intervalo de `TimeEntry` pasa a tener un tipo (`Work`, `Break`, `MealBreak`). Se conserva el invariante «un único intervalo abierto por empleado» (índice único parcial existente). Pausar cierra el `Work` abierto y abre una pausa; terminar la pausa cierra la pausa y abre un `Work`; salir cierra el intervalo abierto sea del tipo que sea. El tiempo trabajado es la suma de los intervalos `Work`; las pausas y comidas se totalizan aparte. La regla de dependencias no cambia (Domain ← Application ← Infrastructure/Api).

## Decisiones

### DD-1: Un intervalo por tipo con un único abierto (frente a pausas anidadas o eventos)

Se mantiene el modelo de intervalos. Las alternativas (pausas como hijas de un fichaje; log de eventos) exigían permitir dos intervalos abiertos o rehacer el modelo y la migración (ver `exploration.md`). Con el modelo elegido el índice `ux_time_entries_employee_id_open`, las consultas y el listado siguen valiendo; el coste es que una pausa parte un día de trabajo en varios intervalos `Work`.

### DD-2: Tipo inmutable y transiciones en el dominio

`TimeEntry` recibe `Type` en `Start(employeeId, type, at)` (sustituye a `Start(employeeId, at)`; un único punto de creación, sin sobrecargas muertas). Las reglas de transición viven en la entidad:

- `Pause(breakType, at)`: solo desde `Work`; cierra `this` y devuelve el intervalo de pausa. Desde pausa lanza `NotWorkingException`. `breakType` distinto de `Break`/`MealBreak` es un error de programación (`ArgumentOutOfRangeException`), no una regla de negocio.
- `Resume(at)`: solo desde `Break`/`MealBreak`; cierra `this` y devuelve un `Work`. Desde `Work` lanza `NoOpenBreakException`.
- `CalculateDuration()` sustituye a `CalculateWorked()`: la duración de cualquier intervalo cerrado; clasificarla como trabajo o pausa es cosa del `Type`. `WorkedHours` se reutiliza como «cantidad de minutos» (no se renombra: YAGNI; su `Purpose` ya es una cantidad de tiempo en minutos).

Errores (`DomainRuleException`, 409 en español): `NotWorkingException` («No está trabajando») para iniciar pausa sin estar en `Work` (sin intervalo abierto o ya en pausa) y `NoOpenBreakException` («No hay ninguna pausa abierta») para terminarla sin pausa. Se usa un único error por regla para sencillez, porque el mensaje es válido en los dos motivos de fallo («solo puede iniciar una pausa mientras trabaja»).

### DD-3: Endpoints dedicados en lugar de un endpoint de transición único

`POST /api/time-entries/start-break`, `start-meal-break` y `end-break`, además de `clock-in` y `clock-out`, que se conservan. Un `POST /transition` con cuerpo (`{ "to": "Break" }`) obligaría a validar un cuerpo y a mapear estados, y abriría la puerta a pares de transición inválidos que hay que rechazar con 400/409. Los endpoints dedicados son coherentes con los actuales (sin cuerpo, sin parámetros que manipular por el cliente, un caso de uso por acción, cobertura de OpenAPI y de autorización por endpoint). `end-break` sirve para pausa y comida: el servidor sabe cuál está abierta. `start-break` y `start-meal-break` comparten caso de uso (`StartBreakUseCase`, parámetro `TimeEntryType`).

### DD-4: Persistencia del tipo como texto con restricción CHECK

La columna `type` es `character varying(16)` NOT NULL con los nombres de miembro (`Work`, `Break`, `MealBreak`) y `ck_time_entries_type` (`type IN (...)`). Frente a `smallint`: es legible en consultas y herramientas de soporte, estable si se reordena o amplía la enumeración (un número depende del orden de declaración) y la conversión explícita (`HasConversion<string>()`) es trivial. El coste (unos bytes más por fila) es irrelevante a esta escala. La restricción CHECK evita valores corruptos escritos fuera de la aplicación (defensa en profundidad; la regla vive en Domain).

### DD-5: Migración `AddTimeEntryType`

`AddColumn type varchar(16) NOT NULL DEFAULT 'Work'` y `AddCheckConstraint`. En PostgreSQL añadir una columna con valor por defecto constante no reescribe la tabla, y las filas existentes (producción) pasan a `Work`: sin pérdida de datos. `Down` elimina la restricción y la columna. El valor por defecto se conserva en la base de datos y NO se declara en el modelo (EF omitiría el CLR por defecto `Work` en los `INSERT` y ocultaría errores); es deliberado: mantiene válidas las inserciones SQL anteriores (por ejemplo pruebas y scripts) y no genera diferencia con el snapshot. Reversión con pausas ya registradas: pasarían a ser filas indistinguibles (ver `proposal.md`).

### DD-6: Atomicidad y concurrencia de la transición

Pausar y reanudar tocan dos filas. `ITimeEntryRepository.ReplaceOpenAsync(closed, next)` abre una transacción, guarda primero el cierre y luego inserta el nuevo abierto (con el índice parcial único no diferible, insertar antes violaría el índice). Un fallo revierte ambos pasos.

Sin control de concurrencia, una pausa y una salida simultáneas podrían dejar un intervalo abierto tras la salida (la pausa cerraría un `Work` ya cerrado y abriría otro intervalo). Por ello se activa el token `xmin` (propiedad sombra `xmin`, tipo `xid`, sin columna real ni cambio en el dominio) en `TimeEntry`; una modificación concurrente produce `DbUpdateConcurrencyException`, que el repositorio traduce a `ConcurrentTimeEntryChangeException` («El fichaje ha cambiado», 409). La violación del índice de intervalo abierto durante `ReplaceOpenAsync` se traduce igual. Esto levanta el aplazamiento DD-9 de `fichaje-extremo-a-extremo`: ahora hay una razón (dos filas por operación) y el coste es bajo. `clock-out` también queda protegido.

### DD-7: Resultado de aplicación y contrato

`TimeEntryResult(Id, Type, ClockInAt, ClockOutAt, DurationMinutes)` y `TimeEntryListResult(Items, TotalWorkedMinutes, TotalBreakMinutes, TotalMealBreakMinutes, Status)`. `workedMinutes` se renombra a `durationMinutes` porque deja de ser siempre trabajo; es un cambio de contrato aceptado (API y Web viajan juntas, sin otros consumidores). `Status` (`TimeTrackingStatus`: `Idle`, `Working`, `OnBreak`, `OnMealBreak`) se deduce del intervalo abierto de los devueltos (el abierto, si existe, es siempre el de `ClockInAt` más reciente) y lo calcula Application para que la Web no tenga que inferir reglas. En la API `type` y `status` se serializan como texto (`JsonStringEnumConverter` en el atributo del tipo; el contrato OpenAPI los declara como enumeraciones). Los totales suman solo intervalos cerrados de los 50 devueltos (regla existente).

Límite de 50: con pausas cada día genera más intervalos (hasta 5-7), por lo que 50 cubre menos días; se mantiene por el principio de cambios pequeños y se anota como riesgo.

### DD-8: Web

La Web recibe `type` y `status` como texto y los traduce a etiquetas en español y a botones mediante una clase estática pura y probada (`TimeEntryPresentation`): `Idle` → entrada; `Working` → pausa, comida y salida; `OnBreak`/`OnMealBreak` → terminar pausa y salida. Es presentación; el servidor sigue siendo quien valida (un `409` se muestra y se recarga). `TimeEntriesTable` (compartida por `/fichajes` y `/equipo`) gana la columna «Tipo», los tres totales y el estado, de modo que el responsable ve las pausas de su equipo sin duplicar código. Los valores desconocidos se muestran tal cual (compatibilidad hacia delante).

### DD-9: Seguridad

Sin cambios de modelo de amenazas: los nuevos endpoints toman al empleado del token (`ICurrentUser`), no aceptan `employeeId` y quedan protegidos por la política de autorización por defecto (la prueba `AuthorizationCoverageTests` los cubre). `/api/team` sigue siendo solo lectura.

## Flujo de pausa (secuencia)

```mermaid
sequenceDiagram
    participant W as Web
    participant A as Api (start-break)
    participant U as StartBreakUseCase
    participant D as TimeEntry (Domain)
    participant R as TimeEntryRepository
    participant P as PostgreSQL
    W->>A: POST /api/time-entries/start-break (Bearer)
    A->>U: ExecuteAsync(Break)
    U->>R: FindOpenAsync(employeeId del token)
    R-->>U: Work abierto (o null → NotWorkingException)
    U->>D: Pause(Break, now)
    D-->>U: Break nuevo (Work cerrado)
    U->>R: ReplaceOpenAsync(Work, Break)
    R->>P: BEGIN; UPDATE (xmin) Work; INSERT Break; COMMIT
    P-->>R: ok (o 23505 / 0 filas → ConcurrentTimeEntryChangeException)
    R-->>U: ok
    U-->>A: TimeEntryResult(Break)
    A-->>W: 200
```

## Archivos afectados

- Domain: `TimeEntryType`, `TimeEntry`, `NotWorkingException`, `NoOpenBreakException`.
- Application: `StartBreakUseCase`, `EndBreakUseCase`, `ClockInUseCase`, `ClockOutUseCase` (sin lógica nueva salvo `Start`), `TimeEntryResult`, `TimeTrackingStatus`, `ITimeEntryRepository`, `ConcurrentTimeEntryChangeException`.
- Infrastructure: `TimeEntryConfiguration`, `TimeEntryRepository`, migración `AddTimeEntryType` (y snapshot).
- Api: `TimeEntryEndpoints`, `TimeEntryResponse`, `TimeEntryListResponse`; `docs/api/openapi.json` regenerado.
- Web: `TimeEntryDto`, `TimeEntryListDto`, `TimeEntriesApiClient`, `TimeEntryPresentation`, `TimeEntriesTable`, `TimeEntries.razor`, `Team.razor` (sin cambios de lógica).
- Documentación: README, ADR 0014, especificaciones fusionadas.

## Plan de pruebas (Strict TDD)

Rojo → verde → refactor por tarea. Unitarias: Domain (tipos, `Pause`, `Resume`, duración; 100 % líneas y ramas), Application con `FakeTimeProvider` y repositorio en memoria (casos de uso, totales, estado; 100 % de líneas), Web (`TimeEntryPresentation`, `WorkedTimeFormatter`). Integración con PostgreSQL real: migración sobre datos antiguos y reversión, restricciones, `ReplaceOpenAsync` (atomicidad y concurrencia), endpoints (ciclo completo, transiciones inválidas, aislamiento, 10 pausas en paralelo), vista de equipo y contrato OpenAPI.
