# Tareas: esqueleto-solucion

Fecha: 2026-10-10 · Fase: sdd-tasks · Base: `proposal.md`, 4 especificaciones (53 escenarios), `design.md` (V1–V17), `openspec/config.yaml` y `AGENTS.md`.

Nota de formato: el límite de palabras habitual de este artefacto se supera de forma deliberada. El usuario pidió trazabilidad completa, evidencia y los 9 pasos TDD por tarea.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ≈ 990–1225 escritas a mano (sin plantillas ni `openapi.json`) |
| 400-line budget risk | High |
| Chained PRs recommended | No |
| Suggested split | Entrega única en dos tandas con punto de control (sin Git, sin remoto) |
| Delivery strategy | single-pr |
| Chain strategy | size-exception |

Decision needed before apply: No (el usuario aceptó `size:exception` el 2026-10-10; alcance completo, sin recortes)
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 (Tanda 1) | Núcleo compilable: solución, `WorkedHours`, `/health`, validación de arranque, prueba de arquitectura, cobertura | Entrega única | `dotnet test TimeTracker.sln -c Release --collect:"XPlat Code Coverage" --results-directory TestResults` | `dotnet run --no-launch-profile --project src/TimeTracker.Api` con `ASPNETCORE_HTTP_PORTS=8081` y `GET /health` (tarea 8.1) | Borrar `src/`, `tests/`, `scripts/` y archivos raíz creados |
| 2 (Tanda 2) | Entrega: OpenAPI, Docker, compose, CI, ADR, README, `config.yaml` | Misma entrega | `dotnet test tests/TimeTracker.ArchitectureTests -c Release` y `docker compose config --services` | `docker build -t timetracker-api .` + `docker run` + `GET /health` (tarea 10.2) | Borrar `Dockerfile`, `docker-compose.yml`, `.github/`, `docs/`, `README.md`, `docker image rm`; restaurar `openspec/config.yaml` desde `config.yaml.bak` |

## Convenciones de ejecución

- Sin Git: no se ejecuta ningún comando `git` (ni `git init`, ni remotos). Todo se ejecuta desde la raíz del repositorio con PowerShell.
- Strict TDD: cada tarea de código se ejecuta en orden RED (prueba que falla, se anota el motivo del fallo) → GREEN (código mínimo) → REFACTOR (suite en verde). Las pruebas que pasan a la primera se marcan "verde a la primera" con su justificación o su mutación temporal.
- Las pruebas usan datos inventados (`https://app.example.test`, etc.). Los identificadores de código van en inglés; la documentación, en español.
- Matriz de amenazas del diseño (§12): todas las filas son N/A; no hay pruebas RED adicionales por este concepto.
- Divergencia con `design.md` §3 (decidida aquí, alineada con la propuesta): OpenAPI (paquetes, `AddOpenApi`, `openapi.json` y `OpenApiContractTests`) pertenece a la Tanda 2. La Web se crea antes que `ArchitectureTests` para que la suite no quede en rojo por una causa ajena.
- Los IDs de escenario (SA-n, AH-n, SC-n, WH-n) se definen en la tabla de trazabilidad del final.

---

# TANDA 1: núcleo compilable

## Fase 1: Raíz y solución

- [x] 1.1 Comprobaciones previas (V1 inicio). Archivos: ninguno. Cubre: precondiciones de todos los requisitos.
  - Ejecutar `dotnet --list-sdks` (debe listar 10.0.302 o superior), `dotnet --version` y `Test-Path .\TimeTracker.sln`/`.\src` (deben ser False: proyecto vacío).
  - Hecho cuando: SDK 10 presente y la raíz solo contiene `AGENTS.md` y `openspec/`.
- [x] 1.2 `global.json`, `Directory.Build.props`, `Directory.Packages.props` (vacío salvo `ManagePackageVersionsCentrally=true`). Cubre: SA-1 (global.json, sin ejecutor MTP), SA-7, SA-8 (configuración). 
  - Contenido según design §3 (`10.0.302`, `latestFeature`, `allowPrerelease:false`; `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`, `AnalysisLevel=latest`, `EnforceCodeStyleInBuild`, `Deterministic`). No se añade sección `test`.
  - Las versiones de paquetes se fijan cuando cada tarea añade un paquete, con `dotnet package search` / `dotnet add package` (V1).
  - Hecho cuando: `dotnet --version` desde la raíz devuelve 10.0.x y `Select-String 'test' global.json` no devuelve resultados.
- [x] 1.3 `TimeTracker.sln` (`dotnet new sln --format sln -n TimeTracker`) y `tests/Directory.Build.props` (importa la raíz; `OutputType=Exe`, `IsPackable=false`, paquetes `Microsoft.NET.Test.Sdk`, `xunit.v3`, `xunit.runner.visualstudio`, `coverlet.collector`, `<Using Include="Xunit" />`). Cubre: SA-1 (formato clásico). 
  - Fijar versiones de los 4 paquetes en `Directory.Packages.props` con `dotnet package search` (V1) y anotarlas.
  - Hecho cuando: `Test-Path TimeTracker.sln` es True, `Test-Path TimeTracker.slnx` es False y `dotnet sln TimeTracker.sln list` no falla (lista vacía).
- [x] 1.4 Archivos raíz: `.gitignore` (`dotnet new gitignore` + `.env`, `TestResults/`, `artifacts/`, `coverage*.xml`), `.gitattributes`, `.dockerignore`, `.editorconfig` (`dotnet new editorconfig` + bloque final justificado, sin `end_of_line` ni `charset`; V11), `.env.example`. Cubre: SA-13, SC-2 (parte ignore), SC-10, SC-11.
  - RED: `Get-ChildItem -Force` no muestra los 5 archivos. GREEN: se crean con el contenido de design §3 (`.env.example`: `Cors__AllowedOrigins__0=`; `# ASPNETCORE_HTTP_PORTS=8080`; claves `# Ai__*` solo comentadas).
  - Hecho cuando: `Get-ChildItem -Force -Name .editorconfig,.gitignore,.gitattributes,.dockerignore,.env.example` lista los 5; `Select-String '^\.env$' .gitignore,.dockerignore` da 2 resultados; `Select-String '^Ai__' .env.example` no da ninguno; no hay línea `ASPNETCORE_HTTP_PORTS=` sin `#`.

## Fase 2: Dominio `WorkedHours` (TDD, 9 pasos del diseño)

Cada paso anota en "Evidencia" (final) el resultado RED y GREEN reales (mensaje de error o resumen de `dotnet test`).

- [x] 2.1 Andamiaje: `src/TimeTracker.Domain/TimeTracker.Domain.csproj` (`net10.0`, sin referencias ni paquetes), `tests/TimeTracker.Domain.Tests/TimeTracker.Domain.Tests.csproj` (`ProjectReference` → Domain) y alta de ambos en la `.sln`. Cubre: SA-1, SA-2, WH-11 (proyecto sin dependencias).
  - Hecho cuando: `dotnet build TimeTracker.sln -warnaserror` termina en 0. Resuelve V2 en 2.2 (descubrimiento de pruebas xUnit v3 con VSTest).
- [x] 2.2 Paso 1 · `FromMinutes_WithPositiveValue_ExposesMinutes` (90). Archivos: `tests/TimeTracker.Domain.Tests/WorkedHoursTests.cs`, `src/TimeTracker.Domain/WorkedHours.cs`. Cubre: WH-1, WH-13.
  - RED (de compilación): `WorkedHours` no existe. GREEN: `readonly struct` mínimo con constructor privado, `Minutes` y `FromMinutes`. 
  - Hecho cuando: `dotnet test tests/TimeTracker.Domain.Tests` descubre y pasa 1 prueba (V2: confirma que xUnit v3 + VSTest funcionan; si no, se aplica el respaldo xUnit v2 y se anota).
- [x] 2.3 Paso 2 · `FromMinutes_WithNegativeValue_Throws` (Theory `-1`, `int.MinValue`; `ParamName == "minutes"`). Cubre: WH-3, WH-4, WH-13.
  - RED: no lanza excepción. GREEN: `ArgumentOutOfRangeException.ThrowIfNegative(minutes)`.
  - Hecho cuando: `dotnet test tests/TimeTracker.Domain.Tests` pasa 3 pruebas (casos de la Theory incluidos).
