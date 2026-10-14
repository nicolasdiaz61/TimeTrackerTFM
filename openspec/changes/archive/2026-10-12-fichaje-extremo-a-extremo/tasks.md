# Tareas: fichaje-extremo-a-extremo

Fecha: 2026-10-11 · Fase: sdd-tasks · Modo: openspec · Base: `proposal.md`, `exploration.md` (D1–D16), 5 especificaciones (124 escenarios tras el recorte del 2026-10-12), `design.md` (DD-1–DD-18, V1–V19; DD-9 y V12 diferidas), `openspec/config.yaml`, `AGENTS.md` y las lecciones del cambio archivado `esqueleto-solucion` (W1–W8, S1–S7).

Nota de formato: el límite de palabras habitual de este artefacto se supera de forma deliberada, como en el cambio archivado. El usuario pidió trazabilidad de 124 escenarios, pasos TDD de `TimeEntry`, validaciones V1–V19, plan de reversión sin Git y orden de recorte.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ≈ 2.380–3.060 escritas a mano, sin migraciones generadas ni `openapi.json` (el diseño preveía 1.465–1.790 tras el recorte; ver "Desviaciones del diseño" D11) |
| 400-line budget risk | High |
| Chained PRs recommended | No |
| Suggested split | Entrega única en 4 tandas con punto de control (sin Git, sin remoto) |
| Delivery strategy | exception-ok |
| Chain strategy | size-exception |

Decision needed before apply: No (`size:exception` aceptado por el usuario, Q11)
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 (Tanda 1) | Domain `TimeEntry` + Application (puertos, 3 casos de uso) con TDD; refuerzo de la prueba de arquitectura | Entrega única | `dotnet test tests/TimeTracker.Domain.Tests` y `dotnet test tests/TimeTracker.Application.Tests` | `docker pull postgres:18-alpine` (tarea 1.2); sin proceso en ejecución: solo bibliotecas | Borrar `src/TimeTracker.Domain/TimeTracking`, `DomainRuleException.cs`, `src/TimeTracker.Application/{Identity,TimeTracking}`, pruebas nuevas; restaurar `..\TimeTracker.rollback\tanda-1` |
| 2 (Tanda 2) | Infrastructure (EF Core, migración inicial, repositorio) + Testcontainers | Misma entrega | `dotnet test tests/TimeTracker.Api.IntegrationTests --filter "FullyQualifiedName~Persistence"` | PostgreSQL 18 real vía Testcontainers (`postgres:18-alpine`) | Borrar `Infrastructure/Persistence`, `.config/dotnet-tools.json`, `Api.IntegrationTests/{Postgres,Persistence}`; restaurar `..\TimeTracker.rollback\tanda-2` |
| 3 (Tanda 3) | Api: identidad, opciones, errores, endpoints, migración por bandera, contrato, compose, CI | Misma entrega | `dotnet test tests/TimeTracker.Api.IntegrationTests` | `docker compose up -d --wait` y `POST /api/time-entries/clock-in` (tarea 12.4) | Borrar `Api/{Identity,Errors,TimeEntries,Persistence}` y pruebas nuevas; `docker compose down -v`; restaurar `..\TimeTracker.rollback\tanda-3` |
| 4 (Tanda 4) | Web `/fichajes`, ADR 0008–0010, README, `config.yaml` | Misma entrega | `dotnet test tests/TimeTracker.ArchitectureTests` y `dotnet build TimeTracker.sln -warnaserror` | Navegador: flujo manual en `/fichajes` (tarea 14.6) | Borrar `Web/TimeEntries`, `Pages/TimeEntries.razor`, `docs/adr/{0008,0009,0010}*`; restaurar `..\TimeTracker.rollback\tanda-4` |

## Convenciones de ejecución

- **Sin Git:** no se ejecuta ningún comando `git` (ni siquiera `git --version`; en el cambio archivado hubo un descuido). Todo se ejecuta desde la raíz del repositorio con PowerShell. Restauración NuGet con el `nuget.config` del repositorio (solo nuget.org).
- **Strict TDD:** cada tarea de código sigue RED (prueba que falla; se anota el mensaje real) → GREEN (código mínimo) → REFACTOR (suite en verde). Una prueba que pasa a la primera se marca "verde a la primera" con justificación y **mutación temporal** (se rompe el código, la prueba falla, se revierte).
- **Datos inventados:** `https://app.example.test`, contraseña ficticia `secreto-prueba` (solo en pruebas) y GUID inventados (`[guid]::NewGuid()`). Identificadores de código en inglés; documentación y textos de usuario en español.
- **Entorno limpio antes de probar o ejecutar:** `Get-ChildItem Env: | Where-Object Name -Match '^(Cors__|ConnectionStrings__|Identity__|Database__|ASPNETCORE_|DOTNET_ENVIRONMENT)'` no debe devolver nada (R7, V19). Docker Desktop en marcha desde la tanda 2.
- **Marcas de recorte:** `[R1]` pulido Web, `[R2]` ADR/README, `[R3]` compose. Sin marca = intocable (backend y pruebas). Orden acordado: R1 → R2 → R3.
- **Alias de comandos** (de `openspec/config.yaml`):

| Alias | Comando |
|---|---|
| `BUILD` | `dotnet build TimeTracker.sln -c Release --no-restore -warnaserror` |
| `TEST` | `dotnet test TimeTracker.sln -c Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults` |
| `FORMAT` | `dotnet format TimeTracker.sln --verify-no-changes --no-restore` |
| `COV-D` | `pwsh scripts/check-coverage.ps1 -ResultsDirectory TestResults -Package TimeTracker.Domain -MinimumPercent 100` |
| `COV-A` | `pwsh scripts/check-coverage.ps1 -ResultsDirectory TestResults -Package TimeTracker.Application -MinimumPercent 80` |
| `T-D`, `T-A`, `T-I`, `T-AR` | `dotnet test tests/TimeTracker.{Domain.Tests,Application.Tests,Api.IntegrationTests,ArchitectureTests}` (con `--filter "FullyQualifiedName~Clase"` para la prueba concreta; compila en Debug) |

## Recortes acordados (2026-10-12)

El usuario aprobó recortar el alcance antes de implementar (opción A). Efecto en los tres documentos (specs, `design.md` y este archivo):

- **Recorte 1 · concurrencia de la salida (DD-9) diferida a seguimiento.** Se elimina `xmin`: sin token de concurrencia ni columna de concurrencia; `UpdateAsync` es una salida simple. Se retiran el requisito «Control de concurrencia de la salida» de `time-entries-persistence` con PE-8, PE-9 y PE-10, la frase de dos salidas simultáneas en `time-tracking`, la tarea 7.8, la validación V12 y la prueba de `clock-out` simultáneo de 11.3. **Riesgo residual aceptado:** dos `clock-out` simultáneos del mismo fichaje pueden dar dos `200` y la última escritura fija `clock_out_at`; la `CHECK` y las reglas de Domain se siguen cumpliendo. La entrada sigue protegida por el índice único parcial (PE-5, PE-7, PE-20, PE-21).
- **Recorte 2 · CORS.** Se retiran los escenarios «CORS conservado en el 409» (TT-46) y «Preflight de POST» (SC-18), y también SC-15–SC-17: el requisito CORS de `startup-configuration` ya no se modifica. No hace falta el cambio D3: la política vigente usa `AllowAnyMethod()`, así que la Web ya puede hacer `GET` y `POST`; `CorsServiceCollectionExtensions.cs` no se toca. Se retiran las tareas 9.7 y 10.11. V11 pasa a verificación manual de UI (14.6, UI-13), sin prueba automática dedicada.
- **Numeración:** los identificadores y números de tarea se conservan (no se renumeran); los retirados figuran como «retirada» y no cuentan.
- **Recuentos reales (contados en los archivos):** 5 especificaciones, 40 requisitos y 124 escenarios (antes 42 y 132): time-tracking 11/48, time-entries-persistence 9/21, time-entries-ui 5/13, solution-architecture 10/27, startup-configuration 5/15. Tareas: 91 (antes 94). Previsión: ≈ 2.380–3.060 líneas (antes ≈ 2.490–3.210).

## Plan de reversión (copia previa de archivos modificados)

