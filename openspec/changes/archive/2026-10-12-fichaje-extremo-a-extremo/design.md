# Diseño: fichaje-extremo-a-extremo

Fecha: 2026-10-11 · Fase: sdd-design · Modo: openspec · Base: `proposal.md`, `exploration.md` (D1–D16, Q1–Q12 aceptados por defecto), código vigente y lecciones del cambio archivado `2026-10-11-esqueleto-solucion` (W1–W8, S1–S7). Las especificaciones se redactan en paralelo; la sección 15 recoge los conflictos esperables con las vigentes.

Convención: **[V-n]** = validación que apply debe comprobar antes de dar el paso por bueno (sección 16). Las versiones de paquetes son las verificadas en la exploración; apply las confirma (V1).

## 1. Enfoque técnico

Backend completo con TDD y, al final, Web mínima. El flujo de un fichaje atraviesa las cuatro capas sin romper la regla de dependencias:

```
Web (/fichajes) --HTTP+CORS--> Api: TimeEntryEndpoints --> Application: ClockIn/ClockOut/GetTimeEntries
                                  |  ICurrentUser (DevelopmentCurrentUser)        |  ITimeEntryRepository, TimeProvider
                                  |  DomainRuleExceptionHandler -> ProblemDetails  v
                                  +-- composition root ------> Infrastructure: TimeEntryRepository -> TimeTrackerDbContext -> PostgreSQL
                                                                Domain: TimeEntry, WorkedHours (sin dependencias)
```

- **Domain** (100 %): `TimeEntry` con reglas puras; recibe instantes, nunca lee el reloj.
- **Application** (≥ 80 %, sin paquetes NuGet): puertos `ITimeEntryRepository` e `ICurrentUser`, `TimeProvider` (BCL) como reloj, tres casos de uso y dos excepciones de conflicto.
- **Infrastructure**: EF Core + Npgsql, mapeo Fluent, índices, traducción de `23505`, migración inicial, fábrica de diseño, opciones de la cadena de conexión.
- **Api** (composition root): endpoints `/api/time-entries`, identidad provisional solo en Development, validación al arrancar, migraciones por bandera, ProblemDetails en español, contrato OpenAPI ampliado.
- **Web**: página `/fichajes`, cliente tipado, `ApiBaseUrl`. Sin reglas de negocio.

Se mantienen los 9 proyectos: las pruebas de integración con PostgreSQL viven en `TimeTracker.Api.IntegrationTests` (D9), así que `RepositoryConventionsTests` y la especificación "exactamente 9 proyectos" no cambian.

## 2. Decisiones técnicas