- [x] 2.4 Paso 3 · `FromMinutes_WithZero_IsValid`. Cubre: WH-2, WH-13.
  - Verde a la primera (borde). Rojo demostrado con mutación temporal `ThrowIfNegativeOrZero` → prueba falla → se revierte.
  - Hecho cuando: evidencia de mutación (rojo) y de reversión (verde) anotada.
- [x] 2.5 Paso 4 · `Equals_SameMinutes_AreEqualWithSameHash` (con `==`, `Equals` y `GetHashCode`). Cubre: WH-9, WH-13.
  - RED (de compilación): un `struct` no tiene `==`. GREEN: pasar a `readonly record struct`.
  - Hecho cuando: `dotnet test tests/TimeTracker.Domain.Tests` en verde.
- [x] 2.6 Paso 5 · `Equals_DifferentMinutes_AreNotEqual` (60 vs 61). Cubre: WH-10, WH-13.
  - Verde a la primera; justificación registrada: pertenece al mismo contrato que el paso 4.
  - Hecho cuando: prueba en verde y justificación anotada.
- [x] 2.7 Paso 6 · `Add_TwoValues_ReturnsExactSumAndKeepsOperands` (90 + 45 = 135; operandos intactos). Cubre: WH-5, WH-13.
  - RED (de compilación): no existe `operator +`. GREEN: `operator +` sin `checked`.
  - Hecho cuando: prueba en verde.
- [x] 2.8 Paso 7 · `Add_BeyondIntMaxValue_ThrowsOverflow` (`int.MaxValue` + 1). Cubre: WH-7, WH-13.
  - RED: el resultado se envuelve y no lanza. GREEN: `checked(left.Minutes + right.Minutes)`.
  - Hecho cuando: prueba en verde con `OverflowException`.
- [x] 2.9 Paso 8 · `Add_WithZero_IsNeutral` (ambos órdenes) y `Add_UpToIntMaxValue_ReturnsIntMaxValue` (`int.MaxValue - 1` + 1). Cubre: WH-6, WH-8, WH-13.
  - Verdes a la primera (borde); rojo demostrado con mutación temporal (p. ej. `left.Minutes + right.Minutes + 1` falla ambas) → se revierte.
  - Hecho cuando: evidencia de mutación y reversión anotada.
- [x] 2.10 Paso 9 · REFACTOR: cuerpos de expresión, nombres, comentarios del porqué. Si el analizador CA2225 exige alternativo con nombre, se añade `Add(left, right)` con su prueba (V12), sin suprimir la regla. Cubre: WH-13, SA-8 (analizadores).
  - Hecho cuando: `dotnet build TimeTracker.sln -warnaserror` y `dotnet test tests/TimeTracker.Domain.Tests` terminan en 0.

## Fase 3: Script de cobertura

- [x] 3.1 `scripts/check-coverage.ps1` (parámetros `-ResultsDirectory`, `-Package`, `-MinimumPercent`; toma el mejor `line-rate`/`branch-rate` entre informes; 0 ramas = 100 %; código ≠ 0 con mensaje claro). Cubre: SA-12, WH-12; V4 (parte script), R3.
  - RED: `pwsh scripts/check-coverage.ps1 -ResultsDirectory TestResults -Package TimeTracker.Domain -MinimumPercent 100` falla (script ausente / sin informes). GREEN: se genera `dotnet test tests/TimeTracker.Domain.Tests --collect:"XPlat Code Coverage" --results-directory TestResults` y el script termina en 0. REFACTOR: prueba negativa con `-MinimumPercent 101` → código ≠ 0.
  - Si Domain no llega a 1 (miembros sintetizados del record): respaldo `tests/coverage.runsettings` con `ExcludeByAttribute=CompilerGeneratedAttribute` y `--settings`.
  - Hecho cuando: el script devuelve 0 con 100 % y ≠ 0 con 101 %.

## Fase 4: Proyectos vacíos

- [x] 4.1 `src/TimeTracker.Application/TimeTracker.Application.csproj` (→ Domain), `src/TimeTracker.Infrastructure/TimeTracker.Infrastructure.csproj` (→ Application), `tests/TimeTracker.Application.Tests/TimeTracker.Application.Tests.csproj` (→ Application); alta en `.sln`. Cubre: SA-1, SA-2, SA-3; V3 y V15 (parte compilación).
  - RED: `dotnet sln TimeTracker.sln list` no lista esos proyectos. GREEN: se crean y compilan sin avisos.
  - Hecho cuando: `dotnet build TimeTracker.sln -warnaserror` en 0 y `dotnet test tests/TimeTracker.Application.Tests` termina en 0 sin pruebas (V3; si VSTest falla con 0 pruebas, se anota y se aplica respaldo).

## Fase 5: Api e integración (cada prueba en rojo primero)

- [x] 5.1 Andamiaje: `dotnet new web -o src/TimeTracker.Api` (`Program.cs` mínimo, sin endpoints; `appsettings*.json` sin CORS), `ProjectReference` → Application e Infrastructure, `tests/TimeTracker.Api.IntegrationTests/` (`.csproj` con `Microsoft.AspNetCore.Mvc.Testing` y → Api; `ApiFactory.cs` con `WebApplicationFactory<Program>` y `UseSetting("Cors:AllowedOrigins:0","https://app.example.test")`). Cubre: SA-1, SA-3; V1 (Mvc.Testing), V7.
  - Si .NET 10 no genera `public partial class Program`, se añade (V7). Fijar versión de `Microsoft.AspNetCore.Mvc.Testing` en `Directory.Packages.props`.
  - Hecho cuando: `dotnet build TimeTracker.sln -warnaserror` en 0 y V7 anotado (¿partial Program? ¿ASP0027?).
- [x] 5.2 `/health` · `HealthEndpointTests.Get_ReturnsOkWithHealthyJson` y `Post_ReturnsMethodNotAllowed`. Archivos: `tests/.../HealthEndpointTests.cs`, `src/TimeTracker.Api/Health/HealthResponse.cs`, `Health/HealthEndpoints.cs`, `Program.cs`. Cubre: AH-1, AH-2, AH-7; V17.
  - RED: la ruta devuelve 404 (GET y POST). GREEN: `HealthResponse(string Status)` y `MapGet("/health")` con `TypedResults.Ok(new HealthResponse("Healthy"))`, `MapHealthEndpoints()` en `Program.cs`. Se compara el *media type* `application/json`.
  - Hecho cuando: `dotnet test tests/TimeTracker.Api.IntegrationTests --filter HealthEndpointTests` pasa; V17 anotado (`POST /health` = 405; si no, se ajusta la ruta).
- [x] 5.3 Sin información sensible · `Get_BodyHasOnlyStatusProperty`, `Get_HeadersDoNotExposeMachineNameOrConfiguration`. Archivos: `HealthEndpointTests.cs`, `Program.cs` (`ConfigureKestrel(o => o.AddServerHeader = false)`). Cubre: AH-3, AH-4.
  - La prueba del cuerpo pasa a la primera; rojo demostrado añadiendo temporalmente una propiedad a `HealthResponse` → falla → se revierte. La de cabeceras: RED si aparece `Server`; GREEN con `AddServerHeader=false`; comprueba que no hay `Environment.MachineName` en ninguna cabecera.
  - Hecho cuando: ambas pruebas en verde y evidencia de la mutación anotada.
- [x] 5.4 Regresión demostrada una vez · cambiar temporalmente el cuerpo a `"Unhealthy"` (o quitar el mapeo). Cubre: AH-8.
  - Hecho cuando: `Get_ReturnsOkWithHealthyJson` falla con la mutación (salida guardada) y vuelve a pasar tras revertir.