Al empezar cada tanda se copian los archivos **existentes** que se van a modificar a `..\TimeTracker.rollback\tanda-N\` conservando la ruta relativa (carpeta hermana, fuera del repositorio para no alterar el globbing de MSBuild ni `dotnet format`). Función auxiliar (se pega en la sesión de PowerShell):

```powershell
function Save-Rollback([int]$Tanda, [string[]]$Files) {
  foreach ($f in $Files) { if (Test-Path $f) {
    $t = Join-Path "..\TimeTracker.rollback\tanda-$Tanda" $f
    New-Item -ItemType Directory -Force (Split-Path $t) | Out-Null; Copy-Item $f $t } }
}
```

| Tanda | Existentes que se copian | Nuevos (se borran al revertir) |
|---|---|---|
| 1 | `Directory.Packages.props`, `tests/TimeTracker.Application.Tests/TimeTracker.Application.Tests.csproj`, `tests/TimeTracker.ArchitectureTests/{DependencyRuleTests,RepositoryConventionsTests}.cs` | `Domain/{DomainRuleException.cs,TimeTracking/*}`, `Application/{Identity,TimeTracking}/*`, `Domain.Tests/TimeEntryTests.cs`, `Application.Tests/{*UseCaseTests.cs,Fakes/*}` |
| 2 | `Directory.Packages.props`, `.editorconfig`, `src/TimeTracker.Infrastructure/TimeTracker.Infrastructure.csproj`, `tests/TimeTracker.Api.IntegrationTests/TimeTracker.Api.IntegrationTests.csproj`, `tests/TimeTracker.ArchitectureTests/RepositoryConventionsTests.cs` | `.config/dotnet-tools.json`, `Infrastructure/Persistence/*` (incl. `Migrations`), `Api.IntegrationTests/{Postgres,Persistence}/*` |
| 3 | `src/TimeTracker.Api/{Program.cs,TimeTracker.Api.csproj}`, `tests/TimeTracker.Api.IntegrationTests/{ApiFactory,StartupConfigurationTests,CorsPolicyTests}.cs`, `tests/TimeTracker.ArchitectureTests/{OpenApiContractTests,RepositoryConventionsTests}.cs`, `docs/api/openapi.json`, `docker-compose.yml`, `.env.example`, `.github/workflows/ci.yml` | `Api/{Configuration,Identity,Errors,Persistence,TimeEntries}/*` nuevos, `Infrastructure/Persistence/{ConnectionStringOptions*,PersistenceServiceCollectionExtensions}.cs`, pruebas nuevas de `Api.IntegrationTests`, `ArchitectureTests/DeliveryArtifactsTests.cs` |
| 4 | `src/TimeTracker.Web/{Program.cs,Layout/NavMenu.razor,TimeTracker.Web.csproj}`, `README.md`, `openspec/config.yaml`, `tests/TimeTracker.ArchitectureTests/{RepositoryConventionsTests,DeliveryArtifactsTests}.cs` | `Web/{TimeEntries/*,Pages/TimeEntries.razor,wwwroot/appsettings.json}`, `docs/adr/{0008,0009,0010}-*.md`, sección de verificación manual de UI en `verify-report.md` |

Revertir una tanda: restaurar sus copias (`Copy-Item -Recurse -Force ..\TimeTracker.rollback\tanda-N\* .`), borrar los nuevos, `docker compose down -v` y `docker image rm postgres:18-alpine` si procede. No hay datos reales que perder. La carpeta `..\TimeTracker.rollback` se elimina al archivar.

## Orden de recorte (si falta tiempo)

| Marca | Qué se recorta (en este orden) | Escenarios que dejarían de cumplirse | Consecuencia |
|---|---|---|---|
| R1 | Tarea 14.5 (pulido Web: enlace en menú, botones deshabilitados durante la petición, estilos) | Ninguno obligatorio (son DEBERÍA) | Anotar en verify como aviso |
| R2 | Fases 15 y 16 salvo `config.yaml` (16.2): ADR 0008–0010 y README | SA-23 (parcial), SA-24, SA-25, PE-24 | Delta o aviso en verify; AGENTS §8 (documentación obsoleta es un defecto) |
| R3 | Tarea 12.2 y la parte `compose up` de 12.4 | SA-1–SA-5, PE-22 (parte compose), V15, V16, V18; criterio de éxito "`docker compose up` responde" | Delta de `solution-architecture` (compose) o aviso; `.env.example` y las pruebas de backend NO se recortan |

Intocables: Domain, Application, Infrastructure, Api, todas sus pruebas, CI y `.env.example`. DD-9 (`xmin`, antigua tarea 7.8) ya está diferida a seguimiento por recorte acordado (ver "Recortes acordados"); no queda nada más recortable en backend.

---

# TANDA 1: Domain y Application (TDD)

## Fase 1: Preparación

- [x] 1.1 **Copia previa y línea base.** Archivos: `..\TimeTracker.rollback\tanda-1\` (4 existentes, ver plan). Cubre: precondición del plan de reversión.
  - Pegar `Save-Rollback`; copiar los 4 archivos de la tanda 1; ejecutar `dotnet --list-sdks` y la línea base con `BUILD` y `TEST` (49 pruebas del esqueleto: Domain 10, Arquitectura 25, Integración 14).
  - Hecho cuando: `(Get-ChildItem -Recurse -File ..\TimeTracker.rollback\tanda-1).Count` = 4, `BUILD` y `TEST` terminan en 0.
  - Evidencia: copia previa de 4 archivos en `..\TimeTracker.rollback\tanda-1` (Count = 4). SDK 10.0.302 (también 8.0.425); runtime `Microsoft.NETCore.App` 10.0.10. Línea base: `restore` rc 0, `BUILD` rc 0 con 0 avisos, `TEST` 49 en verde (Domain 10, Arquitectura 25, Integración 14).
- [x] 1.2 **V2 y V3 (acceso a Docker Hub).** Archivos: ninguno. Cubre: precondición de PE-22, PE-23; V2, V3 (parte inicial).
  - `docker pull postgres:18-alpine`. Si falla: `docker pull postgres:17-alpine` (volumen `/var/lib/postgresql/data`; todas las referencias a `18` en specs, ADR y compose pasan a `17`: avisar al usuario porque implica delta). Si fallan ambas: PARAR y consultar.
  - Ryuk: el tag lo fija Testcontainers 4.16.0, así que aquí no se inventa; solo queda comprobado el acceso a Docker Hub. V3 se cierra en 7.1 con el primer arranque real.
  - Hecho cuando: `docker run --rm postgres:18-alpine postgres --version` imprime `postgres (PostgreSQL) 18.x`.
  - Evidencia: `docker pull postgres:18-alpine` OK (digest `sha256:77f585114c32…`); `docker run --rm postgres:18-alpine postgres --version` → `postgres (PostgreSQL) 18.6`. V2 resuelta (no hizo falta 17-alpine, sin delta). V3 queda para 7.1.
- [x] 1.3 **V1 (versiones) y paquete de pruebas de Application.** Archivos: `Directory.Packages.props`. Cubre: V1; DD-6, DD-10.
  - `dotnet package search` para `Npgsql.EntityFrameworkCore.PostgreSQL` (10.0.3), `Microsoft.EntityFrameworkCore.Relational` y `.Design` (10.0.12), `Testcontainers.PostgreSql` (4.16.0), `Microsoft.Extensions.TimeProvider.Testing` (10.10.0) y `dotnet-ef` (10.0.12); anotar resultado. Añadir hoy solo `Microsoft.Extensions.TimeProvider.Testing` a `Directory.Packages.props` (los demás, en 6.1).
  - Hecho cuando: `dotnet restore TimeTracker.sln` rc 0 y `BUILD` con 0 avisos (sin NU190x con `-warnaserror`).
  - Evidencia: `dotnet package search --exact-match`: Npgsql EF 10.0.3, EF Relational/Design 10.0.12, Testcontainers.PostgreSql 4.16.0, TimeProvider.Testing 10.10.0 y dotnet-ef 10.0.12 existen y son las últimas publicadas (V1 confirmada). Solo se añade TimeProvider.Testing a `Directory.Packages.props`; `restore` rc 0 y `BUILD` con 0 avisos (sin NU190x).

## Fase 2: Domain `TimeEntry` (TDD, 13 pasos del diseño §13 + 1 paso añadido)

Archivos comunes: `tests/TimeTracker.Domain.Tests/TimeEntryTests.cs` y `src/TimeTracker.Domain/TimeTracking/TimeEntry.cs`. Foco: `T-D --filter "FullyQualifiedName~TimeEntryTests"`. Cada paso anota RED y GREEN reales en "Evidencia".

- [x] 2.1 **Paso 1 · `Start_WithUtcInstant_CreatesOpenEntry`.** Cubre: TT-2; DD-3, DD-4.
  - RED (compilación): `TimeEntry` no existe. GREEN: constructor **privado con parámetros** (id, employeeId, clockInAt, clockOutAt), `Start` con `Guid.CreateVersion7(at)`, `IsOpen`, `ClockOutAt { get; private set; }`. La prueba comprueba también `Id.Version == 7`.
  - Hecho cuando: el foco pasa 1 prueba.
  - Evidencia: RED `CS0234: el espacio de nombres 'TimeTracking' no existe en 'TimeTracker.Domain'` (compilación). GREEN: foco 1/1 (incluye `Id.Version == 7`).
- [x] 2.2 **Paso 2 · `Start_WithNonUtcOffset_Throws`** (`+02:00`; `Assert.ThrowsAny<ArgumentException>`). Cubre: TT-4.
  - RED: "No exception was thrown". GREEN: `if (at.Offset != TimeSpan.Zero) throw new ArgumentException(…, nameof(at))`.
  - Hecho cuando: el foco pasa 2 pruebas.
  - Evidencia: RED `Assert.ThrowsAny() Failure: No exception was thrown`. GREEN: foco 2/2.
- [x] 2.3 **Paso 3 · `Start_WithEmptyEmployeeId_Throws`.** Cubre: TT-3.
  - RED: no lanza. GREEN: `employeeId == Guid.Empty` → `ArgumentException`.
  - Hecho cuando: el foco pasa 3 pruebas.
  - Evidencia: RED `Assert.ThrowsAny() Failure: No exception was thrown` (`Guid.Empty`). GREEN: foco 3/3.
- [x] 2.4 **Paso 4 · `Start_TruncatesToWholeSeconds`** (`08:00:00.9876543Z` → `08:00:00Z`). Cubre: TT-1; DD-5.
  - RED: `ClockInAt` conserva la fracción. GREEN: `at.AddTicks(-(at.Ticks % TimeSpan.TicksPerSecond))`.
  - Hecho cuando: el foco pasa 4 pruebas.
  - Evidencia: RED `Actual: 2026-10-12T08:00:00.9876543+00:00 / Expected: …08:00:00.0000000+00:00`. GREEN: foco 4/4.
- [x] 2.5 **Paso 5 · `Close_AtLaterInstant_ClosesEntry`** (08:00 → 16:00). Cubre: TT-6.
  - RED (compilación): `Close` no existe. GREEN: `Close(DateTimeOffset at)` fija `ClockOutAt`; `IsOpen` pasa a falso.
  - Hecho cuando: el foco pasa 5 pruebas.
  - Evidencia: RED `CS1061: 'TimeEntry' no contiene una definición para 'Close'`. GREEN: foco 5/5 (6 con 2.6).
- [x] 2.6 **Paso 6 · `Close_AtSameInstant_IsValid`.** Cubre: TT-7.
  - Verde a la primera (aún no hay validación). La mutación que lo pone en rojo se aplica en 2.7 cuando exista la comprobación (`<` → `<=`).
  - Hecho cuando: prueba en verde y mutación registrada en 2.7.
  - Evidencia: verde a la primera (`Close` aún sin validación): foco 6/6. Mutación registrada en 2.7.
- [x] 2.7 **Paso 7 · `Close_BeforeClockIn_Throws`** (07:59:59 sobre 08:00:00; la entrada sigue abierta; se comprueba `Title` y `Message` del diseño §4). Archivos: `src/TimeTracker.Domain/DomainRuleException.cs`, `TimeTracking/ClockOutBeforeClockInException.cs`. Cubre: TT-8; DD-7.
  - RED: no lanza. GREEN: `DomainRuleException` abstracta (`Title`) y `ClockOutBeforeClockInException` («Hora de salida no válida» / «La hora de salida no puede ser anterior a la de entrada.»); `at < ClockInAt` → lanzar. Después, mutación de 2.6: `<` → `<=` hace fallar `Close_AtSameInstant_IsValid`; revertir.
  - Hecho cuando: el foco pasa 7 pruebas y la mutación queda anotada.
  - Evidencia: RED 1 `CS0246: 'ClockOutBeforeClockInException' no se encontró`; RED 2 (con el tipo creado) `Assert.Throws() Failure: No exception was thrown`. GREEN: foco 7/7. Mutación de 2.6 (`<` → `<=`): `Close_AtSameInstant_IsValid` ROJO (1 error, 6 verdes); revertido, foco 7/7.
- [x] 2.8 **Paso 8 · `Close_WhenAlreadyClosed_Throws`** (`ClockOutAt` intacto). Archivos: `TimeTracking/TimeEntryAlreadyClosedException.cs`. Cubre: TT-9.
  - RED: el segundo `Close` sobrescribe. GREEN: `!IsOpen` → `TimeEntryAlreadyClosedException` («El fichaje ya está cerrado» / «El fichaje ya tiene registrada la salida.»), comprobada primero.
  - Hecho cuando: el foco pasa 8 pruebas.
  - Evidencia: RED 1 `CS0246: 'TimeEntryAlreadyClosedException' no se encontró`; RED 2 (tipo creado) `Assert.Throws() Failure: No exception was thrown` (el segundo `Close` sobrescribía). GREEN: foco 8/8.
- [x] 2.9 **Paso 9 · `Close_WithNonUtcOffset_Throws`.** Cubre: TT-10.
  - RED: no lanza. GREEN: misma comprobación UTC que en `Start`.
  - Hecho cuando: el foco pasa 9 pruebas.
  - Evidencia: RED `Assert.ThrowsAny() Failure: No exception was thrown` (18:00+02:00 = 16:00Z cerraba sin error). GREEN: foco 9/9.
- [x] 2.10 **Paso añadido · `Close_TruncatesToWholeSeconds`** (`Close(08:00:00.9Z)` sobre entrada `08:00:00Z` → `08:00:00Z`, sin excepción). Cubre: TT-5; DD-5. (No figura en los 13 pasos del diseño; la spec lo exige.)
  - RED: `ClockOutAt` conserva `.9`. GREEN: truncar en `Close` antes de comparar con `ClockInAt`.
  - Hecho cuando: el foco pasa 10 pruebas.
  - Evidencia: RED `Actual: 2026-10-12T08:00:00.9000000+00:00 / Expected: …08:00:00.0000000+00:00`. GREEN: foco 10/10 (se trunca antes de comparar con `ClockInAt`).
- [x] 2.11 **Paso 10 · `CalculateWorked_WhenOpen_ReturnsNull`.** Cubre: TT-16.
  - RED (compilación): `CalculateWorked` no existe. GREEN mínimo ("fake it"): `public WorkedHours? CalculateWorked() => null;`.
  - Hecho cuando: el foco pasa 11 pruebas.
  - Evidencia: RED `CS1061: 'TimeEntry' no contiene una definición para 'CalculateWorked'`. GREEN ("fake it" `=> null`): foco 11/11.
- [x] 2.12 **Paso 11 · `CalculateWorked_TruncatesPartialMinutes`** (Theory: 08:00:00→16:30:00 = 510; →08:01:59 = 1; →09:30:59 = 90). Cubre: TT-11, TT-12, TT-14.
  - RED: el resultado es nulo. GREEN: `IsOpen ? null : WorkedHours.FromMinutes(checked((int)(ClockOutAt!.Value - ClockInAt).TotalMinutes))`.
  - Hecho cuando: el foco pasa todas las pruebas.
  - Evidencia: RED 3 casos de la Theory `Actual: null`. GREEN: foco 14/14 (510, 1, 90).
- [x] 2.13 **Paso 12 · `CalculateWorked_AcrossMidnight`** (22:00 → 06:00 del día siguiente = 480; 23:30 → 00:45 = 75). Cubre: TT-15.
  - Verde a la primera (instantes absolutos). Mutación: restar `TimeOfDay` → falla; revertir.
  - Hecho cuando: evidencia de mutación y reversión anotada.
  - Evidencia: verde a la primera (2 casos: 480 y 75; instantes absolutos). Mutación `TimeOfDay - TimeOfDay`: 2 ROJOS (`Actual value was -1365` y `-960`, excepción de `FromMinutes`); revertido.
- [x] 2.14 **Paso 13 · `CalculateWorked_UnderOneMinute_ReturnsZero`** (08:00:00 → 08:00:59). Cubre: TT-13.
  - Verde a la primera. Mutación: `Math.Round(...)` → falla (da 1); revertir.
  - Hecho cuando: evidencia anotada y foco en verde.
  - Evidencia: verde a la primera (foco 17/17). Mutación `Math.Round(...)`: 3 ROJOS (`Actual: 1`, `2`, `91`), incluida esta prueba; revertido, foco 17/17.
- [x] 2.15 **Paso REFACTOR y cobertura de Domain.** Archivos: `TimeEntry.cs`, excepciones. Cubre: TT-49 (Domain); V6 (parte cobertura), V7, V14.
  - Extraer `ToUtcSeconds(at, paramName)` compartido por `Start` y `Close`; resolver avisos CA (p. ej. CA1032 en excepciones) cumpliéndolos, sin desactivar `TreatWarningsAsErrors`. V7: `TimeProvider` y `Guid.CreateVersion7` resuelven a `System.Runtime` (`Domain_ReferencesOnlyBaseFramework` sigue en verde).
  - Hecho cuando: `BUILD` rc 0; `T-D --collect:"XPlat Code Coverage" --results-directory TestResults` seguido de `COV-D` rc 0 (100 % líneas y ramas) sin `ExcludeFromCodeCoverage`.
  - Evidencia: `ToUtcSeconds` privado compartido por `Start` y `Close`; CA1032 no apareció (excepciones con constructor primario sin parámetros). `BUILD` rc 0 con 0 avisos; Domain.Tests 27/27 en Release; `COV-D`: líneas 100,0 %, ramas 100,0 % (rc 0), sin `ExcludeFromCodeCoverage`. V6 (cobertura) y V14 (Domain) resueltas.

## Fase 3: Application (TDD con fakes y `FakeTimeProvider`)

Archivos comunes: `tests/TimeTracker.Application.Tests/*UseCaseTests.cs`, `src/TimeTracker.Application/TimeTracking/*`. Foco: `T-A`.

- [x] 3.1 **Andamiaje.** Archivos: `src/TimeTracker.Application/Identity/ICurrentUser.cs`, `TimeTracking/{ITimeEntryRepository,TimeEntryResult}.cs` (incluye `TimeEntryListResult`), `tests/TimeTracker.Application.Tests/Fakes/{InMemoryTimeEntryRepository,FixedCurrentUser}.cs`, `TimeTracker.Application.Tests.csproj` (+ `Microsoft.Extensions.TimeProvider.Testing`). Cubre: precondición de TT-17–TT-27.
  - La fake respeta el contrato del puerto (filtra por empleado, orden `ClockInAt` descendente, límite) y registra `Added` y `Updated`. No tiene pruebas propias: se valida por uso; su fidelidad con el adaptador real se comprueba en 7.4–7.5.
  - Hecho cuando: `BUILD` rc 0 y `dotnet list tests/TimeTracker.Application.Tests package` muestra TimeProvider.Testing 10.10.0.
  - Evidencia: `ICurrentUser`, `ITimeEntryRepository`, `TimeEntryResult`/`TimeEntryListResult`, fakes `InMemoryTimeEntryRepository` (filtra, ordena, limita; registra `Added`/`Updated`/`Seed`) y `FixedCurrentUser`; `.csproj` con TimeProvider.Testing. `BUILD` rc 0 con 0 avisos; `dotnet list … package` muestra TimeProvider.Testing 10.10.0. Nota: xUnit v2 no tiene `TestContext.Current`; se usa `CancellationToken.None`.
- [x] 3.2 **ClockIn · paso 1 · `ClockIn_WithNoOpenEntry_CreatesEntryAtNow`** (`FakeTimeProvider` 2026-10-12T08:00:00Z). Archivos: `ClockInUseCaseTests.cs`, `ClockInUseCase.cs`. Cubre: TT-17.
  - RED (compilación): `ClockInUseCase` no existe. GREEN: `(ITimeEntryRepository, ICurrentUser, TimeProvider)`; `TimeEntry.Start(user.EmployeeId, time.GetUtcNow())` y `AddAsync`; devuelve `TimeEntryResult`.
  - Hecho cuando: el foco pasa 1 prueba.
  - Evidencia: RED `CS0246: 'ClockInUseCase' no se encontró`. GREEN: foco 1/1.
- [x] 3.3 **ClockIn · paso 2 · `ClockIn_WithOpenEntry_ThrowsAndDoesNotSave`.** Archivos: `OpenTimeEntryAlreadyExistsException.cs`. Cubre: TT-18.
  - RED: se guarda una segunda entrada. GREEN: `FindOpenAsync` previo y `OpenTimeEntryAlreadyExistsException` (deriva de `DomainRuleException`; «Ya existe un fichaje abierto» / «Registre la salida antes de fichar una nueva entrada.»); la prueba comprueba `Title`.
  - Hecho cuando: el foco pasa 2 pruebas.
  - Evidencia: RED 1 `CS0246: 'OpenTimeEntryAlreadyExistsException' no se encontró`; RED 2 (tipo creado) `Assert.Throws() Failure: No exception was thrown` (se guardaba otra entrada). GREEN: foco 2/2.
- [x] 3.4 **ClockIn · paso 3 · `ClockIn_AfterClosingPrevious_CreatesNewEntry`.** Cubre: TT-19.
  - Verde a la primera. Mutación: la fake devuelve cualquier fichaje del empleado como "abierto" → falla; revertir. La garantía real está en PE-6 y PE-12 (tanda 2).
  - Hecho cuando: evidencia anotada.
  - Evidencia: verde a la primera (foco 4/4 con 3.8). Mutación TT-19 (la fake devuelve cualquier fichaje del empleado como abierto): `ClockIn_AfterClosingPrevious_CreatesNewEntry` ROJO; revertida.
- [x] 3.5 **ClockOut · paso 1 · `ClockOut_WithOpenEntry_ClosesAtNowAndPersists`** (abierta 08:00:00Z, reloj 16:00:00Z). Archivos: `ClockOutUseCaseTests.cs`, `ClockOutUseCase.cs`. Cubre: TT-21.
  - RED (compilación). GREEN: `FindOpenAsync` → `Close(now)` → `UpdateAsync` → `TimeEntryResult` con `WorkedMinutes`.
  - Hecho cuando: el foco pasa la prueba.
  - Evidencia: RED `CS0246: 'ClockOutUseCase' no se encontró`. GREEN: foco 1/1 (`FindOpenAsync` con `!` provisional).
- [x] 3.6 **ClockOut · paso 2 · `ClockOut_WithoutOpenEntry_Throws`.** Archivos: `NoOpenTimeEntryException.cs`. Cubre: TT-22.
  - RED: `NullReferenceException`. GREEN: `NoOpenTimeEntryException` («No hay ningún fichaje abierto» / «Registre la entrada antes de fichar la salida.»); la prueba comprueba `Title`.
  - Hecho cuando: el foco pasa la prueba.
  - Evidencia: RED 1 `CS0246`/tipo inexistente y RED 2 `Assert.Throws() Failure: Exception type was not an exact match / Actual: typeof(System.NullReferenceException)`. GREEN (`?? throw new NoOpenTimeEntryException()`): foco 2/2.
- [x] 3.7 **ClockOut · paso 3 · `ClockOut_WhenClockBeforeClockIn_ThrowsAndEntryStaysOpen`** (reloj 07:00:00Z). Cubre: TT-20.
  - Verde a la primera. Mutación: llamar a `UpdateAsync` antes de `Close` → falla la comprobación de "no persistido"; revertir.
  - Hecho cuando: evidencia anotada.
  - Evidencia: verde a la primera tras un fallo del propio test (`FakeTimeProvider.SetUtcNow` no admite retroceder: el test construye el proveedor a las 07:00:00Z). Mutación (`UpdateAsync` antes de `Close`): 2 ROJOS (`Assert.Empty() Failure: Collection was not empty`); revertida. Foco ClockOut 5/5.
- [x] 3.8 **Aislamiento a nivel de caso de uso** · `ClockIn_WithOtherEmployeeOpenEntry_Succeeds` y `ClockOut_DoesNotCloseOtherEmployeeEntry`. Cubre: TT-33, TT-34, TT-35 (nivel unitario; el nivel API está en 10.9).
  - Verdes a la primera. Mutación: `FindOpenAsync` de la fake sin filtro por empleado → fallan ambas; revertir.
  - Hecho cuando: evidencia anotada.
  - Evidencia: `ClockIn_WithOtherEmployeeOpenEntry_Succeeds` y `ClockOut_DoesNotCloseOtherEmployeeEntry` (más `ClockOut_WithOnlyOtherEmployeeOpenEntry_Throws`) verdes a la primera. Mutación (`FindOpenAsync` de la fake sin filtro por empleado): ROJOS `ClockIn_WithOtherEmployeeOpenEntry_Succeeds` y, en ClockOut, 3 pruebas (`Assert.True() Failure`, `No exception was thrown`); revertida.
- [x] 3.9 **GetTimeEntries · paso 1 · `Get_WithClosedAndOpenEntries_TotalsOnlyClosed`** (cerrados de 60 y 30 minutos + uno abierto → total 90, abierto con `WorkedMinutes` nulo). Archivos: `GetTimeEntriesUseCaseTests.cs`, `GetTimeEntriesUseCase.cs`. Cubre: TT-23.
  - RED (compilación). GREEN mínimo: `ListLatestAsync(employeeId, int.MaxValue, …)` y suma de cerradas con `WorkedHours.operator +`.
  - Hecho cuando: el foco pasa la prueba.
  - Evidencia: RED `CS0246: 'GetTimeEntriesUseCase' no se encontró`. GREEN mínimo (`int.MaxValue` y suma con `WorkedHours.operator +`): foco 1/1.
- [x] 3.10 **GetTimeEntries · paso 2 · `Get_WithMoreThanFiftyEntries_ReturnsFiftyMostRecent`** (55 fichajes). Cubre: TT-26.
  - RED: devuelve 55. GREEN: `public const int MaxEntries = 50` y pasar ese límite al repositorio.
  - Hecho cuando: el foco pasa 2 pruebas.
  - Evidencia: RED `Assert.Equal() Failure: Values differ / Actual: 55 / Expected: 50`. GREEN (`MaxEntries = 50`): foco 2/2.
- [x] 3.11 **GetTimeEntries · paso 3** · `Get_OrdersByClockInDescending`, `Get_WithNoEntries_ReturnsEmptyAndZeroTotal`, `Get_TotalIsLimitedToTheFiftyReturned` (55 cerrados de 10 min → 50 y 500). Cubre: TT-24, TT-25, TT-27.
  - Verdes a la primera. Mutaciones: invertir la lista (TT-24), total inicial 1 (TT-25), total sobre todo el histórico (TT-27) → fallan; revertir.
  - Hecho cuando: evidencia anotada.
  - Evidencia: 3 pruebas verdes a la primera (foco 5/5). Mutaciones: invertir la lista (TT-24) → ROJO `Get_OrdersByClockInDescending` y `Get_WithMoreThanFifty…`; total inicial 1 (TT-25) → ROJOS `Actual: 1`, `501`, `91`; total sobre todo el histórico (TT-27) → ROJO `Actual: 550 / Expected: 500`. Todas revertidas (Application.Tests 14/14).
- [x] 3.12 **REFACTOR y cobertura de Application.** Cubre: TT-49 (Application); V14, V17.
  - Extraer `TimeEntryResult.From(entry)` compartido; cumplir CA1848/CA1032 si aparecen. V17: la cobertura se mide solo con `Application.Tests`.
  - Hecho cuando: `BUILD` rc 0; `T-A --collect:"XPlat Code Coverage" --results-directory TestResults\app-only` y `pwsh scripts/check-coverage.ps1 -ResultsDirectory TestResults\app-only -Package TimeTracker.Application -MinimumPercent 80` rc 0.
  - Evidencia: `TimeEntryResult.From(entry)` extraído y usado por los tres casos de uso; sin avisos CA1848/CA1032. `BUILD` rc 0 (0 avisos). `T-A` en Release con cobertura en `TestResultspp-only`: 14/14; `check-coverage` Application líneas 100,0 % y ramas 100,0 % (mínimo 80 %), rc 0. V17 resuelta: Application.Tests alcanza el umbral por sí solo.

## Fase 4: Prueba de arquitectura (DD-18, cierra W2 en las dos capas internas)

- [x] 4.1 **`Application_ReferencesOnlyDomainAndBaseFramework`.** Archivos: `tests/TimeTracker.ArchitectureTests/DependencyRuleTests.cs`. Cubre: SA-9; V7.
  - Reutilizar `IsBaseFramework`. Verde a la primera; RED por infracción temporal: `FrameworkReference Microsoft.AspNetCore.App` en Application y un archivo que use `Microsoft.Extensions.Logging.ILogger` (el compilador elimina referencias sin uso) → falla listando el ensamblado; revertir.
  - Hecho cuando: `T-AR --filter "FullyQualifiedName~DependencyRuleTests"` en verde y evidencia del rojo anotada.
  - Evidencia: verde a la primera (foco 6/6). RED por infracción temporal: `FrameworkReference Microsoft.AspNetCore.App` + archivo con `Microsoft.Extensions.Logging.ILogger` en Application → `TimeTracker.Application referencia ensamblados ajenos al marco base y a Domain: Microsoft.Extensions.Logging.Abstractions`; revertido (archivo borrado y `.csproj` idéntico a la copia).
- [x] 4.2 **`Domain_And_Application_DeclareNoPackageOrFrameworkReferences`** (Theory sobre los dos `.csproj`; lee el XML). Archivos: `RepositoryConventionsTests.cs`. Cubre: SA-8, SA-9, SA-11.
  - Verde a la primera; el rojo se demuestra en 4.3.
  - Hecho cuando: `T-AR --filter "FullyQualifiedName~RepositoryConventionsTests"` en verde.
  - Evidencia: Theory con 2 casos, verde a la primera (`RepositoryConventionsTests` 21/21); su rojo está en 4.3(b).
- [x] 4.3 **Demostraciones únicas (se revierte cada una).** Cubre: SA-7, SA-10, SA-11.
  - (a) SA-10: `FrameworkReference Microsoft.AspNetCore.App` + `IllegalReferenceProbe.cs` en Domain → `Domain_ReferencesOnlyBaseFramework` ROJO nombrando la referencia. (b) SA-11: `<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" VersionOverride="10.0.10" />` temporal en Application → el test XML falla nombrando proyecto y paquete. (c) SA-7: `ProjectReference` Domain → Application → `dotnet build` falla por dependencia circular (MSB4006).
  - Hecho cuando: salidas guardadas, `Test-Path src\TimeTracker.Domain\IllegalReferenceProbe.cs` es False y `Select-String 'Application' src\TimeTracker.Domain\TimeTracker.Domain.csproj` no da resultados.
  - Evidencia: (a) SA-10 `Domain_ReferencesOnlyBaseFramework` ROJO: `TimeTracker.Domain referencia ensamblados ajenos al marco base: Microsoft.Extensions.Logging.Abstractions`. (b) SA-11 `Domain_And_Application_DeclareNoPackageOrFrameworkReferences` ROJO: `src/TimeTracker.Application/TimeTracker.Application.csproj declara referencias no permitidas: PackageReference Microsoft.Extensions.Logging.Abstractions`. (c) SA-7 `dotnet build` con Domain → Application: `error MSB4006: Existe una dependencia circular en el gráfico de dependencias de destino … _GenerateRestoreProjectPathWalk`. Todo revertido: `IllegalReferenceProbe.cs` no existe, Domain no menciona Application, y `restore` + `BUILD` posteriores con 0 avisos y 0 errores.
- [x] 4.4 **Evidencia manual de estructura.** Cubre: SA-6, SA-8.
  - `dotnet sln TimeTracker.sln list` (9 proyectos), `dotnet list <proyecto> reference` de los 5 de `src/` y `dotnet list src/TimeTracker.Application package` (sin paquetes directos).
  - Hecho cuando: coinciden con la lista permitida y Web no referencia proyectos de `src/`.
  - Evidencia: `dotnet sln list` = 9 proyectos. Referencias: Api → Application, Infrastructure; Application → Domain; Infrastructure → Application; Domain y Web sin referencias de proyecto. Application sin paquetes directos ("No se encontró ningún paquete para este marco"). Coincide con la lista permitida.

## Fase 5: Punto de control de la Tanda 1

- [x] 5.1 **Puerta de calidad.** Cubre: SA-12, SA-13, SA-14; V14.
  - `dotnet restore TimeTracker.sln`, `FORMAT`, `BUILD`, `TEST`.
  - Hecho cuando: los cuatro terminan en 0, `BUILD` con 0 avisos, y pasan las pruebas de Domain, Application y arquitectura.
  - Evidencia: `restore` rc 0; `FORMAT` rc 0 (tras corregir una línea en blanco entre grupos de `using` en `ClockInUseCaseTests.cs` y `ClockOutUseCaseTests.cs`); `BUILD` rc 0 con 0 avisos y 0 errores; `TEST` en Release: Domain 27, Application 14, Arquitectura 28, Integración 14 = 83 en verde.
- [x] 5.2 **Cobertura, revisión y parada.** Cubre: TT-49, SA-16, SA-17; V6, V17.
  - `COV-D` y `COV-A` rc 0 sobre `TestResults`; comparar líneas reales con la previsión; anotar V1–V3, V6 (parte cobertura), V7, V14 y V17. **PARADA:** si la tanda consumió más de la mitad del tiempo, el usuario decide si prioriza y aplaza.
  - Hecho cuando: checklist de la Tanda 1 completa y evidencias registradas.
  - Evidencia: `COV-D` rc 0 (líneas 100,0 % y ramas 100,0 %); `COV-A` sobre `TestResults` rc 0 (100,0 % / 100,0 %) y sobre `TestResultspp-only` rc 0. Líneas escritas a mano (incluye líneas en blanco): src 200, pruebas de Domain/Application 454, arquitectura +30, `.csproj`/props +≈6; total ≈ 690 (previsión 610–760). V1, V2, V6 (cobertura), V7, V14 y V17 resueltas; V3 queda para 7.1. PARADA de punto de control: la tanda ha cabido en la previsión; se espera la decisión del usuario para pasar a la Tanda 2.

---

# TANDA 2: Infrastructure y Testcontainers

## Fase 6: Paquetes y herramienta

- [x] 6.1 **Copia previa y paquetes EF (V1, V4).** Archivos: `Directory.Packages.props`, `src/TimeTracker.Infrastructure/TimeTracker.Infrastructure.csproj`, `tests/TimeTracker.Api.IntegrationTests/TimeTracker.Api.IntegrationTests.csproj`. Cubre: DD-10, V1, V4.
  - `Save-Rollback 2 …`; versiones de 1.3 en `Directory.Packages.props`; Infrastructure: `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Relational` (referencia directa, eleva la familia) y `Microsoft.EntityFrameworkCore.Design` con `PrivateAssets="all"`; pruebas de integración: `Testcontainers.PostgreSql` y `Microsoft.Extensions.TimeProvider.Testing`. Anotar `dotnet --list-runtimes` (el runtime instalado era 10.0.10 y los paquetes `Microsoft.Extensions.*` suben a 10.0.12).
  - RED: `dotnet list … package` no muestra los paquetes. Hecho cuando: `BUILD` rc 0 (sin NU1605, NU190x ni MSB3277); `dotnet list src/TimeTracker.Infrastructure package --include-transitive | Select-String 'Microsoft.EntityFrameworkCore'` muestra toda la familia en 10.0.12 y Npgsql EF en 10.0.3; `T-AR` en verde.
  - Evidencia: copia previa de 5 archivos en `..\TimeTracker.rollback\tanda-2` (Directory.Packages.props, .editorconfig, 2 `.csproj`, RepositoryConventionsTests.cs). RED: `dotnet list src/TimeTracker.Infrastructure package` → «No se encontró ningún paquete para este marco». `dotnet --list-runtimes`: `Microsoft.NETCore.App` 10.0.10 (también 8.0.x y 9.0.18). `restore` rc 0 y `BUILD` rc 0 con 0 avisos (sin NU1605, NU190x ni MSB3277). `dotnet list … package --include-transitive`: Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3, Npgsql 10.0.3, `Microsoft.EntityFrameworkCore`/`.Abstractions`/`.Analyzers`/`.Relational`/`.Design` 10.0.12 (familia entera en 10.0.12). `T-AR` 29/29. V1 y V4 resueltas (sin referencias adicionales ni fijado transitivo).
- [x] 6.2 **Herramienta local `dotnet-ef`.** Archivos: `.config/dotnet-tools.json`, `tests/TimeTracker.ArchitectureTests/RepositoryConventionsTests.cs`. Cubre: PE-15.
  - RED: `DotnetTools_DeclaresDotnetEf_10_0_12` falla (no existe el manifiesto). GREEN: `dotnet new tool-manifest` y `dotnet tool install dotnet-ef --version 10.0.12`.
  - Hecho cuando: `dotnet tool restore` rc 0, `dotnet tool run dotnet-ef --version` imprime `10.0.12` y la prueba pasa.
  - Evidencia: RED `DotnetTools_DeclaresDotnetEf_10_0_12` → «Falta .config/dotnet-tools.json (herramienta local dotnet-ef)». GREEN: `dotnet new tool-manifest` + `dotnet tool install dotnet-ef --version 10.0.12` (manifiesto local, nada global). **Desviación menor:** el SDK 10.0.302 crea el manifiesto en `dotnet-tools.json` en la raíz, no en `.config/`; se movió a `.config/dotnet-tools.json` (ruta que exige PE-15). `tool restore` rc 0, `dotnet tool run dotnet-ef --version` → `10.0.12`; `T-AR` 29/29.

## Fase 7: Persistencia (TDD con PostgreSQL real)

Pruebas en `tests/TimeTracker.Api.IntegrationTests` con `[Collection("Postgres")]`; un `EmployeeId` nuevo por prueba, sin limpieza. Foco: `T-I --filter "FullyQualifiedName~Persistence"`.

- [x] 7.1 **Fixture de PostgreSQL y prueba de humo (V2 y V3 definitivas).** Archivos: `tests/…/Postgres/{PostgresFixture,PostgresCollection}.cs`, `Postgres/PostgresFixtureTests.cs`. Cubre: PE-22 (parte fixture), PE-23; V2, V3.
  - RED (compilación): `PostgresFixture` no existe. GREEN: `new PostgreSqlBuilder("postgres:18-alpine").Build()`, `IAsyncLifetime`, `[CollectionDefinition("Postgres")]`; la prueba ejecuta `SELECT version()` y comprueba `PostgreSQL 18`. Anotar el tag de `testcontainers/ryuk` que descarga. Si Ryuk falla: `TESTCONTAINERS_RYUK_DISABLED=true` y `DisposeAsync` del fixture.
  - Sin Docker no se omite en silencio: con `$env:DOCKER_HOST='tcp://127.0.0.1:1'` la prueba falla con un mensaje claro (limpiar la variable después).
  - Hecho cuando: el foco pasa y `docker ps -a --filter ancestor=postgres:18-alpine -q` queda vacío tras la prueba.
  - Evidencia: RED `CS0246: 'PostgresFixture' no se encontró`. GREEN: foco 1/1 (`SELECT version()` empieza por `PostgreSQL 18`); `docker ps -a --filter ancestor=postgres:18-alpine -q` vacío tras la prueba. Ryuk: Testcontainers 4.16.0 descarga `testcontainers/ryuk:0.14.0` y funciona (no hizo falta `TESTCONTAINERS_RYUK_DISABLED`; el contenedor de Ryuk se autodestruye tras la ejecución). V3 resuelta; V2 ya resuelta en 1.2. **Desviación (simulación sin Docker):** `DOCKER_HOST=tcp://127.0.0.1:1` (también con `DOCKER_CONTEXT`/`DOCKER_CONFIG` vacío) NO provoca fallo: Testcontainers 4.16 recurre al siguiente proveedor (Docker Desktop) y la prueba pasa. No se paró Docker Desktop. Sustituto: con una imagen inexistente (`postgres:0.0-inexistente`, cambio temporal revertido) el fixture falla con `InvalidOperationException: No se pudo iniciar el contenedor … Las pruebas de integración requieren Docker en marcha.`; no hay `Skip` en ningún sitio, así que no se omite en silencio.
- [x] 7.2 **RED del esquema · `MigrationTests`.** Archivos: `tests/…/Persistence/MigrationTests.cs`. Cubre: PE-1, PE-2, PE-4, PE-5, PE-6, PE-13, PE-14.
  - Pruebas: `Migrate_OnEmptyDatabase_CreatesTableIndexesAndCheck`, `Model_HasNoPendingChanges` (`HasPendingModelChanges() == false`), `Columns_HaveExpectedTypesAndNullability` y `Columns_AreExactlyFour` (`information_schema.columns`; refuerza PE-1: el modelo no añade columnas de concurrencia), `Indexes_HaveExpectedDefinitions` (`pg_indexes`: `clock_in_at DESC`, `UNIQUE`, `WHERE (clock_out_at IS NULL)`), `Check_RejectsClockOutBeforeClockIn`, `OpenIndex_RejectsSecondOpenRow` (`23505`) y `OpenIndex_AllowsOpenRowAfterClosedRow` (SQL directo).
  - RED (compilación): `TimeTrackerDbContext` no existe.
  - Hecho cuando: el foco falla por compilación y el motivo queda anotado.
  - Evidencia: RED (compilación) `CS1061: 'PostgresFixture' no contiene una definición para 'CreateEmptyDatabaseAsync'` y `'CreateContext'` (el tipo `TimeTrackerDbContext` aún no existía). 8 pruebas escritas en `MigrationTests.cs`.
- [x] 7.3 **GREEN del esquema: contexto, mapeo y migración (V5).** Archivos: `src/TimeTracker.Infrastructure/Persistence/{TimeTrackerDbContext,TimeEntryConfiguration,DesignTimeDbContextFactory}.cs`, `Migrations/*` (generado), `.editorconfig` (`[src/TimeTracker.Infrastructure/Persistence/Migrations/**.cs]` `generated_code = true`), fixture (migra una vez con `MigrateAsync`, `CreateEmptyDatabaseAsync`). Cubre: PE-1, PE-2, PE-4, PE-5, PE-6, PE-13, PE-14; DD-13.
  - Mapeo Fluent: `ToTable("time_entries")`, `HasColumnName`, `timestamp with time zone`, índices `ix_time_entries_employee_id_clock_in_at` (DESC) y `ux_time_entries_employee_id_open` (único, filtro `clock_out_at IS NULL`), `ck_time_entries_clock_out_after_clock_in`. Migración: `dotnet tool run dotnet-ef migrations add InitialTimeEntries --project src/TimeTracker.Infrastructure --startup-project src/TimeTracker.Infrastructure --output-dir Persistence/Migrations`.
  - Hecho cuando: el foco `MigrationTests` pasa entero; `BUILD` rc 0 sin avisos de la migración; `FORMAT` rc 0 (V5; si algo falla, ajuste justificado en `.editorconfig`, nunca `TreatWarningsAsErrors`); `dotnet tool run dotnet-ef migrations list --project src/TimeTracker.Infrastructure --startup-project src/TimeTracker.Infrastructure` muestra `InitialTimeEntries`.
  - Evidencia: GREEN: foco `MigrationTests` 8/8 a la primera tras crear contexto, mapeo, `DesignTimeDbContextFactory` y migración (+ `CreateContext`/`CreateEmptyDatabaseAsync` y `MigrateAsync` único en el fixture). Migración `20261012235858_InitialTimeEntries` generada con el comando exacto de la tarea (`--startup-project src/TimeTracker.Infrastructure` funciona; sin respaldo). `.editorconfig`: sección `[src/TimeTracker.Infrastructure/Persistence/Migrations/**.cs] generated_code = true`. `BUILD` rc 0 con 0 avisos; `FORMAT` rc 0 (tras separar con línea en blanco los grupos de `using` de mis archivos, igual que en la tanda 1); `dotnet-ef migrations list` muestra `20261012235858_InitialTimeEntries` (el aviso «Failed to connect to 127.0.0.1:5432» es esperado: sin base local no puede mostrar el estado pendiente). V5 resuelta (sin avisos ni cambios de formato en la migración; no se probó el `FORMAT` sin el ajuste de `.editorconfig`). V6 EF: el diseño de EF aceptó el constructor privado con parámetros (sin campos de respaldo).
- [x] 7.4 **Repositorio · paso 1 · ida y vuelta y entrada abierta.** Archivos: `Persistence/TimeEntryRepositoryTests.cs`, `src/TimeTracker.Infrastructure/Persistence/TimeEntryRepository.cs`. Cubre: PE-3, PE-12; V6 (EF materializa).
  - RED (compilación): el repositorio no existe. Pruebas: `AddAsync_ThenReadWithNewContext_KeepsUtcInstant` (2026-10-12T08:00:00Z, `Offset` cero) y `FindOpenAsync_IgnoresOtherEmployeesOpenEntry`. GREEN: `AddAsync` y `FindOpenAsync` (con seguimiento: `UpdateAsync` persiste la entidad cargada); lo no cubierto aún lanza `NotImplementedException`.
  - V6: EF usa el constructor privado y las propiedades de solo lectura sin campos de respaldo; si no, campos explícitos.
  - Hecho cuando: el foco pasa las pruebas nuevas.
  - Evidencia: RED `CS0246: 'TimeEntryRepository' no se encontró`. GREEN: foco 2/2 (`ClockInAt` = `2026-10-12T08:00:00Z` con `Offset` cero tras leer con un contexto nuevo). V6 resuelta en ejecución: EF materializa `TimeEntry` con el constructor privado y propiedades de solo lectura, sin campos de respaldo explícitos ni `ExcludeFromCodeCoverage`; Domain sigue al 100 %.
- [x] 7.5 **Repositorio · paso 2 · `ListLatestAsync_ReturnsFiftyNewestOfThatEmployeeOnly`** (55 fichajes del empleado y 3 de otro). Cubre: PE-11.
  - RED: `NotImplementedException`. GREEN: `Where(EmployeeId).OrderByDescending(ClockInAt).Take(limit)` con `AsNoTracking`.
  - Hecho cuando: el foco pasa.
  - Evidencia: RED `System.NotImplementedException`. GREEN (`AsNoTracking`, `Where`, `OrderByDescending`, `Take`): foco 3/3.
- [x] 7.6 **Repositorio · paso 3 · traducción de `23505`.** Cubre: PE-7 (y PE-5 a nivel de repositorio).
  - RED 1: `AddAsync_SecondOpenEntry_ThrowsOpenTimeEntryAlreadyExists` (segunda abierta insertada directamente) → `DbUpdateException`. GREEN 1: capturar cualquier `23505`. RED 2: `AddAsync_OtherUniqueViolation_IsNotTranslated` (mismo `Id`, clave primaria) → falla porque se traduce. GREEN 2: filtrar por `PostgresException.ConstraintName == "ux_time_entries_employee_id_open"`. Añadir `AddAsync_OpenEntriesOfDifferentEmployees_AreAllowed` (verde a la primera).
  - Hecho cuando: el foco pasa.
  - Evidencia: las tres pruebas se escribieron juntas; `OtherUniqueViolation` y `DifferentEmployees` pasaron a la primera (aún no había traducción). RED 1 `Expected: OpenTimeEntryAlreadyExistsException / Actual: DbUpdateException` (inner `23505 … ux_time_entries_employee_id_open`). GREEN 1: captura de cualquier `23505` → 5/6 (la de otra violación única fue la que se rompió). RED 2 `AddAsync_OtherUniqueViolation_IsNotTranslated`: `Expected: DbUpdateException / Actual: OpenTimeEntryAlreadyExistsException` (misma `Id`, clave primaria). GREEN 2: filtro `PostgresException.ConstraintName == "ux_time_entries_employee_id_open"` (constante `TimeEntryConfiguration.OpenEntryIndexName`) → 6/6.
- [x] 7.7 **Repositorio · paso 4 · `UpdateAsync_PersistsClosedEntry`.** Cubre: soporte de TT-21, TT-38.
  - RED: `NotImplementedException`. GREEN: `SaveChangesAsync`.
  - Hecho cuando: el foco pasa.
  - Evidencia: RED `System.NotImplementedException`. GREEN: `context.TimeEntries.Update(entry)` + `SaveChangesAsync` (Update en vez de solo `SaveChanges` para no dejar el parámetro sin uso y cubrir entidades sin seguimiento): foco 7/7 (`ClockOutAt` persistido = 16:00Z y `FindOpenAsync` devuelve nulo).
- ~~7.8~~ **Retirada (recorte 2026-10-12, DD-9 diferida):** concurrencia de la salida con `xmin` (antes PE-8, PE-9, V12). Sin `xmin`, sin token de concurrencia ni columna de concurrencia; `UpdateAsync` de 7.7 es la salida simple. Riesgo residual aceptado: doble `clock-out` simultáneo, última escritura gana.
- [x] 7.9 **Repositorio · paso 6 · `AddAsync_TenParallelOpenInserts_OneSucceedsNineConflict`.** Cubre: PE-20.
  - Verde a la primera. Mutación: comparar `ConstraintName` con otro valor → nueve `DbUpdateException` y la prueba falla; revertir.
  - Hecho cuando: evidencia anotada y una sola fila abierta en la base.
  - Evidencia: verde a la primera (foco 8/8): 10 inserciones paralelas con contextos distintos → 1 éxito, 9 `OpenTimeEntryAlreadyExistsException`, 1 fila abierta en la base. Mutación (`ConstraintName: "otro_indice"`): 2 ROJOS (`…TenParallel…` y `…SecondOpenEntry…`, `Actual: DbUpdateException`); revertida y foco 8/8.
- [x] 7.10 **REFACTOR de la tanda 2.** Cubre: V14; amenazas (inyección SQL, datos sensibles).
  - Sustituir los `NotImplementedException` que queden, cumplir CA (CA1848, CA1515…), sin SQL crudo en `src/`.
  - Hecho cuando: `BUILD` rc 0 y `Get-ChildItem src -Recurse -Filter *.cs | Select-String 'FromSqlRaw|ExecuteSqlRaw|EnableSensitiveDataLogging'` no da resultados.
  - Evidencia: no queda ningún `NotImplementedException` en `src/`; sin avisos CA1848/CA1515. `grep -rnE 'FromSqlRaw|ExecuteSqlRaw|EnableSensitiveDataLogging|NotImplementedException' src --include=*.cs` → sin resultados. `BUILD` rc 0 con 0 avisos. El SQL crudo vive solo en pruebas (`MigrationTests`, parametrizado en los INSERT). V14 resuelta para Infrastructure.

## Fase 8: Punto de control de la Tanda 2

- [x] 8.1 **Puerta de calidad con Docker.** Cubre: SA-13, PE-23.
  - `FORMAT`, `BUILD`, `TEST` con Docker en marcha; `COV-D` y `COV-A` siguen en rc 0.
  - Hecho cuando: todo en 0 y pasan repositorio, carrera y migraciones.
  - Evidencia: `FORMAT` rc 0; `BUILD` rc 0 con 0 avisos y 0 errores; `TEST` en Release con Docker: Domain 27, Application 14, Arquitectura 29, Integración 31 (14 previas + 1 humo + 8 migraciones + 8 repositorio) = 101 en verde, 0 fallos. `COV-D` rc 0 (líneas 100,0 %, ramas 100,0 %); `COV-A` rc 0 (100,0 % / 100,0 %). Sin contenedores `postgres:18-alpine` huérfanos tras la ejecución (`docker ps -a` solo muestra Ryuk transitorio, que desaparece solo).
- [x] 8.2 **Revisión y parada.**
  - Anotar V1, V3, V4, V5, V6 y líneas reales frente a la previsión. **PARADA:** si la tanda se alargó, el usuario decide sobre las tandas 3 y 4.
  - Hecho cuando: checklist completa y evidencias registradas.
  - Evidencia: V1 (Npgsql EF 10.0.3 y familia EF 10.0.12 confirmadas), V3 (Ryuk 0.14.0, sin deshabilitar), V4 (familia EF en 10.0.12, sin NU1605/MSB3277, runtime 10.0.10), V5 (`dotnet-ef` con Infrastructure como proyecto de inicio; migración sin avisos ni cambios de `FORMAT`), V6 (EF materializa con constructor privado, sin campos de respaldo) resueltas. Líneas escritas a mano (incluye blancos): src de Infrastructure 110, pruebas de integración 372, prueba de arquitectura +11, `.csproj`/props ≈ +12, `.editorconfig` +5 → ≈ 510 (migración generada aparte: 177 líneas; `.config/dotnet-tools.json` 12). **Desviaciones de la tanda:** manifiesto movido a `.config/` (6.2) y simulación sin Docker no reproducible con `DOCKER_HOST` (7.1); ambas sin impacto en specs. **Incidencia de proceso:** al final se ejecutó por descuido un `git --version` (salida descartada, sin ningún otro comando git); se anota por la regla «sin Git». PARADA de punto de control: la tanda cabe en la previsión; se espera la decisión del usuario para la Tanda 3.

---

# TANDA 3: Api, identidad, configuración y compose

## Fase 9: Cimientos de configuración

- [x] 9.1 **Copia previa, `ApiFactory` con base válida y aislamiento de entorno (V9, V19).** Archivos: `tests/…/ApiFactory.cs`, `StartupConfigurationTests.cs`, `CorsPolicyTests.cs` (solo adaptación a la base válida), `EnvironmentIsolation.cs` (nuevo). Cubre: regresión de las pruebas CORS existentes (sin escenarios nuevos); V9, V19.
  - `Save-Rollback 3 …`. `ApiFactory` aporta origen, `ConnectionStrings:Default` (`Host=db.invalid;Database=timetracker;Username=timetracker`, sin contraseña) y GUID inventado, y admite sobrescribir o quitar claves; `EnvironmentIsolation` borra variables `Cors__`, `ConnectionStrings__`, `Identity__`, `Database__` y `ASPNETCORE_ENVIRONMENT`. Las pruebas de arranque parten de la base válida y cambian solo la clave bajo prueba; la búsqueda de `OptionsValidationException` recorre `InnerException` y `AggregateException.InnerExceptions`.
  - Hecho cuando: `T-I` con las 14 pruebas existentes en verde (sin comportamiento nuevo).
  - Evidencia: copia previa de 11 archivos en `..\TimeTracker.rollback\tanda-3` (Program.cs, Api.csproj, ApiFactory, StartupConfigurationTests, CorsPolicyTests, OpenApiContractTests, RepositoryConventionsTests, openapi.json, compose, .env.example, ci.yml). `ApiFactory` (ya no sellada) aporta origen, `ConnectionStrings:Default` y GUID inventados; se cambian/quitan claves con `WithWebHostBuilder(b => b.UseSetting(clave, valor|null))` (sin helper propio, KISS). `EnvironmentIsolation.Apply()` (llamado desde el constructor estático de `ApiFactory`) borra `Cors__`, `ConnectionStrings__`, `Identity__`, `Database__` y `ASPNETCORE_ENVIRONMENT`; `StartupFailure` recorre `InnerException` y `AggregateException` (V9). `CorsPolicyTests` no necesitó cambios. `T-I` (sin Postgres/Persistence): 14/14 en verde. V9 (parte) resuelta.
- [x] 9.2 **Cadena de conexión validada · `DatabaseStartupTests`.** Archivos: `Startup/DatabaseStartupTests.cs`, `src/TimeTracker.Infrastructure/Persistence/{ConnectionStringOptions,ConnectionStringOptionsValidator}.cs`, `src/TimeTracker.Api/Configuration/DatabaseServiceCollectionExtensions.cs`, `Program.cs`. Cubre: SC-1, SC-2, SC-3, SC-4.
  - RED: `Startup_WithoutConnectionString_FailsNamingKey`, `…WithBlankConnectionString_Fails`, `…WithMalformedConnectionString_Fails` (`Host=db.invalid;Password=secreto-prueba;;=;Puerto-roto`) y `…DoesNotPrintTheValue` (ni `secreto-prueba` ni el mensaje del analizador, también en excepciones internas). GREEN: `ValidateOnStart`; validador con `NpgsqlConnectionStringBuilder` en `try/catch` sin propagar su mensaje; mensajes en español que nombran `ConnectionStrings:Default`.
  - Mientras 9.3 no esté hecha, ejecutar con `-p:OpenApiGenerateDocuments=false`: el build de Api exporta el contrato arrancando el host y fallaría (ese fallo es el RED de 9.3; anotar su salida).
  - Hecho cuando: `T-I --filter "FullyQualifiedName~DatabaseStartupTests" -p:OpenApiGenerateDocuments=false` pasa.
  - Evidencia: RED: 4 pruebas rojas (`Assert.Contains() Failure: Filter not matched in collection` x3 y `Assert.NotNull() Failure: Value is null`). GREEN: `ConnectionStringOptions` + `ConnectionStringOptionsValidator` (Infrastructure; `NpgsqlConnectionStringBuilder` en `try/catch` que no propaga el mensaje) y `DatabaseServiceCollectionExtensions.AddDatabaseConfiguration` (Api, `ValidateOnStart`): `DatabaseStartupTests` 4/4 con `-p:OpenApiGenerateDocuments=false`. Mutación (añadir `exception.Message` al mensaje): `Startup_WithMalformedConnectionString_DoesNotPrintTheValue` ROJO (`Assert.DoesNotContain() Failure: Sub-string found`); revertida. V9 resuelta (las pruebas parten de la base válida y recorren Aggregate/Inner).
- [x] 9.3 **W4: `DocumentGeneration` (DD-16, V8).** Archivos: `src/TimeTracker.Api/Configuration/DocumentGeneration.cs`, `Program.cs` (sustituye la rama `GetDocument.Insider`). Cubre: SA-26, SA-27 (parte).
  - RED: `dotnet build src/TimeTracker.Api -warnaserror` falla con `OptionsValidationException` (salida de 9.2). GREEN: solo en el proceso `GetDocument.Insider` se fuerza `EnvironmentName=Development` en `WebApplicationOptions` y se aportan valores ficticios (`Cors:AllowedOrigins:0`, `ConnectionStrings:Default` con `Host=db.invalid` sin contraseña); sin cambios en `appsettings*.json`. V8: anotar el entorno con el que arranca la herramienta. Respaldo: exportar con `WebApplicationFactory` (seguimiento W4).
  - Hecho cuando: con el entorno limpio, `dotnet build src/TimeTracker.Api -warnaserror` rc 0 y `Get-Content docs\api\openapi.json -Raw | ConvertFrom-Json` es válido.
  - Evidencia: RED (salida de 9.2): `dotnet build src/TimeTracker.Api -warnaserror` falla con `Microsoft.Extensions.ApiDescription.Server.targets(67,5): error : System.AggregateException … ConnectionStrings:Default: debe definirse con la cadena de conexión de PostgreSQL`. GREEN: `Configuration/DocumentGeneration.cs` (solo con ensamblado de entrada `GetDocument.Insider`: `EnvironmentName=Development` en `WebApplicationOptions` y valores ficticios en memoria; sin tocar `appsettings*.json`); `Program.cs` lo usa. Con entorno limpio `dotnet build src/TimeTracker.Api -warnaserror` rc 0, 0 avisos, y `openapi.json` se lee con `ConvertFrom-Json`. V8: diagnóstico temporal (revertido) → la herramienta arranca con `ASPNETCORE_ENVIRONMENT` y `DOTNET_ENVIRONMENT` vacíos (por tanto `Production`) y `args=--applicationName=TimeTracker.Api`; sin el forzado, la identidad (9.5) lo haría fallar. Respaldo (WebApplicationFactory) no necesario.
- [x] 9.4 **`appsettings*.json` sin claves nuevas.** Archivos: `RepositoryConventionsTests.cs`. Cubre: SC-5.
  - `AppSettings_ContainNoConnectionStringsIdentityOrDatabaseKeys`. Verde a la primera; mutación: añadir temporalmente `"ConnectionStrings": {}` → falla; revertir.
  - Hecho cuando: `T-AR --filter "FullyQualifiedName~RepositoryConventionsTests"` en verde.
  - Evidencia: `AppSettings_ContainNoConnectionStringsIdentityOrDatabaseKeys` verde a la primera (`RepositoryConventionsTests` 23/23). Mutación: `"ConnectionStrings": {}` en `appsettings.json` → ROJO (`appsettings versiona claves prohibidas: appsettings.json: ConnectionStrings`); revertida y 23/23.
- [x] 9.5 **Identidad provisional solo en Development.** Archivos: `Identity/{ProvisionalIdentityOptions,ProvisionalIdentityOptionsValidator,DevelopmentCurrentUser,IdentityServiceCollectionExtensions}.cs`, `Startup/IdentityStartupTests.cs`, `DocumentGeneration.cs` (+ GUID ficticio). Cubre: SC-6, SC-7, SC-8, SC-9; TT-28 (base); V19.
  - RED: `Startup_InDevelopmentWithValidGuid_StartsAndHealthIsOk`, `Startup_InDevelopmentWithoutIdentity_FailsNamingKey`, `Startup_WithInvalidGuid_Fails` (Theory `no-es-un-guid` y GUID vacío), `Startup_InProduction_FailsNamingIdentity` (con configuración completa, incluida la identidad). GREEN: validador **siempre registrado** con `IHostEnvironment`; la propiedad se enlaza como `string?` y se valida con `Guid.TryParse` para controlar el mensaje; `ICurrentUser` solo se registra en Development.
  - V19: con `$env:Identity__DevelopmentEmployeeId` y `$env:ConnectionStrings__Default` definidos en la máquina, `T-I` sigue en verde.
  - Hecho cuando: `T-I --filter "FullyQualifiedName~IdentityStartupTests"` pasa. Evidencia manual: `ASPNETCORE_ENVIRONMENT=Production` y configuración completa con `dotnet run --no-launch-profile --project src/TimeTracker.Api` termina con código ≠ 0 y sin `Now listening`.
  - Evidencia: RED: 5 de 6 rojas (`Assert.Contains() Failure: Filter not matched in collection`: sin identidad, GUID inválido x3, Production); `Startup_InDevelopmentWithValidGuid_StartsAndHealthIsOk` verde a la primera (la clave aún se ignoraba; la cubren las demás). GREEN: `Identity/{ProvisionalIdentityOptions,ProvisionalIdentityOptionsValidator,DevelopmentCurrentUser,IdentityServiceCollectionExtensions}.cs` (validador siempre registrado con `IHostEnvironment`; `ICurrentUser` singleton solo en Development) y GUID ficticio en `DocumentGeneration`: `IdentityStartupTests` 6/6 (filtro `Startup`: 19/19). V19: con `Identity__DevelopmentEmployeeId=no-es-un-guid`, `ConnectionStrings__Default`, `Cors__AllowedOrigins__0`, `Database__ApplyMigrationsOnStartup=quizas` y `ASPNETCORE_ENVIRONMENT=Production` definidos, `T-I` 24/24 en verde y `dotnet build src/TimeTracker.Api -warnaserror` rc 0 (V19 resuelta). Manual: `ASPNETCORE_ENVIRONMENT=Production` + configuración completa con `dotnet run --no-launch-profile` → `OptionsValidationException: Identity: la identidad provisional solo está permitida…`, código de salida 127 (≠ 0; observado también ejecutando la DLL directamente, sin `timeout`) y 0 apariciones de `Now listening`.
- [x] 9.6 **Bandera `Database:ApplyMigrationsOnStartup`.** Archivos: `Configuration/DatabaseOptions.cs`, `DatabaseServiceCollectionExtensions.cs`, `DatabaseStartupTests.cs`. Cubre: SC-10, SC-11.
  - RED: `DatabaseOptions_WithoutSetting_IsFalse` y `Startup_WithNonBooleanFlag_FailsNamingKey` (`quizas`). GREEN: `BindConfiguration("Database")` + `ValidateOnStart` (el fallo de conversión del binder nombra la clave; la prueba lo confirma).
  - Hecho cuando: `T-I --filter "FullyQualifiedName~DatabaseStartupTests"` pasa.
  - Evidencia: RED (compilación): `CS0246: DatabaseOptions` no existe. GREEN: `Configuration/DatabaseOptions.cs` + `BindConfiguration("Database")` y `ValidateOnStart` en `AddDatabaseConfiguration`: `DatabaseStartupTests` 6/6 (el binder lanza un error de conversión que nombra `Database:ApplyMigrationsOnStartup`; confirmado por la prueba sobre el árbol de excepciones).
- ~~9.7~~ **Retirada (recorte 2026-10-12):** CORS `GET`/`POST` (antes SC-15–SC-18, cambio D3). `CorsServiceCollectionExtensions.cs` no se modifica (ya usa `AllowAnyMethod()`) y no hay pruebas dedicadas de preflight; el funcionamiento desde el navegador se comprueba en 14.6 (UI-13).

## Fase 10: Contrato primero, endpoints y errores

Pruebas en `tests/…/TimeEntries/*` y `ErrorHandlingTests.cs`, con `PostgresApiFactory : ApiFactory` (cadena del fixture, identidad nueva por prueba, `FakeTimeProvider` sustituible). Foco: `T-I --filter "FullyQualifiedName~TimeEntry"`.

- [x] 10.1 **RED del contrato.** Archivos: `tests/TimeTracker.ArchitectureTests/OpenApiContractTests.cs`. Cubre: TT-31, TT-41.
  - Pruebas: `ExportedDocument_ContainsTimeEntryEndpoints` (POST clock-in con `201`, POST clock-out con `200`, GET con `200`, `409` en ambos POST, `format: date-time` en `clockInAt` y `clockOutAt`) y `ExportedDocument_DoesNotExposeEmployeeId` (recorre parámetros y esquemas). RED: faltan los endpoints.
  - Hecho cuando: `T-AR --filter "FullyQualifiedName~OpenApiContractTests"` falla por la primera y se anota el motivo.
  - Evidencia: RED: `ExportedDocument_ContainsTimeEntryEndpoints` falla con `Falta la ruta /api/time-entries/clock-in en el contrato.`; `ExportedDocument_DoesNotExposeEmployeeId` verde a la primera (su mutación se hace en 10.12) y `ContainsHealthGet200` sigue en verde (`OpenApiContractTests` 2 verdes, 1 roja).
- [x] 10.2 **clock-in · `ClockIn_WithoutOpenEntry_Returns201WithBody`.** Archivos: `TimeEntryEndpointsTests.cs`, `PostgresApiFactory.cs`, `src/TimeTracker.Api/TimeEntries/{TimeEntryEndpoints,TimeEntryResponse}.cs`, `src/TimeTracker.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`, `Program.cs`. Cubre: TT-36; V13.
  - RED: `404`. GREEN: `MapGroup("/api/time-entries").WithTags("TimeEntries")`, `POST clock-in` con `TypedResults.Created((string?)null, body)`, DTO con `DateTime` UTC, registro de casos de uso, `TimeProvider.System`, repositorio y `DbContext`. La prueba lee el JSON crudo y comprueba que `clockInAt` termina en `Z` y que `clockOutAt` y `workedMinutes` son nulos.
  - Hecho cuando: el foco pasa 1 prueba.
  - Evidencia: RED: `Assert.Equal() Failure … Expected: Created / Actual: NotFound`. GREEN: `PostgresApiFactory` (identidad nueva por instancia, `FakeTimeProvider` en 2026-10-12T08:00:00Z sustituido con `ConfigureTestServices`), `PersistenceServiceCollectionExtensions.AddPersistence` (DbContext con `ConnectionStringOptions` y repositorio) en Infrastructure, `TimeEntries/{TimeEntryEndpoints,TimeEntryResponse}.cs` y `Program.cs`: foco 1/1; el JSON crudo trae `clockInAt` = `2026-10-12T08:00:00Z` (termina en `Z`) y `clockOutAt`/`workedMinutes` nulos. V13 resuelta para `DateTime` UTC. Nota: el registro de los casos de uso y `TimeProvider.System` vive en `TimeEntryEndpoints.AddTimeEntries` (Api = composition root, AGENTS §3) y no en el archivo de Infrastructure que listaba la tarea (sin impacto).
- [x] 10.3 **409 por doble entrada · `ClockIn_WhenOpenEntryExists_Returns409ProblemJsonInSpanish`.** Archivos: `src/TimeTracker.Api/Errors/{DomainRuleExceptionHandler,ProblemDetailsServiceCollectionExtensions}.cs`, `Program.cs`. Cubre: TT-42, TT-44 (primer título).
  - RED: `500`. GREEN: `AddProblemDetails` + `AddExceptionHandler<DomainRuleExceptionHandler>()` (un único `is DomainRuleException` → 409 con `Title` y `Message`); `app.UseExceptionHandler()` como primer middleware, antes de `UseCors()`. Comprueba `application/problem+json`, `status` 409, título «Ya existe un fichaje abierto» y `detail` no vacío.
  - Hecho cuando: el foco pasa.
  - Evidencia: RED: `Expected: Conflict / Actual: InternalServerError`. GREEN: `Errors/{DomainRuleExceptionHandler,ProblemDetailsServiceCollectionExtensions}.cs` (`is DomainRuleException` → 409 con `Title`/`Message`; `LoggerMessage` a nivel `Information` sin `EmployeeId`) y `app.UseExceptionHandler()` antes de `UseCors()`: foco 2/2 (`application/problem+json`, `status` 409, título «Ya existe un fichaje abierto», `detail` no vacío). Sonda temporal (borrada): con cabecera `Origin` válida, el 201 y el 409 devuelven `Access-Control-Allow-Origin` (anticipa V11; la comprobación formal sigue siendo manual en 14.6).
- [x] 10.4 **clock-out · `ClockOut_WithOpenEntry_Returns200WithClosedEntry`.** Cubre: TT-38.
  - RED: `404`/`405`. GREEN: `POST clock-out`. Comprueba `clockOutAt` no nulo y `workedMinutes` entero ≥ 0.
  - Hecho cuando: el foco pasa.
  - Evidencia: RED: `Expected: OK / Actual: NotFound`. GREEN: `POST clock-out` con `ClockOutUseCase`: foco 3/3; `clockOutAt` = `2026-10-12T16:00:00Z` y `workedMinutes` = 480 (más estricto que «≥ 0»).
- [x] 10.5 **409 por salida sin entrada · `ClockOut_WithoutOpenEntry_Returns409`.** Cubre: TT-43, TT-44 (segundo título).
  - Verde a la primera (manejador genérico); comprueba «No hay ningún fichaje abierto». Mutación: cambiar el título de `NoOpenTimeEntryException` → falla; revertir.
  - Hecho cuando: evidencia anotada.
  - Evidencia: Verde a la primera (manejador genérico): foco 4/4. Mutación (título de `NoOpenTimeEntryException` → «Sin fichaje»): ROJO (`Expected: "No hay ningún fichaje abierto" / Actual: "Sin fichaje"`); revertida.
- [x] 10.6 **Minutos truncados · `ClockOut_TruncatesWorkedMinutes`** (entrada 08:00:00Z, `FakeTimeProvider` 09:30:30Z → `90`). Cubre: TT-39.
  - Verde a la primera. Mutación: `Math.Round` en `CalculateWorked` → falla; revertir.
  - Hecho cuando: evidencia anotada.
  - Evidencia: Verde a la primera (`ClockOut_TruncatesWorkedMinutes`). **Ajuste:** con el caso único 09:30:30Z la mutación `Math.Round` NO fallaba (el redondeo bancario de 90,5 da 90); se convirtió en Theory con 30 s y 59 s (ambos → 90). Mutación `Math.Round` en `CalculateWorked`: ROJO solo el caso 59 s (`Expected: 90 / Actual: 91`); revertida y foco en verde.
- [x] 10.7 **GET con total · `Get_ReturnsItemsAndTotalWorkedMinutes` y `ClockIn_ThenGet_HasNoLocationAndSameInstant`.** Cubre: TT-40, TT-37.
  - RED: `404`/`405`. GREEN: `GET /api/time-entries` con `TimeEntryListResponse`. TT-37: `FakeTimeProvider` en `2026-10-12T08:00:00.7Z`; el `201` no lleva `Location` y su `clockInAt` (`2026-10-12T08:00:00Z`) coincide con el primer elemento del GET (mutación: quitar el truncado de `Start` → falla; revertir).
  - Hecho cuando: el foco pasa.
  - Evidencia: RED: `Expected: OK / Actual: NotFound` y `JsonReaderException` (sin cuerpo). GREEN: `GET /api/time-entries` con `TimeEntryListResponse`: foco 8/8 (abierta primero con `workedMinutes` nulo; cerrada 60; total 60). TT-37: el 201 no lleva `Location` y `clockInAt` = `2026-10-12T08:00:00Z` coincide con el primer elemento del GET. Mutación (quitar el truncado en `ToUtcSeconds`): ROJO (`Expected: "2026-10-12T08:00:00Z" / Actual: "2026-10-12T08:00:00.7Z"`); revertida.
- [x] 10.8 **Límite de 50 · `Get_With51Entries_Returns50`** (51 ciclos con `FakeTimeProvider`; total correcto). Cubre: TT-26 (nivel API); amenaza API4.
  - Verde a la primera. Mutación: `MaxEntries = 51` → falla; revertir.
  - Hecho cuando: evidencia anotada.
  - Evidencia: Verde a la primera (51 ciclos de entrada/salida con `FakeTimeProvider`: 50 elementos y total 50). Mutación `MaxEntries = 51`: ROJO (`Expected: 50 / Actual: 51`); revertida.
- [x] 10.9 **Identidad ignorada y aislamiento · `TimeEntryIsolationTests`.** Cubre: TT-28, TT-29, TT-30, TT-32, TT-33, TT-34, TT-35; amenazas API1/BOLA.
  - Dos fábricas con distinto `Identity:DevelopmentEmployeeId` sobre la misma base. Pruebas: `Request_WithEmployeeIdQueryOrHeader_IsIgnored` (`?employeeId=B` y `X-Employee-Id: B`; filas comprobadas en la base), `Get_OnlyReturnsOwnEntries`, `ClockIn_OfDifferentEmployees_BothSucceed`, `ClockOut_CannotCloseOthersEntry` (409 y la ajena sigue abierta), `ClockOut_DoesNotTouchOtherEmployee`.
  - Verdes a la primera. Mutación: `FindOpenAsync` sin filtro por empleado → falla `ClockOut_CannotCloseOthersEntry`; revertir.
  - Hecho cuando: el foco pasa y la mutación queda anotada.
  - Evidencia: Cinco pruebas verdes a la primera (`TimeEntryIsolationTests` 5/5; dos fábricas con distinto `EmployeeId` sobre la misma base; las filas se comprueban en PostgreSQL con un contexto aparte). Mutación (`FindOpenAsync` sin filtro por empleado): ROJAS 4 de 5 (`ClockOut_CannotCloseOthersEntry`, `ClockIn_OfDifferentEmployees_BothSucceed`, `Get_OnlyReturnsOwnEntries`, `Request_WithEmployeeIdQueryOrHeader_IsIgnored`); revertida y 5/5.
- [x] 10.10 **500 genérico y sin datos internos · `ErrorHandlingTests`.** Cubre: TT-45, TT-47; amenaza A05/API8.
  - RED: `UnhandledException_Returns500WithoutDetails` con un repositorio falso que lanza «texto-sensible»: el título por defecto de .NET está en inglés. GREEN: `CustomizeProblemDetails` → «Se produjo un error inesperado», sin `detail`; `LoggerMessage` para el inesperado y `Information` sin `EmployeeId` para conflictos (V14: CA1848). `Conflict_BodyHasNoInternalData` (sin `Exception`, `at TimeTracker` ni el `employeeId`): verde a la primera; mutación: añadir `exception.GetType().Name` al `detail` → falla; revertir.
  - Hecho cuando: el foco pasa.
  - Evidencia: RED: `UnhandledException_Returns500WithoutDetails` → `Expected: "Se produjo un error inesperado" / Actual: "An error occurred while processing your r…"`. GREEN: `CustomizeProblemDetails` (500 → título en español, sin `detail`) en `ProblemDetailsServiceCollectionExtensions`: 2/2 (sin «texto-sensible», `InvalidOperationException` ni `at TimeTracker`). `Conflict_BodyHasNoInternalData` verde a la primera; mutación (añadir `rule.GetType().Name` al `detail`): ROJO; revertida. **Desviación menor de diseño §8:** no se añade un `LoggerMessage` propio para el error inesperado: `ExceptionHandlerMiddleware` ya lo registra con la excepción solo en el servidor (un segundo registro duplicaría la entrada); los conflictos sí usan `LoggerMessage` a nivel `Information` sin `EmployeeId` (CA1848 cumplida).
- ~~10.11~~ **Retirada (recorte 2026-10-12):** prueba de CORS conservado en el 409 (antes TT-46, V11). V11 pasa a verificación manual en 14.6 (UI-13); `app.UseExceptionHandler()` sigue antes de `UseCors()` (10.3).
- [x] 10.12 **Contrato en verde y formato de fechas (V13).** Cubre: TT-31, TT-41, SA-26; V13.
  - Declarar `.ProducesProblem(409)` en ambos POST; `dotnet build` regenera `docs/api/openapi.json`. Mutación de TT-31: añadir temporalmente un parámetro `employeeId` → falla; revertir y regenerar.
  - Hecho cuando: `T-AR --filter "FullyQualifiedName~OpenApiContractTests"` en verde y `Select-String 'clock-in','clock-out','format.*date-time' docs\api\openapi.json` encuentra los tres.
  - Evidencia: Declarado `.ProducesProblem(409)` en ambos POST; `dotnet build src/TimeTracker.Api -warnaserror` rc 0 regenera `docs/api/openapi.json`; `OpenApiContractTests` 3/3 en verde; `Select-String` encuentra `clock-in`, `clock-out` y dos `"format": "date-time"` (`clockInAt`, `clockOutAt`). Mutación TT-31 (parámetro `Guid employeeId` en el GET): `ExportedDocument_DoesNotExposeEmployeeId` ROJO; revertida y contrato regenerado (0 apariciones de `employeeId`). V13 resuelta (`DateTime` UTC → `Z`; OpenAPI `date-time`).

## Fase 11: Migración por bandera, salud y carrera de entrada desde la API

- [x] 11.1 **Bandera de migraciones (V10).** Archivos: `src/TimeTracker.Api/Persistence/DatabaseMigrationHostedService.cs`, `src/TimeTracker.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs` (`MigrateDatabaseAsync`), `DatabaseStartupTests.cs`. Cubre: PE-16, PE-17, PE-18; V10.
  - RED: `Startup_WithFlagTrue_OnEmptyDatabase_CreatesSchema` (tabla creada y `clock-in` → `201`), `Startup_WithFlagUnset_DoesNotTouchSchema` (`to_regclass('time_entries')` nulo), `Startup_WithFlagTrue_AndUnreachableServer_Fails` (con `Timeout` corto). GREEN: `IHostedLifecycleService.StartingAsync` lee la bandera y migra. Bases vacías con nombre único creadas en el contenedor del fixture. V10: corre tras `ValidateOnStart` y antes de que Kestrel escuche, también con `WebApplicationFactory`; respaldo: `StartAsync`.
  - Hecho cuando: `T-I --filter "FullyQualifiedName~DatabaseStartupTests"` pasa.
  - Evidencia: RED: `Startup_WithFlagTrue_OnEmptyDatabase_CreatesSchema` → `Expected: Created / Actual: InternalServerError` y `…AndUnreachableServer_Fails` → `Assert.NotNull() Failure: Value is null` (no fallaba). `…FlagUnset_DoesNotTouchSchema` verde a la primera (aún no se migraba). GREEN: `Api/Persistence/DatabaseMigrationHostedService` (`IHostedLifecycleService.StartingAsync`, `LoggerMessage`) y `PersistenceServiceCollectionExtensions.MigrateDatabaseAsync` (Infrastructure; `MigrateAsync`, nunca `EnsureCreated`): `DatabaseMigrationStartupTests` 4/4 (bases vacías de nombre único en el contenedor del fixture; `to_regclass(…)::text` no nulo tras migrar; servidor inexistente con `Timeout=2` → `NpgsqlException`). V10 resuelta: añadida `…AndBlankConnectionString_FailsValidationBeforeMigrating` (la validación de opciones corre antes que la migración: el fallo nombra `ConnectionStrings:Default` y no es de conexión), y `WebApplicationFactory` sí ejecuta `StartingAsync`. **Hallazgo:** `GetDocument.Insider` también ejecuta `StartingAsync`; con la bandera activa en el entorno el build intentaría migrar y fallaría, así que `DocumentGeneration` fuerza `Database:ApplyMigrationsOnStartup=false` (comprobado: build rc 0 con `Database__ApplyMigrationsOnStartup=true` y una cadena inexistente en el entorno). **Nota de las pruebas:** `UseSetting(clave, null)` no quita la clave, la deja vacía (un booleano vacío falla al enlazar), así que para «bandera sin definir» simplemente no se llama a `UseSetting`.
- [x] 11.2 **Salud sin base de datos · `Health_WithUnreachableDatabase_ReturnsHealthy`.** Cubre: PE-19.
  - Verde a la primera. Mutación: migrar siempre al arrancar → falla; revertir.
  - Hecho cuando: evidencia anotada (200 y `{"status":"Healthy"}`).
  - Evidencia: Verde a la primera (`Health_WithUnreachableDatabase_ReturnsHealthy`: 200 y cuerpo exacto `{"status":"Healthy"}` con la base `db.invalid`; `HealthEndpointTests` 5/5). Mutación (migrar siempre al arrancar): ROJAS las 5 pruebas de salud (el arranque falla al conectar) — con `-p:OpenApiGenerateDocuments=false`; revertida.
- [x] 11.3 **Carrera de entrada desde la API · `TimeEntryConcurrencyTests`.** Cubre: PE-21.
  - `ClockIn_TenParallel_OneCreatedNineConflict` (`Task.WhenAll`; listado con un solo fichaje). Verde a la primera. La salida simultánea no se prueba (DD-9 diferida).
  - Hecho cuando: el foco pasa 3 ejecuciones seguidas.
  - Evidencia: Verde a la primera: `ClockIn_TenParallel_OneCreatedNineConflict` (un `201`, nueve `409`, un solo fichaje en el listado), 3 ejecuciones seguidas en verde. La salida simultánea no se prueba (DD-9 diferida).
- [x] 11.4 **Flujo de extremo a extremo · `Flow_ClockInListClockOutList`.** Cubre: TT-48.
  - `201`, `200` (un abierto), `200`, `200` (uno cerrado y total coherente con `workedMinutes`). Verde a la primera.
  - Hecho cuando: el foco pasa.
  - Evidencia: Verde a la primera: `Flow_ClockInListClockOutList` (`201`; GET con un abierto y total 0; `200`; GET con uno cerrado de 135 minutos y total igual).

## Fase 12: Entrega (`.env.example`, compose, CI, imagen)

Pruebas de texto en `tests/TimeTracker.ArchitectureTests/DeliveryArtifactsTests.cs` (sin analizador YAML disponible; ver W1).

- [x] 12.1 **`.env.example`.** Archivos: `.env.example`, `DeliveryArtifactsTests.cs`. Cubre: SC-12, SC-13, SC-14.
  - RED: `EnvExample_ContainsTheSixNewKeys` (`POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`, `ConnectionStrings__Default`, `Identity__DevelopmentEmployeeId`, `Database__ApplyMigrationsOnStartup=false`), `EnvExample_ListsEveryKeyConsumedByCompose`, `EnvExample_HasNoRealValues`, `EnvExample_HasNoActiveAiKeys`, `EnvExample_DoesNotDefineEmptyPortKey`. GREEN: claves con valor vacío o ficticio y comentarios (formato de la cadena y `Host=localhost` en local; el GUID se inventa).
  - Hecho cuando: `T-AR --filter "FullyQualifiedName~DeliveryArtifactsTests"` en verde (las pruebas de `.env.example`).
  - Evidencia: RED: `EnvExample_ContainsTheSixNewKeys` roja (`.env.example no define POSTGRES_DB`); las otras cuatro pruebas de `.env.example` pasaron a la primera (el archivo previo ya las cumplía; la de «claves de compose» pasó vacía porque compose aún no tenía variables y se re-evalúa en 12.2). GREEN: `.env.example` con las seis claves (`POSTGRES_DB=timetracker`, `POSTGRES_USER=timetracker`, contraseña, cadena e identidad vacías con comentarios del formato, `Database__ApplyMigrationsOnStartup=false`): `EnvExample*` 5/5.
- [x] 12.2 **`docker-compose.yml` con `api` y `postgres` (V16).** Archivos: `docker-compose.yml`, `DeliveryArtifactsTests.cs`. Cubre: SA-1, SA-2, SA-3, SA-4; PE-22; V16. **[R3]**
  - RED: `Compose_DefinesApiAndPostgresWithRequiredSettings` (imagen `postgres:18-alpine` igual que el fixture, `healthcheck` con `pg_isready`, volumen `pgdata:/var/lib/postgresql`, `127.0.0.1:5432:5432` y `127.0.0.1:8080:8080`, `service_healthy`, `ASPNETCORE_ENVIRONMENT=Development`) y `Compose_HasNoCredentialLiterals` (`${POSTGRES_*:?…}` interpolados, no `env_file` en `postgres`). GREEN: servicio `postgres` y `api` con `env_file: .env` y `environment` (`Database__ApplyMigrationsOnStartup=true`, `ConnectionStrings__Default` construida con `Host=postgres`).
  - Hecho cuando: la prueba pasa y, con un `.env` temporal (contraseña inventada), `docker compose config --services` imprime exactamente `api` y `postgres`.
  - Evidencia: RED: `Compose_DefinesApiAndPostgresWithRequiredSettings` y `Compose_HasNoCredentialLiterals` rojas con el compose previo (solo `api`). GREEN: `docker-compose.yml` con `postgres` (`postgres:18-alpine` = `PostgresFixture.Image`, comprobado leyendo el fixture; `healthcheck` con `pg_isready`; `pgdata:/var/lib/postgresql`; `127.0.0.1:5432:5432`; `${POSTGRES_*:?…}` sin `env_file`) y `api` (`env_file: .env`; `ASPNETCORE_ENVIRONMENT=Development`; `Database__ApplyMigrationsOnStartup: "true"`; `ConnectionStrings__Default` con `Host=postgres`; `depends_on … service_healthy`; `127.0.0.1:8080:8080`); `DeliveryArtifactsTests` 8/8 (incluida `EnvExample_ListsEveryKeyConsumedByCompose`, ya con las tres variables `POSTGRES_*`). Mutación (`POSTGRES_PASSWORD: literal-ficticio`): `Compose_HasNoCredentialLiterals` ROJO; revertida. Con `.env` temporal (contraseña y GUID inventados) `docker compose config --services` imprime exactamente `postgres` y `api`.
- [x] 12.3 **CI: cobertura de Application.** Archivos: `.github/workflows/ci.yml`, `DeliveryArtifactsTests.cs`. Cubre: SA-21, SA-22.
  - RED: `Ci_RunsApplicationCoverageAfterTestsAndBeforeAudit` (orden textual: restore, format, build, test, `check-coverage.ps1` de Domain, `check-coverage.ps1 … TimeTracker.Application … -MinimumPercent 80`, auditoría, publish, `docker build`; sin `push: true` ni `docker push`). GREEN: paso nuevo tras el umbral de Domain.
  - Hecho cuando: la prueba pasa. La CI **no se ejecuta** (sin remoto): se reproducen sus pasos en 13.1.
  - Evidencia: RED: `Ci_RunsApplicationCoverageAfterTestsAndBeforeAudit` roja (faltaba `-Package TimeTracker.Application -MinimumPercent 80`). GREEN: paso «Comprobar el umbral de cobertura de la aplicación» (pwsh) tras el de Domain y antes de la auditoría; `DeliveryArtifactsTests` 8/8. Mutación (`docker build … && docker push …`): ROJO; revertida. La CI no se ejecuta (sin remoto): sus pasos se reproducen en 13.1.
- [x] 12.4 **Docker: imagen y compose (V15, V16, V18).** Archivos: ninguno (evidencia). Cubre: SA-5 **[R3]**, SA-18, SA-19, SA-20; V15, V16, V18.
  - Imagen: `docker build -t timetracker-api .`; `docker inspect` con `User` ≠ vacío ni `root` y `Healthcheck`; `docker run` con `Cors__AllowedOrigins__0`, `ConnectionStrings__Default` (servidor inexistente), `Identity__DevelopmentEmployeeId` (GUID inventado) y `ASPNETCORE_ENVIRONMENT=Development`: `GET /health` 200 y estado `healthy`; sin esas variables el contenedor termina con código ≠ 0. `docker history --no-trunc` y `docker image inspect` (de la **imagen**, no del contenedor) sin `POSTGRES_PASSWORD`, `ConnectionStrings` ni `Identity__`.
  - Compose **[R3]**: V18 `Get-NetTCPConnection -LocalPort 5432 -ErrorAction SilentlyContinue` vacío; con `.env` temporal, `docker compose up -d --wait` deja `postgres` healthy y la API arranca después; `\dt` muestra `time_entries`; `POST http://127.0.0.1:8080/api/time-entries/clock-in` responde `201` (V15: Npgsql en Alpine); `docker compose exec postgres ls /var/lib/postgresql` confirma la ruta de PostgreSQL 18 (V16). Después `docker compose down -v` y borrar `.env`.
  - Hecho cuando: cada comprobación queda anotada; sin Docker se marca "no probado".
  - Evidencia: Imagen: `docker build -t timetracker-api .` rc 0; `docker inspect`: `User=1654` (APP_UID, no root) y `Healthcheck` (`wget … /health`); `docker history --no-trunc` y `docker image inspect` de la **imagen**: 0 coincidencias de `POSTGRES_PASSWORD`, `ConnectionStrings`, `Identity__` ni de la contraseña ficticia. `docker run` con `Cors__AllowedOrigins__0`, `ConnectionStrings__Default` (`Host=db.invalid…`), `Identity__DevelopmentEmployeeId` (GUID inventado) y `ASPNETCORE_ENVIRONMENT=Development` (puerto 8091 → 8080): `GET /health` 200 `{"status":"Healthy"}` y el estado pasa a `healthy`. Sin esas variables el contenedor termina con código 139 (≠ 0) y `AggregateException` que nombra `Cors:AllowedOrigins`, `ConnectionStrings:Default` e `Identity`; solo con Cors: 139 con `OptionsValidationException` de `ConnectionStrings:Default` e `Identity`. Compose: V18 `Get-NetTCPConnection -LocalPort 5432` (y 8080) vacío; con `.env` temporal (valores ficticios) `docker compose up -d --wait` en 16 s: `postgres` healthy y luego `api` healthy (`127.0.0.1:5432` y `127.0.0.1:8080` solamente); `\dt` muestra `__EFMigrationsHistory` y `time_entries` (migración aplicada por la bandera); `POST http://127.0.0.1:8080/api/time-entries/clock-in` → `201` (V15: Npgsql funciona en `aspnet:10.0-alpine` con globalización invariante), segundo `clock-in` → `409` «Ya existe un fichaje abierto», `clock-out` → `200`, `GET` → `200`; V16: `ls /var/lib/postgresql` → `18` (PostgreSQL 18.6, ruta correcta). `docker compose down -v` ejecutado (contenedores, red y volumen `timetracker_pgdata` eliminados). **Pendiente manual:** el `.env` temporal (solo valores ficticios: contraseña `clave-ficticia-prueba` y un GUID inventado) NO se pudo borrar porque la regla de denegación del entorno bloquea `rm .env`; no se eludió. Está en `.gitignore` y `.dockerignore`; borrarlo a mano. Imagen `timetracker-api` / `timetracker-api:local` aún presentes (se retiran en 13.1).

## Fase 13: Punto de control de la Tanda 3

- [x] 13.1 **Puerta de calidad completa y reproducción de la CI (W1).** Cubre: SA-12, SA-13, SA-14, SA-16, SA-17; V4, V14.
  - `dotnet restore`, `FORMAT`, `BUILD`, `TEST`, `COV-D`, `COV-A` y, con `DOTNET_CLI_UI_LANGUAGE=en`, `dotnet list TimeTracker.sln package --vulnerable --include-transitive` (la salida no contiene `has the following vulnerable packages`), `dotnet publish src/TimeTracker.Api -c Release --no-build -o artifacts/api` y `docker build -t timetracker-api:ci .`. Borrar `artifacts/` y la imagen `:ci`.
  - Hecho cuando: todo termina en 0 y se anota "CI sin ejecutar" y `pwsh` de `ubuntu-latest` sin verificar.
  - Evidencia: Entorno limpio (sin `Cors__`, `ConnectionStrings__`, `Identity__`, `Database__`, `ASPNETCORE_*`, `DOTNET_ENVIRONMENT`). `restore` rc 0; `FORMAT` rc 0 (sin cambios pendientes); `BUILD` (Release, `-warnaserror`) rc 0 con 0 avisos y 0 errores; `TEST` (Release, con Docker/Testcontainers) en verde: Domain 27, Application 14, Arquitectura 40, Integración 66 = 147, 0 fallos; `COV-D` rc 0 (líneas 100,0 %, ramas 100,0 %); `COV-A` rc 0 (100,0 % / 100,0 %; mínimo 80 %); `dotnet list TimeTracker.sln package --vulnerable --include-transitive` (`DOTNET_CLI_UI_LANGUAGE=en`): 0 apariciones de `has the following vulnerable packages`; `dotnet publish src/TimeTracker.Api -c Release --no-build -o artifacts/api` rc 0; `docker build -t timetracker-api:ci .` rc 0. Borrados `artifacts/` y las imágenes `timetracker-api:ci`, `:local` y `:latest` (`TestResults/` se conserva hasta 17.2). CI sin ejecutar (sin remoto) y `pwsh` de `ubuntu-latest` sin verificar (aquí se usó `pwsh` de Windows). V4 resuelta (familia EF 10.0.12 sin NU1605/MSB3277, `BUILD` limpio) y V14 resuelta para Api (CA1848 con `LoggerMessage`; sin avisos CA).
- [x] 13.2 **Evidencias manuales de arranque.** Cubre: SC-9, SC-19, SA-26, SA-27; V8.
  - SC-19: `$env:ASPNETCORE_ENVIRONMENT='Development'; $env:ASPNETCORE_HTTP_PORTS='8081'` más `Cors__AllowedOrigins__0`, `ConnectionStrings__Default` ficticia e `Identity__DevelopmentEmployeeId`, y `dotnet run --no-launch-profile --project src/TimeTracker.Api`; `Invoke-WebRequest http://localhost:8081/health` → 200. SA-27: en `Production` sin `ConnectionStrings__Default` el arranque falla por validación (los valores ficticios no se aplican). SA-26: con el entorno limpio, `dotnet build src/TimeTracker.Api -c Release -warnaserror` y `docs\api\openapi.json` con los tres endpoints.
  - Hecho cuando: salidas anotadas y variables de entorno limpiadas.
  - Evidencia: SC-19: con `ASPNETCORE_ENVIRONMENT=Development`, `ASPNETCORE_HTTP_PORTS=8081`, `Cors__AllowedOrigins__0`, `ConnectionStrings__Default` (`Host=db.invalid…`) e `Identity__DevelopmentEmployeeId` (GUID inventado), `dotnet run --no-launch-profile --project src/TimeTracker.Api` imprime `Now listening on: http://[::]:8081` y `GET http://localhost:8081/health` → 200 `{"status":"Healthy"}`; proceso detenido (puerto 8081 liberado). SA-27: en `Production` solo con Cors, el arranque falla con `OptionsValidationException` (`ConnectionStrings:Default` e `Identity`), 0 apariciones de `Now listening`, código de salida 127 (≠ 0): los valores ficticios de `DocumentGeneration` no se aplican. SA-26/V8: con el entorno limpio, `dotnet build src/TimeTracker.Api -c Release -warnaserror` rc 0 (0 avisos) y `docs\api\openapi.json` contiene `/health`, `/api/time-entries/clock-in`, `/api/time-entries/clock-out` y `/api/time-entries`. Variables de entorno de la sesión: ninguna definida fuera de subshells (comprobado: «entorno limpio»); servidores de compilación cerrados con `dotnet build-server shutdown`.
- [x] 13.3 **Revisión de amenazas, líneas y parada.**
  - Repasar la matriz de amenazas (tabla al final), `Select-String` de `FromSqlRaw|ExecuteSqlRaw|EnableSensitiveDataLogging` en `src/`, líneas reales frente a la previsión y V8, V9, V10, V13, V15, V16, V18, V19 (V11 se comprueba a mano en 14.6; V12 retirada). **PARADA:** el usuario decide sobre la tanda 4 y el recorte.
  - Hecho cuando: checklist completa y evidencias registradas.
  - Evidencia: Matriz de amenazas: identidad provisional desplegada → `Startup_InProduction_FailsNamingIdentity` (9.5) y compose solo en `127.0.0.1`; suplantación → `Request_WithEmployeeIdQueryOrHeader_IsIgnored` + contrato sin `employeeId` (10.9, 10.12); BOLA → `TimeEntryIsolationTests` (10.9) con mutación roja; inyección SQL → `grep -rnE "FromSqlRaw|ExecuteSqlRaw|EnableSensitiveDataLogging|NotImplementedException" src` sin coincidencias; exposición de errores → `UnhandledException_Returns500WithoutDetails`, `Conflict_BodyHasNoInternalData` y `…DoesNotPrintTheValue` (10.10, 9.2); DoS por listado → `Get_With51Entries_Returns50` (10.8); concurrencia de entrada → `ClockIn_TenParallel_OneCreatedNineConflict` (11.3) y `AddAsync_TenParallelOpenInserts…` (7.9), salida simultánea = riesgo residual aceptado; secretos → `docker history`/`inspect` de la imagen sin coincidencias (12.4). Líneas escritas a mano (incluye blancos): src (Api + Infrastructure) ≈ 374, pruebas ≈ 1.004 (de ellas `DeliveryArtifactsTests` 190, `OpenApiContractTests` +93, `TimeEntryEndpointsTests` ≈ 230), compose + `.env.example` + CI ≈ 67 → ≈ 1.445 frente a la previsión de 885–1.205 (≈ +240: más pruebas de arranque, de aislamiento y de entrega que las previstas; ya dentro de `size:exception`). Validaciones: V8 (`GetDocument.Insider` arranca sin entorno → Production; forzado a Development), V9, V10 (`StartingAsync` tras la validación; también lo ejecuta `GetDocument`), V13, V15, V16, V18 y V19 resueltas; V11 sin prueba dedicada: sonda temporal en 10.3 mostró `Access-Control-Allow-Origin` en el 409, pendiente de la comprobación manual 14.6 (UI-13); V12 retirada. **Incidencias:** (1) el `.env` temporal con valores ficticios no pudo borrarse (regla de denegación sobre `rm .env`; no se eludió): borrar a mano `TimeTracker\.env`; (2) no se ejecutó ningún comando Git (ni `git --version`). PARADA de punto de control: tanda 3 completa; se espera la decisión del usuario sobre la Tanda 4 y el recorte.

---

# TANDA 4: Web, documentación y cierre

## Fase 14: Web `/fichajes` (sin bUnit: RED de compilación, de estructura o manual)

- [x] 14.1 **Copia previa y pruebas de estructura (RED).** Archivos: `tests/…/RepositoryConventionsTests.cs`. Cubre: SA-15, UI-11, UI-12.
  - `Save-Rollback 4 …`. Pruebas: `Web_HasTimeEntriesPageClientAndApiBaseUrl`, `Web_DoesNotHardcodeApiUrl` (ningún `.cs` ni `.razor` contiene `localhost:5274`) y la existente `Web_HasNoProjectReferences`. RED: faltan la página, el cliente y la clave.
  - Hecho cuando: la primera falla y se anota el motivo.
  - Evidencia: Copia previa de 7 archivos en `..\TimeTracker.rollback\tanda-4` (Program.cs, NavMenu.razor, Web.csproj, README.md, config.yaml, RepositoryConventionsTests.cs, DeliveryArtifactsTests.cs). RED: `Web_HasTimeEntriesPageClientAndApiBaseUrl` → «Faltan archivos de la Web: src/TimeTracker.Web/Pages/TimeEntries.razor, …/TimeEntries/TimeEntriesApiClient.cs, …/wwwroot/appsettings.json». `Web_HasNoProjectReferences` (existente) y `Web_DoesNotHardcodeApiUrl` pasaron a la primera (aún no había código); mutación de esta última (archivo `Probe.cs` con `localhost:5274`): ROJO «URL de la API fija en el código: src\TimeTracker.Web\Probe.cs»; archivo borrado.
- [x] 14.2 **`ApiBaseUrl` y registro del cliente.** Archivos: `src/TimeTracker.Web/wwwroot/appsettings.json` (`{"ApiBaseUrl": "http://localhost:5274"}`), `src/TimeTracker.Web/Program.cs`. Cubre: UI-10, UI-11.
  - RED manual: con `ApiBaseUrl` ausente o `no-es-una-url` la Web arranca sin error. GREEN: si falta o no es URI absoluta `http`/`https`, `InvalidOperationException` en español que nombra `ApiBaseUrl`; `AddScoped` del cliente sobre un `HttpClient` propio (DD-17); se conserva la línea `HttpClient` de la plantilla.
  - Hecho cuando: `dotnet build src/TimeTracker.Web -warnaserror` rc 0; la comprobación manual de UI-10 se hace en 14.6.
  - Evidencia: `wwwroot/appsettings.json` con `ApiBaseUrl` = `http://localhost:5274`; **añadido además** `wwwroot/appsettings.Development.json` con el mismo valor (petición expresa del usuario; duplicación asumida). `Program.cs`: conserva la línea `HttpClient` de la plantilla, valida `ApiBaseUrl` (URI absoluta http/https) y lanza `InvalidOperationException` en español que nombra `ApiBaseUrl`; `AddScoped` del cliente con `HttpClient` propio (DD-17). `dotnet build src/TimeTracker.Web -warnaserror` rc 0 (0 avisos). UI-10 comprobada en 14.6.
- [x] 14.3 **Cliente tipado y DTO.** Archivos: `src/TimeTracker.Web/TimeEntries/{TimeEntriesApiClient,TimeEntryDto,TimeEntryListDto,ApiProblemDto,TimeEntriesApiException}.cs`. Cubre: UI-7, UI-8, UI-9 (base).
  - `GetAsync`, `ClockInAsync`, `ClockOutAsync` con `System.Net.Http.Json`; ante `409` lee el ProblemDetails y lanza `TimeEntriesApiException(detail)`; ante otro fallo o red caída, mensaje genérico en español. DTO duplicados, sin `ProjectReference`, sin `employeeId`.
  - Hecho cuando: `dotnet build src/TimeTracker.Web -warnaserror` rc 0 y `FORMAT` rc 0.
  - Evidencia: `TimeEntriesApiClient` (`GetAsync`, `ClockInAsync`, `ClockOutAsync` con `System.Net.Http.Json`; `409` → `TimeEntriesApiException(detail)`; otro fallo, red caída, tiempo agotado o cuerpo ilegible → mensaje genérico en español), `TimeEntryDto`, `TimeEntryListDto`, `ApiProblemDto`, `TimeEntriesApiException`. DTO duplicados, sin `ProjectReference`, sin `employeeId`. `dotnet build src/TimeTracker.Web -warnaserror` rc 0 y `FORMAT` rc 0. El cliente implementa `IDisposable` para liberar su `HttpClient`.
- [x] 14.4 **Página `/fichajes` (núcleo).** Archivos: `src/TimeTracker.Web/Pages/TimeEntries.razor`. Cubre: UI-1 a UI-6.
  - Tabla Entrada/Salida/Minutos en el orden de la API; «en curso» para la abierta (sin minutos); total en minutos y h:mm; mensaje de no hay fichajes con total `0`; botones «Fichar entrada» y «Fichar salida» siempre activos (el servidor decide), recarga tras éxito; error visible; horas `yyyy-MM-dd HH:mm 'UTC'` con `CultureInfo.InvariantCulture`.
  - Hecho cuando: `T-AR --filter "FullyQualifiedName~Web"` en verde, `dotnet build src/TimeTracker.Web -warnaserror` y `FORMAT` rc 0.
  - Evidencia: `Pages/TimeEntries.razor` (`@page "/fichajes"`): tabla Entrada/Salida/Minutos en el orden de la API, «en curso», total en minutos y h:mm, «No hay fichajes registrados.» con total 0, botones siempre activos en este paso, recarga tras éxito, alerta de error y horas `yyyy-MM-dd HH:mm 'UTC'` con `CultureInfo.InvariantCulture`. `T-AR --filter "FullyQualifiedName~Web"` 3/3, build `-warnaserror` rc 0 y `FORMAT` rc 0.
- [x] 14.5 **[R1] Pulido Web.** Archivos: `src/TimeTracker.Web/Layout/NavMenu.razor`, `TimeEntries.razor`. Cubre: UI-1 (enlace en menú, DEBERÍA), UI-3/UI-4 (botones deshabilitados durante la petición, DEBERÍA).
  - Enlace «Fichajes» en el menú, estado de carga, botones deshabilitados mientras hay una petición y estilos mínimos. Se dejan `Counter` y `Weather`.
  - Hecho cuando: `dotnet build src/TimeTracker.Web -warnaserror` rc 0.
  - Evidencia: Enlace «Fichajes» en `NavMenu.razor`; estado de carga «Cargando fichajes…» y `busy` que deshabilita ambos botones durante la petición (`finally` lo restablece). Sin estilos propios (clases de Bootstrap de la plantilla). Se dejan `Counter` y `Weather`. Build `-warnaserror` rc 0 y `FORMAT` rc 0. El estado deshabilitado intermedio no se pudo observar en el navegador (la petición local dura milisegundos); ver `verify-report.md`.
- [x] 14.6 **Verificación manual de UI y registro.** Archivos: `openspec/changes/fichaje-extremo-a-extremo/verify-report.md` (sección "Verificación manual de UI"). Cubre: UI-1 a UI-13.
  - Preparar: `docker compose up -d postgres` (o un PostgreSQL local); API con `dotnet run --project src/TimeTracker.Api` (perfil `http`, `Development`, puerto 5274) y las variables `ConnectionStrings__Default`, `Identity__DevelopmentEmployeeId` (GUID nuevo), `Database__ApplyMigrationsOnStartup=true`, `Cors__AllowedOrigins__0=http://localhost:5172`; Web con `dotnet run --project src/TimeTracker.Web`; navegador en `http://localhost:5172/fichajes`.
  - Orden: sin fichajes (UI-2) → «Fichar entrada» (UI-3) → de nuevo (UI-7, mensaje de la API) → «Fichar salida» (UI-4) → de nuevo (UI-8) → repetir hasta dos cerrados y uno abierto (UI-1, UI-5 con `yyyy-MM-dd HH:mm UTC`, UI-6 «en curso», total) → consola sin errores de CORS, incluidos los POST y una respuesta 409 con cabeceras CORS (UI-13, V11) → pestaña Red con base `http://localhost:5274` (UI-11) → detener la API: recargar y pulsar un botón, mensaje genérico (UI-9) → `ApiBaseUrl` = `no-es-una-url` y luego ausente: error con `ApiBaseUrl` (UI-10) → `dotnet list src/TimeTracker.Web/TimeTracker.Web.csproj reference` sin proyectos de `src/` (UI-12).
  - Hecho cuando: cada escenario figura en el informe con pasos, resultado y fecha; variables de entorno limpiadas y procesos detenidos.
  - Evidencia: Ejecutada de verdad: PostgreSQL 18 (`docker run`, puerto 5433, contraseña ficticia), API con `dotnet run` (`Development`, 5274, migraciones por bandera, CORS a `http://localhost:5172`) y Web con `dotnet run` (5172). **Desviaciones:** (1) `mcp__claude-in-chrome` no estaba disponible: se usó Chrome real en modo headless controlado por el protocolo DevTools con un script de Node (sin instalar nada; ejecución real de Blazor WebAssembly, no una simulación); (2) PostgreSQL con `docker run` y no `docker compose up -d postgres`, para no leer el `.env` temporal. Resultado: UI-1 a UI-13 **verificados** (detalle en `verify-report.md`), incluidos CORS en las respuestas 409 (V11 resuelta), «API caída» (mensaje genérico), `ApiBaseUrl` inválida y ausente (consola: «Falta la configuración «ApiBaseUrl»…»; Blazor lo envuelve en `AggregateException`) y `dotnet list … reference` sin proyectos. **No verificado:** aspecto visual y el estado intermedio de botones deshabilitados. Limpieza: procesos `dotnet` y Chrome detenidos, puertos 5172/5274 libres, contenedor `tt-ui-pg` eliminado, `appsettings` restaurados.

## Fase 15: ADR [R2]

Cada ADR: `Estado` y `Fecha` más `## Contexto`, `## Decisión`, `## Consecuencias`, `## Alternativas` (como 0001–0007). Contenido en `design.md` §12.

- [x] 15.1 **RED y ADR 0008 · `docs/adr/0008-persistencia-postgresql-ef-core.md`.** Archivos: `DeliveryArtifactsTests.cs`. Cubre: SA-23.
  - RED: `Adr_Directory_HasTenAdrsWithFourSections` falla (hay 7). GREEN (parcial): ADR 0008 (PostgreSQL 18, EF Core 10, familia en 10.0.12, mapeo Fluent, índice único parcial, migraciones por bandera, Testcontainers).
  - Hecho cuando: el archivo existe con las cuatro secciones (la prueba sigue en rojo hasta 15.3).
  - Evidencia: RED: `Adr_Directory_HasTenAdrsWithFourSections` (en `DeliveryArtifactsTests`; comprueba 10 archivos `0*.md` numerados, `Estado`, `Fecha` y las cuatro secciones) → `Expected: 10 / Actual: 7`. ADR 0008 creado con las cuatro secciones: PostgreSQL 18, EF Core 10, familia en 10.0.12, mapeo Fluent, índice único parcial, migraciones por bandera, Testcontainers, xUnit v2 y **DD-9 (concurrencia de la salida) diferida como riesgo residual**.
- [x] 15.2 **ADR 0009 · `docs/adr/0009-identidad-provisional.md`.** Cubre: SA-23.
  - `ICurrentUser`, `DevelopmentCurrentUser` solo en Development, fallo de arranque en otro entorno, `GetDocument` forzado a Development con valores ficticios (amplía W4 de ADR 0004 sin reescribirlo).
  - Hecho cuando: cuatro secciones presentes.
  - Evidencia: ADR 0009 creado con las cuatro secciones: `ICurrentUser`, `DevelopmentCurrentUser` solo en Development, fallo de arranque en otro entorno, `GetDocument` forzado a Development con valores ficticios (amplía ADR 0004 sin reescribirlo), compose solo para desarrollo.
- [x] 15.3 **ADR 0010 · `docs/adr/0010-tiempo-utc-y-minutos.md`.** Cubre: SA-23.
  - `TimeProvider`, UTC validado, instantes truncados a segundos, minutos truncados, JSON con `Z`, UI en UTC etiquetado.
  - Hecho cuando: `T-AR --filter "FullyQualifiedName~Adr"` en verde y `(Get-ChildItem docs\adr 0*.md).Count` = 10.
  - Evidencia: ADR 0010 creado con las cuatro secciones: `TimeProvider`, UTC validado, truncado a segundos y a minutos, JSON con `Z`, UI en UTC etiquetado. `T-AR --filter "FullyQualifiedName~Adr"` 1/1 en verde; `(Get-ChildItem docs\adr 0*.md).Count` = 10.

## Fase 16: README y `config.yaml`

- [x] 16.1 **[R2] README.** Archivos: `README.md`. Cubre: SA-25, PE-24.
  - RED: `Select-String 'docker compose up','/fichajes','requieren Docker','ConnectionStrings__Default','Identity__DevelopmentEmployeeId','ASPNETCORE_ENVIRONMENT' README.md` no encuentra las seis. GREEN: secciones de probar (Docker obligatorio para integración), ejecutar la API en local con las variables, `docker compose up`, página `/fichajes`, estructura y enlace a los ADR.
  - Hecho cuando: el comando encuentra las seis.
  - Evidencia: RED: nueva prueba `Readme_DocumentsFichajesDockerRequirementAndRequiredVariables` → «README.md no menciona: /fichajes, requieren Docker, ConnectionStrings__Default, Identity__DevelopmentEmployeeId, ASPNETCORE_ENVIRONMENT, dotnet ef». GREEN: README reescrito con aviso de identidad provisional, Docker obligatorio para integración, tabla de variables, ejecución local (compose solo para postgres + `dotnet run`), `dotnet ef`, `docker compose up`, página `/fichajes`, estructura y enlace a los 10 ADR. Sin URLs inventadas (despliegue, vídeo y slides siguen «pendiente»). Prueba en verde.
- [x] 16.2 **`openspec/config.yaml`.** Archivos: `openspec/config.yaml`. Cubre: SA-23 (parte de configuración).
  - Actualizar `testing.layers.integration` (PostgreSQL real con Testcontainers, `postgres:18-alpine`), `coverage.command`/`tiers` (Application 80 con `check-coverage.ps1`), `context.Estado` y las notas de verificación. Último elemento a recortar del bloque R2.
  - Hecho cuando: el YAML es coherente (revisión manual: no hay analizador) y `Select-String 'PostgreSQL|Testcontainers' openspec\config.yaml` encuentra la capa de integración.
  - Evidencia: `openspec/config.yaml` actualizado: `context` (Pruebas con PostgreSQL real y Testcontainers, Estado), `testing.layers.integration` (Testcontainers `postgres:18-alpine`, requiere Docker), `coverage.command_application` (Application 80 con `check-coverage.ps1`), y tres notas en `verify.notes`. Revisión manual del YAML (sin analizador). `Select-String 'PostgreSQL|Testcontainers' openspec\config.yaml` encuentra la capa de integración (líneas 11 y 66).
- [x] 16.3 **[R2] Seguir el README tal cual.** Cubre: SA-24.
  - Ejecutar sus comandos en una sesión limpia: restore, build, test (con Docker) y arranque de la API con las variables indicadas; `GET /health` responde 200. Corregir el README si algún paso falla.
  - Hecho cuando: los pasos funcionan sin conocimiento adicional.
  - Evidencia: Los pasos del README se ejecutaron: `restore`, `dotnet tool restore` (en la sesión de la Tanda 2), `BUILD`, `TEST` (con Docker), `COV-D`/`COV-A`; arranque local equivalente en 14.6 (mismas variables, `dotnet run --launch-profile http`, `/health` 200 y flujo completo); y la ruta `docker compose up --build` (`docker compose up -d --wait` rc 0, `/health` 200, `clock-in` 201 y segundo `clock-in` 409). **Limitación:** no se siguió literalmente el paso «`docker compose up -d postgres` + cadena con la contraseña de `.env`» porque no se lee el `.env` temporal; en su lugar se usó un PostgreSQL con `docker run` y una contraseña propia. Ninguna corrección necesaria en el README.

## Fase 17: Cierre

- [x] 17.1 **Puerta de calidad final.** Cubre: SA-12, SA-13, SA-14; PE-23; V1–V19 (cierre).
  - `dotnet restore`, `FORMAT`, `BUILD`, `TEST`, `COV-D`, `COV-A`, `docker build -t timetracker-api .`, `docker compose config --services` (con `.env` temporal). Revisar la tabla V1–V19 y anotar el estado de cada una.
  - Hecho cuando: todo en 0 y no queda V sin anotar.
  - Evidencia: Entorno limpio (sin `Cors__`, `ConnectionStrings__`, `Identity__`, `Database__`, `ASPNETCORE_*`, `DOTNET_ENVIRONMENT`). `restore` rc 0; `FORMAT` rc 0; `BUILD` (Release, `-warnaserror`) rc 0 con 0 avisos y 0 errores; `TEST` (Release, con Docker): Domain 27, Application 14, Arquitectura 44, Integración 66 = 151 en verde, 0 fallos; `COV-D` rc 0 (líneas 100,0 %, ramas 100,0 %); `COV-A` rc 0 (100,0 % / 100,0 %; mínimo 80 %); `dotnet list … --vulnerable --include-transitive`: 0 apariciones de `has the following vulnerable packages`; `docker build -t timetracker-api .` rc 0; `docker compose config --services` → `postgres`, `api`; `docker compose up -d --wait` sano y `/health` 200. Estado V1–V19: V1, V2, V3, V4, V5, V6, V7, V8, V9, V10, V13, V14, V15, V16, V17, V18 y V19 resueltas (ver sus tareas); **V11 resuelta en 14.6** (las respuestas 409 llevan `Access-Control-Allow-Origin` y la Web muestra su `detail`); V12 retirada (DD-9 diferida). Ninguna V sin anotar.
- [x] 17.2 **Limpieza y trazabilidad final.** Cubre: criterios de éxito de la propuesta.
  - Eliminar `TestResults/`, `artifacts/`, `.env`, contenedores, imágenes temporales y `IllegalReferenceProbe.cs`; confirmar que no se ejecutó ningún comando Git; completar la tabla de trazabilidad con el resultado de cada escenario; dejar la carpeta `..\TimeTracker.rollback` para el archivo.
  - Hecho cuando: las cuatro checklists están completas y no hay escenarios sin resultado.
  - Evidencia: Eliminados `TestResults/`, `artifacts/` (ya no existía), contenedores (`docker compose down -v`, `tt-ui-pg`), imágenes `timetracker-api`, `:local` y `:latest`; `IllegalReferenceProbe.cs` no existe; servidores de compilación cerrados (`dotnet build-server shutdown`); no queda ningún proceso `dotnet` ni `node` lanzado por esta tanda. **Pendiente manual:** el `.env` temporal de la Tanda 3 (solo valores ficticios) sigue en la raíz porque la regla de la máquina bloquea su borrado y no se eludió; el usuario debe borrarlo (`.env` está en `.gitignore` y `.dockerignore`). No se ejecutó ningún comando Git (ni `git --version`). `..\TimeTracker.rollback` se conserva (tanda-1 a tanda-4) para el archivo. Trazabilidad: ver «Resultados de la Tanda 4» más abajo; los demás escenarios conservan el resultado registrado en la Evidencia de sus tareas. Líneas escritas a mano en la Tanda 4 (incluye blancos): src Web ≈ 204, pruebas ≈ 80, ADR 96, README 163 (reescrito), `config.yaml` +4 → ≈ 547 (≈ 589 con `verify-report.md`) frente a la previsión de 500–590.

---

# Mapa de validaciones V1–V19

| ID | Dónde | ID | Dónde |
|---|---|---|---|
| V1 | 1.3, 6.1, 17.1 | V11 | 14.6 (manual, UI-13) |
| V2 | 1.2, 7.1 | V12 | retirada (DD-9 diferida) |
| V3 | 1.2 (acceso), 7.1 (definitiva) | V13 | 10.2, 10.12 |
| V4 | 6.1, 13.1 | V14 | 2.15, 3.12, 5.1, 7.10, 10.10, 13.1 |
| V5 | 7.3 | V15 | 12.4 |
| V6 | 2.15 (cobertura), 7.4 (EF) | V16 | 12.2, 12.4 |
| V7 | 2.15, 4.1 | V17 | 3.12, 5.2 |
| V8 | 9.3, 13.2 | V18 | 12.4 |
| V9 | 9.1, 9.2 | V19 | 9.1, 9.5 |
| V10 | 11.1 | | |

# Matriz de amenazas del diseño (§14) → tareas

| Amenaza | Prueba RED o verificación | Tarea |
|---|---|---|
| Identidad provisional desplegada | `Startup_InProduction_FailsNamingIdentity` | 9.5 |
| Suplantación por parámetro o cabecera | `Request_WithEmployeeIdQueryOrHeader_IsIgnored` | 10.9 |
| BOLA | Pruebas de aislamiento (lectura y cierre) | 10.9, 3.8 |
| Inyección SQL | Ausencia de `FromSqlRaw`/`ExecuteSqlRaw` en `src/` | 7.10, 13.3 |
| Exposición de errores | `UnhandledException_Returns500WithoutDetails`; contraseña ficticia no aparece | 10.10, 9.2 |
| DoS por listado | `Get_With51Entries_Returns50` | 10.8 |
| Integridad por concurrencia (entrada) | Pruebas de carrera de entrada; la salida simultánea queda como riesgo residual aceptado (DD-9 diferida) | 7.9, 11.3 |
| Secretos | `docker history`/`inspect` de la imagen; `.env` solo local | 12.4 |
| Asignación masiva / límite de tasa | N/A / diferido (riesgo residual aceptado) | — |

# Evidencia (se completa en apply)

| Evidencia | Tarea | Resultado |
|---|---|---|
| RED y GREEN reales de los pasos de `TimeEntry` | 2.1–2.15 | Hecho en la Tanda 1: ver "Evidencia" de cada tarea 2.1–2.15 (Domain 27/27; 100 % líneas y ramas) |
| Mutaciones temporales (verdes a la primera) | 2.6, 2.13, 2.14, 3.4, 3.7, 3.8, 3.11, 7.9, 10.5–10.10 | Hechas todas hasta la Tanda 3 (10.5, 10.6, 10.7, 10.8, 10.9, 10.10 con rojo real y reversión; en 10.6 hizo falta añadir el caso de 59 s porque 30 s no discriminaba) |
| Demostraciones únicas de arquitectura | 4.1, 4.3 | Hecho: Application ajena a Logging.Abstractions (4.1), SA-10, SA-11 y MSB4006 (4.3) |
| Acceso a Docker Hub, tag de Ryuk, `postgres --version` | 1.2, 7.1 | Parcial: `docker pull postgres:18-alpine` OK y `postgres (PostgreSQL) 18.6` (1.2); tag de Ryuk pendiente de 7.1 |
| Versiones y familia EF | 1.3, 6.1 | Parcial: versiones de 1.3 confirmadas; familia EF pendiente de 6.1 |
| Migración generada y `FORMAT` | 7.3 | Hecho en la Tanda 2 (7.3); `FORMAT` y `BUILD` siguen en rc 0 tras la Tanda 3 (13.1) |
| Estado de V1–V19 | 17.1 | Hecho en 17.1: V1–V11 y V13–V19 resueltas (V11 en 14.6); V12 retirada |
| Verificación manual de UI (también en `verify-report.md`) | 14.6 | Hecho en la Tanda 4: UI-1 a UI-13 verificados con Chrome real (headless, protocolo DevTools); sin verificar aspecto visual ni el estado intermedio de botones deshabilitados |
| Docker: imagen, compose, puertos | 12.4, 13.2 | Hecho en la Tanda 3: imagen no root con `HEALTHCHECK`, `/health` 200 en contenedor, `docker compose up -d --wait` sano, `dt` con `time_entries`, `clock-in` 201 en 127.0.0.1:8080, V15, V16 y V18 resueltas, sin secretos en la imagen; falta borrar a mano el `.env` temporal |

# Previsión de líneas por tanda (escritas a mano)

| Grupo | Tanda 1 | Tanda 2 | Tanda 3 | Tanda 4 |
|---|---|---|---|---|
| `src` (Domain, Application, Infrastructure, Api, Web) | 240–290 | 100–125 | 375–475 | 250–290 |
| Pruebas (xUnit, arquitectura) | 365–460 | 260–340 | 440–610 | 50–70 |
| Props, `.csproj`, herramienta, `.editorconfig` | 5–10 | 25–40 | — | — |
| Compose, `.env.example`, CI | — | — | 70–120 | — |
| ADR ×3, README, `config.yaml` | — | — | — | 200–230 |
| **Total de la tanda** | **610–760** | **385–505** | **885–1.205** | **500–590** |
| Previsión del diseño §16 | 450–540 | 350–420 | 385–470 | 280–360 |

Total previsto: ≈ 2.380–3.060 (antes del recorte ≈ 2.490–3.210; −110…−150 líneas: tarea 7.8 ≈ −40…−55 entre `src` y pruebas, tareas 9.7, 10.11 y la prueba de salida de 11.3 ≈ −70…−95 de pruebas). Diseño: ≈ 1.465–1.790. Riesgo de presupuesto de 400 líneas: High. Mitigación: `size:exception` aceptado y cuatro puntos de control (5.2, 8.2, 13.3 y 17.2), cada uno con PARADA.

# Trazabilidad de escenarios (124 de 124 mapeados)

Tipo: A = prueba automática; M = evidencia manual registrada; A+M = ambas.

## time-tracking (48)

| ID | Escenario | Tareas | Tipo |
|---|---|---|---|
| TT-1 | Truncado a segundos | 2.4 | A |
| TT-2 | Fichaje abierto válido | 2.1 | A |
| TT-3 | Identificador de empleado vacío | 2.3 | A |
| TT-4 | Instante que no es UTC | 2.2 | A |
| TT-5 | Salida truncada a segundos | 2.10 | A |
| TT-6 | Cierre válido | 2.5 | A |
| TT-7 | Salida igual a la entrada | 2.6, 2.7 | A |
| TT-8 | Salida anterior a la entrada | 2.7 | A |
| TT-9 | Cierre repetido | 2.8 | A |
| TT-10 | Salida que no es UTC | 2.9 | A |
| TT-11 | Minutos exactos | 2.12 | A |
| TT-12 | Truncado hacia abajo | 2.12 | A |
| TT-13 | Menos de un minuto | 2.14 | A |
| TT-14 | Truncado con segundos sobrantes | 2.12 | A |
| TT-15 | Cruce de medianoche | 2.13 | A |
| TT-16 | Entrada abierta | 2.11 | A |
| TT-17 | Primera entrada | 3.2 | A |
| TT-18 | Segunda entrada con una abierta | 3.3 | A |
| TT-19 | Entrada tras cerrar la anterior | 3.4 | A |
| TT-20 | Reloj anterior a la entrada | 3.7 | A |
| TT-21 | Salida con entrada abierta | 3.5 | A |
| TT-22 | Salida sin entrada abierta | 3.6 | A |
| TT-23 | Total solo de cerrados | 3.9 | A |
| TT-24 | Orden descendente | 3.11 | A |
| TT-25 | Sin fichajes | 3.11 | A |
| TT-26 | Más de 50 fichajes | 3.10, 7.5, 10.8 | A |
| TT-27 | Total limitado a los 50 devueltos | 3.11 | A |
| TT-28 | El empleado sale de la configuración | 9.5, 10.9 | A |
| TT-29 | Un employeeId del cliente se ignora | 10.9 | A |
| TT-30 | Una cabecera de empleado se ignora | 10.9 | A |
| TT-31 | El contrato no expone employeeId | 10.1, 10.12 | A |
| TT-32 | Listado propio | 10.9 | A |
| TT-33 | Entradas abiertas simultáneas de empleados distintos | 3.8, 7.6, 10.9 | A |
| TT-34 | No se puede cerrar el fichaje ajeno | 3.8, 10.9 | A |
| TT-35 | Salida sin tocar al otro empleado | 3.8, 10.9 | A |
| TT-36 | Entrada correcta | 10.2 | A |
| TT-37 | Entrada sin Location y coherente con el listado | 10.7 | A |
| TT-38 | Salida correcta | 10.4 | A |
| TT-39 | Minutos truncados en la salida | 10.6 | A |
| TT-40 | Listado con total | 10.7 | A |
| TT-41 | Contrato OpenAPI | 10.1, 10.12 | A |
| TT-42 | Doble entrada | 10.3 | A |
| TT-43 | Salida sin entrada | 10.5 | A |
| TT-44 | Títulos en español | 10.3, 10.5 | A |
| TT-45 | Error inesperado | 10.10 | A |
| TT-47 | Sin datos internos | 10.10 | A |
| TT-48 | Flujo de extremo a extremo | 11.4 | A |
| TT-49 | Cobertura por niveles | 2.15, 3.12, 5.2 | A |

## time-entries-persistence (21)

| ID | Escenario | Tareas | Tipo |
|---|---|---|---|
| PE-1 | Tipos de columna | 7.2, 7.3 | A |
| PE-2 | Restricción CHECK presente | 7.2, 7.3 | A |
| PE-3 | Ida y vuelta del instante | 7.4 | A |
| PE-4 | Índices presentes | 7.2, 7.3 | A |
| PE-5 | Segunda fila abierta rechazada | 7.2, 7.3, 7.6 | A |
| PE-6 | Fila cerrada no bloquea | 7.2, 7.3 | A |
| PE-7 | Conflicto traducido | 7.6 | A |
| PE-11 | Últimos 50 | 7.5 | A |
| PE-12 | Entrada abierta ajena no se devuelve | 7.4 | A |
| PE-13 | Sin cambios pendientes | 7.2, 7.3 | A |
| PE-14 | Migración sobre base vacía | 7.2, 7.3 | A |
| PE-15 | Herramienta local | 6.2 | A |
| PE-16 | Bandera activada | 11.1 | A |
| PE-17 | Bandera por defecto | 11.1 | A |
| PE-18 | Migración fallida impide el arranque | 11.1 | A |
| PE-19 | Salud sin base de datos | 11.2 | A |
| PE-20 | Carrera en el repositorio | 7.9 | A |
| PE-21 | Carrera a través de la API | 11.3 | A |
| PE-22 | Imagen fija | 7.1, 12.2 | A |
| PE-23 | Suite verde con Docker | 7.1, 8.1, 17.1 | A |
| PE-24 | README documenta el requisito | 16.1 | A+M |

## time-entries-ui (13; verificación manual por decisión de la spec, sin bUnit)

| ID | Escenario | Tareas | Tipo |
|---|---|---|---|
| UI-1 | Carga inicial con datos | 14.4, 14.5, 14.6 | M |
| UI-2 | Sin fichajes | 14.4, 14.6 | M |
| UI-3 | Fichar entrada | 14.4, 14.6 | M |
| UI-4 | Fichar salida | 14.4, 14.6 | M |
| UI-5 | Hora etiquetada | 14.4, 14.6 | M |
| UI-6 | Fichaje en curso | 14.4, 14.6 | M |
| UI-7 | Doble entrada | 14.3, 14.6 | M |
| UI-8 | Salida sin entrada | 14.3, 14.6 | M |
| UI-9 | API caída | 14.3, 14.6 | M |
| UI-10 | ApiBaseUrl ausente o inválida | 14.2, 14.6 | M |
| UI-11 | URL base | 14.1, 14.2, 14.6 | A+M |
| UI-12 | Sin referencias de proyecto | 14.1, 14.6 | A+M |
| UI-13 | Flujo de navegador registrado | 14.6 | M |

## solution-architecture (27)

| ID | Escenario | Tareas | Tipo |
|---|---|---|---|
| SA-1 | Servicios definidos | 12.2 **[R3]** | A+M |
| SA-2 | Configuración de postgres | 12.2 **[R3]** | A |
| SA-3 | Puerto de la API solo local | 12.2 **[R3]** | A |
| SA-4 | Sin credenciales en el archivo | 12.2 **[R3]** | A |
| SA-5 | Arranque ordenado | 12.4 **[R3]** | M |
| SA-6 | Referencias esperadas | 4.4 | M |
| SA-7 | Referencia ilegal no compila | 4.3 | M |
| SA-8 | Application sin paquetes | 4.2, 4.4 | A+M |
| SA-9 | Estado conforme | 4.1, 4.2 | A |
| SA-10 | Referencia ilegal detectada | 4.3 | M |
| SA-11 | Paquete en Application detectado | 4.2, 4.3 | A+M |
| SA-12 | Build limpio | 5.1, 13.1, 17.1 | A |
| SA-13 | Pruebas en verde | 5.1, 8.1, 13.1, 17.1 | A |
| SA-14 | Formato verificado | 5.1, 13.1, 17.1 | A |
| SA-15 | Web con la funcionalidad de fichajes | 14.1, 14.4 | A |
| SA-16 | Informe generado | 5.2, 13.1 | A |
| SA-17 | Umbral de Application | 3.12, 5.2, 13.1 | A |
| SA-18 | Construcción | 12.4 | M |
| SA-19 | Salud dentro del contenedor | 12.4 | M |
| SA-20 | Sin secretos en la imagen | 12.4 | M |
| SA-21 | Pasos presentes | 12.3, 13.1 | A |
| SA-22 | Paso de cobertura de Application | 12.3 | A |
| SA-23 | ADR completos | 15.1–15.3, 16.2 **[R2]** | A |
| SA-24 | README ejecutable | 16.3 **[R2]** | M |
| SA-25 | README cubre la novedad | 16.1 **[R2]** | A+M |
| SA-26 | Contrato generado en build | 9.3, 10.12, 13.2 | A+M |
| SA-27 | Sin efecto en la ejecución normal | 9.3, 13.2 | A+M |

## startup-configuration (15)

| ID | Escenario | Tareas | Tipo |
|---|---|---|---|
| SC-1 | Falta la cadena | 9.2 | A |
| SC-2 | Cadena en blanco | 9.2 | A |
| SC-3 | Formato inválido | 9.2 | A |
| SC-4 | El valor no se imprime | 9.2 | A |
| SC-5 | No versionada | 9.4 | A |
| SC-6 | Development con identidad válida | 9.5 | A |
| SC-7 | Development sin identidad | 9.5 | A |
| SC-8 | Identidad con formato inválido | 9.5 | A |
| SC-9 | Fuera de Development | 9.5, 13.2 | A+M |
| SC-10 | Valor por defecto | 9.6 | A |
| SC-11 | Valor no booleano | 9.6 | A |
| SC-12 | Claves documentadas | 12.1 | A |
| SC-13 | Claves nuevas presentes | 12.1 | A |
| SC-14 | Sin claves de IA activas | 12.1 | A |
| SC-19 | Puerto distinto | 13.2 | M |

## Resultados de la Tanda 4 (escenarios cerrados en esta tanda)

- **time-entries-ui (13):** UI-1 a UI-13 verificados a mano con Chrome real (headless, protocolo DevTools) el 2026-10-12; UI-11 y UI-12 además con prueba automática (`Web_DoesNotHardcodeApiUrl`, `Web_HasNoProjectReferences`). Detalle en `verify-report.md`. Sin verificar: aspecto visual y estado intermedio de botones deshabilitados.
- **solution-architecture:** SA-15 (`Web_HasTimeEntriesPageClientAndApiBaseUrl`), SA-23 (`Adr_Directory_HasTenAdrsWithFourSections` y `config.yaml`), SA-25 y PE-24 (`Readme_DocumentsFichajesDockerRequirementAndRequiredVariables`) cumplidos; SA-24 cumplido con la salvedad indicada en 16.3 (no se siguió literalmente el paso de compose con la contraseña del `.env`); SA-12, SA-13, SA-14, PE-23: puerta final de 17.1 (151 pruebas en verde, `BUILD` y `FORMAT` limpios).
- El resto de escenarios (time-tracking, persistencia, startup-configuration, SA-1 a SA-11 y SA-16 a SA-22, SA-26, SA-27) conserva el resultado registrado en la Evidencia de sus tareas (Tandas 1 a 3).

**Escenarios sin mapear: ninguno.** Totales: 48 + 21 + 13 + 27 + 15 = 124 (132 − 8 retirados por el recorte del 2026-10-12). Identificadores retirados, que no se reutilizan ni se renumeran: TT-46 (CORS conservado en el 409), PE-8 (sin columna adicional), PE-9 y PE-10 (dobles salidas), SC-15, SC-16, SC-17 (CORS vigente, ya no modificado) y SC-18 (preflight de POST).

Escenarios cuyo resultado depende de un recorte: SA-1–SA-5 y PE-22 (parte compose) con R3; SA-23 (parcial), SA-24, SA-25 y PE-24 con R2. Requisitos sin escenario propio y su prueba: no traducir otras violaciones únicas (7.6), logging sin `EmployeeId` (10.10), `UseExceptionHandler` antes de `UseCors` (10.3; CORS en el 409 solo a mano en 14.6).

# Resumen de tareas

| Tanda | Fases | Tareas | De ellas recortables |
|---|---|---|---|
| Tanda 1 | 1–5 | 36 | 0 |
| Tanda 2 | 6–8 | 13 (7.8 retirada) | 0 |
| Tanda 3 | 9–13 | 28 (9.7 y 10.11 retiradas) | 12.2 y parte de 12.4 (R3) |
| Tanda 4 | 14–17 | 14 | 14.5 (R1); 15.1–15.3, 16.1, 16.3 (R2) |
| Total | 17 | 91 (antes 94) | |

Orden y paralelismo: todo es secuencial dentro de cada tanda por dependencias de TDD, salvo: 6.2 respecto a 6.1; 9.4 respecto a 9.2–9.3; en la Tanda 3, 12.1 y 12.3 son independientes de las fases 10–11 y pueden hacerse en cualquier momento tras 9.1; en la Tanda 4, las fases 15 y 16 (documentación) no dependen de la Web y pueden redactarse tras el punto de control 13.3. Un único escritor a la vez (AGENTS §2).

# Desviaciones del diseño

- **D1.** `ConnectionStringOptions`, `ConnectionStringOptionsValidator` y `PersistenceServiceCollectionExtensions` pasan de la Tanda 2 a la 3 (9.2, 10.2, 11.1): sin pruebas de arranque no tendrían RED (Strict TDD).
- **D2.** Paso añadido en `TimeEntry`: `Close_TruncatesToWholeSeconds` (2.10) para cubrir TT-5; las Theory de `CalculateWorked` incluyen los valores de la spec (510, 1, 90, 480).
- **D3.** *Retirada (recorte 2026-10-12).* La política CORS vigente ya usa `AllowAnyMethod()`; `CorsServiceCollectionExtensions.cs` no se modifica y no hay tarea 9.7.
- **D4.** `DocumentGeneration` (W4) llega con el primer validador (9.3), no al final: de lo contrario el build de Api, que exporta el contrato arrancando el host, rompe toda la solución.
- **D5.** El 500 genérico en español requiere `CustomizeProblemDetails` (10.10); el título por defecto de .NET está en inglés y el diseño no lo detalla.
- **D6.** Copias de reversión en `..\TimeTracker.rollback` (fuera del repositorio), no dentro, para no alterar el globbing ni `dotnet format`.
- **D7.** Archivos de prueba no listados en §3: `DeliveryArtifactsTests.cs` (`.env.example`, compose, CI, ADR), `EnvironmentIsolation.cs`, `PostgresApiFactory.cs`, `PostgresFixtureTests.cs`; y pruebas nuevas en `RepositoryConventionsTests` (dotnet-tools, appsettings, estructura Web). Evitan verificar solo a mano lo que se puede comprobar con texto.
- **D8.** V3 (Ryuk) solo puede cerrarse en 7.1: el tag lo fija Testcontainers y no se debe inventar en 1.2.
- **D9.** DD-9 (`xmin`) diferida a seguimiento con aprobación del usuario (opción A, 2026-10-12): se retiraron antes de implementar el requisito, PE-8–PE-10 y V12; riesgo residual documentado en `design.md`.
- **D10.** `Identity:DevelopmentEmployeeId` se enlaza como `string?` y se valida con `Guid.TryParse` (9.5) para controlar el mensaje; el binder de `Guid` lanzaría una excepción de conversión distinta.
- **D11.** La previsión ascendente (≈ 2.380–3.060 líneas, unas 105 pruebas nuevas) supera en un 60–70 % la del diseño (1.465–1.790). Sin cambio de alcance, el calendario del TFM (14 días) es el riesgo principal.
- **D12.** La lógica del cliente Web (parseo del 409, mensaje genérico, `ApiBaseUrl`) solo tiene verificación manual (spec: sin bUnit).

# Riesgos

- Calendario: previsión muy superior a la del diseño (D11); el orden de recorte no reduce backend ni pruebas.
- Docker Hub (V2, V3): si `postgres:18-alpine` no se descarga, `17-alpine` obliga a un delta; si fallan ambas, parar.
- Familia EF 10.0.12 frente al runtime 10.0.10 instalado (V4): posibles conflictos de `Microsoft.Extensions.*`.
- Riesgo residual aceptado (DD-9 diferida): doble `clock-out` simultáneo del mismo fichaje; la última escritura fija `clock_out_at`. La entrada está protegida por el índice único parcial.
- El manejador de excepciones podría borrar las cabeceras CORS del 409 (V11): solo se detectaría en la verificación manual de UI (14.6), sin prueba automática.
- `GetDocument.Insider` es más frágil con más validadores (W4, V8): respaldo con `WebApplicationFactory`.
- PE-21 depende del entrelazado de hilos (11.3); PE-20 es la variante determinista (7.9).
- La CI sigue sin ejecutarse y ahora requiere Docker (W1); `pwsh` en `ubuntu-latest` sin verificar.
- Las pruebas de integración fallan sin Docker (por diseño; no se omiten).
- Candidatas a consolidar si hace falta tiempo, solo con aprobación del usuario: 3.8 (duplicada por 10.9) y las pruebas de texto de `DeliveryArtifactsTests`.