| # | Decisión | Elección | Alternativas descartadas | Justificación |
|---|---|---|---|---|
| DD-1 | Identidad | Puerto `ICurrentUser` en Application; adaptador `DevelopmentCurrentUser` en Api que lee `Identity:DevelopmentEmployeeId`; validador con `ValidateOnStart` que falla fuera de Development | Cabecera/parámetro `employeeId` (viola AGENTS §6); JWT ahora (duplica `autenticacion-y-roles`); esquema de autenticación "Development" con claims (+40 líneas) | Ningún endpoint recibe identidad. Reutiliza el patrón `IValidateOptions` de CORS (ADR 0007), así las pruebas de arranque existentes sirven de plantilla. ADR 0009. |
| DD-2 | Modelo | Solo `TimeEntry` (`Id`, `EmployeeId`, `ClockInAt`, `ClockOutAt?`), sin tabla `Employee` | Tabla `Employee` con FK | YAGNI hasta `autenticacion-y-roles` (Q2). |
| DD-3 | Constructor | Un único constructor **privado con parámetros** que usan `Start` y EF (enlace por constructor); propiedades de solo lectura salvo `ClockOutAt { get; private set; }` | Constructor sin parámetros para EF | Un constructor sin uso en pruebas rompería el 100 % de Domain (V6). |
| DD-4 | Identificador | `Guid.CreateVersion7(at)` generado en `TimeEntry.Start` | `Guid.NewGuid()`; secuencia de la BD | Ordenable por tiempo (índices más compactos); BCL, sin estado estático. |
| DD-5 | Precisión | `Start` y `Close` truncan el instante a segundos enteros y exigen `Offset == TimeSpan.Zero` | Guardar ticks completos | `timestamptz` guarda microsegundos: sin truncar, el cuerpo del 201 (en memoria) y el `GET` posterior (de la BD) diferirían. El truncado a la baja conserva `salida ≥ entrada`. **Posible discrepancia con specs** (sección 15). |
| DD-6 | Reloj | `TimeProvider` inyectado en los casos de uso; `TimeProvider.System` registrado en Api; `FakeTimeProvider` en pruebas | `IClock` propio | Es BCL (`System.Runtime`): Application sigue sin paquetes y pasa la prueba de marco base (V7). ADR 0010. |
| DD-7 | Errores de negocio | Excepciones tipadas que heredan de `DomainRuleException` (Domain, abstracta, con `Title`) → `IExceptionHandler` → 409 ProblemDetails | Tipo `Result` y `TypedResults.Conflict` en el endpoint | Acordado en D4. El manejador es genérico: un único `is DomainRuleException`. Rechazado `Result` por coste en casos de uso y pruebas; se reconsidera si crecen los resultados de negocio. |
| DD-8 | Concurrencia de entrada | Comprobación previa en `ClockIn` + índice único parcial; `23505` con el nombre del índice → `OpenTimeEntryAlreadyExistsException` | Bloqueo `SELECT … FOR UPDATE`; nivel `Serializable` | El índice es la garantía; la comprobación solo da el mensaje en el caso normal. Se filtra por nombre de restricción para no traducir otras violaciones. |
| DD-9 | Concurrencia de salida | **DIFERIDA a seguimiento (recorte acordado 2026-10-12).** Sin `xmin`, sin token de concurrencia ni columna de concurrencia; `UpdateAsync` es una salida simple (`SaveChangesAsync`, última escritura gana) | `xmin` como token (`uint` sombra, `IsRowVersion()`, `DbUpdateConcurrencyException` → `NoOpenTimeEntryException`) | Se recorta alcance: el control no figuraba en la propuesta. **Riesgo residual aceptado:** dos `clock-out` simultáneos del mismo fichaje pueden dar dos `200` y la última escritura fija `clock_out_at` (la `CHECK` y las reglas de Domain se siguen cumpliendo; no hay corrupción de datos ni segunda fila). La **entrada** sigue protegida por el índice único parcial (DD-8). Reabrir en seguimiento si se añade edición de fichajes o más clientes. |
| DD-10 | Paquetes EF | Npgsql EF 10.0.3; `Microsoft.EntityFrameworkCore.Relational` 10.0.12 como referencia **directa** en Infrastructure (eleva toda la familia); `Microsoft.EntityFrameworkCore.Design` 10.0.12 con `PrivateAssets=all` | `CentralPackageTransitivePinningEnabled` global | Fijado explícito y local, sin cambiar el comportamiento de los demás paquetes (V4). |
| DD-11 | Opciones de BD | `ConnectionStringOptions` + validador en Infrastructure (sin paquetes extra: `IValidateOptions` llega con EF); el enlace `BindConfiguration` y `ValidateOnStart` en Api | Paquete `Microsoft.Extensions.Options.ConfigurationExtensions` en Infrastructure | Evita mezclar versiones de `Microsoft.Extensions.*` con el marco compartido de ASP.NET. |
| DD-12 | Migraciones al arrancar | `DatabaseMigrationHostedService : IHostedLifecycleService` (Api) que en `StartingAsync` lee `Database:ApplyMigrationsOnStartup` y llama a `IServiceProvider.MigrateDatabaseAsync()` (Infrastructure) | Llamada en `Program.cs` entre `Build()` y `Run()`; `IHostedService.StartAsync` | Corre **después** de `ValidateOnStart` y **antes** de que Kestrel escuche (V10). `WebApplicationFactory` y `GetDocument` interceptan `Build()`: el código posterior de `Program.cs` no se ejecutaría y la bandera no sería comprobable. |
| DD-13 | Nombres SQL | `ToTable("time_entries")` y `HasColumnName` explícitos | `EFCore.NamingConventions` | Cuatro columnas no justifican un paquete. |
| DD-14 | Fechas en JSON | DTOs de respuesta con `DateTime` (`UtcDateTime`, `Kind=Utc`) | `DateTimeOffset` en el DTO; convertidor propio | `System.Text.Json` escribe `+00:00` para `DateTimeOffset`; con `DateTime` UTC escribe `Z` (V13). |
| DD-15 | 201 de entrada | `TypedResults.Created((string?)null, body)` (sin `Location`) | `Location` a `/api/time-entries/{id}` | No existe `GET` por id (YAGNI); un `Location` roto sería peor que ninguno. |
| DD-16 | W4 | Se amplía la rama `GetDocument.Insider` en una clase `DocumentGeneration`: fuerza `EnvironmentName=Development` en `WebApplicationOptions` y aporta valores ficticios (`Cors`, `ConnectionStrings:Default` con `Host=db.invalid` sin contraseña, `Identity:DevelopmentEmployeeId`) | Relajar el validador de identidad; exportar con `WebApplicationFactory` | `GetDocument` arranca el host y dispara `ValidateOnStart` (V5 archivada), en entorno no Development. La conexión es perezosa: no se conecta. Respaldo: exportación por prueba (V8). |
| DD-17 | Cliente Web | `TimeEntriesApiClient` sobre un `HttpClient` propio con `BaseAddress = ApiBaseUrl`, registrado con `AddScoped` | `AddHttpClient<T>` | Evita añadir `Microsoft.Extensions.Http` al WASM. |
| DD-18 | Prueba de arquitectura | Se añade `Application_ReferencesOnlyDomainAndBaseFramework` y una comprobación XML de que Domain y Application no declaran `PackageReference` ni `FrameworkReference` | Dejarlo como está | Cierra W2 en las dos capas internas, ahora que Infrastructure trae EF. |

## 3. Estructura de archivos