- [x] 5.5 Arranque inválido (clave ausente) y enlace tipado · `StartupConfigurationTests.Startup_WithoutAllowedOrigins_FailsNamingKey` y `Startup_WithValidOrigin_BindsTypedOptions`. Archivos: `tests/.../StartupConfigurationTests.cs`, `src/TimeTracker.Api/Configuration/ApiCorsOptions.cs`, `ApiCorsOptionsValidator.cs`, `CorsServiceCollectionExtensions.cs`, `Program.cs` (`AddRestrictiveCors()`). Cubre: SC-1, SC-3, SC-8, SC-9; V16.
  - RED: el binding no compila (`ApiCorsOptions` no existe) y sin validación la API arranca sin la clave (la prueba de fallo no recibe excepción). GREEN: opciones con `SectionName="Cors"`, `AddOptions<ApiCorsOptions>().BindConfiguration("Cors").ValidateOnStart()`, validador que falla si la lista es nula o vacía con mensaje `Cors:AllowedOrigins: ...` (español, sin valores).
  - La prueba recorre `InnerException` hasta `OptionsValidationException` (V16: se anota el tipo real). Debe quedar aislada de variables `Cors__AllowedOrigins__*` del entorno (R7).
  - Hecho cuando: ambas pruebas pasan y el mensaje contiene `Cors:AllowedOrigins`.
- [x] 5.6 Orígenes inválidos · `Startup_WithInvalidAllowedOrigins_Fails` (Theory: `""`, `*`, `app.example.test/ruta`, `" "`, `ftp://app.example.test`, `https://a.test/ruta`, `https://a.test/`). Cubre: SC-4, SC-5, SC-6, SC-8.
  - RED: los casos no fallan con el validador mínimo. GREEN: reglas del diseño §5 (blanco, `*`, URI absoluta, esquema http/https, `AbsolutePath == "/"`, sin consulta/fragmento/usuario, sin `/` final), añadidas de una en una. Las pruebas comprueban que el mensaje nombra `Cors:AllowedOrigins:<índice>` y **no** repite el valor inválido. REFACTOR: extraer helpers sin cambiar comportamiento.
  - Hecho cuando: `dotnet test tests/TimeTracker.Api.IntegrationTests --filter StartupConfigurationTests` en verde.
- [x] 5.7 Política CORS · `CorsPolicyTests.OnlyConfiguredOrigin_ReceivesAllowOriginHeader` (`https://app.example.test` recibe `Access-Control-Allow-Origin`; `https://otro.example.test` no). Archivos: `tests/.../CorsPolicyTests.cs`, `CorsServiceCollectionExtensions.cs`, `Program.cs` (`app.UseCors()`). Cubre: SC-7.
  - RED: sin política no hay cabecera. GREEN: `AddOptions<CorsOptions>().Configure<IOptions<ApiCorsOptions>>` con `WithOrigins(...).AllowAnyHeader().AllowAnyMethod()`, sin credenciales.
  - Hecho cuando: la prueba pasa y las demás siguen en verde.

## Fase 6: Web

- [x] 6.1 `dotnet new blazorwasm -o src/TimeTracker.Web` (sin PWA ni autenticación), alta en `.sln`, versiones de `PackageReference` trasladadas a `Directory.Packages.props` (NU1008). Archivos: `src/TimeTracker.Web/*`, `Directory.Packages.props`, `.editorconfig` (ajuste justificado solo si aparece un aviso; V12, R2). Cubre: SA-1, SA-3, SA-9, V1 (paquetes WebAssembly).
  - RED: `dotnet build src/TimeTracker.Web -warnaserror` falla con NU1008 / avisos. GREEN: versiones centralizadas; nunca se toca `TreatWarningsAsErrors` ni los archivos de la plantilla.
  - Hecho cuando: `dotnet build TimeTracker.sln -warnaserror` en 0 con 0 avisos y `dotnet sln TimeTracker.sln list` muestra 9 proyectos.

## Fase 7: Prueba de arquitectura y demostraciones

- [x] 7.1 Andamiaje: `tests/TimeTracker.ArchitectureTests/TimeTracker.ArchitectureTests.csproj` (→ Domain, Application, Infrastructure, Api; `AssemblyMetadata("RepositoryRoot","$(MSBuildThisFileDirectory)../../")`) y `RepositoryRoot.cs`; alta en `.sln`. Cubre: SA-1.
  - Hecho cuando: `dotnet build TimeTracker.sln -warnaserror` en 0.
- [x] 7.2 `DependencyRuleTests.ProductionAssembly_ReferencesOnlyAllowedAssemblies` (Theory sobre los 4 ensamblados; permitidos: Domain ∅, Application {Domain}, Infrastructure {Application, Domain}, Api {Application, Infrastructure, Domain}) y `Domain_ReferencesOnlyBaseFramework`. Cubre: SA-5, WH-11; V15 (`Assembly.Load` por nombre).
  - RED: primera ejecución con la tabla de permitidos vacía → falla listando las referencias. GREEN: tabla correcta. El mensaje de fallo lista cada referencia ilegal.
  - Hecho cuando: `dotnet test tests/TimeTracker.ArchitectureTests` en verde; V15 anotado (carga de proyectos vacíos y compilación sin avisos).
- [x] 7.3 `RepositoryConventionsTests`: `Web_HasNoProjectReferences`, `AllProjects_TargetNet10` (9 `.csproj`), `PackageReferences_DoNotDeclareVersion`. Cubre: SA-2, SA-3, SA-5, SA-7.
  - RED: se ejecuta primero con un `.csproj` esperado inexistente (p. ej. ruta mal escrita) → falla; GREEN con las rutas reales. El rojo del atributo `Version` se demuestra en 7.7.
  - Hecho cuando: `dotnet test tests/TimeTracker.ArchitectureTests` en verde.
- [x] 7.4 Demostración única del fallo de la prueba de arquitectura (design §4). Cubre: SA-6.
  - Añadir temporalmente a Domain `<FrameworkReference Include="Microsoft.AspNetCore.App" />` y `IllegalReferenceProbe.cs` con `typeof(Microsoft.Extensions.Logging.ILogger)`; ejecutar `dotnet test tests/TimeTracker.ArchitectureTests` → ROJO con la referencia ilegal en el mensaje (guardar salida en Evidencia); eliminar ambas cosas y confirmar VERDE.
  - Hecho cuando: evidencia roja y verde registrada y `Test-Path src\TimeTracker.Domain\IllegalReferenceProbe.cs` es False.
- [x] 7.5 Demostración del ciclo Domain → Application. Cubre: SA-4.
  - Añadir temporalmente `ProjectReference` Domain → Application; `dotnet build TimeTracker.sln` → falla por dependencia circular (guardar salida); revertir y confirmar build en verde.
  - Hecho cuando: evidencia registrada y `Select-String 'Application' src\TimeTracker.Domain\TimeTracker.Domain.csproj` sin resultados.
- [x] 7.6 Demostración de aviso como error. Cubre: SA-8.
  - Añadir temporalmente una variable local sin usar en `WorkedHours.cs`; `dotnet build TimeTracker.sln -warnaserror` → falla (guardar salida); revertir y confirmar verde.
  - Hecho cuando: evidencia registrada.
- [x] 7.7 Demostración de versión no centralizada. Cubre: SA-7.
  - Añadir temporalmente `Version="1.0.0"` a un `PackageReference` de un `.csproj` (restore puede fallar por CPM; si el restore lo impide, se anota y la prueba se ejecuta con `--no-restore`); `PackageReferences_DoNotDeclareVersion` → ROJO; revertir → VERDE.
  - Hecho cuando: evidencia registrada y búsqueda `Select-String 'PackageReference.*Version=' -Path (Get-ChildItem -Recurse -Filter *.csproj).FullName` sin resultados.
- [x] 7.8 Evidencia manual de estructura. Cubre: SA-1, SA-3.
  - `dotnet sln TimeTracker.sln list` (9 proyectos exactos), `Test-Path TimeTracker.slnx` (False) y `dotnet list <proyecto> reference` para los 5 proyectos de `src/`. Resultado anotado.
  - Hecho cuando: coinciden con la lista permitida y Web no lista proyectos de `src/`.

## Fase 8: Punto de control Tanda 1

- [x] 8.1 Evidencia manual del puerto (startup-configuration). Cubre: SC-12; V8.
  - `$env:Cors__AllowedOrigins__0="https://app.example.test"; $env:ASPNETCORE_HTTP_PORTS=8081; dotnet run --no-launch-profile --project src/TimeTracker.Api` y, en otra terminal, `Invoke-WebRequest http://localhost:8081/health` → 200. Anotar salida y limpiar variables de entorno. V8: si se define `ASPNETCORE_URLS`, tiene prioridad (anotar).
  - Hecho cuando: la evidencia (código 200 y cuerpo) figura en la sección Evidencia.
- [x] 8.2 Puerta de calidad completa. Cubre: SA-9, SA-10, SA-11; V11, V12.
  - `dotnet restore TimeTracker.sln`; `dotnet format TimeTracker.sln --verify-no-changes --no-restore`; `dotnet build TimeTracker.sln -c Release --no-restore -warnaserror`; `dotnet test TimeTracker.sln -c Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults`.
  - Si `dotnet format` reporta cambios en plantillas (V11) o avisos de CA1707/CA1515/CA2225/CA1819 (V12), se ajusta la regla en `.editorconfig` con comentario; nunca se desactiva `TreatWarningsAsErrors`.
  - Hecho cuando: los 4 comandos terminan con código 0, el build muestra 0 avisos y 0 errores y las pruebas de WorkedHours, `/health`, arranque y regla de dependencias pasan.
- [x] 8.3 Cobertura. Cubre: SA-12, WH-12; V2, V4.
  - `pwsh scripts/check-coverage.ps1 -ResultsDirectory TestResults -Package TimeTracker.Domain -MinimumPercent 100`; comprobar `Get-ChildItem TestResults -Recurse -Filter coverage.cobertura.xml` (uno por proyecto de pruebas: 4) y `line-rate="1"` / `branch-rate="1"` para Domain en el informe de `Domain.Tests`.
  - Hecho cuando: el script termina en 0 y hay 4 informes Cobertura.
- [x] 8.4 Revisión del punto de control y limpieza. Cubre: SA-13, SC-2, SC-10; V1, V3, V15 (cierre).
  - Revisar `Select-String` de `appsettings*.json` (sin `Cors:AllowedOrigins` ni secretos), `.env` en `.gitignore`/`.dockerignore`, claves de `.env.example` frente a las consumidas por la API (`Cors__AllowedOrigins__*`, `ASPNETCORE_HTTP_PORTS`). Confirmar versiones fijadas (V1) y eliminar `TestResults/` si procede.
  - **PARADA:** si la Tanda 1 consumió más de la mitad del tiempo disponible, el usuario decide si prioriza el cambio `fichaje-extremo-a-extremo` y aplaza la Tanda 2 (supuesto de la propuesta).
  - Hecho cuando: checklist de la Tanda 1 completa y evidencias registradas.

---

# TANDA 2: entrega

## Fase 9: OpenAPI exportado

- [x] 9.1 `OpenApiContractTests.ExportedDocument_ContainsHealthGet200` (lee `docs/api/openapi.json` con `RepositoryRoot` y comprueba `paths["/health"].get.responses["200"]`). Archivos: `tests/TimeTracker.ArchitectureTests/OpenApiContractTests.cs`, `src/TimeTracker.Api/TimeTracker.Api.csproj` (`Microsoft.AspNetCore.OpenApi`, `Microsoft.Extensions.ApiDescription.Server` con `PrivateAssets=all`, `OpenApiDocumentsDirectory`, `OpenApiGenerateDocumentsOptions=--file-name openapi`), `Program.cs` (`AddOpenApi()` y `MapOpenApi()` solo en Development), `docs/api/openapi.json` (generado). Cubre: AH-5; V1 (OpenApi), V5.
  - RED: el archivo no existe y la prueba falla. GREEN: paquetes y propiedades; `dotnet build` genera el documento. Comprobar que la generación no dispara `ValidateOnStart` (V5); si falla, respaldo: prueba que obtiene el documento con `WebApplicationFactory` y lo compara con el archivo.
  - Hecho cuando: `dotnet build TimeTracker.sln -warnaserror` en 0, `Test-Path docs\api\openapi.json` True, JSON válido (`Get-Content docs\api\openapi.json -Raw | ConvertFrom-Json`) y la prueba pasa. No se edita a mano.
- [x] 9.2 Regeneración tras cambiar un endpoint (una vez). Cubre: AH-6.
  - Añadir temporalmente un endpoint `MapGet("/probe")`; `dotnet build` → `openapi.json` contiene `/probe`; retirarlo y recompilar → desaparece.
  - Hecho cuando: evidencia de aparición y desaparición registrada y `Select-String '/probe' docs\api\openapi.json` sin resultados.

## Fase 10: Docker

- [x] 10.1 Comprobaciones previas y `Dockerfile`. Archivos: `Dockerfile`. Cubre: SA-14 (estructura); V9, V10, V6.
  - Previo: `docker --version`; `docker run --rm mcr.microsoft.com/dotnet/sdk:10.0 dotnet --version` (≥ 10.0.302; si no, no copiar `global.json`, V10). 
  - RED: `docker build -t timetracker-api .` falla (no existe `Dockerfile`). GREEN: multi-stage del diseño §8 (`sdk:10.0` → `aspnet:10.0-alpine`; `ENV ASPNETCORE_HTTP_PORTS=8080`; `EXPOSE 8080`; `USER $APP_UID`; `HEALTHCHECK` con `wget`; `ENTRYPOINT`). Publicar con `-p:OpenApiGenerateDocuments=false` (V6).
  - V9: `docker run --rm --entrypoint sh timetracker-api -c "command -v wget"`; si falta `APP_UID`/`wget`, respaldo `aspnet:10.0` + curl.
  - Hecho cuando: `docker build -t timetracker-api .` termina en 0.
- [x] 10.2 Comportamiento del contenedor. Cubre: SA-14, SA-15, SC-3 (en contenedor), SC-12 (variante contenedor).
  - `docker inspect timetracker-api --format "{{.Config.User}}"` (no vacío ni `root`) y `{{json .Config.Healthcheck}}` (presente).
  - `docker run -d --name tt-api -e Cors__AllowedOrigins__0=https://app.example.test -p 8080:8080 timetracker-api`; `Invoke-WebRequest http://localhost:8080/health` → 200 y, tras `start-period`, `docker inspect --format "{{.State.Health.Status}}" tt-api` = `healthy`.
  - Repetir con `-e ASPNETCORE_HTTP_PORTS=8081 -p 8081:8081`. Sin `Cors__AllowedOrigins__0`, el contenedor termina con código ≠ 0 y su log nombra `Cors:AllowedOrigins`. Eliminar contenedores de prueba.
  - Hecho cuando: todas las comprobaciones anteriores anotadas; si Docker no está disponible, se marca "no probado".
- [x] 10.3 Sin secretos en la imagen. Cubre: SA-16.
  - `docker history --no-trunc timetracker-api` y `docker inspect timetracker-api` no contienen valores de `.env` ni claves de `.env.example` con valor (`Select-String`).
  - Hecho cuando: búsqueda sin coincidencias.
- [x] 10.4 `docker-compose.yml` con solo `api` (`build: .`, `image: timetracker-api:local`, `env_file: .env`, `ports: ["8080:8080"]`). Cubre: SA-17.
  - RED: `docker compose config --services` falla (no hay archivo). GREEN: archivo creado; para validar, copiar `.env.example` a `.env` temporal con un origen ficticio y borrarlo después (no se versiona).
  - Hecho cuando: `docker compose config --services` imprime exactamente `api`.

## Fase 11: CI