| Archivo | Acción | Tanda |
|---|---|---|
| `src/TimeTracker.Domain/DomainRuleException.cs` | Crear: base abstracta (`Title`, `Message` en español) | 1 |
| `src/TimeTracker.Domain/TimeTracking/{TimeEntry, TimeEntryAlreadyClosedException, ClockOutBeforeClockInException}.cs` | Crear | 1 |
| `src/TimeTracker.Application/Identity/ICurrentUser.cs` | Crear | 1 |
| `src/TimeTracker.Application/TimeTracking/{ITimeEntryRepository, ClockInUseCase, ClockOutUseCase, GetTimeEntriesUseCase, TimeEntryResult, OpenTimeEntryAlreadyExistsException, NoOpenTimeEntryException}.cs` | Crear | 1 |
| `tests/TimeTracker.Domain.Tests/TimeEntryTests.cs` | Crear | 1 |
| `tests/TimeTracker.Application.Tests/{ClockInUseCaseTests, ClockOutUseCaseTests, GetTimeEntriesUseCaseTests}.cs`, `Fakes/{InMemoryTimeEntryRepository, FixedCurrentUser}.cs`; `.csproj` (+ TimeProvider.Testing) | Crear/Modificar | 1 |
| `tests/TimeTracker.ArchitectureTests/{DependencyRuleTests, RepositoryConventionsTests}.cs` | Modificar (DD-18) | 1 |
| `Directory.Packages.props` | Modificar: 5 paquetes (§11) | 1–2 |
| `src/TimeTracker.Infrastructure/TimeTracker.Infrastructure.csproj` | Modificar: Npgsql EF, EF Relational, EF Design | 2 |
| `src/TimeTracker.Infrastructure/Persistence/{TimeTrackerDbContext, TimeEntryConfiguration, TimeEntryRepository, ConnectionStringOptions, ConnectionStringOptionsValidator, DesignTimeDbContextFactory, PersistenceServiceCollectionExtensions}.cs` | Crear | 2 |
| `src/TimeTracker.Infrastructure/Persistence/Migrations/*` | Generado (`dotnet ef migrations add InitialTimeEntries`) | 2 |
| `.config/dotnet-tools.json` | Crear: `dotnet-ef` 10.0.12 | 2 |
| `.editorconfig` | Modificar: `[src/TimeTracker.Infrastructure/Persistence/Migrations/**.cs]` `generated_code = true` | 2 |
| `tests/TimeTracker.Api.IntegrationTests/Postgres/{PostgresFixture, PostgresCollection}.cs`, `Persistence/{TimeEntryRepositoryTests, MigrationTests}.cs`; `.csproj` (+ Testcontainers, TimeProvider.Testing) | Crear/Modificar | 2 |
| `src/TimeTracker.Api/Program.cs` | Modificar | 3 |
| `src/TimeTracker.Api/Configuration/{DocumentGeneration, DatabaseOptions, DatabaseServiceCollectionExtensions}.cs` | Crear | 3 |
| `src/TimeTracker.Api/Identity/{ProvisionalIdentityOptions, ProvisionalIdentityOptionsValidator, DevelopmentCurrentUser, IdentityServiceCollectionExtensions}.cs` | Crear | 3 |
| `src/TimeTracker.Api/Errors/{DomainRuleExceptionHandler, ProblemDetailsServiceCollectionExtensions}.cs` | Crear | 3 |
| `src/TimeTracker.Api/Persistence/DatabaseMigrationHostedService.cs` | Crear | 3 |
| `src/TimeTracker.Api/TimeEntries/{TimeEntryEndpoints, TimeEntryResponse}.cs` | Crear | 3 |
| `tests/TimeTracker.Api.IntegrationTests/ApiFactory.cs`, `StartupConfigurationTests.cs` | Modificar (valores base válidos, V9, R7) | 3 |
| `tests/TimeTracker.Api.IntegrationTests/TimeEntries/{TimeEntryEndpointsTests, TimeEntryIsolationTests, TimeEntryConcurrencyTests}.cs`, `ErrorHandlingTests.cs`, `IdentityStartupTests.cs`, `DatabaseStartupTests.cs` | Crear | 3 |
| `tests/TimeTracker.ArchitectureTests/OpenApiContractTests.cs` | Modificar | 3 |
| `docs/api/openapi.json` | Generado | 3 |
| `docker-compose.yml`, `.env.example`, `.github/workflows/ci.yml` | Modificar | 3 |
| `src/TimeTracker.Web/Program.cs`, `Layout/NavMenu.razor` | Modificar | 4 |
| `src/TimeTracker.Web/wwwroot/appsettings.json` | Crear: `{"ApiBaseUrl": "http://localhost:5274"}` | 4 |
| `src/TimeTracker.Web/TimeEntries/{TimeEntriesApiClient, TimeEntryDto, TimeEntryListDto, ApiProblemDto, TimeEntriesApiException}.cs`, `Pages/TimeEntries.razor` | Crear | 4 |
| `docs/adr/{0008-persistencia-postgresql-ef-core, 0009-identidad-provisional, 0010-tiempo-utc-y-minutos}.md` | Crear | 4 |
| `README.md`, `openspec/config.yaml` | Modificar | 4 |

Sin cambios: `Dockerfile` (ya copia el `.csproj` de Infrastructure; las migraciones se compilan en el ensamblado), `.dockerignore`, `appsettings*.json` (no reciben valores de entorno: SC "Ningún secreto versionado"). Las páginas de plantilla `Counter`/`Weather` se dejan (reducir diff).

## 4. Interfaces y contratos

```csharp
// Domain
public abstract class DomainRuleException(string title, string message) : Exception(message)
{ public string Title { get; } = title; }

public sealed class TimeEntry
{
    private TimeEntry(Guid id, Guid employeeId, DateTimeOffset clockInAt, DateTimeOffset? clockOutAt) { … }
    public Guid Id { get; }  public Guid EmployeeId { get; }
    public DateTimeOffset ClockInAt { get; }  public DateTimeOffset? ClockOutAt { get; private set; }
    public bool IsOpen => ClockOutAt is null;
    public static TimeEntry Start(Guid employeeId, DateTimeOffset at);   // Guid.Empty o no UTC → ArgumentException
    public void Close(DateTimeOffset at);   // cerrada → TimeEntryAlreadyClosedException; at < ClockInAt → ClockOutBeforeClockInException
    public WorkedHours? CalculateWorked();  // null si abierta; WorkedHours.FromMinutes(checked((int)(out - in).TotalMinutes))
}

// Application
public interface ICurrentUser { Guid EmployeeId { get; } }
public interface ITimeEntryRepository
{
    Task<TimeEntry?> FindOpenAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TimeEntry>> ListLatestAsync(Guid employeeId, int limit, CancellationToken cancellationToken); // ClockInAt DESC
    Task AddAsync(TimeEntry entry, CancellationToken cancellationToken);    // persiste; 23505 del índice → OpenTimeEntryAlreadyExistsException
    Task UpdateAsync(TimeEntry entry, CancellationToken cancellationToken); // persiste (salida simple; sin control de concurrencia: DD-9 diferida)
}
public sealed record TimeEntryResult(Guid Id, DateTimeOffset ClockInAt, DateTimeOffset? ClockOutAt, int? WorkedMinutes);
public sealed record TimeEntryListResult(IReadOnlyList<TimeEntryResult> Items, int TotalWorkedMinutes);
// ClockInUseCase / ClockOutUseCase: Task<TimeEntryResult> ExecuteAsync(CancellationToken)
// GetTimeEntriesUseCase: Task<TimeEntryListResult> ExecuteAsync(CancellationToken); const int MaxEntries = 50
// Constructores: (ITimeEntryRepository, ICurrentUser, TimeProvider) — GetTimeEntries no necesita TimeProvider
```

El total suma, con `WorkedHours.operator +`, solo las entradas **cerradas de las 50 devueltas** (ver sección 15).

**Endpoints** (`MapGroup("/api/time-entries").WithTags("TimeEntries")`, sin parámetros ni cuerpo de petición):

| Método y ruta | Éxito | Errores | Cuerpo |
|---|---|---|---|
| `POST /api/time-entries/clock-in` | 201 (sin `Location`) | 409 `application/problem+json` | `TimeEntryResponse` |
| `POST /api/time-entries/clock-out` | 200 | 409 | `TimeEntryResponse` |
| `GET /api/time-entries` | 200 | — | `TimeEntryListResponse` |

`TimeEntryResponse { id: uuid, clockInAt: date-time (Z), clockOutAt: date-time|null, workedMinutes: int|null }`; `TimeEntryListResponse { items: TimeEntryResponse[], totalWorkedMinutes: int }`. Cada endpoint declara `.ProducesProblem(409)` para que el contrato lo refleje. Todos responden 500 ProblemDetails genérico ante un fallo inesperado.

**ProblemDetails (español, registro neutro):**

| Excepción | `title` | `detail` |
|---|---|---|
| `OpenTimeEntryAlreadyExistsException` | Ya existe un fichaje abierto | Registre la salida antes de fichar una nueva entrada. |
| `NoOpenTimeEntryException` | No hay ningún fichaje abierto | Registre la entrada antes de fichar la salida. |
| `ClockOutBeforeClockInException` | Hora de salida no válida | La hora de salida no puede ser anterior a la de entrada. |
| `TimeEntryAlreadyClosedException` | El fichaje ya está cerrado | El fichaje ya tiene registrada la salida. |
| Cualquier otra | Se produjo un error inesperado | (sin `detail`; solo `traceId`) |

## 5. Secuencias

```mermaid
sequenceDiagram
  participant W as Web /fichajes
  participant E as TimeEntryEndpoints
  participant U as ClockInUseCase
  participant R as TimeEntryRepository
  participant DB as PostgreSQL
  participant H as DomainRuleExceptionHandler
  W->>E: POST /api/time-entries/clock-in (sin employeeId)
  E->>U: ExecuteAsync()
  U->>U: employeeId = ICurrentUser.EmployeeId; now = TimeProvider.GetUtcNow()
  U->>R: FindOpenAsync(employeeId)
  alt ya hay una abierta
    U-->>H: OpenTimeEntryAlreadyExistsException
    H-->>W: 409 ProblemDetails
  else no hay
    U->>R: AddAsync(TimeEntry.Start(employeeId, now))
    R->>DB: INSERT (parametrizado)
    alt carrera: índice único parcial → 23505
      R-->>H: OpenTimeEntryAlreadyExistsException
      H-->>W: 409
    else ok
      E-->>W: 201 TimeEntryResponse (clockInAt …Z)
    end
  end
```

```mermaid
sequenceDiagram
  participant Op as Operador / compose
  participant P as Program.cs
  participant H as Host
  participant V as Validadores (Cors, ConnectionStrings, Identity)
  participant M as DatabaseMigrationHostedService
  participant K as Kestrel
  Op->>P: arranque (env ASPNETCORE_ENVIRONMENT, ConnectionStrings__Default, Identity__…, Database__…)
  P->>H: Build(); RunAsync()
  H->>V: ValidateOnStart (todas las opciones)
  alt alguna inválida (p. ej. entorno ≠ Development)
    V-->>Op: OptionsValidationException / AggregateException; nombra la clave, nunca el valor; código ≠ 0
  else válidas
    H->>M: StartingAsync
    opt Database:ApplyMigrationsOnStartup = true
      M->>M: MigrateDatabaseAsync() (fallo → el proceso no arranca)
    end
    H->>K: escucha; /health y /api/time-entries disponibles
  end
```

`GET /api/time-entries`: `ListLatestAsync(employeeId, 50)` → `CalculateWorked()` por entrada → total de cerradas → 200.

## 6. Esquema de base de datos y migración

```sql
CREATE TABLE time_entries (
  id            uuid        PRIMARY KEY,
  employee_id   uuid        NOT NULL,
  clock_in_at   timestamptz NOT NULL,
  clock_out_at  timestamptz NULL,
  CONSTRAINT ck_time_entries_clock_out_after_clock_in CHECK (clock_out_at IS NULL OR clock_out_at >= clock_in_at)
);
CREATE INDEX ix_time_entries_employee_id_clock_in_at ON time_entries (employee_id, clock_in_at DESC);
CREATE UNIQUE INDEX ux_time_entries_employee_id_open ON time_entries (employee_id) WHERE clock_out_at IS NULL;
```