- [x] 11.1 `.github/workflows/ci.yml`: disparadores `push` a `main` y `pull_request`; un job en `ubuntu-latest` con los 10 pasos del diseño §9 (checkout, `actions/setup-dotnet` con `global-json-file`, restore, format, build, test con cobertura, `check-coverage.ps1` con `pwsh`, auditoría `dotnet list ... --vulnerable --include-transitive` que falla si informa vulnerables, publish, `docker build` sin push). Cubre: SA-18; V13, V14.
  - RED: `Test-Path .github\workflows\ci.yml` False. GREEN: archivo creado. Versiones de `actions/checkout` y `actions/setup-dotnet` fijadas tras verificarlas (V14); si no se pueden verificar, se anota "sin verificar".
  - V13: ejecutar `dotnet list TimeTracker.sln package --vulnerable --include-transitive` y anotar texto y código de salida (0 aunque haya hallazgos); decidir la condición de fallo según ese texto.
  - Hecho cuando: YAML válido (parse con una herramienta disponible, p. ej. `python -I -c "import yaml,sys; yaml.safe_load(open('.github/workflows/ci.yml'))"`; si no hay, revisión manual anotada) y sin `push: true` ni `docker push`.
- [x] 11.2 Reproducción local de los pasos de CI (la CI no se ejecuta sin remoto, R4). Cubre: SA-18.
  - Ejecutar en PowerShell los pasos 3–10: restore, format, build, test+cobertura, `check-coverage.ps1`, auditoría (revisada a ojo), `dotnet publish src/TimeTracker.Api -c Release --no-build -o artifacts/api`, `docker build -t timetracker-api:ci .`. Borrar `artifacts/` y la imagen `:ci`.
  - Hecho cuando: todos los pasos terminan en 0 y el resultado queda anotado junto con la advertencia "CI sin ejecutar".

## Fase 12: ADR

Cada ADR: encabezado con Estado y Fecha y las 4 secciones `## Contexto`, `## Decisión`, `## Consecuencias`, `## Alternativas`. Contenido en design §10. RED común: el comando de verificación falla porque los archivos no existen.

Verificación: `Get-ChildItem docs\adr -Filter 000*.md | ForEach-Object { $t = Get-Content $_ -Raw; foreach ($s in '## Contexto','## Decisión','## Consecuencias','## Alternativas') { if ($t -notmatch [regex]::Escape($s)) { "$($_.Name): falta $s" } } }` sin salida.

- [x] 12.1 `docs/adr/0001-formato-de-adr.md` y `docs/adr/0002-net10.md`. Cubre: SA-19.
  - Hecho cuando: ambos con las 4 secciones; 0002 menciona LTS hasta 2028-11-14, SDK 10.0.302 y `latestFeature`.
- [x] 12.2 `docs/adr/0003-monolito-modular-y-regla-de-dependencias.md` y `docs/adr/0004-minimal-apis-y-openapi.md`. Cubre: SA-19.
  - Hecho cuando: ambos con las 4 secciones; 0003 recoge `.sln` clásico y la prueba por reflexión/XML; 0004 el contrato exportado.
- [x] 12.3 `docs/adr/0005-frontend-wasm-y-cors.md`, `docs/adr/0006-pila-de-pruebas.md` y `docs/adr/0007-configuracion-y-validacion-al-arrancar.md`. Cubre: SA-19.
  - Hecho cuando: los 7 ADR existen (`(Get-ChildItem docs\adr 000*.md).Count` = 7) y la verificación no produce salida; 0006 documenta el respaldo xUnit v2 y 0007 documenta HTTP dentro del contenedor.

## Fase 13: README

- [x] 13.1 `README.md` en español: descripción, pila, requisitos previos, compilar, probar (con cobertura), ejecutar la API y la Web (design §7: orden y `Cors__AllowedOrigins__0`), Docker/compose (`cp .env.example .env`), estructura y enlace a los ADR. Cubre: SA-20.
  - RED: `Test-Path README.md` False. GREEN: archivo con los comandos exactos de D-1.
  - Hecho cuando: `Select-String 'dotnet test','docker compose','docs/adr' README.md` encuentra los tres.
- [x] 13.2 Seguir el README tal cual (simula máquina limpia con SDK y Docker). Cubre: SA-20, SC-12.
  - Ejecutar sus comandos: restore/build/test y arrancar la API; `GET /health` responde 200. Corregir el README si algún paso falla.
  - Hecho cuando: los pasos del README funcionan sin conocimiento adicional.

## Fase 14: `openspec/config.yaml`

- [x] 14.1 Copiar `openspec/config.yaml` a `openspec/config.yaml.bak` y actualizar `apply.test_command`, `verify.test_command`, `verify.build_command`, `testing.runner`/`layers`/`coverage.command`/`quality.*` con los comandos reales de D-1 (VSTest, `coverlet.collector`, `check-coverage.ps1`, `dotnet format`). Cubre: SA-19 (config), SA-10, SA-12.
  - RED: `Select-String 'test_command: ""' openspec\config.yaml` encuentra valores vacíos. GREEN: se rellenan y `status` pasa a disponible.
  - Hecho cuando: no quedan `test_command`/`build_command` vacíos, `Test-Path openspec\config.yaml.bak` es True y el YAML sigue siendo válido.

## Fase 15: Punto de control final

- [x] 15.1 Puerta de calidad completa del cambio. Cubre: SA-9, SA-10, SA-11, SA-12, SA-14, SA-17; V1–V17 (cierre).
  - Repetir: restore, format `--verify-no-changes`, build `-warnaserror`, test con cobertura, `check-coverage.ps1`, `docker build -t timetracker-api .`, `docker compose config --services`. Revisar la tabla V1–V17 y anotar el estado de cada una.
  - Hecho cuando: todos terminan en 0 y no hay V sin anotar.
- [x] 15.2 Limpieza y trazabilidad final. Cubre: criterios de éxito de la propuesta.
  - Eliminar temporales (`TestResults/`, `artifacts/`, `.env`, contenedores, `IllegalReferenceProbe.cs`), confirmar que no se ejecutó ningún comando Git, marcar la tabla de trazabilidad con el resultado de cada escenario y dejar listo `sdd-verify`.
  - Hecho cuando: las checklists de ambas tandas están completas y la tabla de trazabilidad no tiene escenarios sin resultado.

---

# Mapa de validaciones V1–V17

| ID | Dónde se comprueba | ID | Dónde se comprueba |
|---|---|---|---|
| V1 | 1.3, 5.1, 6.1, 9.1, 8.4 | V10 | 10.1 |
| V2 | 2.2, 8.3 | V11 | 1.4, 8.2 |
| V3 | 4.1 | V12 | 2.10, 6.1, 8.2 |
| V4 | 3.1, 8.3 | V13 | 11.1 |
| V5 | 9.1 | V14 | 11.1 |
| V6 | 10.1 | V15 | 4.1, 7.2, 8.4 |
| V7 | 5.1 | V16 | 5.5 |
| V8 | 8.1 | V17 | 5.2 |
| V9 | 10.1 | | |

# Evidencia (se completa en apply)

Tanda 1 completada el 2026-10-11 (SDK 10.0.302, runtime 10.0.10). Tanda 2 completada el 2026-10-11 (Docker 29.7.2, `mcr.microsoft.com/dotnet/sdk:10.0` = 10.0.401). Sin comandos de Git de trabajo (ver desviaciones de la Tanda 2).