- Migración `InitialTimeEntries` en `Infrastructure/Persistence/Migrations`, creada con `dotnet tool run dotnet-ef migrations add InitialTimeEntries --project src/TimeTracker.Infrastructure --startup-project src/TimeTracker.Infrastructure --output-dir Persistence/Migrations` gracias a `DesignTimeDbContextFactory` (lee `ConnectionStrings__Default` o usa `Host=localhost;Database=timetracker_design`, sin contraseña; no conecta) (V5).
- La restricción `CHECK` es defensa en profundidad; la regla vive en Domain.
- Las pruebas usan `MigrateAsync()`, nunca `EnsureCreated`; `HasPendingModelChanges()` debe ser `false`.
- `EnableSensitiveDataLogging` desactivado (por defecto): los parámetros SQL no se registran.

## 7. Configuración y validación

| Clave | Dónde se valida | Regla y mensaje (nombra la clave, nunca el valor) |
|---|---|---|
| `ConnectionStrings:Default` | `ConnectionStringOptionsValidator` + `ValidateOnStart` | Obligatoria y no en blanco; se analiza con `NpgsqlConnectionStringBuilder` dentro de `try/catch` sin propagar el mensaje original: "ConnectionStrings:Default: debe definirse…" / "…: el formato no es válido." |
| `Identity:DevelopmentEmployeeId` | `ProvisionalIdentityOptionsValidator(IHostEnvironment)` + `ValidateOnStart`, **registrado siempre** | Entorno ≠ Development → "Identity: la identidad provisional solo está permitida en el entorno Development; configure la autenticación real." GUID vacío o ausente → "Identity:DevelopmentEmployeeId: debe ser un GUID distinto de cero." |
| `Database:ApplyMigrationsOnStartup` | `DatabaseOptions` (bool, por defecto `false`) | Valor no booleano → fallo de enlace al arrancar. |
| `Cors:AllowedOrigins` | Sin cambios | — |

`ICurrentUser → DevelopmentCurrentUser` solo se registra en Development; fuera, el validador impide el arranque antes de atender peticiones. `appsettings*.json` no reciben ninguno de estos valores; en local se definen como variables de entorno (README) y en compose vía `.env`.

## 8. Manejo de errores

- `AddProblemDetails` + `AddExceptionHandler<DomainRuleExceptionHandler>()`; `app.UseExceptionHandler()` es el primer middleware, antes de `UseCors()`.
- El manejador trata `DomainRuleException` → 409 con `Title`/`Message`; devuelve `false` para el resto, y el middleware escribe el 500 genérico (sin traza ni mensaje de excepción; también en Development, porque el manejador explícito intercepta antes que la página de excepciones del desarrollador).
- Registro: error inesperado con `LoggerMessage` (V14) y la excepción solo en el servidor; los conflictos, a nivel `Information` sin `EmployeeId`.
- Los mensajes de error de configuración siguen ADR 0007.

## 9. Web

- `Program.cs`: lee `ApiBaseUrl`; si falta o no es URI absoluta `http/https`, lanza `InvalidOperationException` en español (fallo rápido). Registra `TimeEntriesApiClient` con `AddScoped`. La línea de la plantilla con `HttpClient` se conserva para las páginas de ejemplo.
- `TimeEntriesApiClient`: `GetAsync()`, `ClockInAsync()`, `ClockOutAsync()` con `System.Net.Http.Json`; ante 409 lee el ProblemDetails y lanza `TimeEntriesApiException(detail)`; ante otro error, mensaje genérico.
- `Pages/TimeEntries.razor` (`@page "/fichajes"`): tabla Entrada/Salida/Minutos ("en curso" si `null`), total en minutos y h:mm, botones "Fichar entrada" y "Fichar salida" siempre activos salvo durante una petición (el servidor decide; sin reglas en cliente), mensaje de error visible. Horas como `yyyy-MM-dd HH:mm 'UTC'` con `CultureInfo.InvariantCulture`.
- DTO duplicados en Web (sin `ProjectReference`). Verificación manual registrada en `verify-report.md` (sin bUnit).
- Para el flujo local: `Cors__AllowedOrigins__0=http://localhost:5172`.

## 10. Compose, Docker y CI

`docker-compose.yml`:
- `postgres`: `image: postgres:18-alpine`; `environment` con `POSTGRES_DB/USER/PASSWORD: ${…:?…}` interpolados de `.env` (no `env_file`, para no inyectar claves de la API); volumen `pgdata:/var/lib/postgresql` (V16; con 17-alpine sería `/var/lib/postgresql/data`); `ports: "127.0.0.1:5432:5432"`; `healthcheck: ["CMD-SHELL", "pg_isready -U $${POSTGRES_USER} -d $${POSTGRES_DB}"]`.
- `api`: `depends_on: postgres: condition: service_healthy`; `env_file: .env`; `environment` (prevalece sobre `env_file`): `ASPNETCORE_ENVIRONMENT=Development`, `Database__ApplyMigrationsOnStartup=true`, `ConnectionStrings__Default=Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}`; puerto `127.0.0.1:8080:8080` (con identidad provisional no debe quedar expuesto a la red local).

`.env.example` añade, vacías o ficticias: `POSTGRES_DB=timetracker`, `POSTGRES_USER=timetracker`, `POSTGRES_PASSWORD=`, `ConnectionStrings__Default=` (comentario con el formato y `Host=localhost` para ejecución local), `Identity__DevelopmentEmployeeId=` (comentario: GUID inventado), `Database__ApplyMigrationsOnStartup=false`.

`Dockerfile` sin cambios (V15: Npgsql con globalización invariante de Alpine).

`ci.yml`: nuevo paso tras el umbral de Domain: `./scripts/check-coverage.ps1 -ResultsDirectory TestResults -Package TimeTracker.Application -MinimumPercent 80`. Las pruebas con Testcontainers usan el Docker de `ubuntu-latest` (sin ejecutar hasta que haya remoto, W1). No hace falta `dotnet-ef` en CI.