| Evidencia | Tarea | Resultado |
|---|---|---|
| Orden TDD de `WorkedHours` (RED/GREEN reales por paso) | 2.2–2.10 | 2.2 RED `CS0103 'WorkedHours' no existe` → GREEN 1 prueba. 2.3 RED 2 fallos `No exception was thrown` → GREEN 3. 2.4 verde a la primera (4). 2.5 RED `CS0019 operador '==' no aplicable` → GREEN con `record struct` (5). 2.6 verde a la primera, mismo contrato que 2.5 (6). 2.7 RED `CS0019 operador '+'` → GREEN (7). 2.8 RED `Assert.Throws() Failure: No exception was thrown` (la suma se envuelve) → GREEN con `checked` (8). 2.9 verdes a la primera (10). 2.10 refactor: `build -warnaserror` 0 avisos, 10/10 pruebas. |
| Mutaciones temporales (cero válido, bordes de suma, propiedad extra en health) | 2.4, 2.9, 5.3 | 2.4: `ThrowIfNegativeOrZero` → `FromMinutes_WithZero_IsValid` falla (1 fallo, 3 ok); revertido → 4/4. 2.9: `+ 1` en la suma → fallan `Add_WithZero_IsNeutral`, `Add_UpToIntMaxValue_ReturnsIntMaxValue` y `Add_TwoValues_...` (3 fallos); revertido → 10/10. 5.3: `HealthResponse(string Status, string Machine = "x")` → `Get_BodyHasOnlyStatusProperty` falla (`["status","machine"]`); revertido → 4/4. |
| 5.3 cabecera `Server` (RED real en Kestrel) | 5.3 | `TestServer` no emite `Server`, así que `Get_HeadersDoNotExposeMachineNameOrConfiguration` pasó a la primera. RED real con `dotnet run` (puerto 8082): `Server: Kestrel`; tras `AddServerHeader = false` la cabecera desaparece. |
| Fallo de la prueba de arquitectura (referencia ilegal) y vuelta a verde | 7.4 | Con `FrameworkReference Microsoft.AspNetCore.App` + `IllegalReferenceProbe.cs`: ROJO en `Domain_ReferencesOnlyBaseFramework`: "TimeTracker.Domain referencia ensamblados ajenos al marco base: Microsoft.Extensions.Logging.Abstractions" (1 fallo, 23 ok). Revertido: 24/24; `IllegalReferenceProbe.cs` no existe. |
| 7.2 RED de la regla de dependencias | 7.2 | Con la tabla vacía la prueba pasó (el compilador elimina las referencias no usadas). RED real: archivo temporal en Application que usa `WorkedHours` → "TimeTracker.Application referencia ensamblados no permitidos: TimeTracker.Domain"; con la tabla correcta → verde; archivo temporal eliminado. |
| Ciclo Domain → Application, aviso como error, `Version` en PackageReference | 7.5, 7.6, 7.7 | 7.5: `error MSB4006: dependencia circular en el gráfico de destino _GenerateRestoreProjectPathWalk` (1 error, en el restore); revertido → 0 avisos / 0 errores. 7.6: `WorkedHours.cs(19,13): error CS0219: La variable 'unused' está asignada pero su valor nunca se usa`; revertido → 0/0. 7.7: `Version="10.0.10"` en `Api.IntegrationTests.csproj` → `PackageReferences_DoNotDeclareVersion` ROJO ("...declara Version en: Microsoft.AspNetCore.Mvc.Testing", 1 fallo); revertido → 24/24; la búsqueda `PackageReference.*Version=` en los `.csproj` no da resultados. El restore de `ArchitectureTests` no falló con NU1008 (no depende de ese proyecto); no hizo falta `--no-restore`. |
| Listado de solución y referencias | 7.8 | `dotnet sln list` = 9 proyectos (Api, Application, Domain, Infrastructure, Web; Api.IntegrationTests, Application.Tests, ArchitectureTests, Domain.Tests). `TimeTracker.slnx` no existe. Referencias: Domain ninguna; Application → Domain; Infrastructure → Application; Api → Application, Infrastructure; Web ninguna. |
| Puerto 8081 con `dotnet run --no-launch-profile` | 8.1 | Con `Cors__AllowedOrigins__0=https://app.example.test` y `ASPNETCORE_HTTP_PORTS=8081`: `Now listening on: http://[::]:8081`; `GET /health` → `200` y `{"status":"Healthy"}`; con `Origin` permitido recibe `Access-Control-Allow-Origin: https://app.example.test`. V8: con `ASPNETCORE_URLS=http://localhost:8083` escucha en 8083 (8081 sin respuesta) y avisa del solapamiento: `ASPNETCORE_URLS` tiene prioridad. Sin la clave CORS: `OptionsValidationException: Cors:AllowedOrigins: debe definirse al menos un origen permitido.` y el proceso termina. Variables de entorno limpiadas. |
| Puerta de calidad | 8.2 | `dotnet restore` OK; `dotnet format TimeTracker.sln --verify-no-changes --no-restore` rc 0 (tras 2 ajustes en `.editorconfig`, ver V11); `dotnet build -c Release --no-restore -warnaserror`: 0 avisos, 0 errores; `dotnet test -c Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults` rc 0: Domain.Tests 10/10, ArchitectureTests 24/24, Api.IntegrationTests 14/14, Application.Tests sin pruebas (rc 0). |
| Cobertura | 8.3 | `check-coverage.ps1 -Package TimeTracker.Domain -MinimumPercent 100` → "líneas 100,0 %, ramas 100,0 %", rc 0; con 101 → rc 1 ("Cobertura insuficiente"); paquete inexistente → rc 1. 4 informes `coverage.cobertura.xml`; Domain en el de `Domain.Tests`: `line-rate="1" branch-rate="1"` (7 líneas, 0 ramas; en los demás informes `line-rate="0"`). |
| Regresión de `/health` y regeneración de OpenAPI | 5.4, 9.1, 9.2 | 5.4: cuerpo `"Unhealthy"` → `Get_ReturnsOkWithHealthyJson` falla (`Expected: "Healthy" / Actual: "Unhealthy"`, 1 fallo, 3 ok); revertido → 4/4. 9.1 RED: `OpenApiContractTests` falla ("No existe ...\docs\api\openapi.json"); GREEN: tras los paquetes, `dotnet build -warnaserror` genera `docs/api/openapi.json` (OpenAPI 3.1.1, `paths./health.get.responses.200` con `application/json` → `HealthResponse`), JSON válido (`ConvertFrom-Json`), 25/25 pruebas de arquitectura. 9.2: con `app.MapGet("/probe", ...)` el build deja `"/probe"` en el documento (1 coincidencia); retirado y recompilado → 0 coincidencias. |
| Docker: `inspect`, salud, puerto 8081, fallo sin configuración | 10.1–10.4 | 10.1 RED: `docker build` falla ("failed to read dockerfile: open Dockerfile: no such file or directory"); GREEN: `docker build -t timetracker-api .` rc 0 (imagen 178 MB). V9: `command -v wget` = `/usr/bin/wget`; `APP_UID=1654` (`uid=1654(app)`). 10.2: `.Config.User` = `1654` (no root); `Healthcheck` presente (interval 30 s, timeout 3 s, start-period 15 s, retries 3). `docker run -e Cors__AllowedOrigins__0=https://app.example.test -p 8080:8080`: `GET /health` → 200 `{"status":"Healthy"}` (con `Origin` permitido, `Access-Control-Allow-Origin` correcto) y `Health.Status` = `healthy` (~45 s). Con `-e ASPNETCORE_HTTP_PORTS=8081 -p 8081:8081`: 200 y `healthy` (el `HEALTHCHECK` expande la variable). Sin `Cors__AllowedOrigins__0`: el contenedor termina con código **139** (≠ 0; se esperaba 134/SIGABRT, el 139 lo produce la caída del proceso en Alpine) y el log contiene `OptionsValidationException: Cors:AllowedOrigins: debe definirse al menos un origen permitido.`. Contenedores de prueba eliminados. 10.3: `docker history --no-trunc` e `docker inspect` sin coincidencias de `Cors__`, `example.test`, `Ai__` ni `.env` (solo `ASPNETCORE_HTTP_PORTS=8080`); `/app` sin archivos `.env`. 10.4 RED: `docker compose config --services` → "no configuration file provided: not found"; GREEN: imprime exactamente `api` (con `.env` temporal, borrado después). |
| Reproducción local de CI (CI sin ejecutar) | 11.1, 11.2 | **La CI no se ha ejecutado** (no hay remoto). 11.1: YAML revisado a mano (sin parser disponible: Python es un stub de Windows y no hay módulo YAML); sin `push: true` ni `docker push`. 11.2, mismos pasos en local y en este orden: restore rc 0; `dotnet format --verify-no-changes` rc 0; build `-c Release -warnaserror` 0 avisos / 0 errores; test con cobertura rc 0 (Domain.Tests 10, ArchitectureTests 25, Api.IntegrationTests 14; Application.Tests sin pruebas); `check-coverage.ps1` "líneas 100,0 %, ramas 100,0 %" rc 0 (4 informes); auditoría (con `DOTNET_CLI_UI_LANGUAGE=en`): "has no vulnerable packages given the current sources" en los 9 proyectos y la condición de fallo no se activa; `dotnet publish src/TimeTracker.Api -c Release --no-build -o artifacts/api` rc 0; `docker build -t timetracker-api:ci .` rc 0. `artifacts/` y la imagen `:ci` eliminados. Limitaciones: no se verificó `pwsh` en `ubuntu-latest` ni el comportamiento de las acciones. |

## Validaciones V* resueltas (Tanda 1 y Tanda 2: V1–V17 sin pendientes)

| ID | Estado | Resultado |
|---|---|---|
| V1 | Resuelta | `dotnet package search` (nuget.org, 2026-10-11): Microsoft.NET.Test.Sdk 18.10.1, xunit 2.9.3 (xunit.v3 4.0.2 descartado, ver V2), xunit.runner.visualstudio 3.1.5, coverlet.collector 10.1.0, Microsoft.AspNetCore.Mvc.Testing 10.0.10, Microsoft.AspNetCore.Components.WebAssembly y DevServer 10.0.10 (última publicada: 10.0.12; se mantiene la 10.0.10 de la plantilla y del runtime instalado). `OpenApi` y `ApiDescription.Server`: Tanda 2. |
| V2 | Resuelta con RESPALDO | xunit.v3 4.0.2 arrastra `Microsoft.Testing.Platform.MSBuild` y `dotnet test` falla: "Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later". Se aplicó el respaldo xUnit v2 (xunit 2.9.3 + runner 3.1.5, sin `OutputType=Exe`). Descubrimiento con VSTest correcto y `coverage.cobertura.xml` generado. |
| V3 | Resuelta | `dotnet test tests/TimeTracker.Application.Tests` sin pruebas: mensaje "No hay ninguna prueba disponible", código de salida 0 (también dentro de la solución). Genera un informe de cobertura con Domain en `line-rate="0"`. |
| V4 | Resuelta | Domain `line-rate="1"` y `branch-rate="1"` sin runsettings (respaldo R3 innecesario); el script trata 0 ramas como 100 %. |
| V7 | Resuelta | No hizo falta `public partial class Program`: `WebApplicationFactory<Program>` compila y arranca con la clase generada; sin aviso ASP0027. |
| V8 | Resuelta | `ASPNETCORE_URLS` tiene prioridad sobre `ASPNETCORE_HTTP_PORTS` (aviso de solapamiento al arrancar). |
| V11 | Resuelta con ajustes | `dotnet new editorconfig` no define `end_of_line` ni `charset`, pero fija `insert_final_newline = false` (FINALNEWLINE en todos los archivos, incluida la plantilla Web) y `dotnet_separate_import_directive_groups = true` (WHITESPACE en `src/TimeTracker.Web/Program.cs`). Ajustes justificados al final de `.editorconfig`: `[*] insert_final_newline = true` y `[src/TimeTracker.Web/Program.cs] dotnet_separate_import_directive_groups = false`. Plantillas sin editar. |
| V12 | Resuelta | Sin avisos CA1707, CA1515, CA2225 ni CA1819 con `AnalysisLevel=latest`; no se añadió `Add(left, right)` ni se ajustó ninguna regla. |
| V15 | Resuelta | `Assembly.Load` por nombre carga los 4 ensamblados (también los vacíos) y compilan sin avisos. Hallazgo: el compilador elimina las referencias no usadas, así que la prueba solo detecta referencias realmente usadas (por eso las demostraciones 7.2 y 7.4 usan código temporal). Coverlet inyecta una referencia a `netstandard` en Domain: se admite como marco base. |
| V16 | Resuelta | La excepción de arranque en `WebApplicationFactory` es `Microsoft.Extensions.Options.OptionsValidationException` sin envoltura; la prueba mantiene el recorrido de `InnerException` por robustez. |
| V17 | Resuelta | `POST /health` → 405. |
| V5 | Resuelta con ajuste | `--file-name openapi` y `OpenApiDocumentsDirectory` funcionan (`docs/api/openapi.json`). La generación **sí** dispara `ValidateOnStart`: `dotnet-getdocument` arranca el host y falla con `OptionsValidationException`. En lugar del respaldo del diseño, `Program.cs` aporta un origen ficticio (`https://openapi.example.test`) solo si el ensamblado de entrada es `GetDocument.Insider`; la validación no se relaja en ejecución normal. |
| V6 | Resuelta | `-p:OpenApiGenerateDocuments=false` se mantiene en el `publish` de Docker. Comprobado sin la bandera: el build de Docker no falla, pero ejecuta la generación dentro del contenedor y escribe `/src/docs/api/openapi.json` (inofensivo, innecesario). La bandera evita arrancar la aplicación en la imagen y que el contrato se regenere fuera del build local. |
| V9 | Resuelta | La imagen `aspnet:10.0-alpine` define `APP_UID=1654` y trae `wget` (`/usr/bin/wget`). No hizo falta el respaldo `aspnet:10.0` + curl. |
| V10 | Resuelta | `sdk:10.0` trae 10.0.401 (≥ 10.0.302); `global.json` (`latestFeature`) se copia sin problema al build de Docker. |
| V13 | Resuelta | `dotnet list package --vulnerable --include-transitive` termina con **rc 0 aunque haya hallazgos**. Texto (inglés, `DOTNET_CLI_UI_LANGUAGE=en`): `Project ... has the following vulnerable packages` (comprobado con un proyecto temporal con `Microsoft.OpenApi` 2.0.0); sin hallazgos: `has no vulnerable packages given the current sources`. La CI falla si la salida contiene `has the following vulnerable packages` (idioma fijado). Hallazgo adicional: NuGetAudit en el restore con `TreatWarningsAsErrors` ya detectó `Microsoft.OpenApi` 2.0.0 (transitivo de `Microsoft.AspNetCore.OpenApi`) como NU1903 (GHSA-v5pm-xwqc-g5wc) y bloqueó el build; se resolvió con una referencia directa a 2.7.6 (corregida) en `Directory.Packages.props` y `Api.csproj`. V1 (Tanda 2): `Microsoft.AspNetCore.OpenApi` y `Microsoft.Extensions.ApiDescription.Server` 10.0.10 (última publicada: 10.0.12; se mantiene la 10.0.10 del runtime y del resto). |
| V14 | Resuelta parcialmente | API de GitHub (2026-10-11): `actions/checkout` última `v7.0.1` (la etiqueta `v7` existe) y `actions/setup-dotnet` última `v6.0.0` (etiqueta `v6` existe); se fijan `@v7` y `@v6`. `pwsh` en `ubuntu-latest`: **sin verificar** (no hay remoto; se asume preinstalado según la documentación de los runners). |

## Desviaciones de la Tanda 1

- Se añadió `nuget.config` (solo nuget.org, con `<clear />`): el restore se colgaba por un origen privado configurado en la máquina. Un origen privado no debe formar parte del repositorio ni bloquear el restore.
- xUnit v2 en lugar de v3 (V2). `tests/Directory.Build.props` sin `OutputType=Exe` y con el paquete `xunit`; las pruebas de integración no usan `TestContext.Current.CancellationToken` (API de v3).
- `.gitignore`: la plantilla ya incluía `.env`; solo se añadieron `TestResults/`, `artifacts/` y `coverage*.xml`.
- 1.2: `Select-String 'test' global.json` da una coincidencia falsa (`latestFeature`); se comprobó que no hay sección `test`.
- 6.1: la solución tiene 8 proyectos en ese punto (`ArchitectureTests` se crea en 7.1); los 9 se verifican en 7.8.
- 7.2: la prueba por reflexión no puede fallar con la tabla vacía mientras no haya referencias usadas (ver evidencia).
- Se eliminó `app.MapGet("/")` de la plantilla `web` (la tarea pide Api sin endpoints).

## Desviaciones de la Tanda 2