## 11. Paquetes (`Directory.Packages.props`)

| Paquete | Versión | Proyecto |
|---|---|---|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 | Infrastructure |
| `Microsoft.EntityFrameworkCore.Relational` | 10.0.12 | Infrastructure (fijado de la familia) |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.12 (`PrivateAssets=all`) | Infrastructure |
| `Testcontainers.PostgreSql` | 4.16.0 | Api.IntegrationTests |
| `Microsoft.Extensions.TimeProvider.Testing` | 10.10.0 | Application.Tests, Api.IntegrationTests |

Herramienta local: `dotnet-ef` 10.0.12 en `.config/dotnet-tools.json` (`dotnet tool restore`). Se conserva `Microsoft.OpenApi` 2.7.6 (S3).

## 12. ADR

| Archivo | Decisión | Alternativas | Consecuencias |
|---|---|---|---|
| `0008-persistencia-postgresql-ef-core.md` | PostgreSQL 18 + EF Core 10 (Npgsql), familia EF fijada en 10.0.12, mapeo Fluent, índice único parcial, migraciones versionadas aplicadas por bandera, Testcontainers con `MigrateAsync` | `EnsureCreated`; SQLite en pruebas; migrar siempre al arrancar | Las pruebas de integración requieren Docker; despliegue decide si migra al arrancar |
| `0009-identidad-provisional.md` | `ICurrentUser` + `DevelopmentCurrentUser` solo en Development; fallo de arranque en otro entorno; `GetDocument` forzado a Development con valores ficticios (amplía W4 de ADR 0004 sin reescribirlo) | Cabecera/parámetro; JWT ahora; esquema de autenticación Development | Compose solo vale para desarrollo; `autenticacion-y-roles` sustituye el adaptador sin tocar Application |
| `0010-tiempo-utc-y-minutos.md` | `TimeProvider`, UTC validado, instantes truncados a segundos, minutos truncados, JSON con `Z`, UI en UTC etiquetado | `IClock` propio; hora local; redondeo | Sin `tzdata` en la imagen; la zona local llega con saldos |

## 13. Estrategia de pruebas (TDD) y trazabilidad

**Orden TDD de `TimeEntry`** (cada paso rojo → verde → refactor; las pruebas que pasen a la primera se justifican con mutación temporal, como en el cambio archivado):
1. `Start_WithUtcInstant_CreatesOpenEntry` · 2. `Start_WithNonUtcOffset_Throws` · 3. `Start_WithEmptyEmployeeId_Throws` · 4. `Start_TruncatesToWholeSeconds` · 5. `Close_AtLaterInstant_ClosesEntry` · 6. `Close_AtSameInstant_IsValid` · 7. `Close_BeforeClockIn_Throws` · 8. `Close_WhenAlreadyClosed_Throws` · 9. `Close_WithNonUtcOffset_Throws` · 10. `CalculateWorked_WhenOpen_ReturnsNull` · 11. `CalculateWorked_TruncatesPartialMinutes` (90 min 59 s → 90) · 12. `CalculateWorked_AcrossMidnight` (23:30 → 00:45 = 75) · 13. `CalculateWorked_UnderOneMinute_ReturnsZero`.

**Application** (fakes en memoria + `FakeTimeProvider`): entrada sin abierta crea a `now`; entrada con abierta → excepción; salida cierra a `now`; salida sin abierta → excepción; la abierta de **otro** empleado no cuenta ni se cierra; listado del usuario actual en orden descendente, límite 50 pasado al repositorio, `workedMinutes` nulo para abierta, total solo de cerradas, lista vacía → total 0.

**Integración (Testcontainers)**: `PostgresFixture : IAsyncLifetime` (~35 líneas) con `new PostgreSqlBuilder("postgres:18-alpine").Build()`, `MigrateAsync` una vez, `[CollectionDefinition("Postgres")]`; un `EmployeeId` nuevo por prueba, sin limpieza. Las bases para la prueba de la bandera se crean con nombre único en el mismo contenedor.

| Área | Pruebas |
|---|---|
| Repositorio | ida y vuelta conserva UTC; orden y límite; filtro por empleado; segunda abierta insertada directamente → `OpenTimeEntryAlreadyExistsException` (camino `23505` determinista); abiertas de empleados distintos permitidas |
| Migraciones | `HasPendingModelChanges() == false`; tras migrar existen `ux_time_entries_employee_id_open` (consulta a `pg_indexes`) y la `CHECK` |
| API E2E | 201 con cuerpo `…Z`; doble entrada → 409 `application/problem+json` en español; salida sin entrada → 409; entrada + salida con `FakeTimeProvider` (90 min 30 s → 90); 51 ciclos → 50 elementos y total correcto |
| Aislamiento | dos fábricas con distinto `Identity:DevelopmentEmployeeId` sobre la misma BD: B no ve los fichajes de A ni puede cerrar su entrada (409); `?employeeId=` y cabecera `X-Employee-Id` ignorados |
| Carrera | dos `POST clock-in` simultáneos (`Task.WhenAll`) → exactamente un 201 y un 409; la BD contiene una sola abierta |
| Errores | repositorio falso que lanza con un texto "sensible" → 500 sin ese texto ni traza |
| Arranque | sin `ConnectionStrings:Default` y con formato inválido (contraseña ficticia no aparece en el mensaje); entorno `Production` → falla nombrando `Identity`; Development con GUID vacío → falla; bandera `true` sobre BD vacía crea el esquema; bandera ausente no migra |
| Contrato | `OpenApiContractTests`: los tres endpoints con 201/200/409 y `date-time` |
| Arquitectura | DD-18 |

`ApiFactory` aporta valores base válidos (origen, cadena ficticia `Host=db.invalid…`, GUID inventado) y admite sobrescribirlos; `StartupConfigurationTests` y las nuevas pruebas de arranque parten de esos valores y cambian solo la clave bajo prueba, y su búsqueda de `OptionsValidationException` recorre también `AggregateException.InnerExceptions` (V9). El limpiador estático de variables (R7) se amplía a `ConnectionStrings__`, `Identity__` y `Database__`.

**Cobertura**: Domain 100 % de líneas y ramas; Application ≥ 80 % (nuevo paso de CI); Infrastructure, Api y migraciones sin umbral. El script toma el mejor valor entre informes: apply comprueba que `Application.Tests` alcanza el 80 % por sí solo (V17).

Trazabilidad por capacidad (los identificadores definitivos los fija la fase de specs): `time-tracking` → pruebas de Domain y Application; `time-entries-persistence` → repositorio, migraciones, carrera; `time-entries-ui` → verificación manual; `startup-configuration` → arranque; `solution-architecture` → arquitectura, compose (manual: `docker compose config --services` = `api`, `postgres`; `docker compose up` sano), CI (revisión).

## 14. Matriz de amenazas (OWASP Top 10 web y API)

| Amenaza | Aplica | Respuesta de diseño | Prueba RED / verificación |
|---|---|---|---|
| Identidad provisional desplegada (API2, A07) | Sí | Validador siempre registrado; fuera de Development no arranca; compose solo local en `127.0.0.1` | `Startup_InProduction_FailsNamingIdentity` |
| Suplantación por parámetro/cabecera (API1, A01) | Sí | Ningún endpoint acepta identidad; `employeeId` sale de `ICurrentUser` | `Request_WithEmployeeIdQueryOrHeader_IsIgnored` |
| Acceso a objetos ajenos, BOLA (API1) | Sí | Toda consulta filtra por `EmployeeId`; sin acceso por id | Pruebas de aislamiento (lectura y cierre) |
| Inyección SQL (A03) | Sí (mitigada) | Solo LINQ/EF parametrizado; sin SQL crudo en producción; sin entrada del cliente | Revisión en verify: ausencia de `FromSqlRaw`/`ExecuteSqlRaw` en `src/` |
| Exposición de errores (A05, API8) | Sí | 500 genérico; mensajes de configuración sin valores; sin `SensitiveDataLogging` | `UnhandledException_Returns500WithoutDetails`; arranque con contraseña ficticia |
| DoS por listado (API4) | Sí | Límite fijo 50, no controlable por el cliente; índice `(employee_id, clock_in_at DESC)` | `Get_With51Entries_Returns50` |
| Integridad por concurrencia | Sí (parcial) | Entrada: índice único parcial. Salida: DD-9 diferida; **riesgo residual aceptado** (doble `clock-out` simultáneo, última escritura gana) | Prueba de carrera de entrada |
| Secretos (A02) | Sí | Contraseña solo en `.env`; puerto de PostgreSQL en `127.0.0.1` | Manual: `docker history`/`inspect` sin `POSTGRES_PASSWORD` ni `ConnectionStrings` |
| Asignación masiva (API3) | N/A | No hay cuerpos de petición | — |
| Límite de tasa (AGENTS §6) | Diferido | Riesgo residual aceptado; entra con despliegue/autenticación | — |

Matriz de procesos de la skill (rutas tipo documentación, repositorio Git, commit, push, PR): **N/A**: no se ejecutan comandos Git ni de PR; Testcontainers habla con la API de Docker con argumentos fijos.

## 15. Conflictos esperables con las especificaciones vigentes

| Spec vigente | Requisito / escenario | Conflicto | Delta necesario |
|---|---|---|---|
| solution-architecture | Docker Compose con solo la API | Ahora `api` + `postgres` | MODIFIED (ya previsto) |
| solution-architecture | Puerta de calidad local: "Web DEBE compilar con la plantilla `blazorwasm` sin modificar" | Web añade página, cliente y `appsettings.json` | MODIFIED |
| solution-architecture | ADR y README: "0001 a 0007", "siete ADR" | Pasan a ser diez | MODIFIED |
| solution-architecture | Imagen Docker, escenario "Salud dentro del contenedor" (solo `Cors__AllowedOrigins__0`) | Sin `ASPNETCORE_ENVIRONMENT=Development`, `ConnectionStrings__Default` e `Identity__…` el contenedor **no arranca** | MODIFIED del escenario (sin BD real: la conexión es perezosa) |
| solution-architecture | Flujo de CI | Paso nuevo de cobertura de Application (no rompe el orden) | MODIFIED recomendado |
| solution-architecture | `Purpose` excluye PostgreSQL/EF y la pantalla de marcajes | Texto obsoleto | Nota en el delta |
| startup-configuration | Puerto configurable: `dotnet run --no-launch-profile` | Arranca en Production → **falla por identidad** | MODIFIED: definir `ASPNETCORE_ENVIRONMENT=Development` y las nuevas claves |
| startup-configuration | Opciones tipadas / mensajes sin secretos | Ampliación a `ConnectionStrings:Default` e `Identity` | ADDED |
| startup-configuration | `.env.example` | Claves nuevas | Sin cambio de texto (el requisito ya exige todas las consumidas) |
| (nuevas) time-tracking / persistence | — | DD-5 (truncado a segundos) y el total sobre las 50 devueltas pueden no figurar; DD-9 (`xmin`) queda diferida y fuera de las specs | Alinear en specs (hecho) o aceptar como detalle de diseño |

## 16. Tandas, previsión y validaciones