- V5: la generación del contrato dispara `ValidateOnStart`; en vez del respaldo del diseño (prueba con `WebApplicationFactory`), `Program.cs` detecta el proceso `GetDocument.Insider` y aporta un origen ficticio solo ahí (ADR 0004). El contrato se sigue regenerando en cada build.
- `Microsoft.OpenApi` 2.7.6 fijado como referencia directa en `Api.csproj` y `Directory.Packages.props` por la vulnerabilidad GHSA-v5pm-xwqc-g5wc (NU1903) del 2.0.0 transitivo; sin ella el restore falla con `TreatWarningsAsErrors`. Revisar cuando `Microsoft.AspNetCore.OpenApi` publique una versión que dependa de una corregida.
- ADR 0006 documenta xUnit v2 (no v3) por la incompatibilidad de v3 con VSTest en el SDK 10, en vez del respaldo previsto en el diseño; `config.yaml` refleja xUnit v2.
- Docker: el contenedor sin `Cors__AllowedOrigins__0` termina con código 139 (no 134); es distinto de 0, como exige la especificación.
- El Dockerfile restaura con el `nuget.config` del repositorio (solo nuget.org), que se copia a la etapa de compilación; el CI usa el mismo archivo al hacer checkout.
- La CI no se pudo ejecutar ni validar con un parser YAML (sin remoto y sin herramienta YAML disponible): revisión manual y reproducción local de los pasos. `pwsh` en `ubuntu-latest` sin verificar.
- Se ejecutó una vez `git --version` por descuido (solo muestra la versión; no inicializa, configura ni accede a ningún repositorio). No hay directorio `.git` ni se ejecutó ningún otro comando Git.
- Líneas escritas a mano en la Tanda 2: ≈ 515 (Dockerfile 37, compose 8, CI 58, README 127, ADR 208, prueba OpenAPI 20, Api ≈ 25, `config.yaml` ≈ 30, props ≈ 4), dentro del rango previsto salvo README y `config.yaml`, ligeramente por encima.


# Previsión de líneas por tanda (escritas a mano)

| Grupo | Tanda 1 | Tanda 2 |
|---|---|---|
| Archivos raíz (props, `global.json`, ajustes de ignore/editorconfig, `.env.example`) | 90–110 | - |
| `src` (`.csproj`, `WorkedHours`, Api: Program, Health, Configuration) | 160–200 | 10–15 (OpenAPI en `Api.csproj`/`Program.cs`) |
| `tests` (props, `.csproj`, WorkedHours ~70, Api ~150, Architecture ~85) | 315–375 | 25–30 (`OpenApiContractTests`) |
| `scripts/check-coverage.ps1` | 25–35 | - |
| `Dockerfile` + compose | - | 35–45 |
| `ci.yml` | - | 50–65 |
| ADR ×7 | - | 180–230 |
| `README.md` + `config.yaml` | - | 100–130 |
| **Total** | **≈ 590–720** | **≈ 400–515** |
| **Total del cambio** | **≈ 990–1235** | |

Rango equivalente al del diseño (990–1225), con la reubicación de OpenAPI a la Tanda 2. Riesgo de presupuesto de 400 líneas: High. Mitigación: `size:exception` aceptado y dos puntos de control (8.4 y 15.2).

# Trazabilidad de escenarios (53 de 53 mapeados)

| ID | Escenario | Tareas |
|---|---|---|
| SA-1 | Contenido de la solución | 1.3, 2.1, 4.1, 5.1, 6.1, 7.1, 7.8 |
| SA-2 | Destino de plataforma | 2.1, 4.1, 7.3 |
| SA-3 | Referencias esperadas | 4.1, 5.1, 6.1, 7.3, 7.8 |
| SA-4 | Referencia ilegal no compila | 7.5 |
| SA-5 | Estado conforme | 7.2, 7.3 |
| SA-6 | Referencia ilegal detectada | 7.4 |
| SA-7 | Versiones centralizadas | 1.2, 7.3, 7.7 |
| SA-8 | Aviso como error | 1.2, 2.10, 7.6 |
| SA-9 | Build limpio | 6.1, 8.2, 15.1 |
| SA-10 | Pruebas en verde | 8.2, 14.1, 15.1 |
| SA-11 | Formato verificado | 8.2, 15.1 |
| SA-12 | Informe generado | 3.1, 8.3, 14.1, 15.1 |
| SA-13 | Archivos presentes | 1.4, 8.4 |
| SA-14 | Construcción de la imagen | 10.1, 10.2, 15.1 |
| SA-15 | Salud dentro del contenedor | 10.2 |
| SA-16 | Sin secretos en la imagen | 10.3 |
| SA-17 | Servicios de compose | 10.4, 15.1 |
| SA-18 | Pasos de CI presentes | 11.1, 11.2 |
| SA-19 | ADR completos | 12.1, 12.2, 12.3, 14.1 |
| SA-20 | README ejecutable | 13.1, 13.2 |
| AH-1 | API en marcha | 5.2 |
| AH-2 | Método no permitido | 5.2 |
| AH-3 | Solo la propiedad status | 5.3 |
| AH-4 | Cabeceras sin versión | 5.3 |
| AH-5 | Documento exportado | 9.1 |
| AH-6 | Regeneración tras cambiar un endpoint | 9.2 |
| AH-7 | La prueba pasa | 5.2 |
| AH-8 | La prueba detecta una regresión | 5.4 |
| SC-1 | Valor tomado del entorno | 5.5 |
| SC-2 | Ningún secreto versionado | 1.4, 8.4 |
| SC-3 | Falta la clave | 5.5, 10.2 |
| SC-4 | Lista vacía | 5.6 |
| SC-5 | Comodín rechazado | 5.6 |
| SC-6 | Origen con formato inválido | 5.6 |
| SC-7 | Origen permitido y no permitido | 5.7 |
| SC-8 | El mensaje nombra la clave | 5.5, 5.6 |
| SC-9 | Prueba de fallo al arrancar | 5.5 |
| SC-10 | Claves documentadas | 1.4, 8.4 |
| SC-11 | Sin claves de IA activas | 1.4 |
| SC-12 | Puerto distinto | 8.1, 10.2, 13.2 |
| WH-1 | Valor positivo válido | 2.2 |
| WH-2 | Cero es válido | 2.4 |
| WH-3 | Un minuto negativo | 2.3 |
| WH-4 | Valor mínimo del entero | 2.3 |
| WH-5 | Suma de dos valores | 2.7 |
| WH-6 | Cero como elemento neutro | 2.9 |
| WH-7 | Suma que desborda | 2.8 |
| WH-8 | Suma en el límite exacto | 2.9 |
| WH-9 | Mismos minutos | 2.5 |
| WH-10 | Minutos distintos | 2.6 |
| WH-11 | Sin dependencias | 2.1, 7.2 |
| WH-12 | Cobertura del dominio | 3.1, 8.3 |
| WH-13 | Ciclo TDD demostrable | 2.2–2.10 (evidencia) |

Escenarios sin mapear: ninguno. Totales por especificación: solution-architecture 20, api-health 8, startup-configuration 12, worked-hours 13 = 53.
Resultado de la trazabilidad (Tanda 2 incluida): los 53 escenarios tienen resultado. Verificados por prueba automática o evidencia manual registrada: todos los de worked-hours (WH-1..13), api-health (AH-1..8), startup-configuration (SC-1..12) y solution-architecture SA-1..SA-17, SA-19 y SA-20. SA-14, SA-15, SA-16 y SA-17 se comprobaron con Docker real (10.1–10.4). **SA-18 (CI): parcial**: el YAML contiene los pasos exigidos (revisión manual) y se reprodujeron en local, pero el flujo no se ha ejecutado en GitHub.

# Resumen de tareas

| Tanda | Fases | Tareas |
|---|---|---|
| Tanda 1 | 1–8 | 36 |
| Tanda 2 | 9–15 | 16 |
| Total | 15 | 52 |

Orden y paralelismo: todo es secuencial salvo, en la Tanda 2, la Fase 12 (ADR) y la Fase 13 (README), que no dependen de Docker/CI y pueden redactarse en cualquier orden. Las Fases 10–11 dependen de la 9 (el `publish` de Docker usa `OpenApiGenerateDocuments=false`).