| Tanda | Contenido | Líneas a mano | Punto de control |
|---|---|---|---|
| 1 | V1–V3 (paquetes, `docker pull`, Ryuk); Domain + Application con TDD; DD-18 | 450–540 | build, test, cobertura 100 / 80 |
| 2 | Infrastructure, migración, `.editorconfig`, herramienta, fixture y pruebas de repositorio/migraciones | 350–420 | repositorio y `HasPendingModelChanges` en verde |
| 3 | Api (identidad, opciones, errores, endpoints, migraciones), W4, pruebas E2E/arranque/contrato, compose, `.env.example`, CI | 385–470 | E2E en verde; `docker compose up` sano |
| 4 | Web, ADR 0008–0010, README, `config.yaml` | 280–360 | flujo manual registrado |
| **Total** | | **≈ 1.465–1.790** (sin migraciones generadas ni `openapi.json`; recorte 2026-10-12: −65…−90 líneas) | |

- Decision needed before apply: No (`size:exception` aceptado, Q11)
- Chained PRs recommended: No (sin repositorio; las tandas son los puntos de control)
- 400-line budget risk: High

Orden de recorte: pulido Web → ADR/README → compose; DD-9 (`xmin`) ya está diferida a seguimiento (recorte acordado 2026-10-12, junto con las pruebas dedicadas de CORS). Backend y pruebas restantes no se recortan.

| ID | Validación en apply | Respaldo |
|---|---|---|
| V1 | Versiones (§11) con `dotnet package search`; restore con `nuget.config`; NuGetAudit sin NU190x con `-warnaserror` | Fijar la versión corregida transitiva, como `Microsoft.OpenApi` |
| V2 | `docker pull postgres:18-alpine` | `17-alpine` (volumen `/var/lib/postgresql/data`); si falla todo, parar y consultar |
| V3 | Descarga de `testcontainers/ryuk` | `TESTCONTAINERS_RYUK_DISABLED=true` y `DisposeAsync` del fixture |
| V4 | `dotnet list package --include-transitive`: toda la familia `Microsoft.EntityFrameworkCore*` en 10.0.12; sin NU1605/MSB3277; `Microsoft.Extensions.*` 10.0.12 compatibles con el runtime instalado (`dotnet --list-runtimes`) | Referencias directas adicionales o fijado transitivo |
| V5 | `dotnet-ef` con Infrastructure como proyecto de inicio; migración generada sin avisos con `-warnaserror` ni cambios en `dotnet format --verify-no-changes` | `--startup-project src/TimeTracker.Api` (+ Design en Api); ajuste `.editorconfig` justificado |
| V6 | EF materializa `TimeEntry` con el constructor privado y propiedades de solo lectura; Domain llega al 100 % sin `ExcludeFromCodeCoverage` | Campos de respaldo explícitos |
| V7 | `TimeProvider` y `Guid.CreateVersion7` se resuelven a `System.Runtime`: pasan las pruebas de marco base de Domain/Application | — |
| V8 | Entorno con el que `GetDocument.Insider` arranca; con DD-16 `openapi.json` incluye los tres endpoints | Exportación con `WebApplicationFactory` (seguimiento W4 del archivo) |
| V9 | Varias validaciones fallidas → `AggregateException`; las pruebas lo recorren | — |
| V10 | `StartingAsync` corre tras `ValidateOnStart` y antes de que Kestrel escuche; `WebApplicationFactory` lo ejecuta | Migrar en `StartAsync` y documentarlo |
| V11 | El 409 conserva las cabeceras CORS tras el manejador de excepciones: **solo verificación manual de UI** (tarea 14.6, UI-13); sin prueba automática dedicada (recorte 2026-10-12) | Reordenar middleware |
| V12 | **Retirada** (DD-9 diferida a seguimiento): no hay `xmin` que validar | — |
| V13 | `DateTime` UTC se serializa con `Z`; OpenAPI `format: date-time` | Convertidor propio |
| V14 | Reglas CA que aparezcan (CA1032 constructores de excepción, CA1848 logging, CA1515) | Cumplirlas con prueba; nunca desactivar `TreatWarningsAsErrors` |
| V15 | Npgsql en `aspnet:10.0-alpine` (globalización invariante) con `docker compose up` | Imagen Debian |
| V16 | Ruta del volumen de PostgreSQL 18 (`/var/lib/postgresql`) | Documentación oficial de la imagen |
| V17 | `Application.Tests` alcanza el 80 % por sí solo | Más pruebas unitarias |
| V18 | Puerto 5432 libre en la máquina | Cambiar el puerto publicado en `.env` |
| V19 | `UseSetting` de las pruebas prevalece sobre variables de entorno de la máquina (R7 ampliado) | — |

## 17. Riesgos y dudas abiertas

- **Riesgos**: calendario (≈ 1.450–1.800 líneas); Docker Hub (V2/V3); familia EF (V4); migración vs `-warnaserror`/`format` (V5); W4 más frágil (V8); la CI sigue sin ejecutarse y ahora depende de Docker (W1); entrada olvidada abierta bloquea volver a fichar (aceptado); sin límite de tasa (residual).
- **Dudas para specs o usuario**:
  1. ¿`totalWorkedMinutes` es la suma de las 50 devueltas (elegido) o de todo el histórico?
  2. ¿Se acepta truncar los instantes a segundos (DD-5)?
  3. ~~¿Entra el control de concurrencia de la salida (DD-9)?~~ Resuelto el 2026-10-12: pasa a seguimiento (riesgo residual aceptado, ver DD-9).
  4. ¿201 sin `Location` (DD-15)?
  5. ¿Se publica la API de compose solo en `127.0.0.1` (recomendado)?
