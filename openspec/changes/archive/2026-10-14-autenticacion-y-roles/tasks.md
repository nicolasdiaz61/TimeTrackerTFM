# Tareas: autenticacion-y-roles

Fecha: 2026-10-13 · Fase: sdd-tasks · Modo: openspec · Base: `proposal.md`, `exploration.md`, `design.md` (DD-1–DD-30, V1–V24, tandas §16), 9 especificaciones (260 escenarios), `openspec/config.yaml`, `AGENTS.md` y las lecciones del cambio archivado `fichaje-extremo-a-extremo` (D1–D12, W1–W8, S1–S7).

Nota de formato: el límite de palabras habitual de este artefacto se supera de forma deliberada, como en el cambio archivado. El usuario pidió trazabilidad de 260 escenarios, pasos TDD, validaciones V1–V24, tandas con copia previa sin Git, verificación manual de la Web y orden de recorte.

## Decisiones del usuario aplicadas en esta fase

1. Se **mantienen los 260 escenarios** de las specs tal cual. El escenario «Hash almacenado ilegible → 401» (AU-14), que el diseño no lista, se añade a las tareas 9.1 (adaptador) y 10.3 (API).
2. El límite de **100 miembros** (TA-29) se separa en dos tareas: la unitaria (3.9, Application) y la de API (15.4).
3. Tandas con punto de control (PARADA) y copia previa en `..\TimeTracker.rollback\tanda-N`: 1 Domain+Application · 2 Infrastructure · 3a Api núcleo · 3b Api extra · 4 Web · 5 Documentación.
4. Previsión de referencia: **2.400–3.150 líneas** (la del diseño §16). `size:exception` aceptado. Con ello quedan resueltas las dudas abiertas del diseño §20: V13 se acepta (las specs `authentication` y `api-health` ya lo reflejan), DD-24 se mantiene (la spec `startup-configuration` ya lo refleja) y la previsión es la del diseño.
5. Las specs no llevan identificadores de escenario: **los fija este archivo**, por spec y en orden de aparición (AU = authentication, TA = team-access, WS = web-session, SC = startup-configuration, SA = solution-architecture, TK = time-tracking, PE = time-entries-persistence, UI = time-entries-ui, AH = api-health). No coinciden con los del cambio archivado.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ≈ 2.400–3.150 escritas a mano, sin migraciones generadas ni `openapi.json` (la propuesta preveía 1.490–1.955; el diseño incluye las pruebas de seguridad) |
| 400-line budget risk | High |
| Chained PRs recommended | No |
| Suggested split | Entrega única en 6 tandas con punto de control (sin Git, sin remoto): 1, 2, 3a, 3b, 4, 5 |
| Delivery strategy | exception-ok |
| Chain strategy | size-exception |

Decision needed before apply: No (`size:exception` aceptado por el usuario, propuesta)
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 (Tanda 1) | Domain `UserAccount`/`UserRole` + Application (puertos, `LoginUseCase`, casos de uso de equipo, `TimeEntryListResult.From`) con TDD; paquetes JwtBearer y Components.Authorization restaurados | Entrega única | `dotnet test tests/TimeTracker.Domain.Tests` y `dotnet test tests/TimeTracker.Application.Tests` | N/A: solo bibliotecas; `dotnet restore`/`build` comprueban V1/V2 | Borrar `Domain/Users`, `Application/{Users,Authentication,Team}`, pruebas nuevas; restaurar `..\TimeTracker.rollback\tanda-1` |
| 2 (Tanda 2) | Infrastructure: `users`, `UserConfiguration`, `UserRepository`, migración `AddUsers`, pruebas con Testcontainers | Misma entrega | `dotnet test tests/TimeTracker.Api.IntegrationTests --filter "FullyQualifiedName~Persistence"` | PostgreSQL 18 real vía Testcontainers (`postgres:18-alpine`) | Borrar `UserConfiguration`, `UserRepository`, `Migrations/*_AddUsers*`, `IT/Support/UserSeed.cs`, pruebas nuevas; restaurar `tanda-2` |
| 3a (Tanda 3a) | Api núcleo: JWT, hasher, `HttpContextCurrentUser`, login/me, política de respaldo, retirada de la identidad provisional, `TestTokens` y migración de pruebas | Misma entrega | `dotnet test tests/TimeTracker.Api.IntegrationTests --filter "FullyQualifiedName~Authentication\|FullyQualifiedName~Security\|FullyQualifiedName~Startup"` | `dotnet run --no-launch-profile` en `Production` con `Jwt__SigningKey`: `GET /health` 200 y `GET /api/time-entries` 401 (13.1) | Borrar `Api/Authentication` y pruebas nuevas; restaurar `tanda-3a` (incluye `Api/Identity`, `IdentityStartupTests` y fábricas) |
| 3b (Tanda 3b) | Api extra: equipo y política `Manager`, siembra, limitador, cabeceras, StatusCodePages, OpenAPI Bearer, compose 5274, `.env.example` | Misma entrega | `dotnet test tests/TimeTracker.Api.IntegrationTests` | `docker compose up -d --wait` y `POST http://localhost:5274/api/auth/login` de Luis y Marta (20.4) | Borrar `Api/{Team,Seeding,Security}`, `OpenApiSecurityTransformers.cs` y pruebas nuevas; `docker compose down -v`; restaurar `tanda-3b` |
| 4 (Tanda 4) | Web: sesión, proveedor, handler, `/login`, `/equipo`, rutas protegidas | Misma entrega | `dotnet test tests/TimeTracker.ArchitectureTests --filter "FullyQualifiedName~RepositoryConventionsTests"` y `dotnet build TimeTracker.sln -c Release -warnaserror` | Navegador real: flujo de empleado y de responsable contra `http://localhost:5274` (24.1–24.5) | Borrar `Web/{Auth,Team,Http,Shared}`, `Pages/{Login,Team}.razor`; restaurar `tanda-4` |
| 5 (Tanda 5) | ADR 0011/0012, estado del 0009, README, `config.yaml`, pruebas de entrega | Misma entrega | `dotnet test tests/TimeTracker.ArchitectureTests --filter "FullyQualifiedName~DeliveryArtifactsTests"` | N/A: documentación; la puerta final (28.1) ejecuta toda la suite | Borrar `docs/adr/{0011,0012}-*.md`; restaurar `tanda-5` (README, `config.yaml`, ADR 0009) |

## Review Workload Guard

- Presupuesto de revisión: 400 líneas (`additions + deletions`, solo texto escrito a mano). Previsión 2.400–3.150 (6 a 8 veces el presupuesto): riesgo **High**. El cambio anterior superó su previsión de diseño (S5 archivado); esta previsión ya incluye las pruebas de seguridad.
- Aceptado: `size:exception` (estrategia `exception-ok`). No hay repositorio Git ni remoto: «PR» equivale a tanda y cada tanda es una unidad revisable de forma autónoma, con punto de control y **PARADA** (4.2, 7.2, 13.2, 21.2, 25.2, 28.4). La revisión de cada tanda se hace con contexto limpio (AGENTS §2).
- Previsión por tanda (líneas a mano):

| Tanda | `src` | Pruebas | Props, `.csproj`, compose, `.env.example`, ADR, README | Total tanda | Punto de control |
|---|---|---|---|---|---|
| 1 | 180–230 | 265–360 (Domain 90–120, Application 150–200, arquitectura 25–40) | 5–10 | **450–600** | build, test, cobertura 100 / ≥ 80 |
| 2 | 70–95 | 75–115 | 5–10 | **150–220** | repositorio y `HasPendingModelChanges` en verde |
| 3a | 200–260 | 330–420 | 20 (arquitectura y contrato) | **550–700** | 200/401 en verde; sin restos de la identidad provisional |
| 3b | 230–300 | 320–400 | 50 (compose, `.env.example`, entrega) | **600–750** | 403/404/429 probados; `docker compose up` y login en `http://localhost:5274` |
| 4 | 320–420 | 40–60 | 90–120 (evidencia manual en `verify-report.md`) | **450–600** | flujo manual de empleado y de responsable registrado |
| 5 | — | 14–20 | 186–260 (ADR 110–150, README 70–100, `config.yaml` 6–10) | **200–280** | 12 ADR; entrega en verde |
| **Total** | | | | **≈ 2.400–3.150** | |

- Disparador de revisión de alcance: si al cerrar la Tanda 3b (21.2) el total acumulado real supera 3.150 líneas, PARADA y el usuario decide el recorte (R1–R5) antes de empezar las tandas 4 y 5.

## Convenciones de ejecución

- **Sin Git:** no se ejecuta ningún comando `git` (ni `git --version`). Todo desde la raíz del repositorio con PowerShell. Restauración NuGet con el `nuget.config` del repositorio (solo nuget.org).
- **No se lee `.env`:** ni comandos ni pruebas leen `.env` (solo `.env.example`). Para `docker compose` y `dotnet run` se definen variables en la sesión (`$env:...`); si hiciera falta añadir claves al `.env` local, se usa `Add-Content` sin leerlo. Un `.env` temporal del cambio anterior puede seguir en la raíz: si contiene `Identity__DevelopmentEmployeeId` no tiene efecto (SC-16).
- **Strict TDD:** cada tarea de código sigue RED (prueba que falla; se anota el mensaje real) → GREEN (código mínimo) → REFACTOR (suite en verde). Una prueba que pasa a la primera se marca **«verde a la primera»** con justificación y **mutación temporal** (se rompe el código, la prueba falla, se revierte y se anota). Esa lista alimenta AU-61 (28.2).
- **Datos inventados:** `example.test`, contraseñas `clave-de-prueba-123` / `otra-clave-456` (solo pruebas), contraseña de demostración `Demo-Fichajes-2026` (solo README y pruebas de entrega; nunca `.env.example` ni compose), GUID con `[guid]::NewGuid()`, clave de firma de pruebas inventada de al menos 32 bytes. Identificadores de código en inglés; documentación y textos de usuario en español.
- **Entorno limpio antes de probar o ejecutar** (`ENV-CHECK` debe devolver vacío; ampliado con `Jwt__` y `Seed__`). Docker Desktop en marcha desde la Tanda 2.
- **`DocumentGeneration` primero (lección D4 archivada):** el build de `TimeTracker.Api` exporta el contrato arrancando el host (`GetDocument.Insider`). Cada validador `ValidateOnStart` nuevo exige su valor ficticio en `DocumentGeneration` **en la misma tarea** (8.3: `Jwt:SigningKey`; 16.1: `Seed:DemoUsers=false`; 12.2: retirar `Identity`). Solo si el RED de una tarea lo exige se usa `-p:OpenApiGenerateDocuments=false`, y se anota.
- **Abreviaturas de rutas:** `DOM` = `src/TimeTracker.Domain` · `APP` = `src/TimeTracker.Application` · `INF` = `src/TimeTracker.Infrastructure` · `API` = `src/TimeTracker.Api` · `WEB` = `src/TimeTracker.Web` · `DT` = `tests/TimeTracker.Domain.Tests` · `AT` = `tests/TimeTracker.Application.Tests` · `IT` = `tests/TimeTracker.Api.IntegrationTests` · `AR` = `tests/TimeTracker.ArchitectureTests`.
- **Un único escritor a la vez** (AGENTS §2). Marcas de recorte: **[R1]** esquema OpenAPI Bearer · **[R2]** cabeceras · **[R3]** rate limiting · **[R4]** pulido de equipo · **[R5]** fusionar ADR 0012 en 0011. Sin marca = intocable.

```powershell
# ENV-CHECK: no debe devolver nada
Get-ChildItem Env: | Where-Object Name -Match '^(Cors__|ConnectionStrings__|Identity__|Database__|Jwt__|Seed__|ASPNETCORE_|DOTNET_ENVIRONMENT)'
```

| Alias | Comando |
|---|---|
| `RESTORE` | `dotnet restore TimeTracker.sln` |
| `BUILD` | `dotnet build TimeTracker.sln -c Release --no-restore -warnaserror` |
| `TEST` | `dotnet test TimeTracker.sln -c Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults` |
| `FORMAT` | `dotnet format TimeTracker.sln --verify-no-changes --no-restore` |
| `COV-D` | `pwsh scripts/check-coverage.ps1 -ResultsDirectory TestResults -Package TimeTracker.Domain -MinimumPercent 100` |
| `COV-A` | `pwsh scripts/check-coverage.ps1 -ResultsDirectory TestResults -Package TimeTracker.Application -MinimumPercent 80` |
| `VULN` | `$env:DOTNET_CLI_UI_LANGUAGE='en'; dotnet list TimeTracker.sln package --vulnerable --include-transitive` (la salida no contiene `has the following vulnerable packages`) |
| `T-D`, `T-A`, `T-I`, `T-AR` | `dotnet test tests/TimeTracker.{Domain.Tests,Application.Tests,Api.IntegrationTests,ArchitectureTests}` (con `--filter "FullyQualifiedName~Clase"`; compila en Debug) |

## Orden de recorte (si falta tiempo)

Orden acordado: R1 → R2 → R3 → R4 → R5. Intocables: Domain, Application, Infrastructure, autenticación (JWT, hasher, validación estricta), autorización (respaldo, `Manager`, control por objeto), siembra y arranque, retirada de la identidad provisional, contrato de rutas con 401/403/404 y **todas las pruebas de seguridad**.

| Marca | Qué se recorta | Tareas | Escenarios que dejarían de cumplirse | Consecuencia |
|---|---|---|---|---|
| R1 | Esquema OpenAPI Bearer (DD-28, V7) | 19.1 (parte Bearer), 19.2 | AU-56 (DEBERÍA en la spec) | Aviso en verify; se conservan `ProducesProblem(401/403/404/429)` (AU-55, TK-14, TA-54 no se recortan) |
| R2 | Cabeceras de seguridad (DD-20) | 18.1, 18.2 | AU-40, AU-41, AU-42, AU-43 | Delta de `authentication` o aviso; desviación de AGENTS §6 anotada en ADR 0011 |
| R3 | Rate limiting del login (DD-19) | 17.1–17.3, `RequireRateLimiting` | AU-35–AU-39, WS-5 (el mensaje 429 de la Web queda inerte), el `429` de AU-55 y de AU-41 | Delta o aviso; la fuerza bruta queda mitigada solo por PBKDF2 y el tope de 128 caracteres; anotar en ADR 0011 |
| R4 | Pulido de `/equipo` (selector, estados) | 23.5 | WS-30, WS-33 (verificación parcial) | Aviso en verify; lista y tabla básicas se conservan |
| R5 | Fusionar ADR 0012 en 0011 | 26.2 (parte 0012) | SA-20 (pasaría a 11 ADR), parte de SA-22 | Delta de `solution-architecture` (11 ADR), ajuste de `Adr_Directory_...` y del README |

## Plan de reversión (copia previa de archivos a modificar)

Al empezar cada tanda se copian a `..\TimeTracker.rollback\tanda-N\` (N = `1`, `2`, `3a`, `3b`, `4`, `5`), conservando la ruta relativa, los archivos **existentes en ese momento** que la tanda va a modificar o eliminar (incluidos los creados en tandas anteriores: así cada copia restaura el estado del punto de control anterior). `Save-Rollback` conserva la primera copia y nunca sobrescribe:

```powershell
function Save-Rollback([string]$Tanda, [string[]]$Files) {
  foreach ($f in $Files) { if (Test-Path $f) {
    $t = Join-Path "..\TimeTracker.rollback\tanda-$Tanda" $f
    if (Test-Path $t) { continue }
    New-Item -ItemType Directory -Force (Split-Path $t) | Out-Null; Copy-Item $f $t } }
}
```

**Precondición (comprobada hoy):** `..\TimeTracker.rollback\tanda-1`, `tanda-2`, `tanda-3` y `tanda-4` ya existen con las copias del cambio archivado `fichaje-extremo-a-extremo`. `tanda-1`, `tanda-2` y `tanda-4` colisionan con los nombres de este cambio; `3a`, `3b` y `5` no. La tarea 1.1 las aparta a `..\TimeTracker.rollback\fichaje-extremo-a-extremo\` antes de crear las nuevas (ver desviación D13).

| Tanda | Existentes que se copian | Nuevos (se borran al revertir) |
|---|---|---|
| 1 (7) | `Directory.Packages.props`, `API/TimeTracker.Api.csproj`, `WEB/TimeTracker.Web.csproj`, `APP/TimeTracking/{TimeEntryResult,GetTimeEntriesUseCase}.cs`, `AT/Fakes/InMemoryTimeEntryRepository.cs`, `AR/RepositoryConventionsTests.cs` | `DOM/Users/*`, `APP/{Users,Authentication,Team}/*`, `DT/UserAccountTests.cs`, `AT/{LoginUseCaseTests,GetTeamMembersUseCaseTests,GetTeamMemberTimeEntriesUseCaseTests,TimeEntryListResultTests}.cs`, `AT/Fakes/{InMemoryUserRepository,FakePasswordHasher,FakeAccessTokenIssuer}.cs` |
| 2 (4) | `INF/Persistence/{TimeTrackerDbContext,PersistenceServiceCollectionExtensions}.cs`, `INF/Persistence/Migrations/TimeTrackerDbContextModelSnapshot.cs`, `IT/Persistence/MigrationTests.cs` | `INF/Persistence/{UserConfiguration,UserRepository}.cs`, `INF/Persistence/Migrations/*_AddUsers*.cs`, `IT/Support/UserSeed.cs`, `IT/Persistence/{UserSchemaTests,UserConstraintsTests,TimeEntriesForeignKeyTests,UserRepositoryTests}.cs` |
| 3a (24) | `API/{Program.cs,TimeTracker.Api.csproj}`, `API/Configuration/DocumentGeneration.cs`, `API/Health/HealthEndpoints.cs`, `API/TimeEntries/TimeEntryEndpoints.cs`, `API/Identity/*` (4), `IT/{ApiFactory,EnvironmentIsolation,HealthEndpointTests,StartupConfigurationTests,ErrorHandlingTests}.cs`, `IT/TimeEntries/{PostgresApiFactory,TimeEntryEndpointsTests,TimeEntryIsolationTests,TimeEntryConcurrencyTests}.cs`, `IT/Startup/{IdentityStartupTests,DatabaseStartupTests,DatabaseMigrationStartupTests}.cs`, `AR/{OpenApiContractTests,RepositoryConventionsTests}.cs`, `docs/api/openapi.json` | `API/Authentication/*`, `IT/TestTokens.cs`, `IT/Startup/{JwtStartupTests,LegacyIdentityKeyTests}.cs`, `IT/Authentication/*`, `IT/Security/{AuthorizationCoverageTests,FallbackPolicyTests,OpenApiExposureTests,SigningKeyLeakTests}.cs`, `IT/TimeEntries/TimeEntryAuthenticationTests.cs` |
| 3b (14) | `API/{Program.cs,Configuration/DocumentGeneration.cs,Errors/ProblemDetailsServiceCollectionExtensions.cs,Authentication/AuthenticationServiceCollectionExtensions.cs,Authentication/AuthEndpoints.cs,TimeEntries/TimeEntryEndpoints.cs}`, `IT/{CorsPolicyTests,ApiFactory}.cs`, `AR/{OpenApiContractTests,RepositoryConventionsTests,DeliveryArtifactsTests}.cs`, `docker-compose.yml`, `.env.example`, `docs/api/openapi.json` | `API/Team/*`, `API/Seeding/*`, `API/Security/*`, `API/Configuration/OpenApiSecurityTransformers.cs`, `IT/Team/*`, `IT/Seeding/*`, `IT/Startup/SeedStartupTests.cs`, `IT/Security/{AccessErrorsTests,LoginRateLimitTests,SecurityHeadersTests}.cs` |
| 4 (10) | `WEB/{Program.cs,App.razor,_Imports.razor,TimeTracker.Web.csproj}`, `WEB/Layout/NavMenu.razor`, `WEB/Pages/TimeEntries.razor`, `WEB/TimeEntries/{TimeEntriesApiClient,TimeEntriesApiException,ApiProblemDto}.cs`, `AR/RepositoryConventionsTests.cs` | `WEB/{Auth,Team,Http,Shared}/*`, `WEB/Pages/{Login,Team}.razor`, sección de verificación manual en `verify-report.md` |
| 5 (5) | `README.md`, `openspec/config.yaml`, `docs/adr/0009-identidad-provisional.md`, `AR/{DeliveryArtifactsTests,RepositoryConventionsTests}.cs` | `docs/adr/{0011-autenticacion-jwt-roles-y-equipo,0012-sesion-web-y-riesgo-xss}.md` |

Revertir una tanda: restaurar sus copias y las de las posteriores en orden inverso (`Copy-Item -Recurse -Force ..\TimeTracker.rollback\tanda-N\* .`), borrar los nuevos, `docker compose down -v` y, si procede, `docker image rm timetracker-api`. No hay datos reales que perder. La carpeta `..\TimeTracker.rollback` se elimina al archivar (decisión del usuario).

---

# TANDA 1: Domain y Application (TDD)

## Fase 1: Preparación y paquetes

- [x] 1.1 **Copia previa, apartado de copias antiguas y línea base.** Archivos: `..\TimeTracker.rollback\tanda-1\` (7 existentes, ver plan). Cubre: precondición del plan de reversión.
  - Pegar `Save-Rollback`. Mover las copias del cambio archivado: `New-Item -ItemType Directory ..\TimeTracker.rollback\fichaje-extremo-a-extremo; Move-Item ..\TimeTracker.rollback\tanda-1,..\TimeTracker.rollback\tanda-2,..\TimeTracker.rollback\tanda-3,..\TimeTracker.rollback\tanda-4 ..\TimeTracker.rollback\fichaje-extremo-a-extremo\`. Si el movimiento falla, PARAR y consultar (no mezclar contenidos).
  - Copiar los 7 archivos de la Tanda 1. `ENV-CHECK`, `dotnet --list-sdks`, `RESTORE`, `BUILD`, `TEST` (el cierre archivado dejó 151 pruebas: Domain 27, Application 14, Arquitectura 44, Integración 66; anotar las reales).
  - Hecho cuando: `(Get-ChildItem -Recurse -File ..\TimeTracker.rollback\tanda-1).Count` = 7, `Test-Path ..\TimeTracker.rollback\fichaje-extremo-a-extremo\tanda-4` es True, y `BUILD` y `TEST` terminan en 0.
  - Evidencia: `ENV-CHECK` vacío. El movimiento de las 4 copias antiguas a `..\TimeTracker.rollback\fichaje-extremo-a-extremo\` funcionó (sin nombre alternativo; D13 sin desviación): `Test-Path …\fichaje-extremo-a-extremo\tanda-4` = True; `tanda-1` nueva con Count = 7. SDKs 8.0.425 y 10.0.302. Línea base: `RESTORE` rc 0, `BUILD` rc 0 con 0 avisos, `TEST` rc 0 con **151** pruebas (Domain 27, Application 14, Arquitectura 44, Integración 66), igual que el cierre archivado.
- [x] 1.2 **RED de paquetes (V1, V2).** Archivos: `AR/RepositoryConventionsTests.cs`. Cubre: SA-1, SA-3.
  - `AuthPackages_AreDeclaredOnlyInTheirProject`: `Microsoft.AspNetCore.Authentication.JwtBearer` solo en `API/TimeTracker.Api.csproj`; `Microsoft.AspNetCore.Components.Authorization` solo en `WEB/TimeTracker.Web.csproj`; ninguno con atributo `Version`; ambos con `PackageVersion` `10.0.10` en `Directory.Packages.props`. `Solution_DoesNotDependOnAspNetCoreIdentity`: ningún `Microsoft.AspNetCore.Identity` en `Directory.Packages.props` ni en los `.csproj`.
  - RED: la primera falla («falta PackageVersion…»). La segunda es verde a la primera; mutación: añadir un `PackageVersion Microsoft.AspNetCore.Identity.EntityFrameworkCore` temporal → falla; revertir.
  - Hecho cuando: `T-AR --filter "FullyQualifiedName~RepositoryConventionsTests"` falla solo por la primera y la mutación queda anotada.
  - Evidencia: RED: 2 fallos (la primera prueba es una Theory de dos casos, uno por paquete): `Falta PackageVersion de Microsoft.AspNetCore.Authentication.JwtBearer en Directory.Packages.props.` y el equivalente de `Components.Authorization`; 26 verdes (28 en total). La prueba se escribió como Theory (`[InlineData]` paquete/proyecto propietario) en lugar de dos pruebas duplicadas. `Solution_DoesNotDependOnAspNetCoreIdentity`: verde a la primera; mutación (`PackageVersion Microsoft.AspNetCore.Identity.EntityFrameworkCore` temporal) → ROJO `Dependen de ASP.NET Core Identity: Directory.Packages.props`; revertido y comparado con la copia (idéntico).
- [x] 1.3 **GREEN de paquetes (V1, V2, V10).** Archivos: `Directory.Packages.props`, `API/TimeTracker.Api.csproj`, `WEB/TimeTracker.Web.csproj`. Cubre: SA-1, SA-2, SA-4 (parcial); V1, V2.
  - `dotnet package search --exact-match` de ambos paquetes (anotar versiones); añadir los dos `PackageVersion` 10.0.10 y las dos `PackageReference` sin versión. V1: `RESTORE` y `BUILD` sin NU190x; anotar la versión transitiva de `Microsoft.IdentityModel.JsonWebTokens` (la usa el emisor, DD-8). V2: compatibilidad con `Components.WebAssembly` 10.0.10. Respaldo: si aparece NU1903, subir a 10.0.12 (y fijar la versión transitiva corregida como se hizo con `Microsoft.OpenApi`); si cambia la versión, avisar al usuario (la spec cita 10.0.10 con respaldo 10.0.12).
  - SA-2: `dotnet list APP package` no lista paquetes directos (más el test vigente `Domain_And_Application_DeclareNoPackageOrFrameworkReferences`).
  - Hecho cuando: `RESTORE` rc 0, `BUILD` rc 0 con 0 avisos, `T-AR --filter "FullyQualifiedName~RepositoryConventionsTests"` en verde y `VULN` limpio.
  - Evidencia: `dotnet package search --exact-match` (nuget.org): ambos paquetes publican 10.0.10, 10.0.11 y 10.0.12 (última 10.0.12). Se usa 10.0.10 como indica la spec (sin NU1903, no hizo falta el respaldo 10.0.12). `RESTORE` rc 0; `BUILD` rc 0 con 0 avisos; foco `RepositoryConventionsTests` 28/28; `VULN` limpio (ningún proyecto con paquetes vulnerables). **V1 resuelta:** versión transitiva `Microsoft.IdentityModel.JsonWebTokens` **8.19.2** (también Tokens/Logging/Abstractions/Protocols 8.19.2 y `System.IdentityModel.Tokens.Jwt` 8.19.2); sin vulnerabilidades. **V2 resuelta:** `Components.Authorization` 10.0.10 restaura y compila con `Components.WebAssembly` 10.0.10 (Web compila en `BUILD`). SA-2: `dotnet list src/TimeTracker.Application package` → «No packages were found».

## Fase 2: Domain `UserAccount` (TDD, 12 pruebas del diseño §14)

Archivos comunes: `DT/UserAccountTests.cs`, `DOM/Users/{UserRole,UserAccount}.cs`. Foco: `T-D --filter "FullyQualifiedName~UserAccountTests"`. Cada tarea anota RED y GREEN reales.

- [x] 2.1 **`UserRole_HasExactlyEmployeeAndManager`.** Cubre: TA-1.
  - RED (compilación): `UserRole` no existe. GREEN: `enum UserRole { Employee = 0, Manager = 1 }`; la prueba compara `Enum.GetNames` con `["Employee","Manager"]`.
  - Hecho cuando: el foco pasa 1 prueba.
  - Evidencia: RED (compilación) `CS0234: el espacio de nombres 'Users' no existe en 'TimeTracker.Domain'`. GREEN: `enum UserRole { Employee = 0, Manager = 1 }`; foco 1/1.
- [x] 2.2 **Paso 1 · `Create_WithValidData_SetsProperties`.** Cubre: TA-3 (parte: el resto de propiedades conserva los valores).
  - RED (compilación): `UserAccount` no existe. GREEN: constructor **privado** (id, email, displayName, passwordHash, role, managerId) y `Create` que solo asigna; propiedades `Id`, `Email`, `DisplayName`, `PasswordHash`, `Role`, `ManagerId`; constantes `MaxEmailLength = 254` y `MaxDisplayNameLength = 100`.
  - Hecho cuando: el foco pasa 2 pruebas.
  - Evidencia: RED (compilación) `CS0103: el nombre 'UserAccount' no existe`. GREEN: constructor privado + `Create` que solo asigna, constantes 254/100; foco 2/2.
- [x] 2.3 **Paso 2 · `Create_NormalizesEmailToLowerAndTrims` y `NormalizeEmail_LowersInvariant`.** Cubre: TA-3, TA-9.
  - RED: `Email` conserva `  Marta.Responsable@Example.TEST ` y `NormalizeEmail` no existe. GREEN: `NormalizeEmail` público estático = `Trim()` + `ToLowerInvariant()`, usado por `Create`.
  - Verde a la primera la variante con cultura `tr-TR` (`LUIS.EMPLEADO@EXAMPLE.TEST` → `luis.empleado@example.test`, la `I` no se convierte en `ı`); mutación: `ToLower()` en lugar de `ToLowerInvariant()` → falla; revertir.
  - Hecho cuando: el foco pasa y la mutación queda anotada.
  - Evidencia: RED 1: `Expected: "marta.responsable@example.test" / Actual: "  Marta.Responsable@Example.TEST "`. RED 2 (compilación): `CS0117: 'UserAccount' no contiene una definición para 'NormalizeEmail'`. GREEN: `NormalizeEmail` = `Trim().ToLowerInvariant()` usado por `Create`; foco 4/4. La variante `tr-TR` (`NormalizeEmail_LowersInvariant`) es verde a la primera con `NormalizeEmail`; mutación `ToLower()` → ROJO `Expected: "luis.empleado@example.test" / Actual: "luıs.empleado@example.test"`; revertido, foco 4/4.
- [x] 2.4 **Paso 3 · `Create_TrimsDisplayName`.** Cubre: TA-8.
  - RED: `DisplayName` conserva los espacios de `  Marta Ejemplo  `. GREEN: `displayName.Trim()`.
  - Hecho cuando: el foco pasa.
  - Evidencia: RED `Expected: "Marta Ejemplo" / Actual: "  Marta Ejemplo  "`. GREEN: `displayName.Trim()`; foco 5/5.
- [x] 2.5 **Paso 4 · `Create_WithEmptyId_Throws`** (`Assert.ThrowsAny<ArgumentException>`). Cubre: TA-5.
  - RED: «No exception was thrown». GREEN: `id == Guid.Empty` → `ArgumentException`.
  - Hecho cuando: el foco pasa.
  - Evidencia: RED `Assert.ThrowsAny() Failure: No exception was thrown`. GREEN: `id == Guid.Empty` → `ArgumentException`; foco 6/6.
- [x] 2.6 **Paso 5 · `Create_WithBlankOrMalformedOrTooLongEmail_Throws`** (Theory: `""`, `"   "`, `"sin-arroba"`, 255 caracteres). Cubre: TA-6.
  - RED: no lanza. GREEN: correo en blanco, sin `@` o de más de 254 → `ArgumentException`. Añadir `Create_WithEmailOf254Characters_IsValid`: verde a la primera; mutación: `>` → `>=` en el límite → falla; revertir.
  - Hecho cuando: el foco pasa y la mutación queda anotada.
  - Evidencia: RED: 3 casos de la Theory sin excepción (`""`, `"   "`, `"sin-arroba"`; el de 255 caracteres también falla). La Theory incluye los 4 casos con `TheoryData`/`MemberData` (el de 255 no cabe en `InlineData`). GREEN: correo vacío, sin `@` o > 254 → `ArgumentException`; foco 11/11. `Create_WithEmailOf254Characters_IsValid` verde a la primera; mutación `>` → `>=` → ROJO (1 error, 10 verdes); revertido, foco 11/11.
- [x] 2.7 **Paso 6 · `Create_WithBlankOrTooLongDisplayName_Throws`** (en blanco y 101 caracteres). Cubre: TA-7.
  - RED: no lanza. GREEN: validación del nombre. `Create_WithDisplayNameOf100Characters_IsValid` verde a la primera; mutación del límite como en 2.6.
  - Hecho cuando: el foco pasa.
  - Evidencia: RED: 3 casos sin excepción (`""`, `"   "`, 101 caracteres). GREEN: nombre recortado de 1 a 100 caracteres; foco 14/14 (con `…Of100Characters_IsValid`, verde a la primera). Mutación `>` → `>=` en el límite → ROJO (`Create_WithDisplayNameOf100Characters_IsValid`); revertido, foco 15/15.
- [x] 2.8 **Paso 7 · `Create_WithBlankPasswordHash_Throws`.** Cubre: TA-7.
  - RED: no lanza. GREEN: `passwordHash` en blanco → `ArgumentException`.
  - Hecho cuando: el foco pasa.
  - Evidencia: RED: `Create_WithBlankPasswordHash_Throws(passwordHash: "   ")` → `No exception was thrown` (Theory de `""` y `"   "`). GREEN: `string.IsNullOrWhiteSpace(passwordHash)` → `ArgumentException`; foco 17/17.
- [x] 2.9 **Paso 8 · `Create_WithUndefinedRole_Throws`** (`(UserRole)99`). Cubre: TA-10.
  - RED: no lanza. GREEN: `!Enum.IsDefined(role)` → `ArgumentException`.
  - Hecho cuando: el foco pasa.
  - Evidencia: RED `Create_WithUndefinedRole_Throws` → `No exception was thrown`. GREEN: `!Enum.IsDefined(role)`; foco 18/18.
- [x] 2.10 **Pasos 9 y 10 · `Create_WithManagerIdEqualToId_Throws` y `Create_WithEmptyManagerId_Throws`.** Cubre: TA-11.
  - RED: no lanzan. GREEN: `managerId == id` y `managerId == Guid.Empty` → `ArgumentException`.
  - Hecho cuando: el foco pasa.
  - Evidencia: RED: ambas pruebas → `No exception was thrown`. GREEN: `managerId == Guid.Empty || managerId == id` → `ArgumentException`; foco 20/20.
- [x] 2.11 **Paso 11 · `Create_WithNullManagerId_CreatesUserWithoutManager` y `Create_WithManagerId_KeepsIt`.** Cubre: TA-4.
  - Verdes a la primera (la asignación existe desde 2.2). Mutaciones: ignorar `managerId` (asignar `null`) → falla la segunda; lanzar si `managerId` es nulo → falla la primera; revertir.
  - Hecho cuando: el foco pasa y las mutaciones quedan anotadas.
  - Evidencia: Verdes a la primera (foco 22/22). Mutación A (`Create` pasa `null` como `managerId`) → ROJO `Create_WithManagerId_KeepsIt` (`Assert.Equal() Failure: Values differ`); mutación B (lanzar si `managerId is null`) → ROJO (5 pruebas, incluida `Create_WithNullManagerId_CreatesUserWithoutManager`); ambas revertidas, foco 22/22.
- [x] 2.12 **REFACTOR y cobertura de Domain.** Archivos: `UserAccount.cs`. Cubre: AU-60 (Domain); V7, V24 (parte Domain).
  - Extraer los validadores privados, resolver avisos CA cumpliéndolos (sin desactivar `TreatWarningsAsErrors`). V7: `Domain_ReferencesOnlyBaseFramework` sigue en verde.
  - Hecho cuando: `BUILD` rc 0; `T-D --collect:"XPlat Code Coverage" --results-directory TestResults` seguido de `COV-D` rc 0 (100 % líneas y ramas) sin `ExcludeFromCodeCoverage`.
  - Evidencia: Refactor: validadores privados `RequireValidEmail` y `RequireValidDisplayName`; sin avisos CA. `BUILD` rc 0 con 0 avisos. `dotnet test tests/TimeTracker.Domain.Tests -c Release --collect:"XPlat Code Coverage" --results-directory TestResults` → 49/49 (22 de `UserAccountTests` + 27 previas); `COV-D` rc 0: «Paquete TimeTracker.Domain: líneas 100,0 %, ramas 100,0 %» sin `ExcludeFromCodeCoverage`. V7: `DependencyRuleTests` 6/6 (incluye `Domain_ReferencesOnlyBaseFramework`). V24 (parte Domain): `UserAccount` alcanza 100 % con constructor privado.

## Fase 3: Application (TDD con fakes y `FakeTimeProvider`)

Archivos comunes: `AT/*Tests.cs`, `APP/{Users,Authentication,Team,TimeTracking}/*`. Foco: `T-A`.

- [x] 3.1 **Andamiaje (sin comportamiento).** Archivos: `APP/Users/IUserRepository.cs`; `APP/Authentication/{IPasswordHasher,IAccessTokenIssuer,AccessToken,LoginCommand,UserSummary,LoginResult}.cs`; `APP/Team/TeamMemberResult.cs`; `AT/Fakes/{InMemoryUserRepository,FakePasswordHasher,FakeAccessTokenIssuer}.cs`; `AT/Fakes/InMemoryTimeEntryRepository.cs` (añade el contador `ListLatestCalls`). Cubre: precondición de AU-4–AU-13 y TA-24–TA-48.
  - Firmas del diseño §4. `InMemoryUserRepository` respeta el contrato: `FindByEmailAsync` busca por igualdad exacta (no normaliza, para obligar al caso de uso a hacerlo), `ListDirectReportsAsync` filtra por `ManagerId`, ordena por `DisplayName` y respeta el límite recibido (lo registra), `FindDirectReportAsync` solo devuelve reportes directos. `FakePasswordHasher` cuenta llamadas y las de `Verify(null, …)`. `FakeAccessTokenIssuer` registra el usuario recibido. Sin pruebas propias: se validan por uso.
  - Hecho cuando: `BUILD` rc 0.
  - Evidencia: Creados `IUserRepository`, `IPasswordHasher`, `IAccessTokenIssuer`, `AccessToken`, `LoginCommand`, `UserSummary` (sin `From` todavía), `LoginResult`, `TeamMemberResult` y las tres fakes; `InMemoryTimeEntryRepository` suma `ListLatestCalls`. Las fakes añaden `FindByEmailCalls`, `LastEmailRequested` y `LastLimit` (repositorio), `VerifyCalls`, `NullHashVerifications` y `LastVerifiedPassword` (hasher). `BUILD` rc 0 con 0 avisos.
- [x] 3.2 **`TimeEntryListResult.From` (DD-17).** Archivos: `AT/TimeEntryListResultTests.cs`, `APP/TimeTracking/{TimeEntryResult,GetTimeEntriesUseCase}.cs`. Cubre: TK-12 (regresión); DD-17.
  - RED (compilación): `From_WithClosedAndOpenEntries_TotalsOnlyClosed` y `From_WithNoEntries_ReturnsEmptyAndZero`. GREEN: `From(IReadOnlyList<TimeEntry>)` con la suma de cerrados. REFACTOR: `GetTimeEntriesUseCase` lo usa; las 5 pruebas existentes de `GetTimeEntries` quedan en verde antes y después (anotar).
  - Hecho cuando: `T-A` en verde.
  - Evidencia: RED (compilación) `CS0117: 'TimeEntryListResult' no contiene una definición para 'From'` (las 5 pruebas de `GetTimeEntries` estaban en verde en la línea base de 1.1; no se pudo repetir «antes» tras el RED porque el proyecto no compilaba). GREEN: `From` con la suma de cerrados y `GetTimeEntriesUseCase` lo usa (pierde el `using TimeTracker.Domain`). `T-A` 16/16 (14 previas, incluidas las 5 de `GetTimeEntries`, + 2 nuevas).
- [x] 3.3 **Login · paso 1 · `Login_WithValidCredentials_ReturnsTokenAndSummary` y `Login_PassesTheFoundUserToTheIssuer`.** Archivos: `AT/LoginUseCaseTests.cs`, `APP/Authentication/LoginUseCase.cs`, `UserSummary.From`. Cubre: AU-4 y AU-6 (nivel unitario).
  - RED (compilación): `LoginUseCase` no existe. GREEN mínimo: busca por el correo recibido, emite el token con el usuario hallado y devuelve `LoginResult(token, UserSummary.From(user))` (sin verificar contraseña todavía; `UserSummary` no tiene propiedad de hash).
  - Hecho cuando: el foco pasa 2 pruebas.
  - Evidencia: RED (compilación) `CS0246: 'LoginUseCase' no se encontró`. GREEN mínimo + `UserSummary.From`; foco 2/2. **Matiz:** el parámetro `hasher` sin leer provoca `CS9113` (error con `-warnaserror`), así que el GREEN incluyó el transitorio `_ = hasher;` hasta 3.4 (retirado allí).
- [x] 3.4 **Login · paso 2 · `Login_WithWrongPassword_ReturnsNullAndIssuesNothing`.** Cubre: AU-11 (unitario).
  - RED: devuelve token. GREEN: `Verify(user.PasswordHash, password)`; falso → `null`. Comprobar que la contraseña llega sin recortar (`" clave "` se verifica tal cual).
  - Hecho cuando: el foco pasa.
  - Evidencia: RED: `Login_WithWrongPassword_ReturnsNullAndIssuesNothing` devuelve token; `Login_VerifiesThePasswordWithoutTrimming` `Assert.Equal() Failure: Strings differ`. GREEN: `hasher.Verify(user.PasswordHash, password)`; foco 4/4. `Login_VerifiesThePasswordWithoutTrimming` es verde con el GREEN; mutación `command.Password.Trim()` → ROJO (`Assert.NotNull() Failure: Value is null`); revertido.
- [x] 3.5 **Login · paso 3 · `Login_WithUnknownEmail_ReturnsNullAndVerifiesOnceWithNullHash`.** Cubre: AU-10 (unitario), AU-13.
  - RED: `Verify` no se llama con hash nulo (NullReference o 0 llamadas). GREEN: `Verify(user?.PasswordHash, password)` **siempre**; usuario nulo o verificación falsa → `null` (DD-6).
  - Hecho cuando: el foco pasa; `FakePasswordHasher.NullHashVerifications` = 1.
  - Evidencia: RED `System.NullReferenceException` (usuario nulo, `Verify` sin hash nulo). GREEN: `Verify(user?.PasswordHash, password)` siempre y `user is null || !passwordMatches` → `null`; foco 5/5. `NullHashVerifications` = 1 y `VerifyCalls` = 1 comprobados en la prueba.
- [x] 3.6 **Login · paso 4 · `Login_NormalizesTheEmailBeforeLookup`** (`  Luis.Empleado@Example.TEST `). Cubre: AU-5 (unitario).
  - RED: la fake solo conoce el correo normalizado, no lo encuentra. GREEN: `UserAccount.NormalizeEmail(command.Email)` antes de `FindByEmailAsync`.
  - Hecho cuando: el foco pasa.
  - Evidencia: RED `Assert.NotNull() Failure: Value is null` (la fake solo conoce el correo normalizado). GREEN: `UserAccount.NormalizeEmail(email)` antes de `FindByEmailAsync`; foco 6/6; la prueba comprueba además `LastEmailRequested` = `luis.empleado@example.test`.
- [x] 3.7 **Login · paso 5 · `Login_WithInvalidShape_ReturnsNullWithoutQueryingOrVerifying`** (Theory: correo nulo, en blanco, de 255 caracteres; contraseña nula, vacía, `"   "`, de 129). Cubre: AU-8, AU-7 (unitario); DD-7.
  - RED: consulta o lanza. GREEN: guarda de forma antes de buscar (`string.IsNullOrWhiteSpace` en ambos, conforme a la spec «ausentes o en blanco»; ver D5), `MaxPasswordLength = 128`, `UserAccount.MaxEmailLength`. Ni el repositorio ni el hasher reciben llamadas.
  - Añadir `Login_WithBoundaryLengths_IsAccepted` (correo de 254 y contraseña de 128): verde a la primera; mutación `>` → `>=` en cada límite → falla; revertir.
  - Hecho cuando: el foco pasa y las mutaciones quedan anotadas.
  - Evidencia: RED (compilación) `CS0117: 'LoginUseCase' no contiene una definición para 'MaxPasswordLength'`; con la constante: RED de 6 de 8 casos (`Assert.Equal() Failure: Values differ` en `FindByEmailCalls`/`VerifyCalls`; `NullReferenceException` con correo nulo). GREEN: `HasValidShape` (`IsNullOrWhiteSpace` en ambos —D5—, correo ≤ `UserAccount.MaxEmailLength`, contraseña ≤ 128); foco 15/15. `Login_WithBoundaryLengths_IsAccepted` verde a la primera; mutaciones `<=` → `<` en el correo y en la contraseña → ROJO (1 error cada una); revertidas, foco 15/15.
- [x] 3.8 **Equipo · listado.** Archivos: `AT/GetTeamMembersUseCaseTests.cs`, `APP/Team/GetTeamMembersUseCase.cs`. Cubre: TA-24, TA-25, TA-26.
  - RED (compilación): `GetTeamMembersUseCase` no existe. GREEN mínimo: `ListDirectReportsAsync(currentUser.EmployeeId, int.MaxValue)` y mapeo a `TeamMemberResult(Id, DisplayName)`. Pruebas: `Get_ReturnsOnlyDirectReportsOfTheCurrentUser` (marta con luis y ana; pablo sin responsable), `Get_DoesNotIncludeTransitiveReports` (marta → diego → carmen: solo diego), `Get_WithoutReports_ReturnsEmpty`.
  - Las dos últimas son verdes a la primera sobre la fake; mutaciones: la fake devuelve descendientes → falla la segunda; el caso de uso pasa `Guid.Empty` como responsable → falla la primera. La garantía real está en 6.3.
  - Hecho cuando: el foco pasa y las mutaciones quedan anotadas.
  - Evidencia: RED (compilación) `CS0246: 'GetTeamMembersUseCase' no se encontró`. GREEN: `ListDirectReportsAsync(currentUser.EmployeeId, int.MaxValue)` + mapeo; foco 3/3. Mutación 1 (la fake incluye descendientes) → ROJO `Get_DoesNotIncludeTransitiveReports`; mutación 2 (responsable `Guid.Empty`) → ROJO `Get_DoesNotIncludeTransitiveReports` y `Get_ReturnsOnlyDirectReportsOfTheCurrentUser` (el primer intento de la mutación 2 no compilaba por `CS9113` y se rehízo; solo cuenta la ejecución real). Revertidas.
- [x] 3.9 **Equipo · límite de 100 (unitaria).** Cubre: TA-29 (nivel Application; la prueba de API es 15.4).
  - RED: `Get_With101Reports_RequestsLimit100AndReturns100` (101 reportes en la fake; `fake.LastLimit` = 100 y la lista tiene 100) → `Actual: 101 / Expected: 100`. GREEN: `public const int MaxMembers = 100` pasado al repositorio.
  - Hecho cuando: el foco pasa.
  - Evidencia: RED (compilación) `CS0117 … 'MaxMembers'`; con la constante pero pasando `int.MaxValue`: `Expected: 100 / Actual: 2147483647`. GREEN: `MaxMembers = 100` pasado al repositorio; foco 4/4.
- [x] 3.10 **Fichajes de un miembro · paso 1.** Archivos: `AT/GetTeamMemberTimeEntriesUseCaseTests.cs`, `APP/Team/GetTeamMemberTimeEntriesUseCase.cs`. Cubre: TA-32 (unitario).
  - RED (compilación). Prueba `Get_ForADirectReport_ReturnsEntriesAndTotal` (cerrados de 60 y 30 minutos y uno abierto: total 90, abierto con `WorkedMinutes` nulo). GREEN mínimo: `ListLatestAsync(memberId, int.MaxValue)` y `TimeEntryListResult.From`.
  - Hecho cuando: el foco pasa.
  - Evidencia: RED (compilación) `CS0246: 'GetTeamMemberTimeEntriesUseCase' no se encontró`. GREEN mínimo con `ListLatestAsync(memberId, int.MaxValue)` + `From`; foco 1/1. Mismo matiz que 3.3: transitorio `_ = (users, currentUser);` por `CS9113`, retirado en 3.11.
- [x] 3.11 **Fichajes de un miembro · paso 2 · control por objeto.** Cubre: TA-43, TA-44, TA-46, TA-48 (unitario); DD-15.
  - RED: `Get_ForNonDirectReport_ReturnsNullWithoutQueryingEntries` (Theory: miembro de otro responsable, miembro sin responsable, el propio responsable, id inexistente) → devuelve la lista o consulta fichajes (`ListLatestCalls` > 0). GREEN: `FindDirectReportAsync(currentUser.EmployeeId, memberId)`; nulo → `null` sin consultar fichajes.
  - Hecho cuando: el foco pasa; `ListLatestCalls` = 0 en los cuatro casos.
  - Evidencia: RED: los casos de la Theory fallan con `Assert.Null() Failure: Value is not null` (el GREEN transitorio devolvía una lista para cualquier id; se vieron los tres primeros fallos de la salida). GREEN: `FindDirectReportAsync(currentUser.EmployeeId, memberId)`; nulo → `null`; foco 5/5; `ListLatestCalls` = 0 en los cuatro casos.
- [x] 3.12 **Fichajes de un miembro · paso 3 · últimos 50.** Cubre: TA-33 (unitario).
  - RED: `Get_WithMoreThanFiftyEntries_ReturnsFiftyMostRecentAndTotal500` (55 cerrados de 10 minutos) → `Actual: 55 / Expected: 50`. GREEN: reutilizar `GetTimeEntriesUseCase.MaxEntries` (50) como límite.
  - Hecho cuando: el foco pasa.
  - Evidencia: RED `Expected: 50 / Actual: 55`. GREEN: `GetTimeEntriesUseCase.MaxEntries` como límite; foco 6/6 (50 elementos, total 500, primero = día 54).
- [x] 3.13 **Fichajes de un miembro · paso 4.** Cubre: TA-34, TA-35 (unitario).
  - `Get_ForMemberWithoutEntries_ReturnsEmptyAndZero` y `Get_OnlyQueriesTheMembersEntries` (fichajes de luis, ana y marta; el resultado solo contiene los de luis). Verdes a la primera. Mutaciones: total inicial 1 → falla la primera; consultar `currentUser.EmployeeId` en lugar de `memberId` → falla la segunda; revertir.
  - Hecho cuando: el foco pasa y las mutaciones quedan anotadas.
  - Evidencia: Verdes a la primera (foco 8/8). Mutación 1 (`WorkedHours.FromMinutes(1)` como total inicial en `From`) → ROJO 4 pruebas, entre ellas `Get_ForMemberWithoutEntries_ReturnsEmptyAndZero`; mutación 2 (consultar `currentUser.EmployeeId` en lugar del miembro) → ROJO `Get_OnlyQueriesTheMembersEntries` y otras 2; revertidas, `T-A` completo 43/43.
- [x] 3.14 **REFACTOR y cobertura de Application.** Cubre: AU-60 (Application); lección V17 archivada (cobertura medida solo con `Application.Tests`).
  - Revisar duplicación entre los casos de uso, cumplir CA1848/CA1032 si aparecen. `Application_ReferencesOnlyDomainAndBaseFramework` sigue en verde.
  - Hecho cuando: `BUILD` rc 0; `T-A --collect:"XPlat Code Coverage" --results-directory TestResults\app-only` y `pwsh scripts/check-coverage.ps1 -ResultsDirectory TestResults\app-only -Package TimeTracker.Application -MinimumPercent 80` rc 0.
  - Evidencia: Sin duplicación nueva (el cálculo vive solo en `TimeEntryListResult.From`); sin avisos CA1848/CA1032. `BUILD` rc 0 con 0 avisos. `T-A -c Release --collect … --results-directory TestResults\app-only` → 43/43; `check-coverage.ps1 … TestResults\app-only -Package TimeTracker.Application -MinimumPercent 80` rc 0: líneas 100,0 %, ramas 100,0 %. `DependencyRuleTests` 6/6 (`Application_ReferencesOnlyDomainAndBaseFramework` en verde).

## Fase 4: Punto de control de la Tanda 1

- [x] 4.1 **Puerta de calidad.** Cubre: SA-16, SA-18; AU-60.
  - `RESTORE`, `FORMAT`, `BUILD`, `TEST`, `COV-D`, `COV-A`, `VULN`.
  - Hecho cuando: todos terminan en 0, `BUILD` con 0 avisos y las pruebas de Domain, Application y arquitectura pasan.
  - Evidencia: `ENV-CHECK` vacío. `RESTORE` rc 0. `FORMAT` rc 0 (tras corregir una línea en blanco entre grupos de `using` en `UserAccountTests.cs` que `dotnet format` señaló con `WHITESPACE`). `BUILD` rc 0 con 0 avisos y 0 errores. `TEST` rc 0 sobre `TestResults` limpio: **205** pruebas = Domain 49 (+22), Application 43 (+29), Arquitectura 47 (+3), Integración 66 (sin cambios) frente a las 151 de la línea base. `COV-D` rc 0 (líneas 100,0 %, ramas 100,0 %); `COV-A` rc 0 (líneas 100,0 %, ramas 100,0 %, mínimo 80 %). `VULN` rc 0 y sin «has the following vulnerable packages».
- [x] 4.2 **Revisión, líneas y PARADA.**
  - Anotar V1, V2, V7 y la lección V17; contar las líneas escritas a mano frente a 450–600. **PARADA:** el usuario decide si pasa a la Tanda 2.
  - Hecho cuando: checklist de la Tanda 1 completa y evidencias registradas.
  - Evidencia: Revisión de la Tanda 1. **V1** resuelta (10.0.10 sin NU190x; `Microsoft.IdentityModel.JsonWebTokens` 8.19.2), **V2** resuelta (`Components.Authorization` 10.0.10 compila con `Components.WebAssembly` 10.0.10), **V7** (dependencias: `Domain_ReferencesOnlyBaseFramework` y `Application_ReferencesOnlyDomainAndBaseFramework` en verde), **V24** parte Domain (100 % con constructor privado; el enlace de EF queda para 5.3/6.1) y **V17 archivada** (cobertura de Application medida solo con `Application.Tests`: 100 %). Líneas a mano (`wc -l`, incluye blancos y documentación XML): `src` 269 (243 nuevas + 26 de refactor), pruebas 645 (613 nuevas en Domain/Application/fakes + 32 en `InMemoryTimeEntryRepository` y `RepositoryConventionsTests`), props y `.csproj` 5; **total ≈ 919 frente a 450–600** (≈ 1,5–2× la previsión; el exceso viene de las pruebas, 645 frente a 265–360, y de la documentación XML de los puertos). **PARADA:** el usuario decide si pasa a la Tanda 2.

---

# TANDA 2: Infrastructure y Testcontainers

## Fase 5: Esquema `users` y migración `AddUsers`

Pruebas en `IT/Persistence` con `[Collection("Postgres")]` y una base propia por prueba cuando se migra. Foco: `T-I --filter "FullyQualifiedName~Persistence"`.

- [x] 5.1 **Copia previa y línea base.** Archivos: `..\TimeTracker.rollback\tanda-2\` (4 existentes). Cubre: precondición del plan de reversión.
  - `Save-Rollback 2 …` con los 4 archivos. `ENV-CHECK`; Docker en marcha: `docker run --rm postgres:18-alpine postgres --version` imprime `postgres (PostgreSQL) 18.x`.
  - Hecho cuando: `(Get-ChildItem -Recurse -File ..\TimeTracker.rollback\tanda-2).Count` = 4 y `BUILD` y `T-I --filter "FullyQualifiedName~Persistence"` terminan en 0.
  - Evidencia: Carpeta `..\TimeTracker.rollback\tanda-2` no existía (el encargo decía lo contrario): se creó con los 4 archivos, Count = 4. `ENV-CHECK` vacío; `postgres (PostgreSQL) 18.6`; `BUILD` rc 0 con 0 avisos; foco `Persistence` 16/16.
- [x] 5.2 **RED del esquema y `UserSeed`.** Archivos: `IT/Support/UserSeed.cs`, `IT/Persistence/UserSchemaTests.cs`, `IT/Persistence/MigrationTests.cs`. Cubre: PE-4, TA-12.
  - `UserSeed`: crea `UserAccount` con correo único `…{Guid:N}@example.test`, un hash `PasswordHasher<object>` **calculado una sola vez** y compartido (rendimiento: 101 reportes en 15.4) y `InsertRawAsync` con SQL para filas que `Create` rechazaría (restricciones, hash ilegible en 10.3).
  - Pruebas: `Migrate_OnEmptyDatabase_AppliesInitialAndAddUsersInOrder` (PE-4: `GetAppliedMigrationsAsync` termina en `…_InitialTimeEntries`, `…_AddUsers`; existen `time_entries` y `users`) y `Users_Table_HasSixColumnsAndOnlyManagerIdIsNullable` (TA-12, `information_schema.columns`). RED: no existe `users` / `DbSet` (compilación).
  - Hecho cuando: el foco falla por los motivos anotados.
  - Evidencia: RED (compilación): `CS1061: 'TimeTrackerDbContext' no contiene una definición para 'Users'` (desde `UserSeed.AddAsync`). `UserSeed` usa un `PasswordHasher<object>` calculado una sola vez (`Lazy`) y `InsertRawAsync` por SQL parametrizado.
- [x] 5.3 **GREEN del mapeo y la migración (V5 archivada, V24).** Archivos: `INF/Persistence/{UserConfiguration,TimeTrackerDbContext}.cs`, `Migrations/*_AddUsers*.cs` y snapshot (generados). Cubre: PE-3, PE-4, PE-6, TA-12; V24.
  - `UserConfiguration` según el diseño §6: `ToTable("users", …)` con las 3 `CHECK` (`ck_users_role`, `ck_users_manager_not_self`, `ck_users_email_lowercase`), `HasColumnName` explícitos, `Role` con `HasConversion<string>().HasMaxLength(20)`, FK autorreferencial `fk_users_users_manager_id` `Restrict`, índices `ux_users_email` (único) e `ix_users_manager_id`, `Id` con `ValueGeneratedNever()`. `DbSet<UserAccount> Users`. Generar con la herramienta local, con el mismo comando del README que generó `InitialTimeEntries`: `dotnet tool run dotnet-ef migrations add AddUsers --project src/TimeTracker.Infrastructure …`.
  - V24: EF enlaza `UserAccount` por el constructor privado y `Role` se guarda como texto (se confirma con 6.1).
  - Hecho cuando: el foco de 5.2 pasa; `MigrationTests` (`HasPendingModelChanges() == false`) en verde; `FORMAT` rc 0 sin reformatear la migración; `T-AR --filter "FullyQualifiedName~DotnetTools"` en verde.
  - Evidencia: GREEN: `UserConfiguration` (3 CHECK, columnas explícitas, `Role` como `string` de hasta 20, FK autorreferencial, índices `ux_users_email` e `ix_users_manager_id`, `Id` `ValueGeneratedNever`), `DbSet<UserAccount> Users` y migración `20261013114829_AddUsers` generada con el comando del README. Se ajustó `Migrate_OnEmptyDatabase_CreatesTableIndexesAndCheck` (ya no es una sola migración: `Assert.Contains` en lugar de `Single`). `BUILD` rc 0 con 0 avisos; foco `Persistence` 18/18 (`Model_HasNoPendingChanges` en verde); `FORMAT` rc 0 sin reformatear la migración; `T-AR --filter DotnetTools` 1/1.
- [x] 5.4 **Restricciones de `users`.** Archivos: `IT/Persistence/UserConstraintsTests.cs`. Cubre: TA-13, TA-14, TA-15, TA-16, TA-17, TA-18.
  - Inserciones por SQL (Npgsql) comprobando `PostgresException.SqlState` y `ConstraintName`: correo duplicado `23505` (`ux_users_email`), `manager_id` inexistente `23503`, borrar a `marta` con `luis` de reporte `23503` y `luis` conserva su `manager_id`, `manager_id = id` `23514` (`ck_users_manager_not_self`), `role = 'Admin'` `23514` (`ck_users_role`), `Luis.Empleado@example.test` `23514` (`ck_users_email_lowercase`).
  - Verdes a la primera (el esquema ya existe desde 5.3). Mutaciones, una a una sobre una copia temporal de la migración (`Copy-Item` a la carpeta del scratchpad y restaurar): quitar el índice único, la FK, cada `CHECK` → cada prueba falla; revertir y comparar el archivo con su copia.
  - Hecho cuando: seis pruebas en verde y las seis mutaciones anotadas.
  - Evidencia: Primera ejecución: 5 verdes y 1 roja: `DeletingAManagerWithReports_IsRestricted` esperaba `23503` y PostgreSQL devolvió `23001` con `ON DELETE RESTRICT`. Respaldo aplicado (desviación D14, ver 7.2): `DeleteBehavior.NoAction` y migración regenerada → 6/6 (SQLSTATE `23505` `ux_users_email`; `23503` `fk_users_users_manager_id` ×2; `23514` `ck_users_manager_not_self`, `ck_users_role`, `ck_users_email_lowercase`). Seis mutaciones sobre la migración (copia en el scratchpad, restaurada y comparada, idéntica): índice único → ROJO `DuplicateEmail…`; sin FK → ROJO `UnknownManagerId…` y `DeletingAManager…`; FK con `Cascade` → ROJO `DeletingAManager…`; sin `ck_users_manager_not_self`, sin `ck_users_role`, sin `ck_users_email_lowercase` → ROJO la prueba correspondiente en cada caso (1 fallo, 5 verdes).
- [x] 5.5 **Sin FK en `time_entries`.** Archivos: `IT/Persistence/TimeEntriesForeignKeyTests.cs`. Cubre: PE-1, PE-2.
  - `TimeEntries_HaveNoForeignKeyAndKeepPrimaryKeyAndCheck` (`pg_constraint`: ninguna de tipo `f`; sí la PK y `ck_time_entries_clock_out_after_clock_in`) y `OrphanEntry_IsAccepted` (`employee_id` sin fila en `users`). Verdes a la primera; mutación: `AddForeignKey` de `time_entries.employee_id` a `users` en la migración → fallan ambas; revertir.
  - Hecho cuando: el foco pasa y la mutación queda anotada.
  - Evidencia: Verdes a la primera (foco 2/2; `contype::text` necesario porque `contype` es de tipo `"char"`). Mutación: `AddForeignKey` de `time_entries.employee_id` a `users` en `AddUsers` → ROJO las dos pruebas; revertido y comparado con la copia (idéntico).
- [x] 5.6 **`AddUsers` sobre una base con fichajes.** Archivos: `IT/Persistence/MigrationTests.cs`. Cubre: PE-5.
  - `AddUsers_OnDatabaseWithEntries_KeepsThem`: migrar hasta `InitialTimeEntries` (`IMigrator.MigrateAsync(<id>)`), insertar un fichaje por SQL, aplicar el resto y comparar la fila (mismos valores). Verde a la primera; mutación: `migrationBuilder.Sql("DELETE FROM time_entries")` en `Up` → falla; revertir.
  - Hecho cuando: el foco pasa y la mutación queda anotada.
  - Evidencia: Verde a la primera (`AddUsers_OnDatabaseWithEntries_KeepsThem`, con `IMigrator.MigrateAsync(<id de InitialTimeEntries>)`, un fichaje por SQL y el resto de migraciones). Mutación: `migrationBuilder.Sql("DELETE FROM time_entries")` en `Up` → ROJO; revertido y comparado con la copia (idéntico). Foco `Persistence` 27/27.

## Fase 6: Repositorio de usuarios

- [x] 6.1 **Ida y vuelta (V24).** Archivos: `IT/Persistence/UserRepositoryTests.cs`, `INF/Persistence/UserRepository.cs`, `PersistenceServiceCollectionExtensions.cs`. Cubre: TA-19; V24.
  - RED (compilación): `AddAsync_ThenFindByEmail_RoundTripsAllProperties` con un `Manager` y un contexto nuevo; comprueba `Id`, `Email`, `DisplayName`, `PasswordHash`, `Role`, `ManagerId` y `SELECT role` = `'Manager'`. GREEN: `UserRepository` (`AsNoTracking` en lecturas, `AddAsync` con `SaveChangesAsync`) con los otros dos métodos como `NotImplementedException` transitorio (se retiran en 6.3); `AddScoped<IUserRepository, UserRepository>`.
  - Hecho cuando: el foco pasa.
  - Evidencia: RED (compilación): `CS0246: el nombre del tipo … 'UserRepository' no se encontró`. GREEN: `UserRepository` (`AsNoTracking` en lecturas, `AddAsync` con `SaveChangesAsync`), los otros dos métodos como `NotImplementedException` transitorio, y `AddScoped<IUserRepository, UserRepository>`; foco 1/1 (incluye `SELECT role` = `'Manager'`). **V24 confirmada:** la ida y vuelta rehidrata por el constructor privado.
- [x] 6.2 **Búsqueda por correo.** Cubre: TA-20.
  - RED: `FindByEmailAsync_IsCaseInsensitiveAndReturnsNullWhenMissing` (`Luis.Empleado@Example.Test` encuentra a luis; `nadie@example.test` → nulo) → nulo para el primero. GREEN: el repositorio vuelve a normalizar con `UserAccount.NormalizeEmail` (idempotente; ver D4).
  - Hecho cuando: el foco pasa.
  - Evidencia: RED: `Assert.Equal() Failure: Values differ / Actual: null` (búsqueda exacta). GREEN: `UserAccount.NormalizeEmail` dentro del repositorio; foco 2/2.
- [x] 6.3 **Reportes directos.** Cubre: TA-21, TA-22, TA-23, TA-25 (garantía real).
  - RED: `ListDirectReportsAsync_ReturnsDirectReportsOrderedByDisplayName` (marta con luis y ana; pablo sin responsable; carmen reporte de ana → `ana`, `luis`, sin `marta`, `pablo` ni `carmen`), `…_RespectsTheLimit` (3 reportes, límite 2 → los dos primeros por nombre), `FindDirectReportAsync_ReturnsOnlyDirectReports` (`(marta, luis)` → luis; `(marta, pablo)`, `(marta, marta)` e id inexistente → nulo). GREEN: LINQ parametrizado (`Where` por `ManagerId`, `OrderBy(DisplayName)`, `Take`); retirar los `NotImplementedException`.
  - Nota: el orden depende de la colación de la base; las pruebas usan nombres ASCII. Con acentos o mayúsculas podría variar (riesgo anotado).
  - Hecho cuando: el foco pasa; `Select-String 'NotImplementedException' -Path INF -Recurse` sin coincidencias.
  - Evidencia: RED: las 3 pruebas fallan con `NotImplementedException`. GREEN: `Where(ManagerId)` + `OrderBy(DisplayName)` + `Take(limit)` y `FirstOrDefault(Id == memberId && ManagerId == managerId)` (el propio responsable queda excluido porque `manager_id <> id`); foco `UserRepositoryTests` 5/5. Sin `NotImplementedException` en `src`. Riesgo anotado: el orden depende de la colación de la base; las pruebas usan nombres ASCII.
- [x] 6.4 **Correos únicos por prueba.** Archivos: `IT/Persistence/UserRepositoryTests.cs`. Cubre: PE-13; amenaza A09 (datos de prueba).
  - `UserSeed_UnderParallelUse_NeverCollidesOnEmail` (`Task.WhenAll` de 20 inserciones en la misma base). Verde a la primera; mutación: correo fijo en `UserSeed` → falla con `23505`; revertir.
  - Hecho cuando: el foco pasa 3 ejecuciones seguidas y la mutación queda anotada.
  - Evidencia: Verde a la primera: `UserSeed_UnderParallelUse_NeverCollidesOnEmail` (20 altas con `Task.WhenAll`); 3 ejecuciones seguidas del foco `UserRepositoryTests` 6/6. Mutación: correo fijo `{name}@example.test` en `UserSeed` → ROJO `23505 duplicate key … "ux_users_email"`; revertido y comparado con la copia (idéntico).

## Fase 7: Punto de control de la Tanda 2

- [x] 7.1 **Puerta de calidad.** Cubre: SA-16, SA-18 (parcial); PE-3.
  - `FORMAT`, `BUILD`, `TEST`; `Select-String 'FromSqlRaw|ExecuteSqlRaw|EnableSensitiveDataLogging|NotImplementedException' -Path src -Recurse` sin coincidencias.
  - Hecho cuando: todo en 0 y sin coincidencias.
  - Evidencia: `FORMAT` rc 0; `RESTORE` rc 0; `BUILD` rc 0 con 0 avisos y 0 errores; `TEST` sobre `TestResults` limpio rc 0: **222** pruebas = Domain 49, Application 43, Arquitectura 47 (sin cambios), Integración 83 (+17 sobre las 66; ejecutadas contra PostgreSQL 18 real con Testcontainers); `COV-D` rc 0 (100,0 %/100,0 %) y `COV-A` rc 0 (100,0 %/100,0 %). Búsqueda `FromSqlRaw|ExecuteSqlRaw|EnableSensitiveDataLogging|NotImplementedException` en `src`: sin coincidencias. Sin contenedores de Testcontainers residuales (`docker ps -a`).
- [x] 7.2 **Revisión, líneas y PARADA.**
  - Anotar V24 (parte EF) y el resultado de las mutaciones; líneas reales frente a 150–220. **PARADA:** el usuario decide si pasa a la Tanda 3a.
  - Hecho cuando: checklist de la Tanda 2 completa.
  - Evidencia: Revisión de la Tanda 2. **V24** resuelta (EF enlaza `UserAccount` por el constructor privado y `Role` se guarda como texto `'Manager'`, probado en 6.1; `HasPendingModelChanges() == false`). Mutaciones: 5.4 seis de seis rojas, 5.5 y 5.6 rojas, 6.4 roja (`23505`); todas revertidas y comparadas con la copia. **Desviación D14:** la FK `fk_users_users_manager_id` usa `ON DELETE NO ACTION` (`DeleteBehavior.NoAction`) en lugar de `RESTRICT` del diseño §6, porque PostgreSQL devuelve `23001` (`restrict_violation`) con `RESTRICT` y la spec TA-15 exige `23503`; la protección es la misma (la FK no es diferible). Migración regenerada (se borraron sus archivos y se restauró el snapshot desde `tanda-2` porque `migrations remove` exige conexión a la base). Líneas a mano (`wc -l`): `src` 76 (`UserConfiguration` 38, `UserRepository` 33, +3 `DbContext`, +2 registro), pruebas ≈ 415 (`UserSeed` 51, `UserSchemaTests` 29, `UserConstraintsTests` 99, `TimeEntriesForeignKeyTests` 50, `UserRepositoryTests` 133, +53 en `MigrationTests`), props/csproj 0; **total ≈ 491 frente a 150–220** (≈ 2,2× la previsión; el exceso viene de las pruebas, ≈ 415 frente a 75–115). Migración generada (`AddUsers` 57 + Designer 126 líneas) no cuenta. **PARADA:** el usuario decide si pasa a la Tanda 3a.

---

# TANDA 3a: Api núcleo (JWT, hasher, identidad desde el token)

## Fase 8: Cimientos de configuración y de pruebas

- [x] 8.1 **Copia previa y línea base.** Archivos: `..\TimeTracker.rollback\tanda-3a\` (24 existentes, ver plan). Cubre: precondición del plan de reversión.
  - `Save-Rollback 3a …`; `ENV-CHECK` (con `Jwt__` y `Seed__`); `BUILD` y `T-I` en verde.
  - Hecho cuando: `(Get-ChildItem -Recurse -File ..\TimeTracker.rollback\tanda-3a).Count` = 24 y la línea base está en verde.
  - Evidencia: Copia en `..\TimeTracker.rollback\tanda-3a` con los 24 archivos existentes (Count = 24; se hizo con `cp` conservando rutas relativas, sin sobrescribir). `ENV-CHECK` vacío (con `Jwt__` y `Seed__`); Docker 29.7.2 en marcha. Línea base: `BUILD` rc 0 con 0 avisos; `T-I` 83/83 (Release).
- [x] 8.2 **`TestTokens` y fábricas (V14, V6).** Archivos: `IT/TestTokens.cs`, `IT/ApiFactory.cs`, `IT/TimeEntries/PostgresApiFactory.cs`. Cubre: precondición de AU-18–AU-30 y TK-1–TK-4.
  - `TestTokens.Create(Guid userId, string role, DateTimeOffset issuedAt, TimeSpan? lifetime = null, string? signingKey = null, string issuer = "timetracker-api", string audience = "timetracker-web", …)` más opciones `subject` (texto), `name`, `email`, `algorithm` (HS256/HS512), `notBefore`, `expires`; `CreateUnsigned` (`alg: none`) y `ReplacePayload(token, …)` (conserva la firma original). Se construye **a mano** (base64url + `HMACSHA256/512`), sin reutilizar el emisor de producción. Vida por defecto 30 días (D8).
  - `ApiFactory`: constante `ValidSigningKey` (≥ 32 bytes inventados) aportada como `Jwt:SigningKey` (opción virtual para no aportarla en SC-1); `Clock` (`FakeTimeProvider` en `2026-10-12T08:00:00Z`) pasa a la base y sustituye a `TimeProvider` en `ConfigureTestServices` (D7); `ConfigureClient` añade `Authorization: Bearer` de `DefaultUserId` con rol `Employee`; `CreateAnonymousClient()` = `CreateDefaultClient()` sin cabecera. `PostgresApiFactory(connectionString, employeeId?, role = "Employee")` emite el token con `Clock.GetUtcNow()`. Se conserva `Identity:DevelopmentEmployeeId` hasta 12.2.
  - Sin pruebas propias: el control positivo de `TestTokens` está en 10.5 y 11.1 (un token válido se acepta; los negativos difieren en una sola cosa).
  - Hecho cuando: `BUILD` rc 0 y `T-I` sigue en verde.
  - Evidencia: Creados `IT/TestTokens.cs` (JWT a mano con base64url + `HMACSHA256/512`; `Create`, `CreateUnsigned`, `ReplacePayload(token, Action<JsonObject>)`; vida por defecto 30 días) y reescritos `ApiFactory` y `PostgresApiFactory`. `ApiFactory` suma `ValidSigningKey` (40 bytes inventados), `DefaultUserId`, `Clock` (`FakeTimeProvider` 2026-10-12T08:00Z, sustituye a `TimeProvider` con `ConfigureTestServices`, y `PostgresApiFactory` ya no tiene reloj propio), `CreateAnonymousClient()` (= `CreateDefaultClient()`, que no invoca `ConfigureClient`), `CreateClientFor(id, role)`, `TokenFor` y `ConfigureClient` con Bearer del usuario por defecto (ganchos virtuales `AuthenticatedUserId`/`AuthenticatedRole`). `PostgresApiFactory(connectionString, employeeId?, role = "Employee")`. Desviación menor: no se hizo virtual la clave de firma; el patrón `UseSetting(clave, null)` ya existente basta para quitarla. `Identity:DevelopmentEmployeeId` se conserva (transitorio hasta 12.2). `BUILD` rc 0 con 0 avisos; `T-I` 83/83.
- [x] 8.3 **Clave de firma validada al arrancar (V21).** Archivos: `IT/Startup/JwtStartupTests.cs`, `API/Authentication/{JwtOptions,JwtOptionsValidator,JwtDefaults,AuthenticationServiceCollectionExtensions}.cs`, `API/Configuration/DocumentGeneration.cs`, `API/Program.cs`. Cubre: SC-1, SC-2, SC-3, SC-4, SC-5, AU-59 (parte), SA-26 (parte); V21.
  - RED: `Startup_WithoutSigningKey_FailsNamingKey`, `Startup_WithBlankSigningKey_Fails`, `Startup_With31Bytes_FailsNamingKey` y `Startup_DoesNotPrintTheSigningKey` (`corta-secreta-prueba`, también en excepciones internas) → `Assert.Contains() Failure: Filter not matched`. GREEN: `JwtOptions`, `JwtOptionsValidator` («Jwt:SigningKey: debe definirse.» / «debe tener al menos 32 bytes.», con `Encoding.UTF8.GetByteCount`), `ValidateOnStart`, y **en la misma tarea** `Jwt:SigningKey` ficticia (≥ 32 bytes) en `DocumentGeneration`.
  - Verde a la primera: `Startup_With32AsciiOr16EnyeChars_StartsAndHealthIsOk` (SC-4). Mutación: comparar `Length` en lugar de bytes → falla el caso de 16 `ñ`; revertir.
  - Hecho cuando: `T-I --filter "FullyQualifiedName~JwtStartupTests"` pasa y `dotnet build API -warnaserror` rc 0.
  - Evidencia: RED (4 de 6): `Startup_WithoutSigningKey_FailsNamingKey`, `Startup_WithBlankSigningKey_Fails`, `Startup_With31Bytes_FailsNamingKey` y `Startup_DoesNotPrintTheSigningKey` → `Assert.Contains() Failure: Filter not matched in collection`. GREEN: `JwtOptions` (`SigningKey` como texto), `JwtOptionsValidator` («Jwt:SigningKey: debe definirse.» / «debe tener al menos 32 bytes.», con `Encoding.UTF8.GetByteCount`), `AddJwtAuthentication` (opciones + `ValidateOnStart`) en `Program.cs` y `Jwt:SigningKey` ficticia en `DocumentGeneration` en la misma tarea; foco 6/6; `BUILD` (incluye la exportación del contrato) rc 0 con 0 avisos. `Startup_With32AsciiOr16EnyeChars_StartsAndHealthIsOk` (2 casos): verde a la primera; mutación `Length` en lugar de bytes → ROJO el caso de 16 `ñ`; revertido, 6/6. `JwtDefaults` no se crea aquí (nada lo usa aún): llega en 9.2.
- [x] 8.4 **`appsettings*.json` y secretos versionados.** Archivos: `AR/RepositoryConventionsTests.cs`. Cubre: SC-6, SC-18.
  - Renombrar `AppSettings_ContainNoConnectionStringsIdentityOrDatabaseKeys` a `AppSettings_ContainNoSensitiveKeys` (prohíbe `ConnectionStrings`, `Database`, `Identity`, `Jwt`, `Seed` y `Cors:AllowedOrigins`); comprobar que existe una prueba de que `.env` figura en `.gitignore` y `.dockerignore` (si no, añadirla, sin leer `.env`). Verdes a la primera; mutación: `"Jwt": {}` temporal en `appsettings.json` → falla; revertir.
  - Hecho cuando: `T-AR --filter "FullyQualifiedName~RepositoryConventionsTests"` en verde y la mutación anotada.
  - Evidencia: Renombrada a `AppSettings_ContainNoSensitiveKeys` (prohíbe `ConnectionStrings`, `Database`, `Identity`, `Jwt`, `Seed` y `Cors:AllowedOrigins`) y añadida `EnvFile_IsIgnored` (Theory `.gitignore`/`.dockerignore` contienen la línea `.env`; sin leer `.env`). No existía prueba previa de `.env` en las exclusiones. Verdes a la primera (30/30 en `RepositoryConventionsTests`); mutación `"Jwt": {}` en `appsettings.json` → ROJO `appsettings versiona claves prohibidas: appsettings.json: Jwt`; revertido (contenido original comprobado).

## Fase 9: Adaptadores de la API

Pruebas en `IT/Authentication` (no necesitan base de datos). Foco: `T-I --filter "FullyQualifiedName~Authentication"`.

- [x] 9.1 **`AspNetPasswordHasher` (V3, V20).** Archivos: `IT/Authentication/AspNetPasswordHasherTests.cs`, `API/Authentication/AspNetPasswordHasher.cs`. Cubre: AU-1, AU-2, AU-3, AU-14 (adaptador); DD-6; V3.
  - RED (compilación). Pruebas: `Hash_DoesNotContainThePasswordAndVerifies` (`clave-de-prueba-123`), `Verify_WithWrongPassword_ReturnsFalseWithoutThrowing` (`otra-clave-456`), `Hash_TwiceSamePassword_DiffersAndBothVerify`, `Verify_WithNullHash_ReturnsFalse`, `Verify_WithUnreadableHash_ReturnsFalseWithoutThrowing` (Theory: `no-es-un-hash`, `AAAA`, `""`, `"   "`), `Hash_UsesFormatV3` (primer byte `0x01`; PRF e iteraciones leídos del hash: la aserción es «≥ 100.000 iteraciones» y el valor real se anota para el ADR 0011).
  - GREEN: singleton que envuelve `PasswordHasher<object>`; el constructor calcula una vez el hash ficticio de un GUID; `Verify(null, p)` verifica contra él y devuelve `false`; `Success` y `SuccessRehashNeeded` → `true`; `FormatException` u otro fallo de lectura del hash → `false` (si el RED no la exige, no se añade el `try/catch`). Nunca registra contraseñas ni hashes.
  - Verdes a la primera (probable): la sal aleatoria (mutación: hash cacheado → falla) y el hash ilegible (mutación: `Convert.FromBase64String(hash)` previo a verificar → lanza y falla).
  - Hecho cuando: el foco pasa y las mutaciones quedan anotadas.
  - Evidencia: RED (compilación) `CS0246: AspNetPasswordHasher no se encontró`. GREEN mínimo (singleton sobre `PasswordHasher<object>`, hash ficticio de un GUID calculado una vez, `Verify(null, p)` contra él y siempre `false`, `Success`/`SuccessRehashNeeded` → `true`) sin `try/catch`: 8 de 9 verdes y el caso `no-es-un-hash` ROJO con `System.FormatException: The input is not a valid Base-64 string…` (el RED exigía el captura; `AAAA`, `""` y `"   "` ya devolvían falso). Se añadió solo `catch (FormatException)` → 9/9. **V3 resuelta:** formato V3 con PRF = 2 (HMAC-SHA512) y **100.000 iteraciones** (valor para el ADR 0011); la prueba exige ≥ 100.000. Verdes a la primera tras el GREEN: sal aleatoria (mutación: `Hash` devuelve el hash ficticio cacheado → ROJO `Hash_TwiceSamePassword_DiffersAndBothVerify` y `Hash_DoesNotContain…`, 2 fallos) y el hash ilegible, cuya mutación fue la propia ausencia del captura (ROJO real). Revertido el cambio y comparado.
- [x] 9.2 **`JwtAccessTokenIssuer` (V15, V20).** Archivos: `IT/Authentication/JwtAccessTokenIssuerTests.cs`, `API/Authentication/{JwtAccessTokenIssuer,JwtDefaults}.cs`. Cubre: AU-15, AU-16, AU-17; V15.
  - RED (compilación). Pruebas que decodifican el token **a mano** (base64url + `System.Text.Json`): `Issue_ContainsAllClaimsAndHs256Header` (`sub`, `name`, `email` en minúsculas, `role` = `Manager`, `jti`, `iat`, `nbf`, `exp`, `iss` = `timetracker-api`, `aud` = `timetracker-web`, `alg` = `HS256`), `Issue_ExpiresThirtyMinutesAfterNow` (`FakeTimeProvider` en `2026-10-12T08:00:00Z` → `exp` = `…08:30:00Z`; `AccessToken.ExpiresAt` igual), `Issue_DoesNotContainHashOrPassword`, `Issue_TwoTokensHaveDifferentJti`.
  - GREEN: `JsonWebTokenHandler` con `SetDefaultTimesOnTokenCreation = false` y `IssuedAt`/`NotBefore`/`Expires` de `TimeProvider` (DD-8); constantes de `JwtDefaults`. Si la versión de IdentityModel no respeta los tiempos del descriptor, claims `iat/nbf/exp` explícitos (respaldo de V15).
  - `Issue_TwoTokensHaveDifferentJti` verde a la primera; mutación: `jti` constante → falla; revertir.
  - Hecho cuando: el foco pasa; V15 anotada.
  - Evidencia: RED (compilación) `CS0246: JwtAccessTokenIssuer no se encontró`. GREEN: `JwtDefaults` (emisor, audiencia, 30 min, `ClockSkew` 30 s) y `JwtAccessTokenIssuer` con `JsonWebTokenHandler` (`SetDefaultTimesOnTokenCreation = false`, `IssuedAt/NotBefore/Expires` del `TimeProvider`, claims `sub/name/email/role/jti` por diccionario); las 4 pruebas (decodifican a mano) en verde a la primera tras el GREEN. **V15 resuelta:** no hizo falta el respaldo de claims explícitos; `iat`, `nbf` y `exp` del descriptor se respetan (exp = 08:30:00Z). `Issue_TwoTokensHaveDifferentJti` verde a la primera; mutación `jti` constante → ROJO (2 fallos: esa y `Issue_ContainsAllClaims…`); revertido. Añadida `Issue_WithFractionalSeconds_ExpiresAtMatchesTheExpClaim`: RED `Expected: 0 / Actual: 700` (`ExpiresAt` conservaba milisegundos aunque `exp` va en segundos) → GREEN truncando el instante a segundos enteros; foco 5/5 (14/14 con el hasher).
- [x] 9.3 **`HttpContextCurrentUser`.** Archivos: `IT/Authentication/HttpContextCurrentUserTests.cs`, `API/Authentication/HttpContextCurrentUser.cs`. Cubre: TK-5, TK-6; DD-14.
  - RED (compilación). `EmployeeId_ReturnsTheSubClaim` (`DefaultHttpContext` con identidad autenticada), `EmployeeId_WithoutAuthenticatedUser_Throws` (identidad anónima y `HttpContext` nulo) y `EmployeeId_WithNonGuidSub_Throws` (defensa en profundidad). GREEN: `IHttpContextAccessor`, `Guid.TryParse` del claim `sub`, `InvalidOperationException` en otro caso.
  - Mutación (verde a la primera de las dos últimas tras el GREEN): devolver `Guid.Empty` sin usuario → falla; revertir.
  - Hecho cuando: el foco pasa.
  - Evidencia: RED (compilación) `CS0246: HttpContextCurrentUser no se encontró`. GREEN: `IHttpContextAccessor`, claim `sub` con `Guid.TryParse`, distinto de `Guid.Empty`, `InvalidOperationException` en otro caso; pruebas: sub válido, identidad anónima, sin `HttpContext`, `sub` no GUID y `Guid.Empty` (Theory), sin claim `sub`; 6/6 (las de excepción, verdes a la primera tras el GREEN). Mutaciones: devolver `Guid.Empty` sin usuario → ROJO 5 de 6; quitar la comprobación de `Guid.Empty` → ROJO 1 (el caso de GUID vacío); ambas revertidas y comparadas.

## Fase 10: Login y consulta de sesión

- [x] 10.1 **RED del contrato de autenticación.** Archivos: `AR/OpenApiContractTests.cs`. Cubre: AU-55 (parte), AU-57, TK-14 (parte), TK-7.
  - `ExportedDocument_ContainsAuthEndpoints` (login `post` con `200`, `400`, `401`; me `get` con `200`, `401`; los tres endpoints de fichaje con `401`) y `ExportedDocument_DoesNotExposePasswordHash` (verde a la primera; mutación: añadir `PasswordHash` a `UserResponse` → falla). El `429` y el esquema Bearer se añaden en 19.1. RED: «Falta la ruta /api/auth/login en el contrato».
  - Hecho cuando: `T-AR --filter "FullyQualifiedName~OpenApiContractTests"` falla por la primera y se anota el motivo.
  - Evidencia: Añadidas `ExportedDocument_ContainsAuthEndpoints` (login `post` 200/400/401; me `get` 200/401; los tres de fichaje con 401) y `ExportedDocument_DoesNotExposePasswordHash`. RED: `Falta la ruta /api/auth/login en el contrato.` (1 fallo, 4 verdes). Tras 10.2 el fallo pasó a `Falta la ruta /api/auth/me`; tras 10.5 queda solo el `401` de los tres endpoints de fichaje, que llega en 12.2 (`ProducesProblem(401)`). `ExportedDocument_DoesNotExposePasswordHash` verde a la primera; mutación (añadir `PasswordHash` a `UserResponse`, ya existente el DTO) → ROJO; revertida y contrato regenerado en verde. (Cierre en 12.2) Con `.ProducesProblem(401)` en los tres endpoints de fichaje, `T-AR --filter OpenApiContractTests` pasa 5/5 y `docs/api/openapi.json` queda sincronizado.
- [x] 10.2 **Login correcto.** Archivos: `IT/Authentication/LoginEndpointTests.cs`, `API/Authentication/{AuthEndpoints,LoginRequest,LoginResponse,UserResponse}.cs`, `AuthenticationServiceCollectionExtensions.cs`, `Program.cs`. Cubre: AU-4, AU-5, AU-6, AU-16 (API).
  - Con `PostgresApiFactory` y `UserSeed`. RED: `Login_WithValidCredentials_Returns200WithTokenAndUser` (cliente anónimo; `application/json`, `accessToken` no vacío, `expiresAt` = `Clock` + 30 min terminado en `Z`, `user.role` = `Employee`), `Login_NormalizesEmail` (`  Luis.Empleado@Example.TEST  `) y `Login_Response_HasNoHashOrPassword` (ni `passwordHash`, ni el hash almacenado, ni la contraseña enviada) → `404`.
  - GREEN: `POST /api/auth/login` (`AllowAnonymous`), DTO con `DateTime` UTC (`Z`) y `Role` en texto, registro de `LoginUseCase`, `AspNetPasswordHasher` (singleton), `JwtAccessTokenIssuer` y `JwtOptions`; `.ProducesProblem(400)` y `.ProducesProblem(401)`.
  - Hecho cuando: el foco pasa; 10.1 pasa a verde en lo declarado.
  - Evidencia: Con `PostgresApiFactory` y `UserSeed`. RED: las 3 pruebas (`Login_WithValidCredentials_Returns200WithTokenAndUser`, `Login_NormalizesEmail`, `Login_Response_HasNoHashOrPassword`) → `Expected: OK / Actual: NotFound`. GREEN: `LoginRequest`/`LoginResponse`/`UserResponse` (DateTime UTC con `Z`, `Role` en texto), `AuthEndpoints` (`POST /api/auth/login`, `AllowAnonymous`, `IResult` con `Produces<LoginResponse>`, `ProducesProblem(400/401)`), registro de `LoginUseCase`, `AspNetPasswordHasher` y `JwtAccessTokenIssuer` en `AddJwtAuthentication` y `MapAuthEndpoints` en `Program.cs`; foco 3/3, `expiresAt` = `2026-10-12T08:30:00Z`. Contrato: 10.1 sigue rojo solo por `/me`.
- [x] 10.3 **Credenciales no válidas indistinguibles.** Archivos: `IT/Authentication/LoginEndpointTests.cs`, `AuthEndpoints.cs`. Cubre: AU-7, AU-10, AU-11, AU-12, AU-14 (API); amenaza A07/A09.
  - RED: `Login_UnknownEmail_Returns401ProblemJson`, `Login_WrongPassword_Returns401ProblemJson`, `Login_UnknownEmailAndWrongPassword_ProduceIdenticalResponses` (mismo estado, tipo de medio, `type`, `title` «Credenciales no válidas» y `detail` «El correo electrónico o la contraseña no son correctos.», salvo `traceId`), `Login_WithInvalidShape_Returns401LikeWrongCredentials` (Theory: correo en blanco, contraseña ausente, correo de 255, contraseña de 129; sin token) y `Login_WithUnreadableStoredHash_Returns401NotServerError` (`UserSeed.InsertRawAsync` con `password_hash = 'no-es-un-hash'`) → `Expected: Unauthorized / Actual: …`.
  - GREEN: `TypedResults.Problem` 401 con ese título y detalle; `LoggerMessage` de nivel `Information` para el intento fallido **sin** correo, contraseña ni IP (A09).
  - Verde a la primera probable: la prueba del hash ilegible (la cubre el adaptador de 9.1); mutación: llamar a `Convert.FromBase64String` sobre el hash en el adaptador → `500` y falla; revertir.
  - Hecho cuando: el foco pasa y la mutación queda anotada.
  - Evidencia: Pruebas (11 en `LoginEndpointTests`): correo inexistente, contraseña errónea, respuestas idénticas (estado, tipo de medio, `status`, `title`, `detail`, `type`, salvo `traceId`), campos en blanco/ausentes (Theory de 3), correo de 255 y contraseña de 129 en una prueba, y hash ilegible por `UserSeed.InsertRawAsync`. **Verdes a la primera**: no hubo RED porque el GREEN de 10.2 ya incluía la rama 401 (el caso de uso devuelve `null` y el endpoint debía responder algo); desviación de orden, no de resultado. Mutaciones: título «Credenciales incorrectas» → ROJO 7 de 11; quitar el `catch (FormatException)` del adaptador (hash `no-es-un-hash` → 500) → ROJO 1 (`Login_WithUnreadableStoredHash_Returns401NotServerError`); ambas revertidas y comparadas. `LoggerMessage` `Information` del intento fallido sin correo, contraseña ni IP.
- [x] 10.4 **Cuerpo ausente o ilegible (V18).** Archivos: `IT/Authentication/LoginEndpointTests.cs`, `AuthEndpoints.cs`, `Errors/ProblemDetailsServiceCollectionExtensions.cs` (solo si el RED lo exige). Cubre: AU-9; V18.
  - RED: `Login_WithoutBodyOrBrokenJson_Returns400ProblemJson` (Theory: sin cuerpo y sin `Content-Type`; `Content-Type: application/json` con cuerpo vacío; `{"email":`) en `Development` (entorno por defecto de la fábrica) y en `Production` (configuración completa) → se espera `500` o `415` en alguno. GREEN: parámetro `LoginRequest?` opcional con `400` explícito para el nulo y, para el JSON roto, `StatusCodeSelector` de `ExceptionHandlerOptions` que devuelve el estado de `BadHttpRequestException`.
  - Hecho cuando: el foco pasa en los dos entornos, `application/problem+json`, sin token emitido; V18 anotada con el estado real de cada caso.
  - Evidencia: RED: `Login_WithoutBodyOrBrokenJson_Returns400ProblemJson` (Theory entorno × caso: Development y Production; sin cuerpo / JSON vacío con `Content-Type` / `{"email":`). Primera ronda (solo Development): sin cuerpo y JSON vacío ya daban 400 (parámetro opcional con 400 explícito); JSON roto → `Expected: BadRequest / Actual: InternalServerError` (en Development `ThrowOnBadRequest` lanza `BadHttpRequestException`). GREEN 1: `ExceptionHandlerOptions.StatusCodeSelector` (400 para `BadHttpRequestException`) y título «Solicitud no válida» para 400 sin `detail`. Segunda ronda, tras retirar la identidad provisional (12.2) y poder arrancar `Production`: JSON roto → `Expected: "application/problem+json" / Actual: null` (en Production el enlace responde 400 sin cuerpo y no lanza). GREEN 2 (respaldo, desviación D-3a-2): `RouteHandlerOptions.ThrowOnBadRequest = true` en todos los entornos para que el manejador de excepciones dé el mismo 400 problem+json; 6/6. **V18 resuelta:** Development: sin cuerpo 400 (código propio), JSON vacío 400, JSON roto 400 (con selector); Production: sin cuerpo 400, JSON vacío 400, JSON roto 400 solo con `ThrowOnBadRequest = true` (sin él, 400 vacío); todos `application/problem+json`, sin token emitido.
- [x] 10.5 **JwtBearer base y `GET /api/auth/me` (V4).** Archivos: `IT/Authentication/MeEndpointTests.cs`, `API/Authentication/{AuthenticationServiceCollectionExtensions,AuthEndpoints,UserResponse}.cs`, `Program.cs` (`UseAuthentication`, `UseAuthorization` tras `UseCors`). Cubre: AU-18, AU-19, AU-32, AU-33, AU-34; V4.
  - RED: `Me_WithValidToken_ReturnsClaimsData` (AU-18 y AU-32; es el control positivo de `TestTokens`), `Me_AsManager_ReturnsManagerRole` (AU-34), `Me_WithoutToken_Returns401` (AU-19) y `Me_DoesNotQueryTheDatabase` (AU-33, `ApiFactory` con `db.invalid`) → `404`.
  - GREEN: `AddJwtBearer` mínimo (firma con `Jwt:SigningKey`, emisor y audiencia de `JwtDefaults`, `ValidateLifetime`, `MapInboundClaims = false`, `NameClaimType = "name"`, `RoleClaimType = "role"`) y `GET /api/auth/me` (`RequireAuthorization()` explícito por ahora; se retira en 11.6) que lee `sub`, `email`, `name` y `role` de los claims. V4: `sub` y `role` llegan sin renombrar.
  - `Me_DoesNotQueryTheDatabase` verde a la primera; mutación: inyectar `IUserRepository` y consultarlo en `/me` → falla (conexión a `db.invalid`); revertir.
  - Hecho cuando: el foco pasa; V4 anotada (la comprobación con `[Authorize(Roles)]` queda en 15.2).
  - Evidencia: RED: 4 pruebas (`Me_WithValidToken_ReturnsClaimsData`, `Me_AsManager_ReturnsManagerRole`, `Me_WithoutToken_Returns401`, `Me_DoesNotQueryTheDatabase`) → 404. GREEN: `AddAuthentication().AddJwtBearer()` con `JwtBearerOptions` configurados desde `JwtOptions` (clave HS256 UTF-8, emisor/audiencia de `JwtDefaults`, `ValidateLifetime`, `MapInboundClaims = false`, `NameClaimType = "name"`, `RoleClaimType = "role"`), `AddAuthorization`, `UseAuthentication/UseAuthorization` tras `UseCors` y `GET /api/auth/me` con `RequireAuthorization()` explícito (se retira en 11.6). Segundo RED inesperado: `Me_WithoutToken_Returns401` dio `OK` porque `CreateDefaultClient()` también invoca `ConfigureClient`; `CreateAnonymousClient()` ahora quita la cabecera (corrige 8.2). Foco 4/4. `Me_DoesNotQueryTheDatabase` verde a la primera; mutación (inyectar `IUserRepository` y consultarlo en `/me`) → ROJO 3 de 4 (conexión a `db.invalid`); revertida. **V4 resuelta (parte):** `sub` y `role` llegan sin renombrar (`/me` los lee con esos nombres); la comprobación con `[Authorize(Roles)]` queda en 15.2.
- [x] 10.6 **La clave no se filtra.** Archivos: `IT/Security/SigningKeyLeakTests.cs`. Cubre: AU-31; amenaza A02/A09.
  - `SigningKey_DoesNotAppearInResponsesOrLogs`: `Jwt:SigningKey` reconocible, `ILoggerProvider` que captura todos los mensajes a nivel `Trace`, arranque, un login, una petición con `401` y `GET /openapi/v1.json`; ningún cuerpo, cabecera ni mensaje contiene el valor. Verde a la primera; mutación: registrar `JwtOptions` completo en el emisor → falla; revertir.
  - Hecho cuando: el foco pasa y la mutación queda anotada.
  - Evidencia: `SigningKey_DoesNotAppearInResponsesOrLogs`: clave reconocible, `ILoggerProvider` a nivel `Trace` con `ClearProviders`, login real (PostgreSQL), `/me` sin token (401) y `/openapi/v1.json`; ni cuerpos, ni cabeceras, ni mensajes (con excepciones) contienen el valor, y el registro capturó mensajes (no vacío). Verde a la primera; mutación (registrar `JwtOptions.SigningKey` en el endpoint de login) → ROJO; revertida y compilada de nuevo.

## Fase 11: Validación estricta del token y política de respaldo

Pruebas en `IT/Authentication/TokenValidationTests.cs` y `IT/Security/*`; cada token se compara con un control válido que difiere en una sola cosa. Foco: `T-I --filter "FullyQualifiedName~TokenValidation"`.

- [x] 11.1 **Reloj de la aplicación (V6, DD-11).** Cubre: AU-18, AU-20, AU-21, AU-27, AU-28 (parte); V6.
  - RED: `Token_FromLogin_IsAcceptedByMe` (login con `Clock` en 2026-10-12 y `GET /me` → `200`; si la validación usa el reloj del sistema, el token ya habrá caducado), `Token_ExpiredTwoMinutesAgo_Returns401`, `Token_NotBeforeFiveMinutesAhead_Returns401`.
  - GREEN: `JwtBearerOptions.TimeProvider` o, si IdentityModel no lo usa para la vida del token, `LifetimeValidator` propio con el `TimeProvider` de DI (~10 líneas); `ClockSkew = 30 s`. Verde a la primera: `Token_ExpiredTenSecondsAgo_Returns200` (AU-21); mutaciones: `ClockSkew = Zero` → falla; el `ClockSkew` por defecto (5 min) → falla AU-20; revertir.
  - Hecho cuando: el foco pasa; V6 anotada (qué opción hizo falta).
  - Evidencia: RED (`TokenValidationTests`, 5 pruebas): `Token_FromLogin_IsAcceptedByMe` → `Expected: OK / Actual: Unauthorized` (el token del reloj ficticio de 2026-10-12 ya había caducado frente al reloj del sistema); `Token_NotBeforeFiveMinutesAhead_Returns401` → `Actual: OK`; `Token_ExpiredTenSecondsAgo_Returns200` → `Actual: Unauthorized` (también por el reloj del sistema). `Token_ExpiredTwoMinutesAgo_Returns401` pasaba solo por caducidad real (no demuestra nada hasta tener el reloj de la aplicación; la mutación posterior lo ejerce). **V6 resuelta (hizo falta el respaldo):** `TokenValidationParameters` no tiene `TimeProvider` en IdentityModel 8.19.2 y `JwtBearerOptions.TimeProvider` compila pero no gobierna la vigencia (mismos 3 fallos) → `LifetimeValidator` propio con el `TimeProvider` de DI (≈ 6 líneas) y `ClockSkew = JwtDefaults.ClockSkew` (30 s). Foco 5/5. Mutaciones: `ClockSkew = Zero` → ROJO `Token_ExpiredTenSecondsAgo_Returns200`; `ClockSkew` por defecto (5 min) → ROJO 3 (`ExpiredTwoMinutesAgo`, `NotBeforeFiveMinutesAhead`, `AfterClockAdvance31Minutes`); reloj del sistema (`DateTime.UtcNow`) → ROJO 4; todas revertidas y comparadas.
- [x] 11.2 **Caducidad según el reloj de la aplicación.** Cubre: AU-28.
  - `Token_FromLogin_AfterClockAdvance31Minutes_Returns401`: login, `Clock.Advance(TimeSpan.FromMinutes(31))`, `GET /me` → `401`. Verde a la primera tras 11.1; mutación: validar con el reloj del sistema → la primera mitad de 11.1 falla; revertir.
  - Hecho cuando: el foco pasa.
  - Evidencia: `Token_FromLogin_AfterClockAdvance31Minutes_Returns401`: login, comprobación `200`, `Clock.Advance(31 min)` y `401`. Verde a la primera tras 11.1. Mutaciones (ver 11.1): reloj del sistema → ROJO (esa y otras 3); `ClockSkew` de 5 min → ROJO (exp + 1 min < 5 min de tolerancia). Foco `TokenValidation` 6/6 en ese momento.
- [x] 11.3 **Firma, algoritmo, emisor y audiencia.** Cubre: AU-22 (sobre `/api/auth/me`), AU-23, AU-26, AU-30; amenaza API2/A02.
  - RED: `Token_WithAlteredPayloadKeepingSignature_Returns401` (`ReplacePayload`), `Token_SignedWithHs512_Returns401`, `Token_WithAlgNone_Returns401`, `Token_WithOtherIssuer_Returns401`, `Token_WithOtherAudience_Returns401`, `Token_SignedWithOtherKey_Returns401` (otra clave de 32 bytes). GREEN: `ValidAlgorithms = [HS256]`, `RequireSignedTokens`, `RequireExpirationTime`, emisor y audiencia validados.
  - Pueden pasar a la primera (la validación por defecto ya cubre firma, emisor y audiencia); en ese caso mutaciones: quitar `ValidAlgorithms` → falla HS512; `ValidateIssuer = false` → falla el emisor; `ValidateAudience = false` → falla la audiencia; revertir. El literal de AU-22 (`/api/team/members`) se repite en 15.9.
  - Hecho cuando: el foco pasa y las mutaciones quedan anotadas.
  - Evidencia: Seis pruebas sobre `/api/auth/me` (`Token_Valid_IsAccepted` como control): payload alterado conservando la firma, HS512, `alg: none`, emisor ajeno, audiencia ajena y otra clave. Primera ejecución: todas verdes, pero HS512 era un falso verde (IdentityModel exige ≥ 512 bits de clave para HS512 y la clave de pruebas medía 40 bytes). Se subió `ApiFactory.ValidSigningKey` a 65 bytes (suficiente para HS512) y entonces `Token_SignedWithHs512_Returns401` → RED `Expected: Unauthorized / Actual: OK`. GREEN: `ValidAlgorithms = [HS256]`, `RequireSignedTokens`, `RequireExpirationTime`, emisor y audiencia explícitos. Mutaciones: quitar `ValidAlgorithms` → ROJO HS512; `ValidateIssuer = false` → ROJO emisor; `ValidateAudience = false` → ROJO audiencia; revertidas y comparadas; 12/12.
- [x] 11.4 **`sub` y `role` válidos (DD-10).** Cubre: AU-25, AU-24 (sobre `/me`); amenaza API1/API2.
  - RED: `Token_WithRoleAdmin_Returns401`, `Token_WithNonGuidSub_Returns401`, `Token_WithEmptyGuidSub_Returns401` (sobre `/api/auth/me`; el literal de AU-24 sobre `GET /api/time-entries` está en 12.1). GREEN: `OnTokenValidated` que hace `context.Fail` si `sub` no es un GUID distinto de vacío o `role` no es `Employee`/`Manager`.
  - Hecho cuando: el foco pasa.
  - Evidencia: RED (3 casos): `Token_WithRoleAdmin_Returns401` → `Actual: OK`; `Token_WithInvalidSub_Returns401` con `no-es-un-guid` → `Actual: InternalServerError` (el `Guid.Parse` de `/me`) y con el GUID vacío → `Actual: OK`. GREEN: `OnTokenValidated` (`RejectInvalidClaims`) con `context.Fail` si `sub` no es GUID distinto de vacío o `role` no es `Employee`/`Manager` (sensible a mayúsculas); 15/15. El literal de AU-24 sobre `GET /api/time-entries` queda para 12.1.
- [x] 11.5 **`WWW-Authenticate` sin detalle (V23).** Cubre: AU-29; V23, V5 (parte).
  - RED: `Token_Expired_WwwAuthenticateHasBearerWithoutErrorDescription` → la cabecera trae `error_description`. GREEN: `IncludeErrorDetails = false`. La respuesta `401` sigue sin cuerpo hasta 14.1.
  - Hecho cuando: el foco pasa.
  - Evidencia: RED: `Token_Expired_WwwAuthenticateHasBearerWithoutErrorDescription` → `Assert.DoesNotContain() Failure: Sub-string found` (el 401 traía `error="invalid_token", error_description=…`). GREEN: `IncludeErrorDetails = false`; 16/16. **V23 resuelta:** el 401 de JwtBearer ya no incluye `error_description`. V5 (parte): la cabecera `WWW-Authenticate: Bearer` está; el cuerpo ProblemDetails en español llega en 14.1.
- [x] 11.6 **Política de respaldo autenticada (V13, V14).** Archivos: `IT/Security/FallbackPolicyTests.cs`, `IT/HealthEndpointTests.cs`, `API/Authentication/AuthenticationServiceCollectionExtensions.cs`, `API/Health/HealthEndpoints.cs`, `API/Authentication/AuthEndpoints.cs`, `Program.cs`. Cubre: AU-45 (parte), AU-46, AU-47, AU-48, AH-1, AH-2, AH-3; V13, V14.
  - RED: `Health_WithoutToken_Returns200` (cliente anónimo), `ProtectedEndpoints_WithoutToken_Return401` (`GET /api/auth/me`, los tres de fichaje; los de equipo se añaden en 15.9), `UnmappedEndpoint_WithoutToken_Returns401` (endpoint de prueba mapeado con un `IStartupFilter` en `ConfigureTestServices`, sin atributos), `NonexistentRouteAndPostHealth_WithoutToken_Return401` y `NonexistentRouteAndPostHealth_WithToken_Return404And405` (el cuerpo `problem+json` se comprueba en 14.1). Adaptar `HealthEndpointTests`: `GET` con `CreateAnonymousClient()` (AH-1); `Post_ReturnsMethodNotAllowed` pasa a `Post_WithToken_ReturnsMethodNotAllowed` (AH-2); nueva `Post_WithoutToken_ReturnsUnauthorized` (AH-3).
  - GREEN: `FallbackPolicy = RequireAuthenticatedUser`, `.AllowAnonymous()` en `/health` y en el login (el `MapOpenApi` se trata en 11.7); retirar el `RequireAuthorization()` explícito de `/me` (la prueba sigue en verde por el respaldo). V13: anotar que `POST /health` y las rutas inexistentes responden `401` sin token (aceptado; reflejado en `api-health`). V14: `ConfigureClient` se conserva tras `WithWebHostBuilder`; si no, ayudante `CreateAuthenticatedClient()`.
  - Hecho cuando: el foco pasa; V13 y V14 anotadas.
  - Evidencia: RED (`FallbackPolicyTests`): `ProtectedEndpoints_WithoutToken_Return401` (Theory de 4: `/me` y los tres de fichaje) → los tres de fichaje `Expected: Unauthorized / Actual: InternalServerError`; `NonexistentRouteAndPostHealth_WithoutToken_Return401` → `Actual: NotFound`; `UnmappedEndpoint_WithoutToken_Returns401_AndWithTokenReachesIt` → `Expected: Unauthorized / Actual: OK` (la sonda existe: el control autenticado da 200). La sonda se mapea con un `IStartupFilter` que hace `UseRouting()` antes de `next(app)` y `UseEndpoints` después: `WebApplication` respeta ese enrutado externo y la sonda atraviesa la canalización real. `Health_WithoutToken_Returns200` y `…WithToken_Return404And405` verdes a la primera. GREEN: `AddAuthorizationBuilder().SetFallbackPolicy(RequireAuthenticatedUser)`, `.AllowAnonymous()` en `/health`, retirado el `RequireAuthorization()` de `/me`; `HealthEndpointTests` adaptado (`GET` anónimo, `Post_WithToken_ReturnsMethodNotAllowed`, `Post_WithoutToken_ReturnsUnauthorized`); `CreateAnonymousClient` pasó a extensión de `WebApplicationFactory<Program>` (`WithWebHostBuilder` devuelve la base). Foco 8/8 y `T-I` 153/153. Mutación: quitar `.AllowAnonymous()` de `/health` → ROJO 6 (salud anónima y arranque con clave límite). **V13 resuelta:** sin token, `POST /health` y una ruta inexistente responden 401; con token, 405 y 404 (aceptado y ya reflejado en `api-health`). **V14 resuelta:** `ConfigureClient` se conserva tras `WithWebHostBuilder` (el control autenticado de la sonda dio 200); no hace falta `CreateAuthenticatedClient()`.
- [x] 11.7 **Documento OpenAPI solo en Development y cobertura de autorización (V22).** Archivos: `IT/Security/{OpenApiExposureTests,AuthorizationCoverageTests}.cs`, `Program.cs`. Cubre: AU-44, AU-49, AU-50; V22.
  - RED: `OpenApi_InDevelopment_IsServedWithoutToken` (`200`; con el respaldo activo da `401`), `OpenApi_InProduction_WithToken_Returns404_AndWithoutToken_Returns401` (entorno `Production` con configuración completa; nunca se sirve el documento). GREEN: `app.MapOpenApi().AllowAnonymous()` solo en `Development`.
  - `AuthorizationCoverageTests.AnonymousEndpoints_AreExactlyHealthLoginAndOpenApi` recorre `EndpointDataSource` en `Development`. Verde a la primera; mutación: `.AllowAnonymous()` en `/api/auth/me` → falla; revertir.
  - Hecho cuando: el foco pasa y la mutación queda anotada.
  - Evidencia: RED: `OpenApi_InDevelopment_IsServedWithoutToken` → `Expected: OK / Actual: Unauthorized`; `AuthorizationCoverageTests.AnonymousEndpoints_AreExactlyHealthLoginAndOpenApi` → `Actual: ["GET /health", "POST /api/auth/login"]` (faltaba OpenAPI). GREEN: `app.MapOpenApi().AllowAnonymous()` (solo en Development). La cobertura recorre el `EndpointDataSource` y compara con `["GET /health", "GET /openapi/{documentName}.json", "POST /api/auth/login"]`; no fue verde a la primera (tuvo RED); mutación `.AllowAnonymous()` en `/api/auth/me` → ROJO (`Actual` incluye `GET /api/auth/me`); revertida. `OpenApi_InProduction_WithToken_Returns404_AndWithoutToken_Returns401` se añadió tras 12.2 (antes `Production` no arrancaba; D-3a-1): verde a la primera; mutación (mapear OpenAPI en cualquier entorno) → ROJO `Actual: OK`; revertida. **V22 resuelta:** `MapOpenApi().AllowAnonymous()` sirve el documento en Development sin token y en Production no existe (404 con token, 401 sin él).

## Fase 12: Identidad desde el token y retirada de la provisional

- [x] 12.1 **RED de la identidad real y de los restos.** Archivos: `IT/TimeEntries/TimeEntryAuthenticationTests.cs`, `IT/Startup/{JwtStartupTests,LegacyIdentityKeyTests}.cs`, `AR/RepositoryConventionsTests.cs`. Cubre: TK-1, TK-2, TK-3, TK-4, TK-13 (estado), AU-24, SC-14, SC-15, SC-16, SA-5 (src y tests), SA-6.
  - RED: `Identity_ComesFromTheToken` (tokens de luis y ana: el listado de luis contiene su fichaje y el de ana no), `ClockInWithEmployeeIdQuery_IsIgnored` y `ClockInWithXEmployeeIdHeader_IsIgnored`, `TimeEntryEndpoints_WithoutToken_Return401AndCreateNothing` (los tres; filas contadas en la base), `GetTimeEntries_WithExpiredToken_Returns401`, `GetTimeEntries_WithInvalidSub_Returns401` (`no-es-un-guid` y `Guid.Empty`, AU-24 literal), `Startup_InProduction_WithValidConfiguration_StartsAndHealthIsOk` (SC-14), `Startup_InDevelopment_WithoutIdentityKey_Starts` (SC-15), y en `LegacyIdentityKeyTests` `Startup_WithLegacyIdentityKey_IgnoresItAndGetTimeEntriesWithoutTokenIs401` (SC-16; la clave heredada se construye en ese archivo, que la prueba de restos exime por nombre: D3).
  - `Code_HasNoProvisionalIdentityLeftovers` (`src/` y `tests/`, sin `bin/` ni `obj/`, sin `LegacyIdentityKeyTests.cs` ni la propia prueba: ni `DevelopmentCurrentUser`, ni `ProvisionalIdentity`, ni `Identity:DevelopmentEmployeeId`) y `Api_HasHttpContextCurrentUser_AndNoProvisionalIdentityTypes` (existe `HttpContextCurrentUser.cs`; no existe la carpeta `API/Identity`).
  - Hecho cuando: las pruebas fallan por los motivos anotados (el empleado sale de la configuración, `Production` no arranca, restos presentes).
  - Evidencia: RED (ejecución previa a 12.2): `Identity_ComesFromTheToken` → `Expected: 0 / Actual: 1` (la identidad salía de la configuración, no del token); `ClockInWithEmployeeIdQuery_IsIgnored` y `ClockInWithXEmployeeIdHeader_IsIgnored` → `Expected: 1 / Actual: 0` (el fichaje se creaba para el empleado de la configuración, no para el `sub`); `Startup_InProduction_WithValidConfiguration_StartsAndHealthIsOk` → `OptionsValidationException: Identity: la identidad provisional solo está permitida en el entorno Development…`; `Code_HasNoProvisionalIdentityLeftovers` → restos en `Program.cs`, `DocumentGeneration.cs`, `Api/Identity/*` (4), `ApiFactory.cs`, `IdentityStartupTests.cs`, `PostgresApiFactory.cs`; `Api_HasHttpContextCurrentUser_AndNoProvisionalIdentityTypes` → «La carpeta Api/Identity… debe desaparecer». Verdes a la primera (el 401 ya lo daba 11.6 y la validación del token 11.1–11.4): `TimeEntryEndpoints_WithoutToken_Return401AndCreateNothing` (filas contadas antes y después), `GetTimeEntries_WithExpiredToken_Returns401`, `GetTimeEntries_WithInvalidSub_Returns401` (2 casos, AU-24 literal) y `LegacyIdentityKeyTests` (SC-16); sus mutaciones están en 11.3, 11.4 y 11.6 (quitar la política de respaldo o el validador de claims). `Startup_InDevelopment_WithoutIdentityKey_Starts` (SC-15) solo se ejerce de verdad tras quitar la clave de las fábricas en 12.2 (antes las fábricas la aportaban); su rojo equivalente fue el de `Production`. La prueba de restos exime por nombre a `LegacyIdentityKeyTests.cs` y a `RepositoryConventionsTests.cs` (contiene los literales).
- [x] 12.2 **GREEN: identidad del token y retirada de la provisional.** Archivos: `API/Authentication/AuthenticationServiceCollectionExtensions.cs`, `API/TimeEntries/TimeEntryEndpoints.cs` (`.ProducesProblem(401)`), `Program.cs`, `API/Configuration/DocumentGeneration.cs`, `IT/{ApiFactory,EnvironmentIsolation}.cs`; **eliminar** `API/Identity/*` (4 archivos y la carpeta) e `IT/Startup/IdentityStartupTests.cs`. Cubre: TK-1–TK-4, SC-14–SC-16, SA-5 (parcial), SA-6, AU-59.
  - `ICurrentUser` → `HttpContextCurrentUser` (`Scoped`, `AddHttpContextAccessor`); quitar `AddProvisionalIdentity`; `DocumentGeneration` sin `Identity:DevelopmentEmployeeId`; `ApiFactory` sin la clave de identidad (`DefaultUserId` sustituye a `ValidEmployeeId`); `EnvironmentIsolation` borra `Cors__`, `ConnectionStrings__`, `Database__`, `Jwt__`, `Seed__` y `ASPNETCORE_ENVIRONMENT`. `JwtStartupTests` sustituye a `IdentityStartupTests` (AU-59).
  - Hecho cuando: las pruebas de 12.1 pasan; `Select-String 'DevelopmentCurrentUser|ProvisionalIdentity|Identity:DevelopmentEmployeeId' -Path API,IT,AR -Recurse` solo encuentra `LegacyIdentityKeyTests.cs` y la prueba de restos; `Test-Path API\Identity` es False.
  - Evidencia: GREEN: `ICurrentUser` → `HttpContextCurrentUser` (`Scoped`, `AddHttpContextAccessor`) en `AddJwtAuthentication`; retirados `AddProvisionalIdentity` y su `using`, la clave de `DocumentGeneration` y de las dos fábricas; `EnvironmentIsolation` borra `Cors__`, `ConnectionStrings__`, `Database__`, `Jwt__`, `Seed__` y `ASPNETCORE_ENVIRONMENT`; `.ProducesProblem(401)` en los tres endpoints de fichaje; eliminados `src/TimeTracker.Api/Identity/*` (4 archivos y la carpeta) e `IdentityStartupTests.cs` (sustituida por `JwtStartupTests`, AU-59). Las pruebas de 12.1 pasan; `T-I` 159/159 y `T-AR` 53/53 en ese punto; `Test-Path src\TimeTracker.Api\Identity` es False; los únicos restos de `DevelopmentCurrentUser|ProvisionalIdentity|Identity:DevelopmentEmployeeId` en `src` y `tests` están en `LegacyIdentityKeyTests.cs` y en la propia prueba de restos. La API ya arranca en `Production` con configuración completa, lo que permitió completar las variantes `Production` de 10.4 y 11.7 (D-3a-1).
- [x] 12.3 **Migrar las pruebas existentes a tokens (V14).** Archivos: `IT/ErrorHandlingTests.cs`, `IT/StartupConfigurationTests.cs`, `IT/Startup/{DatabaseStartupTests,DatabaseMigrationStartupTests}.cs`, `IT/TimeEntries/{TimeEntryEndpointsTests,TimeEntryIsolationTests,TimeEntryConcurrencyTests}.cs`. Cubre: TK-8, TK-9, TK-10, TK-11, TK-12, TK-2, TK-3, PE-7, PE-8, PE-9, PE-10, SC-17 (regresión).
  - Las pruebas parten del cliente autenticado por defecto (`Employee`); `TimeEntryIsolationTests` usa dos fábricas con identidades distintas (ahora por token); `DatabaseMigrationStartupTests` comprueba `clock-in` con token válido → `201` (PE-7); `Health_WithUnreachableDatabase_ReturnsHealthy` sigue anónimo (PE-10). Retirar todo uso de `Identity:` y de `ValidEmployeeId`.
  - Hecho cuando: `T-I` completo en verde; `Select-String 'Identity' -Path IT -Recurse` solo encuentra `LegacyIdentityKeyTests.cs`.
  - Evidencia: Las pruebas existentes (`ErrorHandlingTests`, `StartupConfigurationTests`, `DatabaseStartupTests`, `DatabaseMigrationStartupTests`, `TimeEntryEndpointsTests`, `TimeEntryIsolationTests`, `TimeEntryConcurrencyTests`) parten del cliente autenticado por defecto (`Employee`) y no necesitaron cambios de código: la fábrica emite el token (`PostgresApiFactory`: el de su `EmployeeId`) y `TimeEntryIsolationTests` usa dos fábricas con identidades distintas, ahora por token. `DatabaseMigrationStartupTests` comprueba `clock-in` con token válido → 201 (PE-7) con `ApiFactory` y `DefaultUserId`; `Health_WithUnreachableDatabase_ReturnsHealthy` sigue anónimo (PE-10, con `CreateAnonymousClient`). Retirado todo uso de `Identity:` y `ValidEmployeeId`. `T-I` completo en verde (PostgreSQL 18 real con Testcontainers); la búsqueda de `Identity` en `tests/TimeTracker.Api.IntegrationTests` solo encuentra `LegacyIdentityKeyTests.cs` y usos de `ClaimsIdentity` y `Microsoft.AspNetCore.Identity` (el hasher de `UserSeed`), sin relación con la identidad provisional.
- [x] 12.4 **Exportación del contrato sin identidad (V21).** Archivos: `API/Configuration/DocumentGeneration.cs`, `docs/api/openapi.json` (generado). Cubre: SA-26 (parte), SA-27; V21.
  - `Production_WithoutSigningKey_FailsNamingKey` (SA-27: el entorno `Production` sin `Jwt__SigningKey` falla por validación nombrando `Jwt:SigningKey`; los valores ficticios no se aplican). Manual (SA-26): con `ENV-CHECK` vacío, `dotnet build API --no-incremental -warnaserror` rc 0 y `docs\api\openapi.json` contiene login, me y los tres de fichaje (las rutas de equipo llegan en la Tanda 3b). V21: `GetDocument.Insider` genera con la clave ficticia sin ejecutar la siembra.
  - Hecho cuando: la prueba pasa y la salida del build queda anotada.
  - Evidencia: `Production_WithoutSigningKey_FailsNamingKey` (en `JwtStartupTests`): `Production` sin `Jwt:SigningKey` falla con validación que nombra `Jwt:SigningKey`; los valores ficticios solo se aplican bajo la herramienta de exportación (verde a la primera, ya cubierto por el GREEN de 8.3; sin mutación propia porque `IsRunningUnderTool` depende del ensamblado de entrada). Manual (SA-26): con `ENV-CHECK` vacío, `dotnet build src/TimeTracker.Api -c Release --no-incremental -warnaserror` rc 0 con 0 avisos; SHA-256 de `docs/api/openapi.json` idéntico antes y después (`beb232cf…`); contiene `POST /api/auth/login` (200/400/401), `GET /api/auth/me` (200/401) y los tres de fichaje con 401 (`clock-in` 201/401/409, `clock-out` 200/401/409, `GET` 200/401); sin `passwordHash` ni `employeeId`; aún sin esquema Bearer, sin 429 (19.1) y sin rutas de equipo (3b). **V21 resuelta:** `GetDocument.Insider` genera el contrato con la `Jwt:SigningKey` ficticia; la siembra (3b) no existe aún y se desactivará con `Seed:DemoUsers = false` en 16.1.

## Fase 13: Punto de control de la Tanda 3a

- [x] 13.1 **Puerta de calidad y arranque real.** Cubre: SA-16, SA-18, SA-4 (parcial); AU-60; V10, V20.
  - `RESTORE`, `FORMAT`, `BUILD`, `TEST`, `COV-D`, `COV-A`, `VULN`. Arranque manual: con `$env:ASPNETCORE_ENVIRONMENT='Production'`, `Cors__AllowedOrigins__0=http://localhost:5172`, `ConnectionStrings__Default='Host=db.invalid;Database=timetracker;Username=timetracker'` y `Jwt__SigningKey` generada con `[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))`, `dotnet run --no-launch-profile --project src/TimeTracker.Api`: `GET /health` 200 y `GET /api/time-entries` 401. Limpiar las variables.
  - Hecho cuando: todo en 0, `BUILD` con 0 avisos (V20: CA1848, CA1062, CA5404 cumplidas, sin desactivar nada) y la salida del arranque anotada.
  - Evidencia: `ENV-CHECK` vacío antes y después. `RESTORE` rc 0. `FORMAT` rc 0 (sin cambios pendientes). `BUILD` rc 0 con 0 avisos y 0 errores (V20: CA1848 cumplida con `LoggerMessage`, CA1062 y CA5404 sin avisos; nada desactivado; el build exporta el contrato con `GetDocument.Insider`). `TEST` sobre `TestResults` limpio rc 0: **309** pruebas = Domain 49, Application 43, Arquitectura 53 (+6), Integración 164 (+81; PostgreSQL 18 real con Testcontainers). `COV-D` rc 0 (líneas 100,0 %, ramas 100,0 %); `COV-A` rc 0 (líneas 100,0 %, ramas 100,0 %, mínimo 80 %). `VULN` rc 0 y sin «has the following vulnerable packages» (V10: sin vulnerabilidades, incluidas las transitivas `Microsoft.IdentityModel.*` 8.19.2). Sin contenedores de Testcontainers residuales (`docker ps -a` solo muestra contenedores ajenos al proyecto, que no se tocaron). Arranque real (`dotnet run --no-launch-profile -c Release`, variables solo en ese proceso: `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_URLS=http://127.0.0.1:5291` para no usar el puerto por defecto, `Cors__AllowedOrigins__0=http://localhost:5172`, `ConnectionStrings__Default='Host=db.invalid;Database=timetracker;Username=timetracker'`, `Jwt__SigningKey` de 64 caracteres generada con `pwsh`): `Hosting environment: Production`, `GET /health` → `200 {"status":"Healthy"}`; `GET /api/time-entries` → `401` con `WWW-Authenticate: Bearer` y cuerpo vacío (el ProblemDetails llega en 14.1); `GET /openapi/v1.json` → `401`; la clave no aparece en el registro de arranque. Proceso detenido y variables limpias. **Hallazgo para el README (Tanda 5):** el comando de generación de la clave `[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))` falla en Windows PowerShell 5.1 (`GetBytes` estático no existe) y solo funciona en PowerShell 7 (`pwsh`); documentarlo con `pwsh`.
- [x] 13.2 **Revisión, líneas y PARADA.**
  - Repasar la matriz de amenazas (secciones «BOLA», «Token forjado», «Enumeración», «Secretos» del mapa final); líneas reales frente a 550–700; anotar V3, V4, V5 (parte), V6, V13, V14, V15, V18, V21, V22, V23. **PARADA:** el usuario decide si pasa a la Tanda 3b.
  - Hecho cuando: checklist de la Tanda 3a completa.
  - Evidencia: Revisión de la Tanda 3a. **Matriz de amenazas:** BOLA/IDOR de fichajes: la identidad sale solo del `sub` (`Identity_ComesFromTheToken`, `?employeeId=` y `X-Employee-Id` ignorados, sin `employeeId` en el contrato); el control por objeto del equipo llega en 3b. Token forjado, `alg: none` y confusión de algoritmo: payload alterado, HS512, `none`, otra clave, emisor y audiencia ajenos, `sub`/`role` inválidos y caducidad → 401 (mutaciones en 11.1–11.4). Enumeración: 401 idéntico salvo `traceId`, `Verify` con hash nulo (unitaria) y hash ilegible → 401. Secretos: clave validada al arrancar por bytes sin imprimir el valor, fuera de `appsettings*.json` (`AppSettings_ContainNoSensitiveKeys`), sin fuga en respuestas ni registros (`SigningKeyLeakTests`). **Validaciones:** V3 (PRF HMAC-SHA512 e iteraciones 100.000, 9.1), V4 (parte: `sub`/`role` sin renombrar, 10.5; `[Authorize(Roles)]` en 15.2), V5 (parte: `WWW-Authenticate: Bearer` sin `error_description`; el cuerpo ProblemDetails en español llega en 14.1), V6 (hizo falta `LifetimeValidator` propio, 11.1), V13 (401 sin token en `POST /health` y rutas inexistentes, 11.6), V14 (`ConfigureClient` se conserva tras `WithWebHostBuilder`, 11.6), V15 (el descriptor respeta `iat/nbf/exp`, 9.2), V18 (Production exige `ThrowOnBadRequest = true`, 10.4), V21 (contrato con clave ficticia, 12.4), V22 (OpenAPI anónimo solo en Development, 11.7), V23 (sin `error_description`, 11.5), V20 y V10 (13.1). **Desviaciones de la tanda:** D-3a-1 las variantes `Production` de 10.4 y 11.7 se completaron tras 12.2 (hasta retirar `ProvisionalIdentityOptionsValidator` la API no arrancaba fuera de Development); D-3a-2 `RouteHandlerOptions.ThrowOnBadRequest = true` (respaldo de V18); D-3a-3 `LifetimeValidator` propio en lugar de `TimeProvider` en las opciones (respaldo de V6); D-3a-4 `ApiFactory.ValidSigningKey` de 65 bytes (con 40 el rechazo de HS512 era un falso verde por el tamaño de clave) y `CreateAnonymousClient` como extensión que quita la cabecera (`CreateDefaultClient` también invoca `ConfigureClient`); D-3a-5 `Errors/ProblemDetailsServiceCollectionExtensions.cs` (en la lista de 3b) se modificó en 3a (10.4, título 400 y selector) y su copia en `tanda-3a` se reconstruyó después con el contenido original leído antes de editar (el archivo no estaba en los 24); D-3a-6 las pruebas de 10.3 fueron verdes a la primera porque el GREEN de 10.2 ya incluía la rama 401 (mutaciones registradas); D-3a-7 `JwtDefaults` se creó en 9.2 y la clave de firma no se hizo virtual en la fábrica (basta `UseSetting(clave, null)`); **Incidencia de proceso:** durante 9.3 se lanzó por error `git --version` al final de un comando compuesto (con la salida descartada; sin efecto en el repositorio ni en archivos), contraviniendo la prohibición de Git; no se volvió a ejecutar ningún comando `git`. **Líneas a mano** (`wc -l` de archivos nuevos y `diff` de modificados con sus copias de `tanda-3a`; incluye blancos y documentación XML; las eliminaciones cuentan): `src` 434 (nuevos 331 en `Api/Authentication`, modificados 33 y 70 eliminadas de `Api/Identity`), pruebas 1.470 (nuevas 1.250, modificadas 170 y 50 eliminadas de `IdentityStartupTests`), props/`.csproj` 0; **total ≈ 1.904 frente a 550–700** (≈ 2,7–3,5× la previsión; el exceso viene de las pruebas, 1.470 frente a 330–420, por la matriz de seguridad de 260 escenarios). **Acumulado (Tandas 1, 2 y 3a): ≈ 919 + 491 + 1.904 = 3.314** frente a la referencia de 2.400–3.150 del cambio completo (sin contar las eliminaciones: 1.784 en la 3a y 3.194 acumulado): **la referencia total ya está superada antes de 3b, 4 y 5** (previsión restante 1.250–1.630). El disparador de 21.2 se dará con seguridad; se avisa ahora. **PARADA:** el usuario decide si pasa a la Tanda 3b.

---

# TANDA 3b: Api extra (errores, equipo, siembra, limitador, cabeceras, contrato, entrega)

## Fase 14: Errores de acceso como ProblemDetails y CORS

- [x] 14.1 **Copia previa y `UseStatusCodePages` (V5, V9).** Archivos: `..\TimeTracker.rollback\tanda-3b\` (14 existentes, ver plan); `IT/Security/AccessErrorsTests.cs`, `API/Errors/ProblemDetailsServiceCollectionExtensions.cs`, `Program.cs`. Cubre: AU-51, AU-47 (con token), AU-12 (regresión), TK-13; V5, V9.
  - `Save-Rollback 3b …`; `ENV-CHECK`; `BUILD` y `T-I` en verde.
  - RED: `Unauthorized_WithoutToken_IsProblemJsonInSpanishWithoutDetail` (`401`, `application/problem+json`, `status` 401, `title` «Se requiere autenticación», sin `detail`, `WWW-Authenticate` contiene `Bearer`), `Unauthorized_WithExpiredToken_OnTimeEntries_IsProblemJson` (TK-13), `NotFound_WithToken_IsProblemJson` («Recurso no encontrado») y `MethodNotAllowed_WithToken_IsProblemJson` (`POST /health`, «Método no permitido»), `AccessErrorBodies_HaveNoTraces`; y de regresión V9: `LoginUnauthorized_KeepsItsDetail`, `Conflict409_KeepsItsBody`, `UnhandledException_KeepsItsTitle`.
  - GREEN: `app.UseStatusCodePages()` tras `UseExceptionHandler()` (DD-21) y `CustomizeProblemDetails` que pone el título en español por estado (401, 403, 404, 405, 429) **solo si `Detail` es nulo**. Respaldo de V5: escribir el cuerpo en `JwtBearerEvents.OnChallenge`/`OnForbidden`.
  - Hecho cuando: el foco pasa y los cuerpos 401/403/404/405 son `problem+json` sin `detail`; el 403 y el 429 se prueban en 15.2 y 17.1.
  - Evidencia: `ENV-CHECK` vacío. Copia en `..\TimeTracker.rollback\tanda-3b` con los 14 archivos existentes (Count = 14, hecha con `cp -n` conservando rutas; incluye `.env.example`, copiado sin leer su contenido); después, antes de editarlos, se añadieron a la copia `IT/Security/{AuthorizationCoverageTests,FallbackPolicyTests}.cs`, `IT/Authentication/TokenValidationTests.cs` e `IT/TimeEntries/TimeEntryIsolationTests.cs` (ver D-3b-1). `BUILD` rc 0 con 0 avisos. RED (`AccessErrorsTests`, 8 pruebas): 4 fallos, todos `Expected: "application/problem+json" / Actual: null` (401 sin token, 401 con token caducado, 404 y 405 con token, sin cuerpo) y 4 verdes de regresión V9 (`LoginUnauthorized_KeepsItsDetail`, `Conflict409_KeepsItsBody`, `UnhandledException_KeepsItsTitle`, `AccessErrorBodies_HaveNoTraces`). GREEN: `app.UseStatusCodePages()` tras `UseExceptionHandler()` y `CustomizeProblemDetails` con un diccionario de títulos por estado (400, 401, 403, 404, 405, 429) aplicado solo si `Detail` es nulo. **V5 resuelta:** `UseStatusCodePages` usa el `IProblemDetailsService` ya registrado y escribe `application/problem+json`; no hizo falta el respaldo en `JwtBearerEvents`. **V9 resuelta:** el 401 de login (con `detail`), el 409 y el 500 conservan su cuerpo. `T-I` completo 172/172 (164 + 8).
- [x] 14.2 **CORS con `Authorization` y en errores (V8).** Archivos: `IT/CorsPolicyTests.cs`. Cubre: AU-54, AU-53 (parte 401); V8.
  - `Preflight_WithAuthorizationHeader_IsAllowedWithoutCredentials` (`OPTIONS /api/time-entries`, `Origin: http://localhost:5172` configurado en `Cors:AllowedOrigins`, `Access-Control-Request-Headers: authorization`; permite origen y cabecera, sin `Access-Control-Allow-Credentials`) y `Unauthorized_HasCorsOrigin` (`401` sin token con `Origin`). Verdes a la primera (la política ya usa `AllowAnyHeader()` y CORS va antes de la autenticación). Mutaciones: `WithHeaders("content-type")` → falla el preflight; mover `UseCors()` tras `UseAuthorization()` → falla el `401`; revertir.
  - Hecho cuando: el foco pasa y las mutaciones quedan anotadas.
  - Evidencia: `CorsPolicyTests` suma `Preflight_WithAuthorizationHeader_IsAllowedWithoutCredentials` y `Unauthorized_HasCorsOrigin` (origen `http://localhost:5172` aportado por `Cors:AllowedOrigins:0` en una fábrica derivada). **Verdes a la primera** (3/3 con la existente). Mutaciones: `AllowAnyHeader()` → `WithHeaders("content-type")` → ROJO el preflight (1 fallo); `UseCors()` movido tras `UseAuthorization()` → ROJO preflight y 401 (2 fallos); ambas revertidas y comprobadas. **V8 resuelta (parte):** el preflight con `authorization` se acepta sin `Access-Control-Allow-Credentials` y el 401 conserva `Access-Control-Allow-Origin`.

## Fase 15: Equipo y rol `Manager`

Archivos comunes: `IT/Team/TeamEndpointsTests.cs`, `API/Team/{TeamEndpoints,TeamMemberResponse}.cs`. Datos: `UserSeed` (marta `Manager`; luis y ana reportes de marta; pablo sin responsable; diego `Manager` con carmen de reporte) y fichajes sembrados con `FakeTimeProvider`. Foco: `T-I --filter "FullyQualifiedName~Team"`.

- [x] 15.1 **RED del contrato del equipo.** Archivos: `AR/OpenApiContractTests.cs`. Cubre: TA-53, TA-54.
  - `ExportedDocument_ContainsOnlyTheTwoTeamGetOperations` (solo `/api/team/members` y `/api/team/members/{memberId}/time-entries`, solo `get`, parámetro `memberId` de tipo `uuid`) y `ExportedDocument_TeamOperationsDeclareTheirResponses` (`200`/`401`/`403` y `200`/`401`/`403`/`404`). RED: faltan las rutas.
  - Hecho cuando: `T-AR --filter "FullyQualifiedName~OpenApiContractTests"` falla por esas pruebas.
  - Evidencia: RED: 2 fallos, 5 verdes; `Falta la ruta /api/team/members en el contrato.` (las dos pruebas).
- [x] 15.2 **Rol `Manager` (V4, V8).** Cubre: TA-38, TA-39, TA-40, TA-41, TA-42, AU-52, AU-53 (403); amenaza API5.
  - RED: `Members_AsEmployee_Returns403ProblemJsonInSpanish` («Acceso denegado», `status` 403), `MemberEntries_AsEmployee_Returns403_ForExistingAndUnknownMember` (el rol se comprueba antes que el miembro), `Members_WithoutToken_Returns401`, `Members_AsManager_Returns200`, `Members_WithUnknownRoleToken_Returns401` (`Admin`) y `Forbidden_HasCorsOrigin` → `404`.
  - GREEN: política `Manager` (`RequireRole("Manager")`), `MapGroup("/api/team").RequireAuthorization("Manager")` y los dos endpoints con respuesta provisional (lista vacía / `200`). V4: `RequireRole` funciona con `RoleClaimType = "role"`. `Members_WithUnknownRoleToken_Returns401` es verde a la primera (11.4); mutación: quitar la comprobación de rol de `OnTokenValidated` → responde `403` y falla; revertir.
  - Hecho cuando: el foco pasa.
  - Evidencia: pruebas en `IT/Team/TeamEndpointsTests.cs` (colección Postgres) con `IT/Support/TeamScenario.cs` (marta `Manager` con luis y ana; pablo sin responsable; diego `Manager` con carmen; fichajes con `TimeEntry.Start/Close`). RED (7 pruebas): 5 con `Expected: Forbidden|OK / Actual: NotFound` (empleado en listado, empleado en fichajes con miembro existente y desconocido —Theory de 2 casos—, `Forbidden_HasCorsOrigin` y `Members_AsManager_Returns200`); `Members_WithoutToken_Returns401` y `Members_WithUnknownRoleToken_Returns401` ya daban 401 (política de respaldo y 11.4: verdes a la primera). GREEN: `API/Team/TeamEndpoints.cs` con la política `Manager` (`RequireRole("Manager")`), `MapGroup("/api/team").RequireAuthorization("Manager")` y respuestas provisionales; 7/7. **V4 resuelta:** `RequireRole` funciona con `RoleClaimType = "role"` y `MapInboundClaims = false` (403 al empleado, 200 al responsable). Mutación de `Members_WithUnknownRoleToken_Returns401`: `roleIsValid = true` en `RejectInvalidClaims` → ROJO `Expected: Unauthorized / Actual: Forbidden`; revertida. **Matiz de proceso:** tras revertir con `mv` desde una copia, el compilador no detectó el cambio (la fecha del archivo era anterior al binario) y una ejecución dio un falso rojo; se resolvió con `touch`, y las demás reversiones se hacen con `cp`. **V8 resuelta:** el 403 conserva `Access-Control-Allow-Origin`.
- [x] 15.3 **Listado del equipo.** Cubre: TA-27, TA-28, TA-30, TA-31; amenaza API3.
  - RED: `Members_ReturnsOnlyDirectReportsOrderedByName` (`ana`, `luis`; sin `pablo`), `Members_BodyHasOnlyIdAndDisplayName` (nombres de propiedad exactos; ni `email`, `passwordHash`, `role` ni `managerId`), `Members_OfAnotherManager_DoesNotContainOthers` (diego solo ve a carmen), `Members_WithoutReports_ReturnsEmptyItems` → la lista provisional está vacía.
  - GREEN: `GetTeamMembersUseCase` en el endpoint, `TeamMemberResponse(Guid Id, string DisplayName)` y `TeamMemberListResponse`; registro `AddTeam()` en `Program.cs`.
  - Hecho cuando: el foco pasa.
  - Evidencia: RED: las 4 pruebas fallan (el endpoint provisional devuelve un arreglo vacío, no `{ items }`). GREEN: `GetTeamMembersUseCase` en el endpoint, `TeamMemberResponse(Guid Id, string DisplayName)` y `TeamMemberListResponse(Items)` en `API/Team/TeamMemberResponse.cs`; `AddTeam()` en `Program.cs`; foco 11/11. Comprueban orden (`Ana Ejemplo`, `Luis Ejemplo`, sin Pablo), nombres de propiedad exactos (`items`; `id`, `displayName`), ausencia de `email`/`passwordHash`/`role`/`managerId`, aislamiento entre responsables y lista vacía.
- [x] 15.4 **Límite de 100 miembros (API).** Cubre: TA-29 (nivel API; la unitaria es 3.9); amenaza API4.
  - `Members_With101DirectReports_ReturnsAtMost100` (101 reportes con nombres `Miembro 001`…`Miembro 101`, insertados en un solo `SaveChanges` con el hash compartido de `UserSeed`; la respuesta trae 100 y no incluye `Miembro 101`). Verde a la primera (el límite ya lo aplica el caso de uso); mutación: `MaxMembers = 101` → falla; revertir.
  - Hecho cuando: el foco pasa y la mutación queda anotada.
  - Evidencia: `Members_With101DirectReports_ReturnsAtMost100` (101 reportes `Miembro 001`…`101` en un solo `SaveChanges`). **Verde a la primera** (el límite ya lo aplica el caso de uso). Mutación `MaxMembers = 101` → ROJO `Expected: 100 / Actual: 101`; revertida.
- [x] 15.5 **Fichajes de un miembro.** Cubre: TA-32, TA-33, TA-34, TA-35.
  - RED: `MemberEntries_ForDirectReport_Returns200WithTotalAndZTimestamps` (60 + 30 minutos y uno abierto: total 90, abierto con `workedMinutes` nulo, instantes terminados en `Z`), `MemberEntries_With55ClosedOf10Minutes_ReturnsFiftyAndTotal500`, `MemberEntries_WithoutEntries_ReturnsEmptyAndZero`, `MemberEntries_ContainsOnlyTheMembersEntries` (fichajes de luis, ana y marta) → la respuesta provisional no trae datos.
  - GREEN: `GetTeamMemberTimeEntriesUseCase` en el endpoint, mismo `TimeEntryListResponse` y mapeo que `GET /api/time-entries`.
  - Pueden pasar a la primera tras el primer GREEN; mutaciones: límite 51 → falla el segundo; consultar `currentUser.EmployeeId` → falla el cuarto; revertir.
  - Hecho cuando: el foco pasa y las mutaciones quedan anotadas.
  - Evidencia: RED: las 4 pruebas fallan (el endpoint provisional devuelve el id como número/texto, sin `items` ni total). GREEN: `GetTeamMemberTimeEntriesUseCase` registrado y mapeo con el mismo `TimeEntryListResponse` (el `null` se trata como lista vacía de forma transitoria hasta 15.7); foco 16/16 (instantes terminados en `Z`, abierto con `clockOutAt` y `workedMinutes` nulos, total 90; 55 cerrados de 10 min → 50 y 500; sin fichajes → vacío y 0; solo los del miembro). Mutaciones: `MaxEntries = 51` → ROJO el de los 55 (1 fallo); consultar `currentUser.EmployeeId` en lugar del miembro → ROJO 3 (los de 55, solo-del-miembro y 200 con total); revertidas.
- [x] 15.6 **Solo lectura.** Cubre: TA-36, TA-37.
  - `MemberEntries_Post_WithManagerToken_Returns405_AndLeavesTheEntryOpen` (el fichaje abierto de luis sigue abierto y no se crea ninguno) y `MemberEntries_Post_WithoutToken_Returns401_AndChangesNothing`. Verdes a la primera (el grupo solo mapea `GET`); mutación: mapear un `POST` temporal → falla; revertir.
  - Hecho cuando: el foco pasa y la mutación queda anotada.
  - Evidencia: `MemberEntries_Post_WithManagerToken_Returns405_AndLeavesTheEntryOpen` (el fichaje abierto de luis sigue abierto y marta no gana ninguno) y `MemberEntries_Post_WithoutToken_Returns401_AndChangesNothing`. **Verdes a la primera** (18/18). Mutación: mapear un `POST` temporal en el grupo → ROJO la primera (`Actual: OK`); la segunda sigue en 401 por la política del grupo; revertida.
- [x] 15.7 **Control por objeto con 404 idéntico.** Cubre: TA-43, TA-44, TA-45, TA-46, TA-47; amenaza API1/BOLA.
  - RED: `MemberEntries_OfAnotherManagersMember_Returns404ProblemJson` (diego pide a luis), `MemberEntries_OfUnknownMember_Returns404ProblemJson`, `MemberEntries_NotFoundResponses_AreIdentical` (miembro ajeno, inexistente, sin responsable —pablo— y el propio: mismo estado, tipo de medio, `type`, `title` «Recurso no encontrado» y `detail` nulo, salvo `traceId`), `MemberEntries_OfSelf_Returns404`, `MemberEntries_WithNonGuidId_Returns404` (con token `Manager`; sin token `401`) → hasta aquí devuelve `200` para cualquiera.
  - GREEN: `null` del caso de uso → `TypedResults.NotFound()` (el cuerpo lo pone `UseStatusCodePages`).
  - Hecho cuando: el foco pasa.
  - Evidencia: RED (4 de 5; 23 pruebas en total): miembro ajeno, inexistente, idénticos y propio → `Expected: NotFound / Actual: OK`; `MemberEntries_WithNonGuidId_Returns404` ya daba 404 con token y 401 sin él (verde a la primera: la ruta `{memberId:guid}` no casa con otro texto; sin token manda la política de respaldo). GREEN: el caso de uso devuelve `null` → `TypedResults.NotFound()` (cuerpo `problem+json` «Recurso no encontrado» sin `detail` de `UseStatusCodePages`), con un `Results<Ok<TimeEntryListResponse>, NotFound>` explícito; 23/23. La prueba de identidad compara estado, tipo de medio y cuerpo sin `traceId` para los cuatro casos (ajeno, inexistente, pablo sin responsable y la propia marta).
- [x] 15.8 **Un empleado nunca ve datos ajenos.** Archivos: `IT/Team/TeamEndpointsTests.cs`, `IT/TimeEntries/TimeEntryIsolationTests.cs`. Cubre: TA-2, TA-49, TA-50, TA-51, TA-52; amenaza API1.
  - `Employee_ListsOnlyOwnEntries`, `Manager_TimeEntriesList_ContainsOnlyOwnEntries`, `GetTimeEntries_WithMemberIdAndEmployeeIdQuery_IgnoresThem` (luis con `?memberId=<ana>&employeeId=<ana>`), `Manager_ClockInWithMemberIdQuery_CreatesOwnEntry` (el fichaje es de marta y luis no gana ninguno) y `Manager_CanClockIn_Returns201`. Verdes a la primera; mutación: aceptar un parámetro `memberId` en `GET /api/time-entries` → falla la tercera; revertir.
  - Hecho cuando: el foco pasa y la mutación queda anotada.
  - Evidencia: las cinco pruebas se añadieron en `IT/Team/TeamEndpointsTests.cs` (no se tocó `TimeEntryIsolationTests`, aunque su copia previa se guardó; ver D-3b-1). **Verdes a la primera** (28/28 con el resto). Mutación: `GET /api/time-entries` con un parámetro opcional `memberId` que sustituye al usuario del token → ROJO `GetTimeEntries_WithMemberIdAndEmployeeIdQuery_IgnoresThem`; revertida.
- [x] 15.9 **Cobertura de autorización del equipo y token alterado.** Archivos: `IT/Security/{AuthorizationCoverageTests,FallbackPolicyTests}.cs`, `IT/Authentication/TokenValidationTests.cs`. Cubre: AU-22 (literal), AU-44, AU-45.
  - `TeamEndpoints_RequireTheManagerPolicy` (`/api/team/*` con `IAuthorizeData.Policy == "Manager"`), `ProtectedEndpoints_WithoutToken_Return401` ampliada con `GET /api/team/members` y `GET /api/team/members/{memberId}/time-entries`, y `Token_AlteredToManager_OnTeamMembers_Returns401` (payload con `role` = `Manager` y firma original). Verdes a la primera; mutación: `RequireAuthorization()` sin política en el grupo → falla la primera; revertir.
  - Hecho cuando: el foco pasa y la mutación queda anotada.
  - Evidencia: `TeamEndpoints_RequireTheManagerPolicy` (los dos endpoints `/api/team/*` llevan `IAuthorizeData.Policy == "Manager"`), dos filas nuevas en la Theory de `ProtectedEndpoints_WithoutToken_Return401` y `Token_AlteredToManager_OnTeamMembers_Returns401` (con control: el token de empleado intacto da 403 y el alterado a `Manager` con la firma original da 401). **Verdes a la primera** (29/29 con `AuthorizationCoverage`, `FallbackPolicy` y `TokenValidation`). Mutación: `RequireAuthorization()` sin política en el grupo → ROJO `TeamEndpoints_RequireTheManagerPolicy`; revertida.
- [x] 15.10 **GREEN del contrato del equipo.** Archivos: `API/Team/TeamEndpoints.cs`, `docs/api/openapi.json` (generado). Cubre: TA-53, TA-54, TA-55.
  - `.ProducesProblem(401)` y `.ProducesProblem(403)` en el grupo, `.ProducesProblem(404)` en los fichajes del miembro; `dotnet build API -warnaserror` regenera el contrato. Mutación de TA-55: un parámetro `Guid employeeId` temporal en la ruta del equipo → `ExportedDocument_DoesNotExposeEmployeeId` falla; revertir y regenerar.
  - Hecho cuando: `T-AR --filter "FullyQualifiedName~OpenApiContractTests"` en verde y 0 apariciones de `employeeId` en `docs\api\openapi.json`.
  - Evidencia: `.ProducesProblem(401)` y `.ProducesProblem(403)` en el grupo y `.ProducesProblem(404)` en los fichajes del miembro; `dotnet build src/TimeTracker.Api -c Release -warnaserror` rc 0 con 0 avisos regenera el contrato; `OpenApiContractTests` 7/7 y 0 apariciones de `employeeId`. Mutación de TA-55: parámetro `Guid? employeeId` temporal en `GET /api/team/members` → ROJO `ExportedDocument_DoesNotExposeEmployeeId`; revertida, contrato regenerado e idéntico al anterior (comparado byte a byte).

## Fase 16: Siembra de usuarios de demostración

- [x] 16.1 **Configuración de la siembra (V21).** Archivos: `IT/Startup/SeedStartupTests.cs`, `API/Seeding/{SeedOptions,SeedOptionsValidator,SeedingServiceCollectionExtensions}.cs`, `API/Configuration/DocumentGeneration.cs`, `Program.cs`. Cubre: SC-7, SC-8, SC-9, SC-10, SC-11, SC-12, SC-13; V21.
  - RED: `SeedOptions_WithoutSetting_DemoUsersIsFalse_AndStartsWithoutPassword`, `Startup_WithNonBooleanDemoUsers_FailsNamingKey` (`quizas`), `Startup_DemoUsersTrueWithoutPassword_FailsNamingKey`, `Startup_With11CharPassword_Fails`, `Startup_DoesNotPrintDemoPassword` (`corta-demo`, 10 caracteres, también en excepciones internas).
  - GREEN: `SeedOptions` (`DemoUsers` booleano, `DemoPassword`), `SeedOptionsValidator` («Seed:DemoPassword: debe tener al menos 12 caracteres cuando Seed:DemoUsers es true.»), `ValidateOnStart`, y **en la misma tarea** `Seed:DemoUsers=false` en `DocumentGeneration`.
  - Verdes a la primera: `Startup_With12CharPassword_StartsWithMigratedDatabase` (SC-11, base del fixture con migraciones) y `Startup_DemoUsersFalse_IgnoresShortPassword` (SC-12). Mutaciones: `< 12` → `<= 12` → falla SC-11; validar aunque la bandera sea `false` → falla SC-12; revertir.
  - Hecho cuando: el foco pasa y `dotnet build API -warnaserror` rc 0.
  - Evidencia: `IT/Startup/SeedStartupTests.cs` (7 pruebas, colección Postgres para la de 12 caracteres). RED de compilación (`CS0234: 'Seeding' no existe`); con solo `SeedOptions` creado, RED de comportamiento: 4 fallos (`Startup_WithNonBooleanDemoUsers…`, `…TrueWithoutPassword…`, `…With11CharPassword…`, `…DoesNotPrintDemoPassword`: sin validador el arranque no falla) y 3 verdes de golpe (`SeedOptions_WithoutSetting…`: el valor por defecto de la propiedad ya es `false`; `With12CharPassword` y `DemoUsersFalse_IgnoresShortPassword`, que son las verdes a la primera previstas). GREEN: `SeedOptions`, `SeedOptionsValidator` («Seed:DemoPassword: debe tener al menos 12 caracteres cuando Seed:DemoUsers es true.»; sin imprimir el valor), `AddDemoSeeding()` con `ValidateOnStart` y, **en la misma tarea**, `Seed:DemoUsers = false` en `DocumentGeneration`; foco 7/7 y `dotnet build API -c Release -warnaserror` rc 0 con 0 avisos. Mutaciones: `<` → `<=` → ROJO `Startup_With12CharPassword_StartsWithMigratedDatabase`; validar aunque la bandera sea `false` → ROJO `Startup_DemoUsersFalse_IgnoresShortPassword`; revertidas. **V21 resuelta (parte siembra):** `GetDocument.Insider` genera el contrato con `Seed:DemoUsers = false` y no ejecuta la siembra (build rc 0).
- [x] 16.2 **RED de la siembra.** Archivos: `IT/Seeding/DemoUsersSeederTests.cs`. Cubre: TA-56–TA-63.
  - Una base vacía con nombre único por prueba (como `DatabaseMigrationStartupTests`), `Database:ApplyMigrationsOnStartup=true`, `Seed:DemoUsers=true` y una contraseña de prueba de 12 o más caracteres. Pruebas: `Seeding_CreatesTheFourUsersWithRolesAndManagers` (TA-56), `Seeding_StoresOnlyVerifiableHashes` (TA-57), `Seeding_RunTwice_KeepsFourUsersWithSameIds` (TA-58, segundo arranque sobre la misma base), `Seeding_WithExistingMarta_KeepsHerHashAndLinksReportsToHer` (TA-59), `Seeding_Disabled_LeavesUsersEmpty` (TA-60), `SeededMarta_LogsInAndSeesAnaAndLuisNotPablo` (TA-61), `Seeding_WithUnreachableDatabase_FailsStartup` (TA-62, `Timeout=2`) y `Seeding_DoesNotLogThePassword` (TA-63, registrador que captura).
  - Fallan: TA-56, TA-57, TA-58, TA-59, TA-61 (no hay usuarios) y TA-62 (el host arranca). TA-60 y TA-63 pasan sin siembra (se justifican en 16.4).
  - Hecho cuando: las fallas coinciden con lo anotado.
  - Evidencia: `IT/Seeding/DemoUsersSeederTests.cs` (8 pruebas; base vacía por prueba con `CreateEmptyDatabaseAsync`, `Database:ApplyMigrationsOnStartup=true`, `Seed:DemoUsers=true`, contraseña de prueba `Clave-Demo-De-Prueba-26`) y `IT/Support/CapturingLoggerProvider.cs` (extraído de `SigningKeyLeakTests`, cuyo comportamiento no cambia; ver D-3b-1). RED: 5 fallos (TA-56 `Expected: [4 correos] / Actual: []`, TA-58 `Expected: 4 / Actual: 0`, TA-59 `Expected: 4 / Actual: 1`, TA-61 login → no es 200 y TA-62 el host arranca); TA-57 pasó **en vacío** (`Assert.All` sobre una lista vacía): se corrigió añadiendo `Assert.Equal(4, users.Count)`, con lo que también falla en el RED. TA-60 y TA-63 pasan sin siembra, como estaba previsto (ver 16.4).
- [x] 16.3 **GREEN del sembrador (V16).** Archivos: `API/Seeding/{DemoUsers,DemoUsersSeeder}.cs`, `SeedingServiceCollectionExtensions.cs`, `Program.cs`. Cubre: TA-56–TA-59, TA-61, TA-62; V16.
  - `DemoUsersSeeder : IHostedService` con el trabajo en `StartAsync` (DD-22), registrado tras `AddDatabaseConfiguration`. Usuarios inventados: Marta (`Manager`), Luis y Ana (`Employee`, con `ManagerId` = id vigente de Marta) y Pablo (`Employee`), con nombres «Marta Ejemplo», «Luis Ejemplo», «Ana Ejemplo» y «Pablo Ejemplo»; `Guid.CreateVersion7(TimeProvider.GetUtcNow())`; crea solo si `FindByEmailAsync` no lo encuentra; hash con `IPasswordHasher`; `LoggerMessage` con el recuento, sin correos ni contraseña. Con la base inaccesible el arranque falla.
  - V16: las `StartingAsync` (migración) terminan antes que cualquier `StartAsync`, y el sembrador corre antes de que Kestrel escuche, también con `WebApplicationFactory` (se comprueba con `SeededMarta_LogsInAndSeesAnaAndLuisNotPablo` sin arranque previo).
  - Hecho cuando: el foco de 16.2 pasa salvo TA-60/TA-63 (ya verdes); V16 anotada.
  - Evidencia: `API/Seeding/{DemoUsers,DemoUsersSeeder}.cs`: `DemoUsersSeeder : IHostedService` con el trabajo en `StartAsync`, registrado en `AddDemoSeeding()`; Marta primero, luego Luis y Ana con `ManagerId` = id vigente de Marta (la creada o la existente) y Pablo; crea solo si `FindByEmailAsync` no lo encuentra; hash con `IPasswordHasher` por usuario creado; `Guid.CreateVersion7(TimeProvider.GetUtcNow())`; `LoggerMessage` solo con recuentos («creados» y «ya existentes»). Foco 8/8 de golpe (los 6 rojos pasan a verde). **V16 resuelta:** `SeededMarta_LogsInAndSeesAnaAndLuisNotPablo` hace login y consulta el equipo sin ninguna petición previa que dispare la siembra, y `Seeding_WithUnreachableDatabase_FailsStartup` falla con `NpgsqlException` sin migraciones activas (el fallo es del propio sembrador, en `StartAsync`); la migración (`StartingAsync`) termina antes. La comprobación con Kestrel real (que se siembra antes de escuchar) queda en 20.4. Nota: con la siembra activa y la migración desactivada sobre una base sin esquema el arranque falla (tabla `users` ausente): es el comportamiento esperado.
- [x] 16.4 **Idempotencia, bandera y registro (mutaciones).** Cubre: TA-58, TA-60, TA-63; amenaza A09.
  - Verdes a la primera TA-60 y TA-63 (y TA-58 tras 16.3). Mutaciones: crear sin comprobar la existencia → TA-58 y TA-59 fallan (`23505`); sembrar aunque la bandera sea `false` → TA-60 falla; registrar la contraseña en el `LoggerMessage` → TA-63 falla; revertir.
  - Hecho cuando: las tres mutaciones quedan anotadas y el foco está en verde.
  - Evidencia: TA-60 (`Seeding_Disabled_LeavesUsersEmpty`) y TA-63 (`Seeding_DoesNotLogThePassword`) fueron **verdes a la primera** (sin siembra no hay usuarios ni mensajes con la contraseña); TA-58 pasó tras 16.3. Mutaciones: crear sin comprobar la existencia → ROJO TA-58 y TA-59 con `PostgresException 23505 … ux_users_email`; condición de la bandera invertida (sembrar con la bandera a `false` y contraseña nula) → ROJO (el arranque falla al hashear, TA-60 y otras de la clase); `logger.LogInformation("clave {Password}", …)` tras el mensaje de la siembra → ROJO `Seeding_DoesNotLogThePassword`. Un primer intento de la segunda mutación (`if (false)`) no compilaba por `CS0162` (código inaccesible con `-warnaserror`) y se rehízo; solo cuenta la ejecución real. Todas revertidas y comparadas con la copia; `BUILD` rc 0.

## Fase 17: Límite de intentos de login **[R3]**

Archivos comunes: `IT/Security/LoginRateLimitTests.cs`, `API/Security/RateLimitingServiceCollectionExtensions.cs`, `API/Authentication/AuthEndpoints.cs`, `Program.cs`. Cada prueba crea su propia fábrica (los contadores no se comparten). Foco: `T-I --filter "FullyQualifiedName~LoginRateLimit"`.

- [x] 17.1 **Undécimo intento → 429 (V9, V17).** Cubre: AU-36; DD-19; amenaza A07/API4.
  - RED: `Login_EleventhAttemptWithinMinute_Returns429ProblemJsonWithRetryAfter` (`status` 429, `title` «Demasiadas solicitudes», `detail` «Espere un minuto antes de volver a intentarlo.», cabecera `Retry-After`) → el undécimo devuelve `401`.
  - GREEN: política `login` de ventana fija (10 por minuto, partición por `RemoteIpAddress`, `"sin-ip"` si no hay), `QueueLimit = 0`, `RejectionStatusCode = 429`, `OnRejected` con `Retry-After`; `.RequireRateLimiting("login")`; `app.UseRateLimiter()` tras `UseCors()` (DD-21). V9: el cuerpo lo escribe `UseStatusCodePages`; respaldo: escribirlo en `OnRejected`. V17: en `TestServer` no hay IP (una partición por fábrica).
  - Tras el GREEN, ejecutar todo `T-I`: las clases que hagan más de 10 logins por fábrica fallarán con `429`; aplicar el respaldo de V17: un `IStartupFilter` de prueba en `ApiFactory` que fija `RemoteIpAddress` desde la cabecera `X-Test-Remote-Ip`, con una IP distinta por prueba.
  - Hecho cuando: el foco y `T-I` completo pasan; V17 anotada.
  - Evidencia: RED (`Login_EleventhAttemptWithinMinute_Returns429ProblemJsonWithRetryAfter`, `PostgresApiFactory`): `Expected: TooManyRequests / Actual: Unauthorized`. GREEN: `API/Security/RateLimitingServiceCollectionExtensions.cs` (política `login`, ventana fija de 10 por minuto, partición por `RemoteIpAddress` o `"sin-ip"`, `QueueLimit = 0`, `RejectionStatusCode = 429`), `.RequireRateLimiting("login")` en el login y `app.UseRateLimiter()` tras `UseCors()`. **V9 resuelta con el respaldo:** el `detail` en español («Espere un minuto antes de volver a intentarlo.») solo lo conoce el limitador, así que `OnRejected` escribe el `ProblemDetails` completo con `IProblemDetailsService` y añade `Retry-After` (`UseStatusCodePages` no lo pisa porque ya hay cuerpo). **V17 resuelta sin el respaldo:** `TestServer` no aporta IP (una partición por host) y, tras ejecutar todo `T-I` con el limitador activo, ninguna clase supera 10 logins por fábrica: **226/226** en verde; no hizo falta el `IStartupFilter` de IP de prueba.
- [x] 17.2 **Diez intentos, intentos correctos y otras rutas.** Cubre: AU-35, AU-37, AU-38.
  - `Login_TenWrongAttempts_NoneReturns429` (los diez dan `401`), `Login_SuccessfulAttemptsAlsoCount` (10 logins correctos y el undécimo, correcto, da `429`), `OtherRoutes_AreNotLimited` (tras un `429`, 20 × `GET /api/auth/me` con token vigente → `200`). Verdes a la primera; mutaciones: `PermitLimit = 9` → falla la primera; contar solo los fallidos → falla la segunda; `RequireRateLimiting` global → falla la tercera; revertir.
  - Hecho cuando: el foco pasa y las tres mutaciones quedan anotadas.
  - Evidencia: `Login_TenWrongAttempts_NoneReturns429`, `Login_SuccessfulAttemptsAlsoCount` y `OtherRoutes_AreNotLimited` (20 × `GET /api/auth/me` con token → 200 tras un 429). **Verdes a la primera** tras 17.1 (5/5 con las otras dos). Mutaciones: `PermitLimit = 9` → ROJO las 5 (cualquier prueba que haga diez intentos, incluida `Login_TenWrongAttempts_NoneReturns429`); limitador **global** (`GlobalLimiter` de 10 por minuto) → ROJO `OtherRoutes_AreNotLimited`; la mutación «contar solo los fallidos» no es expresable sin código nuevo, así que se sustituyó por **quitar `RequireRateLimiting`** → ROJO 4 (entre ellas `Login_SuccessfulAttemptsAlsoCount`, que ya no obtiene el 429). Revertidas.
- [x] 17.3 **CORS en el 429 (V8).** Cubre: AU-39.
  - `RateLimited429_HasCorsOrigin` (`Origin: http://localhost:5172` permitido; el `429` lleva `Access-Control-Allow-Origin`). Verde a la primera; mutación: `UseRateLimiter()` antes de `UseCors()` → falla; revertir.
  - Hecho cuando: el foco pasa y la mutación queda anotada.
  - Evidencia: `RateLimited429_HasCorsOrigin` (origen `http://localhost:5172` en `Cors:AllowedOrigins`). **Verde a la primera** (5/5 la clase). Mutación: `UseRateLimiter()` antes de `UseCors()` → ROJO (1 fallo: el 429 sin `Access-Control-Allow-Origin`); revertida. **V8 resuelta:** el 429 conserva `Access-Control-Allow-Origin` (CORS antes del limitador).

## Fase 18: Cabeceras de seguridad **[R2]**

Archivos comunes: `IT/Security/SecurityHeadersTests.cs`, `API/Security/SecurityHeadersMiddleware.cs`, `Program.cs` (`app.UseSecurityHeaders()` como primer middleware, DD-21).

- [x] 18.1 **Cabeceras en respuestas correctas, sin caché y CSP.** Cubre: AU-40, AU-42, AU-43.
  - RED: `Health_Response_HasTheFourSecurityHeaders` (`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Content-Security-Policy`), `ApiRoutes_HaveNoStore_OnSuccessAndUnauthorized` (`GET /api/auth/me` con token y sin token), `Csp_IsRestrictive` (existe, no vacía, sin `unsafe-inline` ni `*`).
  - GREEN: middleware que registra `Response.OnStarting` (DD-20) con `default-src 'none'; frame-ancestors 'none'` y `Cache-Control: no-store` bajo `/api`. Sin HSTS (ADR 0007).
  - Hecho cuando: el foco pasa.
  - Evidencia: `IT/Security/SecurityHeadersTests.cs`. RED: `Health_Response_HasTheFourSecurityHeaders` (`200: falta la cabecera X-Content-Type-Options.`), `ApiRoutes_HaveNoStore_OnSuccessAndUnauthorized` y `Csp_IsRestrictive` fallan (la de 18.2 también). GREEN: `API/Security/SecurityHeadersMiddleware.cs` (`Response.OnStarting`: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`, y `Cache-Control: no-store` bajo `/api`) y `app.UseSecurityHeaders()` como primer middleware; 4/4.
- [x] 18.2 **Cabeceras también en los errores.** Cubre: AU-41; amenaza API8.
  - `ErrorResponses_HaveTheFourSecurityHeaders` (`401` sin token, `429` con 11 logins, `404` con token y `500` con un repositorio falso que lanza). Verdes a la primera por `OnStarting`; mutación: fijar las cabeceras al entrar (sin `OnStarting`) → falla el `500` porque `ExceptionHandler` limpia las cabeceras; revertir.
  - Hecho cuando: el foco pasa y la mutación queda anotada.
  - Evidencia: `ErrorResponses_HaveTheFourSecurityHeaders` (401 sin token, 429 tras diez logins, 404 con token y 500 con un repositorio que lanza). **Verde a la primera** tras 18.1 (4/4 la clase). Mutación: fijar las cabeceras al entrar (sin `OnStarting`) → ROJO `500: falta la cabecera X-Content-Type-Options.` (`ExceptionHandler` limpia las cabeceras); revertida y `BUILD` rc 0. Sin HSTS (ADR 0007).

## Fase 19: Contrato OpenAPI y esquema Bearer

- [x] 19.1 **RED del contrato completo.** Archivos: `AR/OpenApiContractTests.cs`. Cubre: AU-55, AU-56, TK-14.
  - `ExportedDocument_LoginDeclares200_400_401_429` (y `/api/auth/me` con `200`/`401`), `ExportedDocument_TimeEntryEndpointsDeclare401` (clock-in `201`/`401`/`409`, clock-out `200`/`401`/`409`, `GET` `200`/`401`, `format: date-time` en `clockInAt` y `clockOutAt`) y `ExportedDocument_DeclaresBearerScheme` **[R1]** (`components.securitySchemes.Bearer` de tipo `http`, `bearer`, `JWT`; `/api/time-entries` lo requiere; `/api/auth/login` y `/health` declaran `security: []`). RED: faltan el `429` y el esquema.
  - Hecho cuando: las pruebas fallan por esos motivos.
  - Evidencia: `ExportedDocument_LoginDeclares200_400_401_429` (con `/api/auth/me` 200/401) y `ExportedDocument_DeclaresBearerScheme` (esquema `Bearer` `http`/`bearer`/`JWT`; `/api/time-entries` y `/api/team/members` lo requieren; `/api/auth/login` y `/health` no, con la regla «la operación sustituye al requisito global»). RED: 2 fallos, 7 verdes: `POST /api/auth/login no declara 429.` y `KeyNotFoundException` (sin `securitySchemes`). La comprobación de `401` en los tres endpoints de fichaje y `date-time` ya existían en `ExportedDocument_ContainsAuthEndpoints` y `ExportedDocument_ContainsTimeEntryEndpoints` (10.1/12.2), así que no se duplicó `ExportedDocument_TimeEntryEndpointsDeclare401`.
- [x] 19.2 **GREEN del contrato y de los transformadores (V7) [R1].** Archivos: `API/Configuration/OpenApiSecurityTransformers.cs`, `API/Authentication/AuthEndpoints.cs`, `Program.cs`. Cubre: AU-55, AU-56; V7.
  - `.ProducesProblem(429)` en el login. `AddBearerSecurity()`: transformador de documento con el esquema (API de `Microsoft.OpenApi` 2.7.6 y `OpenApiSecuritySchemeReference`) y requisito global; transformador de operación con `security: []` en las operaciones con `IAllowAnonymous`. V7: si la API de `Microsoft.OpenApi` no admite el esquema, **recortar R1** (retirar solo la prueba del esquema y los transformadores; el `429` y los `401` se conservan) y avisar.
  - Hecho cuando: `T-AR --filter "FullyQualifiedName~OpenApiContractTests"` en verde y V7 anotada.
  - Evidencia: `.ProducesProblem(429)` en el login; `API/Configuration/OpenApiSecurityTransformers.cs` con `AddBearerSecurity(this OpenApiOptions)` (esquema `Bearer` `http`/`bearer`/`JWT` con `document.AddComponent` y `OpenApiSecuritySchemeReference`, requisito global y `security: []` en las operaciones con `IAllowAnonymous`), enganchado con `AddOpenApi(options => options.AddBearerSecurity())`. **V7 resuelta:** la API de `Microsoft.OpenApi` 2.7.6 admite el esquema y la referencia; **no se recorta R1**. Un primer intento registraba los transformadores con `services.Configure<OpenApiOptions>(…)`, que configura las opciones sin nombre y no las del documento `v1` (el contrato salió sin esquema); se rehízo como método de extensión sobre `OpenApiOptions` (desviación menor de firma: la tarea decía `AddBearerSecurity()`). `OpenApiContractTests` 9/9 y `docs/api/openapi.json` con `securitySchemes.Bearer`, `security: [{Bearer}]` global y `security: []` en `/health` y en el login.
- [x] 19.3 **Contrato sincronizado y sin datos prohibidos.** Cubre: AU-58, AU-57, TA-55, TK-7, SA-26.
  - Con `ENV-CHECK` vacío: `$h1 = (Get-FileHash docs\api\openapi.json -Algorithm SHA256).Hash`; `dotnet build API --no-incremental -warnaserror`; `$h2 = …`; `$h1 -eq $h2` (repetir dos veces). `Select-String 'passwordHash|employeeId' docs\api\openapi.json` sin coincidencias; el documento contiene las rutas de login, me, fichajes y equipo (V21 completa).
  - Hecho cuando: los hashes coinciden, no hay coincidencias prohibidas y el documento se lee con `ConvertFrom-Json`.
  - Evidencia: con `ENV-CHECK` vacío, SHA-256 de `docs\api\openapi.json` antes y después de dos `dotnet build src/TimeTracker.Api -c Release --no-incremental -warnaserror` (rc 0, 0 avisos): `f0c63377be770c93…` las tres veces (idéntico). Cero coincidencias de `passwordHash|employeeId`. `pwsh` lo lee con `ConvertFrom-Json` y las rutas son `/health`, `/api/auth/login`, `/api/auth/me`, los tres de fichaje y las dos de equipo. **V21 completa:** el contrato incluye login, me, fichajes y equipo, con esquema Bearer y sin datos prohibidos. `OpenApiContractTests` 9/9.

## Fase 20: Entrega (`.env.example`, compose, imagen, puerto)

- [x] 20.1 **`.env.example`.** Archivos: `AR/DeliveryArtifactsTests.cs`, `.env.example`. Cubre: SC-19, SC-20, SC-21, SC-22, SC-23.
  - RED: `EnvExample_ContainsTheDocumentedKeys` (renombra a `…SixNewKeys`; 8 claves: `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`, `ConnectionStrings__Default`, `Database__ApplyMigrationsOnStartup`, `Jwt__SigningKey`, `Seed__DemoUsers`, `Seed__DemoPassword`), `EnvExample_AuthenticationKeysAreEmptyOrFalseAndDocumented` (`Jwt__SigningKey=` y `Seed__DemoPassword=` vacías y activas, `Seed__DemoUsers=false`, comentario de `Seed__DemoPassword` con «solo en local», «12 caracteres» y «README», sin `Demo-Fichajes-2026`) y `EnvExample_HasNoProvisionalIdentity`; regresión de `EnvExample_HasNoRealValues` y `EnvExample_HasNoActiveAiKeys`.
  - GREEN: editar `.env.example` (retirar `Identity__DevelopmentEmployeeId`; comentario con el comando de generación de la clave).
  - Hecho cuando: `T-AR --filter "FullyQualifiedName~DeliveryArtifactsTests"` en verde para `.env.example`.
  - Evidencia previa (parcial, **bloqueada**): RED hecho en `DeliveryArtifactsTests` y `RepositoryConventionsTests`: `EnvExample_ContainsTheDocumentedKeys` (8 claves; «.env.example no define Jwt__SigningKey.»), `EnvExample_AuthenticationKeysAreEmptyOrFalseAndDocumented` (`KeyNotFoundException: Jwt__SigningKey`), `EnvExample_HasNoDevelopmentEmployeeKey` y `DeliveryFiles_HaveNoProvisionalIdentity(.env.example)` (ambas: «Sub-string found: Identity__DevelopmentEmployeeId»); regresión `EnvExample_HasNoRealValues` ampliada con `Jwt__SigningKey` y `Seed__DemoPassword`. **GREEN no aplicado:** las herramientas `Read`, `Edit` y `Write` y los comandos `cat`/`grep` sobre `.env.example` están denegados por la configuración de permisos del usuario («File is in a directory that is denied by your permission settings»); no se eludió con otras vías. El archivo propuesto (8 claves activas, `Jwt__SigningKey=` y `Seed__DemoPassword=` vacías, `Seed__DemoUsers=false`, comentario de uso solo local, 12 caracteres y README, comando `pwsh` para generar la clave, `Cors__AllowedOrigins__0` vacío, `ASPNETCORE_HTTP_PORTS` y `Ai__*` comentadas, sin `Identity__DevelopmentEmployeeId` ni la contraseña de demostración) está en el scratchpad de la sesión como `env.example.proposed`, y un script que replica las comprobaciones de las pruebas lo da por válido. Para cerrar: sustituir `.env.example` por ese archivo y ejecutar `T-AR --filter "FullyQualifiedName~DeliveryArtifactsTests|FullyQualifiedName~DeliveryFiles_HaveNo"` (5 pruebas hoy rojas).
  - Evidencia final (2026-10-14): el usuario sustituyó `.env.example` por `env.example.proposed` (el archivo propuesto ya no existe). No se lee `.env.example` (los permisos lo deniegan; no se esquiva): se verifica por las pruebas. Las 5 pruebas antes rojas (`EnvExample_ContainsTheDocumentedKeys`, `EnvExample_AuthenticationKeysAreEmptyOrFalseAndDocumented`, `EnvExample_HasNoDevelopmentEmployeeKey`, `EnvExample_ListsEveryKeyConsumedByCompose` y `DeliveryFiles_HaveNoProvisionalIdentity(.env.example)`) pasan dentro de Arquitectura 71/71 en `dotnet test TimeTracker.sln -c Release --no-build`; suite completa 393/393. Cierra SC-19 a SC-23.
- [x] 20.2 **`docker-compose.yml` (puerto 5274).** Archivos: `AR/DeliveryArtifactsTests.cs`, `docker-compose.yml`. Cubre: SA-5 (compose y `.env.example`), SA-10, SA-11, SA-12, SA-13, SA-14, PE-11.
  - RED: `Compose_PublishesApiOn5274LocalOnlyInDevelopment` (`127.0.0.1:5274:8080`, `ASPNETCORE_ENVIRONMENT=Development`), `Compose_RequiresJwtAndSeedPasswordFromEnvAndEnablesSeeding` (`${Jwt__SigningKey:?…}`, `${Seed__DemoPassword:?…}`, `Seed__DemoUsers: "true"`, sin literales ni `Identity__`), `Compose_UsesTheSameImageAsTheFixture` (PE-11), regresión de `Compose_DefinesApiAndPostgresWithRequiredSettings` y `Compose_HasNoCredentialLiterals`, y la extensión de `Code_HasNoProvisionalIdentityLeftovers` a `docker-compose.yml` y `.env.example` (SA-5).
  - GREEN: editar compose (`127.0.0.1:5274:8080`, claves obligatorias con mensaje, siembra activa, comentario de cabecera actualizado: sin identidad provisional, solo en `127.0.0.1` por la contraseña de demostración pública). Mutación: sustituir `${Jwt__SigningKey:?…}` por un literal → falla; revertir.
  - SA-10 (manual): con variables de sesión ficticias definidas (`POSTGRES_*`, `Jwt__SigningKey`, `Seed__DemoPassword`), `docker compose config --services` imprime exactamente `api` y `postgres`.
  - Hecho cuando: `T-AR --filter "FullyQualifiedName~DeliveryArtifactsTests"` en verde y la salida de `config --services` anotada.
  - Evidencia: RED: `Compose_PublishesApiOn5274LocalOnlyInDevelopment` y `Compose_RequiresJwtAndSeedPasswordFromEnvAndEnablesSeeding` fallan (`Sub-string not found` / `Pattern not found`); `Compose_UsesTheSameImageAsTheFixture` verde a la primera (la imagen ya coincidía); se retiró de `Compose_DefinesApiAndPostgresWithRequiredSettings` la aserción de `127.0.0.1:8080:8080`. GREEN: `docker-compose.yml` con `127.0.0.1:5274:8080` como única publicación de `api`, `Jwt__SigningKey: ${Jwt__SigningKey:?…}`, `Seed__DemoPassword: ${Seed__DemoPassword:?…}`, `Seed__DemoUsers: "true"` y el comentario de cabecera actualizado (sin identidad provisional; solo `127.0.0.1` por la contraseña de demostración pública); 5/5 en `Compose_`. Mutación: `Jwt__SigningKey` con un literal → ROJO `Compose_RequiresJwtAndSeedPasswordFromEnvAndEnablesSeeding`; revertida. SA-10: con las variables ficticias del scratchpad, `docker compose -f docker-compose.yml -f <override> --env-file <ficticio> -p ttsmoke3b config --services` imprime exactamente `postgres` y `api`. `DeliveryArtifactsTests` completo sigue sin estar en verde solo por `.env.example` (20.1).
- [x] 20.3 **Imagen Docker (V11).** Archivos: ninguno (evidencia; `Dockerfile` sin cambios). Cubre: SA-7, SA-8, SA-9; V11.
  - `docker build -t timetracker-api .` rc 0; `docker inspect`: `User` distinto de vacío y de `root`, y `Healthcheck`; `docker run` con `Cors__AllowedOrigins__0`, `ConnectionStrings__Default` (servidor inexistente) y `Jwt__SigningKey` de 32 caracteres o más (sin `ASPNETCORE_ENVIRONMENT=Development`): `GET /health` sin token 200 y estado `healthy` (V11: el `HEALTHCHECK` usa el `/health` anónimo); sin `Jwt__SigningKey` el contenedor termina con código ≠ 0 nombrando `Jwt:SigningKey`; `docker history --no-trunc` e `docker image inspect` de la **imagen** sin `Jwt__SigningKey` ni el valor usado.
  - Hecho cuando: cada comprobación queda anotada; sin Docker se marca «no probado».
  - Evidencia: `docker build -t timetracker-api .` rc 0 (`Dockerfile` sin cambios). `docker inspect`: `User = 1654` (distinto de vacío y de `root`) y `Healthcheck` con `wget … /health` (intervalo 30 s, 3 reintentos). `docker run -d` con `Cors__AllowedOrigins__0`, `ConnectionStrings__Default` (servidor inexistente) y `Jwt__SigningKey` de más de 32 caracteres, sin `ASPNETCORE_ENVIRONMENT`: `GET /health` sin token → `200 {"status":"Healthy"}` y el contenedor queda `healthy` (**V11 resuelta**: el `HEALTHCHECK` usa el `/health` anónimo). Sin `Jwt__SigningKey` el contenedor termina con código **139** (≠ 0) y el registro dice «Jwt:SigningKey: debe definirse.». `docker history --no-trunc` y `docker image inspect` de la imagen: 0 apariciones del valor de la clave usada ni de `Jwt__SigningKey`. Contenedores de prueba eliminados.
- [x] 20.4 **Compose de extremo a extremo (V12).** Archivos: ninguno (evidencia). Cubre: SA-15, SA-12 (en ejecución), TA-56 y TA-61 (en ejecución); V12.
  - Sin leer `.env`: definir en la sesión `POSTGRES_PASSWORD`, `Jwt__SigningKey` (generada), `Seed__DemoPassword = 'Demo-Fichajes-2026'` y `Cors__AllowedOrigins__0 = 'http://localhost:5172'` (si el `.env` local ya los define, tienen prioridad las de la sesión). `docker compose up -d --wait`: `postgres` queda `healthy` y `api` después; `Invoke-RestMethod -Method Post http://localhost:5274/api/auth/login` con `luis.empleado@example.test` → `200` con token; con `marta.responsable@example.test` y `GET /api/team/members` → ana y luis, sin pablo; con el token de Luis, el mismo `GET` → `403`; `Get-NetTCPConnection -LocalPort 5274,5432` solo en `127.0.0.1`. Después `docker compose down -v` y limpiar las variables.
  - Hecho cuando: cada paso queda anotado con su salida (valores ficticios).
  - Evidencia: sin leer `.env`: archivo de variables ficticias fuera del repo (`--env-file` del scratchpad con `POSTGRES_*`, una `Jwt__SigningKey` ficticia de más de 32 caracteres y `Seed__DemoPassword = Demo-Fichajes-2026`) y un archivo override fuera del repo que retira `env_file` (`!reset []`) y aporta `Cors__AllowedOrigins__0 = http://localhost:5172`; proyecto `ttsmoke3b`. `docker compose … up -d --build --wait` rc 0: `postgres` `Healthy`, después `api` `Healthy`; `ps`: `api Up (healthy) 127.0.0.1:5274->8080/tcp` y `postgres Up (healthy) 127.0.0.1:5432->5432/tcp`; registro de la API: «Aplicando las migraciones pendientes…», «Usuarios de demostración sembrados: 4 creados y 0 ya existentes.» y después «Now listening on: http://[::]:8080» (la siembra termina antes de escuchar: **V12 resuelta**). En vivo contra `http://localhost:5274`: `POST /api/auth/login` de `luis.empleado@example.test` → `200` (rol `Employee`, token de 437 caracteres) y de `marta.responsable@example.test` → `200` (rol `Manager`); marta `GET /api/team/members` → `200` con «Ana Ejemplo» y «Luis Ejemplo» (sin Pablo); el token de Luis en el mismo `GET` → **`403`**; `GET /api/time-entries` sin token → **`401`** (`application/problem+json`, `title` «Se requiere autenticación», `WWW-Authenticate: Bearer`, las cuatro cabeceras de seguridad y `Cache-Control: no-store`); marta pide los fichajes de un GUID inexistente → **`404`**; 12 logins erróneos seguidos (con los 2 correctos previos ya contados): `401` ×8 y **`429`** ×4 desde el undécimo intento del minuto; con el limitador agotado, `GET /api/auth/me` con token vigente → `200`. `Get-NetTCPConnection -State Listen -LocalPort 5274,5432`: ambos solo en `127.0.0.1`. La contraseña de demostración y la clave de firma no aparecen en el registro de la API (0 coincidencias). Después `docker compose … down -v` (contenedores, red y volumen `ttsmoke3b_pgdata` eliminados; sin restos del proyecto en `docker ps -a` ni `docker volume ls`); los archivos de variables solo existen en el scratchpad (valores ficticios).
- [x] 20.5 **Puerto configurable.** Archivos: ninguno (evidencia). Cubre: SC-24.
  - `$env:ASPNETCORE_HTTP_PORTS='8081'` más `Cors__AllowedOrigins__0`, `ConnectionStrings__Default` ficticia y `Jwt__SigningKey` válida; `dotnet run --no-launch-profile --project src/TimeTracker.Api`; `Invoke-WebRequest http://localhost:8081/health` → 200. Detener el proceso y limpiar las variables.
  - Hecho cuando: la salida queda anotada (no hay prueba automática: el puerto no es comprobable con `WebApplicationFactory`).
  - Evidencia: `ASPNETCORE_HTTP_PORTS=8081`, `Cors__AllowedOrigins__0=http://localhost:5172`, `ConnectionStrings__Default=Host=db.invalid;…` y una `Jwt__SigningKey` ficticia de más de 32 caracteres, exportadas solo para el proceso; `dotnet run --no-launch-profile -c Release --project src/TimeTracker.Api`: «Now listening on: http://[::]:8081» y «Hosting environment: Production»; `GET http://localhost:8081/health` → `200 {"status":"Healthy"}`. Proceso detenido (nada escucha en 8081) y variables limpias. Un primer intento con los valores como prefijo del comando falló por el entrecomillado del shell y se repitió con `export`; solo cuenta la ejecución real.

## Fase 21: Punto de control de la Tanda 3b

- [x] 21.1 **Puerta de calidad completa.** Cubre: SA-4, SA-16, SA-18; AU-60; V10, V20.
  - `RESTORE`, `FORMAT`, `BUILD`, `TEST`, `COV-D`, `COV-A`, `VULN`; `Select-String 'FromSqlRaw|ExecuteSqlRaw|EnableSensitiveDataLogging|NotImplementedException' -Path src -Recurse` sin coincidencias; contratos sincronizados (19.3).
  - Hecho cuando: todo en 0 y `BUILD` con 0 avisos.
  - Evidencia previa (parcial; la tarea sigue abierta por 20.1): `RESTORE` rc 0; `FORMAT` rc 0; `BUILD` rc 0 con 0 avisos y 0 errores (V20: nada desactivado; el build exporta el contrato). `TEST` sobre `TestResults` limpio: **386** pruebas = Domain 49 (49 verdes), Application 43 (43), Arquitectura 64 (59 verdes y **5 rojas**) e Integración 230 (230 verdes; PostgreSQL 18 real con Testcontainers). Las 5 rojas son únicamente las de `.env.example` pendientes de 20.1 (`EnvExample_ContainsTheDocumentedKeys`, `…AuthenticationKeysAreEmptyOrFalseAndDocumented`, `…HasNoDevelopmentEmployeeKey`, `…ListsEveryKeyConsumedByCompose` y `DeliveryFiles_HaveNoProvisionalIdentity(.env.example)`) por el bloqueo D-3b-4. `COV-D` rc 0 (líneas 100,0 %, ramas 100,0 %); `COV-A` rc 0 (líneas 100,0 %, ramas 100,0 %; mínimo 80 %). `VULN` rc 0: ningún proyecto con paquetes vulnerables (V10). Búsqueda de `FromSqlRaw|ExecuteSqlRaw|EnableSensitiveDataLogging|NotImplementedException` en `src`: sin coincidencias. Contrato sincronizado (19.3: SHA-256 estable en tres builds). Sin contenedores de Testcontainers ni de compose residuales; imágenes `timetracker-api` y `timetracker-api:local` eliminadas.
  - Evidencia final (2026-10-14): `RESTORE` rc 0; `FORMAT` (`dotnet format TimeTracker.sln --verify-no-changes`) rc 0; `BUILD` (`-c Release -warnaserror`) rc 0 con 0 avisos y 0 errores; `TEST` (`dotnet test TimeTracker.sln -c Release --no-build --collect:"XPlat Code Coverage"`) rc 0 con entorno limpio (sin variables `Jwt__`, `Seed__`, `ConnectionStrings__`): **393/393 verdes** = Domain 49, Application 43, Arquitectura 71, Integración 230 (PostgreSQL 18 real con Testcontainers); `COV-D` rc 0 (líneas 100,0 %, ramas 100,0 %); `COV-A` rc 0 (100,0 % y 100,0 %; mínimo 80 %); `VULN` (`dotnet list TimeTracker.sln package --vulnerable --include-transitive`) rc 0: los 9 proyectos sin paquetes vulnerables. Puerta repetida tras aplicar `.env.example` (cierra 20.1).
- [x] 21.2 **Revisión de amenazas, líneas y PARADA.**
  - Repasar la matriz de amenazas del mapa final; revisar los `LoggerMessage` (sin correo, contraseña, hash, token ni IP); líneas reales acumuladas frente a 1.750–2.270 (tandas 1 a 3b). **PARADA:** el usuario decide sobre las tandas 4 y 5 y sobre el recorte (disparador: acumulado > 3.150).
  - Hecho cuando: checklist de la Tanda 3b completa.
  - Evidencia previa (parcial; la tarea sigue abierta por 20.1): **Matriz de amenazas** (BOLA, token forjado, enumeración, secretos, fuerza bruta): BOLA de equipo: el control por objeto vive en `FindDirectReportAsync` (miembro ajeno, inexistente, sin responsable y el propio responsable dan el mismo 404, comparado campo a campo salvo `traceId`) y el rol se exige antes que el miembro (403 al empleado exista o no); ni un empleado ni un responsable ven fichajes ajenos desde `GET /api/time-entries`, con `memberId` o con `employeeId` (mutación del parámetro: roja). Token forjado: igual que 3a más el token alterado a `Manager` (401). Enumeración: 404 idéntico y 429 que no distingue correos. Fuerza bruta: 10 por minuto y por IP (probado; en vivo 401 ×8 y 429 ×4). Secretos: la clave y la contraseña de demostración no llegan a los registros (`Seeding_DoesNotLogThePassword` y el registro del contenedor), ni al compose (obligatorias, sin literal) ni a la imagen. Mínima exposición: el equipo devuelve solo `id` y `displayName`. **`LoggerMessage` revisados** (4 en `src/TimeTracker.Api`: migración, regla de negocio con solo el tipo, login rechazado y siembra con recuentos): sin correo, contraseña, hash, token ni IP. **Líneas a mano de la Tanda 3b** (`wc -l` de nuevos y `diff` de modificados contra `tanda-3b`, con blancos y documentación XML como en 3a; excluido `openapi.json` generado): `src` 390 (nuevos 349 en `Api/{Team,Seeding,Security}` y `OpenApiSecurityTransformers.cs`; modificados 41), pruebas 1.460 (nuevas 1.207; modificadas 253), compose 9; **total ≈ 1.859** (más ≈ 25 de `.env.example` pendiente) frente a 600–750 de la previsión (≈ 2,5–3,1×). **Acumulado (Tandas 1, 2, 3a y 3b): ≈ 3.314 + 1.859 = 5.173** (≈ 5.200 con `.env.example`) frente a la referencia de 2.400–3.150: el disparador de esta tarea (> 3.150) ya estaba superado desde 3a; el usuario aceptó `size:exception` y mantener R1–R5, así que no se recorta nada y solo se informa de la cifra (previsión restante de las Tandas 4 y 5: 650–880). **Desviaciones de la tanda:** D-3b-1 además de las 14 copias previas se copiaron, antes de editarlos, `AuthorizationCoverageTests`, `FallbackPolicyTests`, `TokenValidationTests`, `TimeEntryIsolationTests` (no se modificó; los casos de 15.8 viven en `TeamEndpointsTests`) y `SigningKeyLeakTests` (se extrajo `CapturingLoggerProvider` a `IT/Support` para reutilizarlo en 16.2); D-3b-2 (17.1) el cuerpo del 429 lo escribe `OnRejected` con `IProblemDetailsService` porque el `detail` es propio del limitador (respaldo de V9); D-3b-3 `AddBearerSecurity` es un método de extensión sobre `OpenApiOptions` (registrarlo con `services.Configure<OpenApiOptions>` no alcanza al documento `v1`); D-3b-4 **`.env.example` no se pudo editar**: la configuración de permisos deniega leerlo y escribirlo (también `cat`/`grep` sobre él), así que 20.1 queda con el RED hecho y el GREEN pendiente; el contenido propuesto está en el scratchpad de la sesión como `env.example.proposed`, validado con un script que replica las comprobaciones, y la tarea se cierra al aplicarlo y ejecutar `T-AR --filter DeliveryArtifactsTests`; D-3b-5 la ruta del miembro es `{memberId:guid}` (un identificador que no es GUID no casa con la ruta y da el mismo 404; sin token, 401); D-3b-6 las pruebas de `.env.example` se llaman `EnvExample_ContainsTheDocumentedKeys` y `EnvExample_HasNoDevelopmentEmployeeKey` (el nombre con «ProvisionalIdentity» chocaba con `Code_HasNoProvisionalIdentityLeftovers`); D-3b-7 dos mutaciones se sustituyeron por equivalentes expresables (17.2 «contar solo los fallidos» → quitar `RequireRateLimiting`; 16.4 bandera → condición invertida); D-3b-8 sin `Jwt__SigningKey` el contenedor termina con código 139 (≠ 0) nombrando `Jwt:SigningKey`. **Validaciones de la tanda:** V5, V7, V8, V9, V11, V12, V16, V17 y V21 resueltas (14.1, 15.2/17.3, 17.1, 19.2, 16.3, 19.3, 20.3 y 20.4). **Hallazgo para el README (Tanda 5):** el comando de generación de `Jwt__SigningKey`, `[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))`, falla en Windows PowerShell 5.1 (`GetBytes` estático no existe) y solo funciona en PowerShell 7: documentarlo con `pwsh -NoProfile -Command "…"` (así figura en el `.env.example` propuesto). **PARADA:** el usuario decide sobre las Tandas 4 y 5; antes, aplicar `.env.example` (20.1) y repetir `TEST`.
  - Evidencia final (2026-10-14): resuelto el bloqueo de 20.1 (`.env.example` aplicado) y repetido `TEST`: 393/393 verdes (ver 21.1). La matriz de amenazas, los `LoggerMessage` y las líneas (≈ 1.859 en 3b; acumulado ≈ 5.173, `size:exception` aceptado) de la evidencia previa no cambian. PARADA superada: el usuario decidió sobre las tandas 4 y 5 (ya hechas).

---

# TANDA 4: Web (sesión, `/login`, `/equipo`, rutas protegidas)

Sin bUnit ni E2E (decisión de las specs): el RED de cada tarea es de **compilación**, de **estructura** (prueba de arquitectura sobre el texto) o **manual** (24.x), y el GREEN se comprueba con `BUILD`, `FORMAT` y la verificación del navegador. La Web no contiene reglas de negocio ni URL fijas: ningún archivo de `WEB/` escribe una URL absoluta (ni siquiera de ejemplo).

## Fase 22: Cimientos de la Web

- [x] 22.1 **Copia previa y línea base.** Archivos: `..\TimeTracker.rollback\tanda-4\` (10 existentes). Cubre: precondición del plan de reversión; V2.
  - `Save-Rollback 4 …`; `ENV-CHECK`; `dotnet build WEB -c Release -warnaserror` (V2: `Components.Authorization` 10.0.10 convive con `Components.WebAssembly` 10.0.10; respaldo 10.0.12 en ambos), `BUILD` y `T-AR`.
  - Hecho cuando: `(Get-ChildItem -Recurse -File ..\TimeTracker.rollback\tanda-4).Count` = 10 y la línea base está en verde.
  - Evidencia: Copia previa en `..\TimeTracker.rollback\tanda-4\` con **10** archivos (comprobado con `find`), conservando rutas (el plan lista 10: `Program.cs`, `App.razor`, `_Imports.razor`, `TimeTracker.Web.csproj`, `NavMenu.razor`, `TimeEntries.razor`, los tres de `TimeEntries/` y `RepositoryConventionsTests.cs`). `ENV-CHECK` vacío. Línea base: `BUILD` rc 0 con 0 avisos y 0 errores (V2: `Components.Authorization` 10.0.10 convive con `Components.WebAssembly` 10.0.10, sin necesidad del respaldo 10.0.12). Línea base de arquitectura: solo las 5 pruebas de `.env.example`. Desviación D-4-1: `Layout/NavMenu.razor.css` no figura entre las 10 copias del plan y también se modifica; no se copió antes de tocarlo (ver D-4-1).
- [x] 22.2 **RED de estructura de la Web.** Archivos: `AR/RepositoryConventionsTests.cs`. Cubre: SA-19, WS-8 (parte), WS-39, UI-11; amenaza A03 (XSS).
  - Ampliar `Web_HasTimeEntriesPageClientAndApiBaseUrl` a `Web_HasLoginFichajesAndTeamPagesWithClientsAndTokenHandler` (páginas `/login`, `/fichajes` con `[Authorize]` y `/equipo` con `Roles = "Manager"`; `TimeEntriesApiClient`, `TeamApiClient`, `AuthApiClient`, `BearerTokenHandler`; `ApiBaseUrl` en `wwwroot/appsettings.json`), `Web_HasNoRawHtmlLocalStorageOrTokenLogging` (sin `MarkupString`, `innerHTML`, `localStorage`, `document.cookie`, ni `Console.Write*`/`Log*` con la palabra `token`) y `Web_HasNoTimeEntriesApiException` (la excepción se renombra en 22.3). Se conservan `Web_DoesNotHardcodeApiUrl` y `Web_HasNoProjectReferences`.
  - RED: la primera y la tercera fallan. La segunda es verde a la primera; mutación: un `MarkupString` temporal en una página → falla; revertir.
  - Hecho cuando: `T-AR --filter "FullyQualifiedName~RepositoryConventionsTests"` falla solo por la primera y la tercera, y la mutación queda anotada.
  - Evidencia: RED: `dotnet test tests/TimeTracker.ArchitectureTests --filter "FullyQualifiedName~RepositoryConventionsTests"` falla `Web_HasLoginFichajesAndTeamPagesWithClientsAndTokenHandler` y `Web_HasNoTimeEntriesApiException` (más la conocida de `.env.example`); `Web_HasNoRawHtmlLocalStorageOrTokenLogging` **verde a la primera** (no había ninguno de esos usos). Mutación: `MarkupString` temporal en `Home.razor` → ROJO («Uso prohibido en la Web: …Home.razor»); revertida y verde. Segunda mutación tras ajustar la expresión: `Console.WriteLine(token)` temporal en `TokenStore.cs` → ROJO; revertida. Ajuste durante el GREEN: la primera expresión regular de «registro del token» (`Log\w*`) daba un falso positivo con `LoginAsync(`; se afinó a `Console.Write*` y `.Log(Trace|Debug|Information|Warning|Error|Critical)`. Se conservan `Web_DoesNotHardcodeApiUrl` y `Web_HasNoProjectReferences` (esta usa ahora un ayudante común `WebSourceFiles`).
- [x] 22.3 **`Http/ApiRequest` y `ApiRequestException` (refactor con regresión manual).** Archivos: `WEB/Http/{ApiRequest,ApiRequestException}.cs` (nuevos; `ApiProblemDto` se mueve aquí), `WEB/TimeEntries/TimeEntriesApiClient.cs`; **eliminar** `WEB/TimeEntries/TimeEntriesApiException.cs`. Cubre: UI-5, UI-6, UI-7 (regresión); DD-27.
  - Extraer la lógica común (envío, lectura del `ProblemDetails`, mensaje genérico, estado HTTP en la excepción) sin cambiar el comportamiento. `ApiRequestException` expone el estado para que el handler y las páginas distingan 401, 403 y 404.
  - Hecho cuando: `BUILD` rc 0 y la tercera prueba de 22.2 pasa; el comportamiento de los mensajes 409 y de «API caída» se comprueba a mano en 24.2 y 24.5.
  - Evidencia: Nuevos `Http/ApiRequest.cs` (envío común, lectura del `ProblemDetails` solo en `409`, mensaje genérico, `GenericErrorMessage`), `Http/ApiRequestException.cs` (con `StatusCode`) y `Http/ApiProblemDto.cs` (movido); `TimeEntriesApiClient` pasa de 57 a 18 líneas y delega en `ApiRequest`; eliminado `TimeEntries/TimeEntriesApiException.cs`. `Web_HasNoTimeEntriesApiException` en verde. Comportamiento de mensajes 409 y «API caída» comprobado en 24.2 y 24.5 (sin cambios).
- [x] 22.4 **`TokenStore`, `JwtAuthenticationStateProvider` y `ReturnUrl` (V19).** Archivos: `WEB/Auth/{TokenStore,JwtAuthenticationStateProvider,ReturnUrl}.cs`, `WEB/Program.cs`. Cubre: WS-3, WS-8, WS-9, WS-10, WS-12, WS-13, WS-14, WS-23 (lógica); V19, V20.
  - RED (compilación): `Program.cs` registra los tipos antes de existir. GREEN: `TokenStore` (scoped; copia en memoria y `sessionStorage` vía `IJSRuntime.InvokeAsync("sessionStorage.getItem" | "setItem" | "removeItem")` con la clave `timetracker.accessToken`, sin archivo JS propio; respaldo V19: `wwwroot/js/session.js`); `JwtAuthenticationStateProvider` (payload en base64url con relleno, `exp` contra `TimeProvider`, token ausente, mal formado o caducado → se borra y queda anónimo sin errores técnicos; `ClaimsIdentity(claims, "jwt", "name", "role")`; `SignInAsync(token)` y `SignOutAsync()` notifican el cambio); `ReturnUrl.IsSafe` (empieza por `/` y no por `//` ni `/\`; nunca una URL absoluta). El token no se escribe en consola ni en la interfaz.
  - Casos de `IsSafe` para 24.4: seguros `/fichajes`, `/equipo`; inseguros `//ejemplo.test`, `/\ejemplo.test`, `https://ejemplo.test`, `javascript:alert(1)`, vacío, `fichajes`.
  - Hecho cuando: `BUILD` rc 0.
  - Evidencia: RED de compilación: `Program.cs`/páginas referenciaban `TokenStore`, `JwtAuthenticationStateProvider` y `ReturnUrl` antes de existir. GREEN: `TokenStore` (memoria + `sessionStorage` con `IJSRuntime.InvokeAsync("sessionStorage.getItem" | "setItem" | "removeItem")`, clave `timetracker.accessToken`; **V19 resuelta sin archivo JS propio**, probado en navegador: guardar, leer, recargar y borrar), `JwtAuthenticationStateProvider` (base64url con relleno, `exp` contra `TimeProvider`, ausente/mal formado/caducado → se borra y queda anónimo sin errores; `ClaimsIdentity(claims, "jwt", "name", "role")`; roles en arreglo soportados; `SignInAsync`/`SignOutAsync` notifican), `ReturnUrl.IsSafe` (empieza por `/`, no por `//` ni `/` + barra invertida, sin caracteres de control). Comprobado a mano en 24.4 (WS-2, WS-3, WS-13, WS-14, WS-23). Desviación menor: el literal de la barra invertida se expresa como `(char)92` en una constante para evitar problemas de escape. `BUILD` rc 0.
- [x] 22.5 **`BearerTokenHandler`, `AuthApiClient` y cableado (V20).** Archivos: `WEB/Auth/{BearerTokenHandler,AuthApiClient,LoginRequestDto,LoginResponseDto}.cs`, `WEB/Program.cs`, `WEB/TimeEntries/TimeEntriesApiClient.cs`. Cubre: WS-20, WS-21, WS-22, WS-24, UI-8, UI-12; DD-27; V20.
  - `BearerTokenHandler : DelegatingHandler` añade `Authorization: Bearer` solo a las peticiones bajo `ApiBaseUrl`; ante un `401` con sesión llama a `SignOutAsync()` y a `NavigateTo("login?returnUrl=…")` con la ruta actual codificada; un `403` no cierra la sesión. Cada cliente con handler crea su `HttpClient` con `InnerHandler = new HttpClientHandler()` (CA2000, V20); `AuthApiClient` se registra **sin** handler (el `401` del login no redirige). `Program.cs`: `AddAuthorizationCore()`, `AddCascadingAuthenticationState()`, `TokenStore`, `JwtAuthenticationStateProvider` (también como `AuthenticationStateProvider`) y `ApiBaseUrl` como hoy.
  - Hecho cuando: `BUILD` rc 0 con 0 avisos y `Web_DoesNotHardcodeApiUrl` en verde.
  - Evidencia: `BearerTokenHandler` (constructor con `apiBaseUri`, `TokenStore`, proveedor y `NavigationManager`): añade `Authorization: Bearer` solo si esquema, host y puerto coinciden con `ApiBaseUrl`; ante `401` con token → `SignOutAsync()` y `NavigateTo("login?returnUrl=" + ruta actual codificada)`; el `403` no cierra la sesión. Cada cliente con handler crea su `HttpClient` con `InnerHandler = new HttpClientHandler()` en `CreateAuthorizedClient` de `Program.cs` (CA2000 sin avisos, V20); `AuthApiClient` sin handler. `Program.cs`: `AddSingleton(TimeProvider.System)`, `AddAuthorizationCore()`, `AddCascadingAuthenticationState()`, `TokenStore`, `JwtAuthenticationStateProvider` y `AuthenticationStateProvider` apuntando a él, `ApiBaseUrl` como antes. `LoginRequestDto`/`LoginResponseDto` duplicados a propósito (la Web no referencia `src`). `BUILD` rc 0 con 0 avisos; `Web_DoesNotHardcodeApiUrl` en verde; WS-20, WS-21, WS-22, WS-24 y UI-8 comprobados en 24.2 y 24.4.

## Fase 23: Páginas y menú

- [x] 23.1 **Rutas protegidas y menú.** Archivos: `WEB/App.razor`, `WEB/_Imports.razor`, `WEB/Layout/NavMenu.razor`, `WEB/Shared/RedirectToLogin.razor`. Cubre: WS-16, WS-17, WS-25, WS-26, WS-27, WS-34, WS-35, UI-3.
  - `AuthorizeRouteView` con `NotAuthorized`: sin sesión → `RedirectToLogin` (`login?returnUrl=…`); con sesión pero sin el rol → «No tiene permiso para ver esta página.». `NavMenu` con `AuthorizeView`: sin sesión, enlace «Iniciar sesión»; con sesión, «Fichajes», nombre del usuario con su rol en español («Empleado», «Responsable») y «Cerrar sesión» (borra el token de `sessionStorage` y de memoria, actualiza el estado y navega a `/login`); «Equipo» solo con `Roles="Manager"`. Los enlaces de la plantilla (inicio, contador, tiempo) no cambian.
  - RED: compilación (`_Imports` y `App.razor` referencian `RedirectToLogin`). Hecho cuando: `BUILD` rc 0; la comprobación visual es 24.2.
  - Evidencia: `App.razor` con `AuthorizeRouteView` (`NotAuthorized`: sin sesión → `RedirectToLogin`, con sesión → «No tiene permiso para ver esta página.»; `Authorizing`: «Comprobando la sesión...»), `_Imports.razor` (+ `Authorization`, `Components.Authorization`, `TimeTracker.Web.Auth`, `TimeTracker.Web.Shared`), `Shared/RedirectToLogin.razor` y `NavMenu.razor` con `AuthorizeView` («Iniciar sesión» sin sesión; «Fichajes», «Equipo» solo con `Roles="Manager"`, «Nombre (Empleado|Responsable)» y botón «Cerrar sesión» con sesión). `NavMenu.razor.css` gana dos reglas para el botón y la línea de sesión. `BUILD` rc 0; comportamiento comprobado en 24.2 y 24.3 (aspecto visual **no verificado**).
- [x] 23.2 **`/login`.** Archivos: `WEB/Pages/Login.razor`. Cubre: WS-1, WS-2, WS-3, WS-4, WS-5, WS-6, WS-7, WS-36.
  - `EditForm` con correo y contraseña (`type="password"`); campos vacíos → aviso en español sin enviar la petición; `AuthApiClient`; éxito → `SignInAsync` y navegación a `returnUrl` si `ReturnUrl.IsSafe`, si no a `/fichajes`; `401` → «Correo o contraseña incorrectos.» (el mismo mensaje para correo inexistente y contraseña errónea) y contraseña vaciada; `429` → «Demasiados intentos. Espere un minuto e inténtelo de nuevo.»; red u otro estado → mensaje genérico sin datos técnicos; botón deshabilitado mientras se envía.
  - RED: manual (24.2 y 24.4). Hecho cuando: `BUILD` rc 0 y `Web_HasNoRawHtmlLocalStorageOrTokenLogging` en verde.
  - Evidencia: `Pages/Login.razor` (`EditForm`, correo y contraseña `type="password"`, `returnUrl` por `SupplyParameterFromQuery`): campos vacíos → aviso sin petición; éxito → `SignInAsync` y `ReturnUrl.IsSafe` ? destino : `/fichajes`; `401` → «Correo o contraseña incorrectos.» y contraseña vaciada; `429` → «Demasiados intentos. Espere un minuto e inténtelo de nuevo.»; otro → mensaje genérico; botón deshabilitado y reentrada bloqueada mientras `busy`. Comprobado en 24.2, 24.4 y 24.5. `Web_HasNoRawHtmlLocalStorageOrTokenLogging` en verde.
- [x] 23.3 **`/fichajes` protegida y `TimeEntriesTable`.** Archivos: `WEB/Pages/TimeEntries.razor`, `WEB/Shared/TimeEntriesTable.razor`. Cubre: UI-1, UI-2, UI-4, UI-8.
  - `[Authorize]`; extraer la tabla (horas `yyyy-MM-dd HH:mm UTC`, «en curso», minutos de la API, total en minutos y h:mm, mensaje «sin fichajes») a un componente presentacional reutilizado por `/equipo`. Un `401` ya no se muestra como error de la pantalla (lo gestiona el handler).
  - Hecho cuando: `BUILD` rc 0; comportamiento comprobado en 24.2.
  - Evidencia: `/fichajes` con `@attribute [Authorize]`; nueva `Shared/TimeEntriesTable.razor` (formato `yyyy-MM-dd HH:mm UTC`, «en curso», minutos, total en minutos y h:mm, «No hay fichajes registrados.») usada por `/fichajes` y `/equipo`; un `401` ya no se muestra como error de la pantalla (lo gestiona el handler). `TimeEntries.razor` pasa de 109 a 82 líneas. Comprobado en 24.2 y 24.4.
- [x] 23.4 **`/equipo`.** Archivos: `WEB/Team/{TeamApiClient,TeamMemberDto}.cs`, `WEB/Pages/Team.razor`, `WEB/Program.cs`. Cubre: WS-17, WS-18, WS-19, WS-28, WS-29, WS-31, WS-32, WS-36.
  - `[Authorize(Roles = "Manager")]`; `TeamApiClient` con `BearerTokenHandler`; lista los miembros (nombre visible) y, al elegir uno, muestra sus fichajes con `TimeEntriesTable` en solo lectura (sin botones de fichar ni editar); equipo vacío → «No tiene personas a su cargo.»; `404`/`403`/otros → mensaje en español sin romper la pantalla. Un empleado ve el mensaje de falta de permiso y no se llama a `/api/team`.
  - Hecho cuando: `BUILD` rc 0; comportamiento comprobado en 24.3.
  - Evidencia: `Team/TeamApiClient.cs` (`GetMembersAsync`, `GetTimeEntriesAsync`, con `BearerTokenHandler`), `Team/TeamMemberDto.cs` (+ `TeamMemberListDto`) y `Pages/Team.razor` con `@attribute [Authorize(Roles = "Manager")]`: lista de miembros, fichajes del elegido con `TimeEntriesTable` sin botones, «No tiene personas a su cargo.», `404`/`403`/otros con mensaje en español. Un empleado no llega a renderizar la página (0 peticiones a `/api/team`, WS-18). Comprobado en 24.2 y 24.3.
- [x] 23.5 **Pulido de `/equipo` [R4].** Archivos: `WEB/Pages/Team.razor`. Cubre: WS-30, WS-33.
  - Selector (`<select>`) con «Seleccione una persona», cambio de miembro sin recargar, estados de carga y mensajes diferenciados: `404` «Esa persona ya no forma parte de su equipo.» y `403` «No tiene permiso para ver este equipo.». Si se recorta: lista de botones y mensaje único (WS-30/WS-33 quedan con verificación parcial).
  - Hecho cuando: `BUILD` rc 0; comprobado en 24.3.
  - Evidencia: Hecho (no se recorta R4): `<select>` con «Seleccione una persona», cambio de miembro sin recargar, indicador «Cargando fichajes...» y descarte de respuestas antiguas, y mensajes diferenciados `404` «Esa persona ya no forma parte de su equipo.» y `403` «No tiene permiso para ver este equipo.». Comprobado en 24.3 (WS-30, WS-33); el `403` en `/equipo` solo se alcanzaría con un token manipulado, probado en la API en 24.4 (WS-24), no en esta pantalla.
- [x] 23.6 **Compilación, formato y arquitectura de la Web.** Cubre: WS-38, WS-39, UI-11, SA-19; V20.
  - `dotnet build WEB -c Release -warnaserror`, `FORMAT`, `T-AR`; `Select-String 'MarkupString|innerHTML|localStorage' -Path WEB -Recurse` (excluye `bin/` y `obj/`) sin coincidencias.
  - Hecho cuando: todo en 0 y sin coincidencias.
  - Evidencia: `dotnet build src/TimeTracker.Web -c Release -warnaserror` y `BUILD` de la solución: rc 0, 0 avisos, 0 errores. `FORMAT`: el primer `--verify-no-changes` falló (WHITESPACE: faltaba una línea en blanco entre `using System*` y el resto en 3 archivos nuevos de `Auth/`); se aplicó `dotnet format TimeTracker.sln --no-restore` y la verificación quedó en rc 0. `T-AR` solo con las 5 conocidas de `.env.example`. Búsqueda de `MarkupString|innerHTML|localStorage` en `src/TimeTracker.Web` (`.cs`, `.razor`, `.html`, sin `bin/` ni `obj/`): 0 coincidencias; `localhost:5274` en `.cs`/`.razor`: 0.

## Fase 24: Verificación manual de la Web (E2E)

Se registra en la sección «Verificación manual de la Web» de `verify-report.md` (informe preliminar), con valores ficticios y una línea por escenario: resultado, fecha y observación. Navegador real (Chrome o Edge con DevTools, o Chrome headless por protocolo DevTools como en el cambio anterior). Lo que no se pudo verificar (aspecto visual, estado intermedio de botones) se declara.

- [x] 24.1 **Entorno.** Cubre: UI-10; precondición de WS-40 y WS-41.
  - API con `docker compose up -d --wait` (variables de sesión de 20.4, incluida `Cors__AllowedOrigins__0=http://localhost:5172`) en `http://localhost:5274`. Web con `dotnet run --project src/TimeTracker.Web --no-launch-profile --urls http://localhost:5172`. `Select-String ApiBaseUrl src\TimeTracker.Web\wwwroot\appsettings.json` muestra `http://localhost:5274` (UI-10). Credenciales de la demostración: los cuatro correos `@example.test` con `Demo-Fichajes-2026`.
  - Hecho cuando: `/login` carga y la pestaña Network muestra las peticiones a `http://localhost:5274`.
  - Evidencia: Ver `verify-report.md`, «Entorno (24.1)»: `docker compose … -p ttsmoke4 up -d --build --wait` rc 0 (variables ficticias en archivo de sesión fuera del repositorio, sin leer `.env`; override con `Cors__AllowedOrigins__0=http://localhost:5172`), API en `127.0.0.1:5274`, Web con `dotnet run --no-launch-profile --urls http://localhost:5172`; `ApiBaseUrl` = `http://localhost:5274`; en la pestaña de red las peticiones van a `http://localhost:5274`. Navegador: Chrome headless por protocolo DevTools (respaldo; las herramientas MCP no estaban disponibles).
- [x] 24.2 **Flujo del empleado (Luis).** Cubre: WS-1, WS-4, WS-7, WS-8, WS-9, WS-10, WS-11, WS-16, WS-17, WS-18, WS-20, WS-21, WS-25, WS-26, WS-34, WS-35, WS-36, WS-40, UI-1, UI-2, UI-3, UI-4, UI-5, UI-6, UI-12, UI-13.
  - Sin sesión: `/fichajes` y `/equipo` llevan a `/login` y no hay peticiones a `/api/time-entries` ni `/api/team` (WS-16, WS-17, UI-3); el menú solo ofrece «Iniciar sesión» (WS-25). Campos vacíos: aviso y 0 peticiones (WS-7). Contraseña incorrecta y correo inexistente: mismo mensaje, sigue en `/login`, contraseña vacía (WS-4); `POST /api/auth/login` sin `Authorization` (WS-21).
  - Login correcto: navega a `/fichajes`; menú con «Fichajes», nombre y «Cerrar sesión», sin «Equipo» (WS-1, WS-26). `sessionStorage['timetracker.accessToken']` existe y no hay token en `localStorage` ni cookies (WS-8); el token no está en la URL, el texto ni la consola (WS-11).
  - Fichar entrada → fila «en curso»; segunda entrada → mensaje 409 en español y listado sin cambios (UI-5); fichar salida; salida sin entrada → mensaje (UI-6); total coherente con la API (UI-1). Ana sin fichajes → «no hay fichajes» y total 0 (UI-2). Sesiones de Luis y Ana en pestañas distintas: cada tabla solo con lo propio (UI-4).
  - Network: `GET`/`POST /api/time-entries` con `Authorization: Bearer`, ninguna petición a otro origen con esa cabecera (WS-20), sin `employeeId` en ruta, consulta, cabecera ni cuerpo (UI-12) y sin errores de CORS en los POST (UI-13). F5 conserva la sesión (WS-9); borrar la clave de `sessionStorage` y pulsar «Fichar entrada» sin recargar sigue enviando el token (WS-10).
  - `/equipo` como Luis: mensaje de falta de permiso y 0 peticiones a `/api/team` (WS-18). «Cerrar sesión»: `sessionStorage` sin token, `/login`, menú sin sesión (WS-34); atrás o abrir `/fichajes`: `/login` y ninguna petición con `Authorization` (WS-35). Todos los textos del recorrido en español (WS-36).
  - Hecho cuando: cada caso queda registrado y WS-40 se declara cumplido.
  - Evidencia: Ver `verify-report.md`, «Flujo del empleado, Luis (24.2)»: WS-1, WS-4, WS-7, WS-8, WS-9, WS-10, WS-11, WS-16, WS-17, WS-18, WS-20, WS-21, WS-25, WS-26, WS-34, WS-35, WS-36, WS-40, UI-1 a UI-6, UI-12 y UI-13 **verificados** (una primera pasada se repitió tras corregir el guion; solo cuenta la segunda). No verificado: aspecto visual.
- [x] 24.3 **Flujo del responsable (Marta).** Cubre: WS-12, WS-19, WS-27, WS-28, WS-29, WS-30, WS-31, WS-32, WS-33, WS-41, TA-2 (UI).
  - Login como Marta: menú con «Fichajes» y «Equipo» (WS-27); el estado muestra su nombre y rol `Manager` (WS-12). Ficha entrada y salida. `/equipo`: lista `Ana Ejemplo` y `Luis Ejemplo`, sin Pablo (WS-19, WS-28).
  - Preparar los fichajes de Luis por SQL, sin tener a Luis con otra entrada abierta (ajustar los nombres de columna con `\d time_entries`): `docker compose exec -T postgres psql -U $env:POSTGRES_USER -d $env:POSTGRES_DB -c "INSERT INTO time_entries (id, employee_id, clock_in_at, clock_out_at) SELECT gen_random_uuid(), u.id, v.i::timestamptz, v.o::timestamptz FROM users u, (VALUES ('2026-10-12 08:00:00+00','2026-10-12 09:00:00+00'),('2026-10-12 10:00:00+00','2026-10-12 10:30:00+00'),('2026-10-13 08:00:00+00',NULL)) v(i,o) WHERE u.email='luis.empleado@example.test'"`. Elegir a Luis: tres filas con `yyyy-MM-dd HH:mm UTC`, la abierta «en curso» y total 90 igual al de la API (WS-29). Elegir a Ana: solo sus fichajes (WS-30). Sin botones de fichar (WS-31).
  - Equipo vacío (WS-32): `UPDATE users SET manager_id = NULL WHERE email IN ('luis.empleado@example.test','ana.empleada@example.test')` y recargar `/equipo`: mensaje de que no tiene personas a su cargo. Miembro que ya no es del equipo (WS-33): restaurar los vínculos, cargar la lista, elegir a Luis, ejecutar de nuevo el `UPDATE` solo para Luis y volver a elegirlo: mensaje en español (`404`) y pantalla operativa. Restaurar: `UPDATE users SET manager_id = (SELECT id FROM users WHERE email='marta.responsable@example.test') WHERE email IN ('luis.empleado@example.test','ana.empleada@example.test')`.
  - Hecho cuando: cada caso queda registrado y WS-41 se declara cumplido (Marta no ve a Pablo).
  - Evidencia: Ver `verify-report.md`, «Flujo del responsable, Marta (24.3)»: WS-12, WS-19, WS-27 a WS-33, WS-41 y TA-2 (UI) **verificados**; el total de Luis (90) coincide con la API; Marta no ve a Pablo. La preparación por SQL usó `docker compose -p ttsmoke4 exec -T postgres psql` (los fichajes de Luis se borraron antes para que el total fuese 90). Vínculos restaurados (2 miembros). Aspecto visual no verificado.
- [x] 24.4 **Sesión, 401 y expiración (editando `sessionStorage`).** Cubre: WS-2, WS-3, WS-13, WS-14, WS-15, WS-22, WS-23, WS-24, UI-8.
  - `returnUrl`: `/login?returnUrl=/equipo` con Marta → `/equipo` (WS-2); `/login?returnUrl=//ejemplo.test` y `/login?returnUrl=https://ejemplo.test` → `/fichajes` sin salir del origen (WS-3).
  - Expiración local (WS-13, WS-23): con sesión, en la consola del navegador reescribir el `exp` del payload del token a un instante pasado (decodificar el segundo segmento, cambiar `exp`, recodificar en base64url y guardar) y navegar a `/fichajes`: el token se elimina y se llega a `/login?returnUrl=%2Ffichajes`. Token mal formado (WS-14): guardar `texto-cualquiera` y recargar: no autenticado, almacenamiento limpio, sin error técnico.
  - Firma alterada (WS-15, WS-22, UI-8): cambiar solo la firma del token; `/fichajes` abre como autenticado, la API responde `401` y se llega a `/login` con `returnUrl`; repetirlo pulsando «Fichar entrada» (sesión eliminada, sin mensaje técnico en `/fichajes`).
  - `403` sin cerrar sesión (WS-24): con la sesión de Luis, `fetch` de la API a `/api/team/members` con el token de `sessionStorage` desde la consola → `403`; la sesión y el menú siguen igual.
  - Hecho cuando: cada caso queda registrado con su resultado.
  - Evidencia: Ver `verify-report.md`, «Sesión, 401 y expiración (24.4)»: WS-2, WS-3 (5 variantes), WS-13, WS-14 (3 variantes), WS-15, WS-22, WS-23, WS-24 y UI-8 **verificados**. UI-8 con el botón necesitó interceptar con DevTools (`Fetch.fulfillRequest`) la primera lectura del listado para poder pulsar «Fichar entrada» con la firma alterada; se declara así.
- [x] 24.5 **Errores y límites.** Cubre: WS-5, WS-6, WS-36, WS-37, UI-7, UI-9.
  - 11 logins fallidos en un minuto: mensaje de espera en español y pantalla operativa (WS-5; esperar un minuto después). `docker compose stop api`: login y botones de `/fichajes` muestran un mensaje genérico en español sin trazas (WS-6, UI-7); `docker compose start api`. `docker compose stop postgres` con sesión abierta: `/fichajes` y `/equipo` reciben `500` y muestran un mensaje genérico (WS-37); `docker compose start postgres`.
  - UI-9: copia de `wwwroot/appsettings.json`, poner `ApiBaseUrl` ausente y luego `no-es-una-url`: la Web falla al arrancar con un mensaje en español que nombra `ApiBaseUrl`; restaurar el archivo. Al terminar: `docker compose down -v` y detener la Web.
  - Hecho cuando: cada caso queda registrado y lo no verificado figura como tal.
  - Evidencia: Ver `verify-report.md`, «Errores y límites (24.5)»: WS-5 (undécimo intento → 429 en español; tras 62 s el login vuelve a funcionar), WS-6/UI-7 (API detenida), WS-37 (PostgreSQL detenido → `500` y mensaje genérico en `/fichajes` y `/equipo`) y UI-9 (`ApiBaseUrl` ausente o `no-es-una-url`; hay que cambiar `appsettings.json` **y** `appsettings.Development.json`) **verificados**. Al terminar: `docker compose -p ttsmoke4 down -v` (volumen y red eliminados), imagen `timetracker-api:local` eliminada, Web y procesos de Chrome detenidos, `appsettings` restaurados y comparados. Lo no verificado figura en el informe (aspecto visual, estado intermedio de botones, solo Chrome headless).

## Fase 25: Punto de control de la Tanda 4

- [x] 25.1 **Puerta de calidad.** Cubre: SA-16, SA-18, WS-38.
  - `RESTORE`, `FORMAT`, `BUILD`, `TEST`, `COV-D`, `COV-A`, `VULN`.
  - Hecho cuando: todo en 0 y la suite completa en verde.
  - Evidencia: `RESTORE` rc 0; `FORMAT` rc 0; `BUILD` rc 0 con 0 avisos y 0 errores; `TEST` sobre `TestResults` limpio: **388** pruebas = Domain 49 (49 verdes), Application 43 (43), Arquitectura 66 (61 verdes y **5 rojas**, solo las de `.env.example` de 20.1/D-3b-4: `EnvExample_ContainsTheDocumentedKeys`, `…AuthenticationKeysAreEmptyOrFalseAndDocumented`, `…HasNoDevelopmentEmployeeKey`, `…ListsEveryKeyConsumedByCompose` y `DeliveryFiles_HaveNoProvisionalIdentity(.env.example)`) e Integración 230 (230 verdes). La Tanda 4 añade 2 pruebas de arquitectura (3 sustituyen a 1). `COV-D` rc 0 (líneas 100,0 %, ramas 100,0 %); `COV-A` rc 0 (100,0 % y 100,0 %; mínimo 80 %). `VULN` rc 0: ningún proyecto con paquetes vulnerables. La Web no entra en la cobertura (sin pruebas unitarias por decisión de las especificaciones).
- [x] 25.2 **Revisión, líneas y PARADA.**
  - Líneas reales frente a 450–600; anotar V2, V19, V20 y lo no verificado de la Web (lógica de `ReturnUrl` y del proveedor solo a mano). **PARADA:** el usuario decide si pasa a la Tanda 5.
  - Hecho cuando: checklist de la Tanda 4 completa.
  - Evidencia: **Líneas a mano de la Tanda 4** (`wc -l` de nuevos y `diff --strip-trailing-cr` de modificados contra `tanda-4`; incluye blancos y XML-doc): nuevos ≈ 561 (`Auth` 228, `Http` 63, `Team` 24, `Shared` 52, `Login.razor` 82, `Team.razor` 112), modificados ≈ 268 (`Program.cs` 32, `App.razor` 18, `_Imports.razor` 4, `NavMenu.razor` 54, `TimeEntries.razor` 49, `TimeEntriesApiClient.cs` 47, `RepositoryConventionsTests.cs` 64), `NavMenu.razor.css` +22, eliminadas 8 (`TimeEntriesApiException.cs` y la copia antigua de `ApiProblemDto.cs`); **total ≈ 860** (src ≈ 795, pruebas ≈ 64) frente a 450–600 (≈ 1,4–1,9×; el exceso viene de `Team.razor` con el pulido R4 y de la separación en componentes). **Acumulado (Tandas 1, 2, 3a, 3b y 4): ≈ 5.173 + 860 ≈ 6.030** (≈ 6.060 con `.env.example`), frente a la referencia de 2.400–3.150 (el usuario aceptó `size:exception`). Más ≈ 110 líneas de evidencia manual en `verify-report.md` (no cuentan como código). **V2:** convive `Components.Authorization` 10.0.10 con `Components.WebAssembly` 10.0.10. **V19:** `sessionStorage` por `IJSRuntime` sin archivo JS propio, probado en navegador. **V20:** reglas CA nuevas sin avisos (CA2000 con `HttpClientHandler`, CA1062 con `ArgumentNullException.ThrowIfNull`); nada desactivado. **Desviaciones:** D-4-1 `Layout/NavMenu.razor.css` se modificó sin copia previa y, por un error de edición, se sobrescribió; se reconstruyó a partir de la salida de CSS con ámbito de `obj/Release/net10.0/scopedcss/Layout/NavMenu.razor.rz.scp.css` (reglas idénticas; los finales de línea pueden diferir del original) y se le añadieron dos reglas; D-4-2 el regex de «registro del token» se afinó (falso positivo con `LoginAsync`); D-4-3 UI-9 exige cambiar los dos `appsettings`; D-4-4 el 6.º fallo de arquitectura que se vio durante el RED era la misma prueba de `.env.example` de la línea base. **No verificado:** aspecto visual, estado intermedio de botones, otros navegadores y la expiración real (se simuló); lógica de `ReturnUrl` y del proveedor solo a mano. **PARADA:** el usuario decide si pasa a la Tanda 5.

---

# TANDA 5: Documentación y cierre

## Fase 26: ADR

- [x] 26.1 **Copia previa y RED de los ADR.** Archivos: `..\TimeTracker.rollback\tanda-5\` (5 existentes), `AR/DeliveryArtifactsTests.cs`. Cubre: SA-20, SA-21, SA-22.
  - `Save-Rollback 5 …`. Renombrar `Adr_Directory_HasTenAdrsWithFourSections` a `Adr_Directory_HasTwelveAdrsWithFourSections` (Contexto, Decisión, Consecuencias, Alternativas), y añadir `Adr0009_IsSupersededBy0011_AndAdr0011StatesItSupersedesIt` y `Adr0011And0012_RecordTheirRequiredConsequences` (0011: ausencia de refresh token y de FK con su disparador; 0012: `sessionStorage` y riesgo XSS).
  - Hecho cuando: las tres pruebas fallan por los motivos anotados.
  - Evidencia: `Save-Rollback 5` equivalente: `..\TimeTracker.rollback\tanda-5\` con las 5 copias (`README.md`, `openspec/config.yaml`, ADR 0009, `DeliveryArtifactsTests.cs`, `RepositoryConventionsTests.cs`; Count = 5). RED (`T-AR --filter Adr`): `Adr_Directory_HasTwelveAdrsWithFourSections` → `Assert.Equal() Failure: Values differ` (10 ADR en lugar de 12); `Adr0009_IsSupersededBy0011_AndAdr0011StatesItSupersedesIt` → falta el archivo del 0011; `Adr0011And0012_RecordTheirRequiredConsequences` → falta el archivo del 0011. Las tres rojas.
- [x] 26.2 **GREEN de los ADR.** Archivos: `docs/adr/0011-autenticacion-jwt-roles-y-equipo.md`, `docs/adr/0012-sesion-web-y-riesgo-xss.md`, `docs/adr/0009-identidad-provisional.md` (solo la línea de estado). Cubre: SA-20, SA-21, SA-22; V3.
  - **0011** (Aceptado; sustituye al 0009): DD-1, DD-6, DD-9 a DD-13, DD-15, DD-19, DD-22 a DD-24; PBKDF2 con los parámetros reales de V3; consecuencias: sin refresh token (desviación consciente de AGENTS §6, disparador: antes de sesiones largas o despliegue público), sin FK de `time_entries` (disparador: alta/baja de usuarios), `ForwardedHeaders` antes de desplegar tras un proxy, rehash si cambian los parámetros, un usuario borrado conserva su token hasta 30 minutos, contraseña de demostración pública; alternativas: Identity/`MapIdentityApi`, cookies, OIDC, refresh token, FK ahora. **0012** (Aceptado): DD-25 a DD-27; `sessionStorage` y riesgo XSS aceptado con mitigaciones (30 minutos, sin HTML crudo, cabeceras en la API); alternativas: cookies HttpOnly (BFF), `localStorage`, solo memoria. **0009**: cambiar únicamente `- Estado: Aceptado` por `- Estado: Sustituido por 0011` (el texto literal de la spec; ver D6); el cuerpo no se reescribe. **[R5]** si se recorta, el contenido del 0012 pasa a una sección del 0011 (11 ADR).
  - Hecho cuando: `T-AR --filter "FullyQualifiedName~DeliveryArtifactsTests"` en verde para los ADR.
  - Evidencia: creados `docs/adr/0011-autenticacion-jwt-roles-y-equipo.md` (41 líneas; estado «Aceptado; sustituye al 0009»; DD-1, DD-6 a DD-13, DD-15, DD-16, DD-19, DD-22 a DD-24 y DD-30; hash V3 verificado: PBKDF2 con HMAC-SHA512 y 100.000 iteraciones (9.1); 30 minutos sin refresh token como desviación consciente de AGENTS §6; `ManagerId`; sin clave foránea en `time_entries` con la que sí tiene `users.manager_id`; disparadores de revisión (refresh token, clave foránea, `ForwardedHeaders`, rehash); usuario borrado con token vivo 30 min; contraseña de demostración pública; alternativas: Identity/`MapIdentityApi`, cookies, OIDC, refresh token, clave foránea ahora, prohibir la siembra fuera de Development) y `0012-sesion-web-y-riesgo-xss.md` (28 líneas; DD-25 a DD-27; `sessionStorage` y XSS aceptado con mitigaciones; alternativas: cookies HttpOnly/BFF, `localStorage`, solo memoria). ADR 0009: solo la línea de estado, ahora `- Estado: Sustituido por 0011` (literal de la spec; cuerpo intacto). `T-AR --filter Adr`: 3/3 en verde. **Desviación D-5-1:** la primera versión de la prueba escribió mal las expresiones regulares (`\S`, `\d` perdieron la barra al pasar por el shell) y falló por ello; corregida con `Edit`. **V3 anotada en el ADR 0011.** R5 no se aplica (12 ADR).

## Fase 27: README y `config.yaml`

- [x] 27.1 **RED del README y de `config.yaml`.** Archivos: `AR/DeliveryArtifactsTests.cs`, `AR/RepositoryConventionsTests.cs`. Cubre: SA-5 (README y `config.yaml`), SA-23, SA-24, SA-25, PE-14.
  - `Readme_DocumentsAuthenticationUsersAndVariables`: `docker compose up`, rutas `/login`, `/fichajes` y `/equipo`, puerto `5274`, `Jwt__SigningKey`, `Seed__DemoUsers`, `Seed__DemoPassword`, `ASPNETCORE_ENVIRONMENT=Development`, `ConnectionStrings__Default`, `Cors__AllowedOrigins__0=http://localhost:5172`, los cuatro correos `@example.test`, `Demo-Fichajes-2026`, el aviso de no activar la siembra en un despliegue real, `docker compose down -v`, que las pruebas de integración requieren Docker, el comando `RandomNumberGenerator` de generación de la clave y los 12 ADR; y **sin** `Identity__DevelopmentEmployeeId`. `ConfigYaml_ReflectsAuthenticationAndDropsTheProvisionalNote`. Ampliar `Code_HasNoProvisionalIdentityLeftovers` a `README.md` y `openspec/config.yaml` (cierra SA-5).
  - Hecho cuando: las pruebas fallan por los motivos anotados.
  - Evidencia: `Readme_DocumentsFichajesDockerRequirementAndRequiredVariables` renombrada a `Readme_DocumentsAuthenticationUsersAndVariables` (23 textos requeridos: `docker compose up`, `docker compose down -v`, `/login`, `/fichajes`, `/equipo`, `5274`, `requieren Docker`, `ConnectionStrings__Default`, `ASPNETCORE_ENVIRONMENT`, `Jwt__SigningKey`, `Seed__DemoUsers`, `Seed__DemoPassword`, `Cors__AllowedOrigins__0=http://localhost:5172`, `RandomNumberGenerator`, los cuatro correos, `Demo-Fichajes-2026`, `despliegue real`, `dotnet ef` y los enlaces a los ADR 0011 y 0012; y sin `Identity__DevelopmentEmployeeId`) y `ConfigYaml_ReflectsAuthenticationAndDropsTheProvisionalNote` (`autenticacion-y-roles`, `JWT`, `Employee`, `Manager`, `ADR 0008-0012`; sin la nota «Identidad provisional (solo Development» ni `Identity__`). La ampliación de `Code_HasNoProvisionalIdentityLeftovers` (SA-5) se hizo en la `Theory` `DeliveryFiles_HaveNoProvisionalIdentity`, con `README.md` y `openspec/config.yaml` como casos nuevos (en lugar de tocar la prueba de `.cs`). RED: `Readme_…` rojo, `ConfigYaml_…` rojo y `DeliveryFiles_HaveNoProvisionalIdentity("README.md")` rojo (`Sub-string found`). **Verde a la primera:** `DeliveryFiles_HaveNoProvisionalIdentity("openspec/config.yaml")` (el archivo nunca contuvo esa clave); mutación: añadir `# Identity__DevelopmentEmployeeId` a `config.yaml` → ROJO en este caso y en `ConfigYaml_…`; revertido y comparado (idéntico).
- [x] 27.2 **GREEN del README.** Archivos: `README.md`. Cubre: SA-23, SA-24, SA-25, PE-14.
  - Reescribir las secciones afectadas: estado (autenticación real y roles), pila, compilar y probar (Docker obligatorio para la integración), ejecutar en local (`$env:` con `ASPNETCORE_ENVIRONMENT`, `ConnectionStrings__Default`, `Jwt__SigningKey`, y `Seed__*` para la siembra), `docker compose up` en `http://localhost:5274` con `Cors__AllowedOrigins__0=http://localhost:5172`, tabla de usuarios de prueba (Marta Ejemplo `Manager`; Luis, Ana y Pablo `Employee`, dominio `example.test`) con la contraseña de demostración, aviso de la siembra y de `docker compose down -v` (borra los fichajes de desarrollo anteriores), estructura y 12 ADR. Sin `Identity__DevelopmentEmployeeId`.
  - SA-23 (manual, en 28.1): seguir los comandos del README en limpio hasta iniciar sesión en `http://localhost:5274`.
  - Hecho cuando: la prueba del README pasa.
  - Evidencia: README reescrito (estado y aviso de la siembra; pila con JWT; PowerShell 7 requerido para la clave; tabla de variables con `Jwt__SigningKey`, `ASPNETCORE_ENVIRONMENT`, `Seed__DemoUsers` y `Seed__DemoPassword` y sin `Identity__`; sección «Generar la clave de firma» con `pwsh -NoProfile -Command "[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))"` y la nota de que falla en Windows PowerShell 5.1; ejecución local con `$env:` y `Seed__*`; Compose en `http://localhost:5274` con `Cors__AllowedOrigins__0=http://localhost:5172`, aviso de `docker compose down -v` por los fichajes antiguos y de no ejecutar a la vez la API local y la de Compose; sección «Usuarios de prueba» con los cuatro correos `@example.test`, roles y `Demo-Fichajes-2026`; aviso de no activar la siembra en un despliegue real; estructura y 12 ADR). Sin URLs inventadas de despliegue, vídeo ni slides (siguen «pendiente»). Detalle veraz: Compose interpola `${Jwt__SigningKey:?…}` en todo el archivo, así que incluso `docker compose up -d postgres` exige `Jwt__SigningKey` y `Seed__DemoPassword`; el README lo dice. `Readme_DocumentsAuthenticationUsersAndVariables` en verde. **SA-23 (seguir el README en limpio hasta el login) no ejecutado de forma literal:** exigiría crear `.env` (no se lee ni se escribe); lo equivalente (compose con `--env-file` ficticio, login de Luis y Marta en `http://localhost:5274`) se verificó en 20.4 y 24.1, y el texto de las rutas, variables y puertos del README coincide con el compose y las specs.
- [x] 27.3 **GREEN de `config.yaml`.** Archivos: `openspec/config.yaml`. Cubre: SA-24 (parte), SA-5.
  - Actualizar el «Estado» del `context`: autenticación con JWT propio y roles `Employee`/`Manager`, equipo = reportes directos, ADR 0008 a 0012, siembra de demostración, sin identidad provisional; mantener las notas vigentes (CI sin ejecutar, xUnit v2, riesgo residual de la salida simultánea). Las `rules` no cambian.
  - Hecho cuando: `ConfigYaml_ReflectsAuthenticationAndDropsTheProvisionalNote` y `Code_HasNoProvisionalIdentityLeftovers` en verde.
  - Evidencia: «Estado» del `context` actualizado (cambio `autenticacion-y-roles`: JWT propio de 30 min sin refresh token, roles `Employee`/`Manager`, equipo = reportes directos vía `ManagerId`, `/login` y `/equipo`, siembra desactivada por defecto, sin identidad provisional, ADR 0011-0012 con el 0009 sustituido, compose en 5274; «ADR 0008-0012 vigentes»); se conservan CI sin ejecutar, xUnit v2 y el riesgo residual de la salida simultánea. `rules` sin cambios. `ConfigYaml_…` y `DeliveryFiles_HaveNoProvisionalIdentity("openspec/config.yaml")` en verde.

## Fase 28: Cierre

- [x] 28.1 **Puerta de calidad final.** Cubre: SA-4, SA-16, SA-17, SA-18, SA-23 (manual), AU-58, AU-59, AU-60, PE-12.
  - `ENV-CHECK` vacío; `RESTORE`, `FORMAT`, `BUILD`, `TEST`, `COV-D` (100 % líneas y ramas), `COV-A` (≥ 80 %, medido solo con `Application.Tests`), `VULN`. Comprobar AU-59: `Test-Path IT\Startup\JwtStartupTests.cs` es True y `Test-Path IT\Startup\IdentityStartupTests.cs` es False. Contrato: hash antes y después de `dotnet build API --no-incremental` idénticos. SA-23: seguir el README en una sesión limpia hasta el login en `http://localhost:5274`.
  - Hecho cuando: todo en 0 y el recuento de pruebas por proyecto queda anotado.
  - Evidencia previa (parcial; **sigue abierta** por `.env.example` y por el SA-23 literal): `ENV-CHECK` vacío; `RESTORE` rc 0; `FORMAT` rc 0; `BUILD` rc 0 con 0 avisos y 0 errores; `TEST` sobre `TestResults` limpio: **393** pruebas = Domain 49 (49 verdes), Application 43 (43), Arquitectura 71 (66 verdes y **5 rojas**: exactamente las de `.env.example` de la línea base, `EnvExample_ContainsTheDocumentedKeys`, `…AuthenticationKeysAreEmptyOrFalseAndDocumented`, `…HasNoDevelopmentEmployeeKey`, `…ListsEveryKeyConsumedByCompose` y `DeliveryFiles_HaveNoProvisionalIdentity(.env.example)`) e Integración 230 (230 verdes). La Tanda 5 suma 5 pruebas de arquitectura (los 12 ADR sustituyen a la de 10; +2 de ADR, +1 de `config.yaml`, +2 casos de la `Theory`, y el README se renombra). `COV-D` rc 0 (100,0 % líneas y ramas); `COV-A` rc 0 (100,0 % y 100,0 %). `VULN` rc 0: sin paquetes vulnerables. AU-59: `IT\Startup\JwtStartupTests.cs` existe y `IdentityStartupTests.cs` no. Contrato: SHA-256 de `docs/api/openapi.json` idéntico antes y después de `dotnet build src/TimeTracker.Api --no-incremental` (`f0c63377be770c93…`). **Pendiente:** aplicar `.env.example` (20.1) y repetir `TEST`; SA-23 literal con un `.env` real (ver 27.2).
  - Evidencia final (2026-10-14): `RESTORE` rc 0; `FORMAT` (`dotnet format TimeTracker.sln --verify-no-changes`) rc 0; `BUILD` (`-c Release -warnaserror`) rc 0 con 0 avisos y 0 errores; `TEST` (`dotnet test TimeTracker.sln -c Release --no-build --collect:"XPlat Code Coverage"`) rc 0 con entorno limpio (sin variables `Jwt__`, `Seed__`, `ConnectionStrings__`): **393/393 verdes** = Domain 49, Application 43, Arquitectura 71, Integración 230 (PostgreSQL 18 real con Testcontainers); `COV-D` rc 0 (líneas 100,0 %, ramas 100,0 %); `COV-A` rc 0 (100,0 % y 100,0 %; mínimo 80 %); `VULN` (`dotnet list TimeTracker.sln package --vulnerable --include-transitive`) rc 0: los 9 proyectos sin paquetes vulnerables. Con `.env.example` aplicado se cierran las 5 rojas de la evidencia previa. Contrato y AU-59 según la evidencia previa (sin cambios de código desde entonces). **SA-23 literal** (README con un `.env` real hasta el login) no se repite en esta ejecución: queda como advertencia W3; la cobertura equivalente con compose y variables ficticias consta en 20.4/27.2.
- [x] 28.2 **Validaciones V1–V24, verdes a la primera y amenazas.** Cubre: AU-61; V1–V24.
  - Completar la tabla de evidencia con el estado de V1 a V24 (ninguna sin anotar), la lista de pruebas «verde a la primera» con su mutación o justificación (insumo de `verify-report.md`) y el repaso de la matriz de amenazas.
  - Hecho cuando: no queda V sin anotar ni prueba verde a la primera sin mutación o justificación.
  - Evidencia: **V1–V24, todas resueltas** (tarea donde consta): V1 (1.3: JwtBearer 10.0.10, `Microsoft.IdentityModel.*` 8.19.2, sin NU190x) · V2 (1.3, 22.1: `Components.Authorization` 10.0.10 con `Components.WebAssembly` 10.0.10) · V3 (9.1: formato V3, PBKDF2 HMAC-SHA512, 100.000 iteraciones; anotada en el ADR 0011) · V4 (11.5, 15.2: `RequireRole` con `RoleClaimType="role"` y `MapInboundClaims=false`) · V5 (11.5, 14.1: `UseStatusCodePages` basta, sin `JwtBearerEvents`) · V6 (11.1: hizo falta el respaldo, `LifetimeValidator` propio con el `TimeProvider`) · V7 (19.2: API de `Microsoft.OpenApi` compatible) · V8 (14.2, 15.2, 17.3: CORS en 401, 403 y 429) · V9 (14.1, 17.1: 429 con el respaldo `OnRejected`; el 401 de login, el 409 y el 500 conservan su cuerpo) · V10 (`VULN` limpio en 1.3, 13.1, 21.1, 25.1 y 28.1) · V11 (20.3: `HEALTHCHECK` sano con `/health` anónimo) · V12 (20.4: la siembra termina antes de escuchar; login de Luis y Marta en 5274) · V13 (11.6: aceptado, 401 sin token en rutas inexistentes y `POST /health`; reflejado en `api-health`) · V14 (11.6, 12.3: `ConfigureClient` se conserva) · V15 (9.2: `iat`, `nbf`, `exp` del descriptor respetados) · V16 (16.3: la siembra corre tras las migraciones y antes de escuchar) · V17 (17.1: sin respaldo; 226/226 con el limitador activo) · V18 (10.4: 400 del enlace; JSON roto con selector) · V19 (22.4: `sessionStorage` por `IJSRuntime` sin archivo JS, probado en navegador) · V20 (nada desactivado: 9.1–9.3, 10.5, 13.1, 21.1, 22.5, 23.6, 25.2; `BUILD` con 0 avisos) · V21 (8.3, 12.4, 16.1, 19.3: `DocumentGeneration` y contrato estable) · V22 (11.7) · V23 (11.5: el 401 no incluye `error_description`) · V24 (2.12, 5.3, 6.1: enlace de EF por constructor privado y `HasPendingModelChanges()` falso). **Verdes a la primera:** cada una consta en su tarea con mutación revertida o justificación (índice por tarea: 1.2, 2.3, 2.6, 2.7, 2.11, 3.4, 3.7, 3.8, 3.13, 5.4–5.6, 6.4, 8.3, 8.4, 9.1–9.3, 10.1, 10.3, 10.5, 10.6, 11.1, 11.2, 11.6, 11.7, 12.1, 12.4, 13.2, 14.2, 15.2, 15.4, 15.6, 15.8, 15.9, 16.1, 16.4, 17.2, 17.3, 18.2, 20.2, 22.2, 25.2 y 27.1). La única que solo tenía justificación, `MemberEntries_WithNonGuidId_Returns404` (15.7: la ruta `{memberId:guid}` no casa con otro texto), recibió ahora su mutación: quitar `:guid` de la ruta en `TeamEndpoints.cs` → ROJO (`Assert.Equal() Failure`, esperado `NotFound`); revertido y comparado (idéntico), `BUILD` rc 0 y `TeamEndpointsTests` 28/28. **Matriz de amenazas:** sin cambios respecto al repaso de 21.2; la fila «Token robado / XSS» queda cubierta por el ADR 0012 (26.2) y la de «Contraseña de demostración pública» por el aviso del README (27.2).
- [x] 28.3 **Limpieza.**
  - Eliminar `TestResults/`, `artifacts/`, contenedores (`docker compose down -v`), imágenes `timetracker-api*`, copias temporales del scratchpad y variables de sesión (`Jwt__`, `Seed__`, `POSTGRES_`, `Cors__`); `dotnet build-server shutdown`; confirmar que no se ejecutó ningún comando Git. El `.env` local no se ha leído ni modificado: informar al usuario de que puede contener claves obsoletas (inocuas, SC-16). `..\TimeTracker.rollback` (con `tanda-1` a `tanda-5` y `fichaje-extremo-a-extremo\`) se conserva hasta el archivado.
  - Hecho cuando: no quedan procesos ni contenedores del cambio.
  - Evidencia: `TestResults/` eliminado (al terminar la puerta y de nuevo tras la mutación de 28.2); `artifacts/` no existe; `docker ps -a` y `docker volume ls` solo muestran contenedores y volúmenes ajenos al cambio (ninguno `timetracker`/`ttsmoke`); imágenes `timetracker-api*` ya eliminadas en 21.1; sin variables `Jwt__`, `Seed__`, `POSTGRES_`, `Cors__` en la sesión (`ENV-CHECK` vacío); `dotnet build-server shutdown` ejecutado; los archivos temporales viven en el scratchpad de la sesión. `..\TimeTracker.rollback` (con `tanda-1` a `tanda-5`, `tanda-3a`, `tanda-3b` y `fichaje-extremo-a-extremo\`) se conserva hasta el archivado. El `.env` local no se ha leído ni modificado (puede contener claves obsoletas, inocuas, SC-16). **Incumplimiento anotado (D-5-2):** al ejecutar la mutación de 15.7 se encadenó por error `git --version` (con salida descartada) al final de un comando compuesto; es el único comando de Git de la Tanda 5, no cambió nada (solo imprime la versión) y no hay repositorio, pero **no puede afirmarse que no se ejecutó ningún comando Git**.
- [x] 28.4 **Revisión final, líneas y PARADA.**
  - Líneas reales por tanda frente a la previsión; resumen de desviaciones; **PARADA:** siguiente fase `sdd-verify`.
  - Hecho cuando: checklist completa.
  - Evidencia previa (parcial; **abierta** por 20.1/21.1/21.2/28.1): **Líneas a mano de la Tanda 5** (`wc -l` de nuevos y `diff --strip-trailing-cr` de modificados contra `tanda-5`): ADR nuevos 69 (0011: 41; 0012: 28), README 86, `config.yaml` 2, ADR 0009 2, pruebas 69 (`DeliveryArtifactsTests` 67, `RepositoryConventionsTests` 2); **total ≈ 228** frente a 200–280 (dentro de la previsión; las líneas de los ADR y del README son largas). **Acumulado (Tandas 1 a 5): ≈ 6.030 + 228 ≈ 6.258** (≈ 6.283 con `.env.example`), frente a la referencia de 2.400–3.150 (≈ 2×; `size:exception` aceptado). **Desviaciones de la Tanda 5:** D-5-1 (regex de la prueba de ADR corregida), D-5-2 (un `git --version` encadenado por error, ver 28.3), D-5-3 la ampliación de `Code_HasNoProvisionalIdentityLeftovers` a README y `config.yaml` se implementó como casos nuevos de la `Theory` `DeliveryFiles_HaveNoProvisionalIdentity`, D-5-4 el README advierte de que Compose exige `Jwt__SigningKey` y `Seed__DemoPassword` incluso para `docker compose up -d postgres`. **Resta para cerrar el cambio:** (1) que el usuario aplique `env.example.proposed` como `.env.example` (20.1), (2) repetir `TEST` (esperado 393/393) y marcar 20.1, 21.1, 21.2 y 28.1, (3) SA-23 literal con un `.env` real si el usuario quiere repetirlo, (4) PARADA y `sdd-verify`.
  - Evidencia final (2026-10-14): resuelto lo que mantenía abierta la tarea (20.1, 21.1, 21.2 y 28.1 cerradas con 393/393 verdes, `COV-D`/`COV-A` 100 %, `VULN` limpio). Líneas por tanda y desviaciones: ver la evidencia previa (acumulado ≈ 6.283 frente a 2.400–3.150, `size:exception` aceptado). PARADA: siguiente fase `sdd-verify` (informe actualizado a PASS WITH WARNINGS).

---

# Mapa de validaciones V1–V24

| ID | Dónde | ID | Dónde |
|---|---|---|---|
| V1 | 1.3, 21.1, 28.1 | V13 | 11.6 |
| V2 | 1.3, 22.1 | V14 | 8.2, 11.6, 12.3 |
| V3 | 9.1, 26.2 | V15 | 9.2 |
| V4 | 10.5, 15.2 | V16 | 16.3 |
| V5 | 11.5 (parte), 14.1, 15.2 | V17 | 17.1 |
| V6 | 11.1, 11.2 | V18 | 10.4 |
| V7 | 19.2 | V19 | 22.4 |
| V8 | 14.2, 15.2, 17.3 | V20 | 9.1–9.3, 10.5, 13.1, 21.1, 22.5, 23.6 |
| V9 | 14.1, 17.1 | V21 | 8.3, 12.4, 16.1, 19.3 |
| V10 | 1.3, 13.1, 21.1, 28.1 | V22 | 11.7 |
| V11 | 20.3 | V23 | 11.5 |
| V12 | 20.4 | V24 | 2.12, 5.3, 6.1 |

Respaldos previstos por el diseño §18: V1 → 10.0.12 y versión transitiva fijada; V2 → 10.0.12 en ambos; V4 → ajustar `RoleClaimType`; V5 → `OnChallenge`/`OnForbidden`; V6 → `LifetimeValidator` propio; V7 → recortar R1; V8 → reordenar el pipeline; V9 → `OnRejected` escribe el cuerpo; V13 → política por grupos (pierde «seguro por defecto»); V14 → `CreateAuthenticatedClient()`; V15 → claims explícitos; V16 → `StartingAsync` registrada tras la de migraciones; V17 → filtro de IP de prueba; V18 → `StatusCodeSelector`; V19 → `session.js`; V21 → ajustar `DocumentGeneration`.

# Matriz de amenazas del diseño (§15) → pruebas RED y tareas

| Amenaza | Prueba RED o verificación | Tarea |
|---|---|---|
| BOLA/IDOR (API1, A01) | Miembro ajeno, inexistente, sin responsable y propio → 404 idéntico; fichajes filtrados por `sub`; el caso de uso no consulta fichajes | 3.11, 15.7, 15.8, 12.1, 12.3 |
| Autorización por función (API5) | Empleado → 403 antes de evaluar el miembro; respaldo autenticado; `/api/team/*` exige `Manager` | 15.2, 15.9, 11.6, 11.7 |
| Fuerza bruta (API4, A07) | Undécimo login → 429; PBKDF2; contraseña ≤ 128 (residual: ataque distribuido e IP tras proxy, **R3** lo recorta) | 17.1–17.3, 9.1, 3.7 |
| Enumeración de usuarios (A07) | Cuerpos 401 idénticos; `Verify` con hash nulo (unitaria) | 10.3, 3.5 |
| Token forjado, `alg: none`, confusión de algoritmo (API2, A02) | Payload alterado, HS512, `none`, otra clave, emisor y audiencia ajenos, `sub` y `role` inválidos | 11.3, 11.4, 15.9 |
| Token robado / XSS (A03, A07) | Sin `MarkupString` ni `innerHTML` (prueba de estructura); token solo en `sessionStorage`; ADR 0012 | 22.2, 24.2, 26.2 |
| Redirección abierta | `ReturnUrl.IsSafe` y verificación manual con `//ejemplo.test` y `https://ejemplo.test` | 22.4, 24.4 |
| Secretos (A02, A05) | `JwtStartupTests`; `AppSettings_ContainNoSensitiveKeys`; `.env.example` vacío; la clave no se filtra | 8.3, 8.4, 10.6, 20.1, 20.3 |
| Datos en logs (A09) | `LoggerMessage` sin correo, contraseña, hash, token ni IP; registrador que captura | 10.3, 10.6, 16.4, 21.2 |
| Exposición de errores (API8) | Cuerpos 401/403/404/405/429 sin trazas; 500 genérico | 14.1, 18.2 |
| CORS (API8) | Preflight con `authorization`; CORS en 401, 403 y 429 | 14.2, 15.2, 17.3 |
| DoS (API4) | Listas acotadas (50 y 100) en unitaria y API; tope de contraseña; limitador | 3.7, 3.9, 3.12, 15.4, 15.5 |
| Asignación masiva (API3) | `LoginRequest` con dos campos; `UserResponse` sin `passwordHash`; contrato sin `employeeId` | 10.1, 10.2, 19.3 |
| Contraseña de demostración pública | Siembra desactivada por defecto; compose en `127.0.0.1`; README avisa; `.env.example` vacío | 16.1, 20.1, 20.2, 27.2 |
| Inyección SQL (A03) | Solo LINQ parametrizado; sin `FromSqlRaw`/`ExecuteSqlRaw` en `src/` | 6.3, 7.1, 21.1 |

# Evidencia (se completa en apply)

| Evidencia | Tarea | Resultado |
|---|---|---|
| Copias previas por tanda y apartado de las copias antiguas | 1.1, 5.1, 8.1, 14.1, 22.1, 26.1 | 1.1 hecho (Tanda 1: Count = 7; antiguas en `fichaje-extremo-a-extremo`); resto pendiente |
| RED y GREEN reales de `UserAccount`, `LoginUseCase` y equipo | 2.1–2.12, 3.3–3.13 | hecho (Tanda 1), anotado en cada tarea |
| Mutaciones temporales (verdes a la primera) | 1.2, 2.3, 2.6, 2.7, 2.11, 3.7, 3.8, 3.13, 5.4–5.6, 6.4, 8.3, 8.4, 9.1–9.3, 10.1, 10.3, 10.5, 10.6, 11.1–11.3, 11.7, 14.2, 15.2, 15.4–15.6, 15.8–15.10, 16.1, 16.4, 17.2, 17.3, 18.2, 20.2, 22.2 | Tanda 1 hecha: 1.2, 2.3, 2.6, 2.7, 2.11, 3.4, 3.7, 3.8, 3.13; resto pendiente |
| Versiones de paquetes y `VULN` | 1.3, 13.1, 21.1, 25.1, 28.1 | 1.3 y 4.1 hechos (JwtBearer y Components.Authorization 10.0.10; IdentityModel 8.19.2; `VULN` limpio); resto pendiente |
| Parámetros reales de `PasswordHasher` (V3) y reloj de JwtBearer (V6) | 9.1, 11.1 | pendiente |
| Estados de V13, V17, V18 y V22 | 11.6, 17.1, 10.4, 11.7 | pendiente |
| Docker: imagen, compose, puertos, login de demostración | 20.3, 20.4, 20.5 | pendiente |
| Verificación manual de la Web (también en `verify-report.md`) | 24.1–24.5 | hecho (2026-10-14) |
| Estado de V1–V24 | 28.2 | pendiente |

# Líneas por tanda (previsión frente a real; el real se anota en apply)

| Tanda | Previsión | Real | Punto de control |
|---|---|---|---|
| 1 | 450–600 | ≈ 919 (src 269, pruebas 645, props y csproj 5; `wc -l` con blancos y documentación) | 4.2 |
| 2 | 150–220 | pendiente | 7.2 |
| 3a | 550–700 | pendiente | 13.2 |
| 3b | 600–750 | pendiente | 21.2 |
| 4 | 450–600 | ≈ 860 (src ≈ 795, pruebas ≈ 64; `wc -l` de nuevos y `diff` de modificados contra `tanda-4`) | 25.2 |
| 5 | 200–280 | ≈ 228 (ADR 69, README 86, `config.yaml` 2, ADR 0009 2, pruebas ≈ 69; `wc -l` de nuevos y `diff` de modificados contra `tanda-5`) | 28.4 |
| **Total** | **≈ 2.400–3.150** | pendiente | |

# Trazabilidad de escenarios (260 de 260 mapeados)

Tipo: **A** = prueba automática · **C** = comando de verificación sin prueba dedicada (build, formato, cobertura, hash del contrato) · **M** = evidencia manual registrada · **A+M** = ambas. Los identificadores los fija este archivo (por spec, en orden de aparición). Un número como `15.9` es una tarea; `Tanda 3a/3b` indica que el escenario se completa en dos tandas.

## authentication (61)

| ID | Escenario | Tareas | Tipo |
|---|---|---|---|
| AU-1 | El hash no contiene la contraseña | 9.1 | A |
| AU-2 | Contraseña incorrecta (hash) | 9.1 | A |
| AU-3 | Sal aleatoria | 9.1 | A |
| AU-4 | Inicio de sesión correcto | 3.3, 10.2 | A |
| AU-5 | Correo normalizado | 3.6, 10.2 | A |
| AU-6 | Sin hash en la respuesta | 3.3, 10.2 | A |
| AU-7 | Cuerpo incompleto | 3.7, 10.3 | A |
| AU-8 | Entrada inválida sin consulta | 3.7 | A |
| AU-9 | Cuerpo ausente o ilegible | 10.4 | A |
| AU-10 | Correo inexistente | 3.5, 10.3 | A |
| AU-11 | Contraseña errónea | 3.4, 10.3 | A |
| AU-12 | Respuestas idénticas | 10.3, 14.1 | A |
| AU-13 | Verificación ficticia | 3.5 | A |
| AU-14 | Hash almacenado ilegible (añadido por decisión del usuario) | 9.1, 10.3 | A |
| AU-15 | Claims del token | 9.2 | A |
| AU-16 | Vigencia de 30 minutos | 9.2, 10.2 | A |
| AU-17 | Identificador único por token | 9.2 | A |
| AU-18 | Token válido | 10.5, 11.1 | A |
| AU-19 | Sin token | 10.5 | A |
| AU-20 | Token caducado | 11.1 | A |
| AU-21 | Tolerancia de reloj | 11.1 | A |
| AU-22 | Token alterado | 11.3, 15.9 | A |
| AU-23 | Algoritmo distinto | 11.3 | A |
| AU-24 | Sub no válido | 11.4, 12.1 | A |
| AU-25 | Rol desconocido | 11.4 | A |
| AU-26 | Emisor o audiencia ajenos | 11.3 | A |
| AU-27 | Token aún no vigente | 11.1 | A |
| AU-28 | Caducidad según el reloj de la aplicación | 11.1, 11.2 | A |
| AU-29 | Sin detalle del error en la cabecera | 11.5 | A |
| AU-30 | Token firmado con otra clave | 11.3 | A |
| AU-31 | La clave no se filtra | 10.6 | A |
| AU-32 | Datos del token | 10.5 | A |
| AU-33 | Sin base de datos | 10.5 | A |
| AU-34 | Rol de responsable | 10.5 | A |
| AU-35 | Diez intentos permitidos | 17.2 **[R3]** | A |
| AU-36 | Undécimo intento | 17.1 **[R3]** | A |
| AU-37 | Los intentos correctos también cuentan | 17.2 **[R3]** | A |
| AU-38 | Otras rutas sin límite | 17.2 **[R3]** | A |
| AU-39 | CORS en el 429 | 17.3 **[R3]** | A |
| AU-40 | Cabeceras en una respuesta correcta | 18.1 **[R2]** | A |
| AU-41 | Cabeceras en errores (401, 429, 404, 500) | 18.2 **[R2]** | A |
| AU-42 | Sin caché bajo /api | 18.1 **[R2]** | A |
| AU-43 | CSP restrictiva | 18.1 **[R2]** | A |
| AU-44 | Endpoints anónimos permitidos | 11.7, 15.9 | A |
| AU-45 | Endpoints protegidos sin token | 11.6, 15.9 | A |
| AU-46 | Endpoint nuevo sin atributos | 11.6 | A |
| AU-47 | Ruta inexistente o método no permitido sin token | 11.6, 14.1 | A |
| AU-48 | Salud anónima | 11.6 | A |
| AU-49 | OpenAPI en Development | 11.7 | A |
| AU-50 | OpenAPI en Production | 11.7 | A |
| AU-51 | 401 como ProblemDetails | 14.1 | A |
| AU-52 | 403 como ProblemDetails | 15.2 | A |
| AU-53 | CORS en 401 y 403 | 14.2, 15.2 | A |
| AU-54 | Preflight con Authorization | 14.2 | A |
| AU-55 | Rutas de autenticación en el contrato | 10.1, 19.1, 19.2 (Tanda 3a/3b) | A |
| AU-56 | Esquema Bearer | 19.1, 19.2 **[R1]** | A |
| AU-57 | Sin datos prohibidos | 10.1, 19.3 | A |
| AU-58 | Contrato sincronizado | 19.3, 28.1 | C |
| AU-59 | Pruebas de arranque de JWT | 8.3, 12.2, 28.1 | A |
| AU-60 | Cobertura por niveles | 2.12, 3.14, 4.1, 28.1 | A |
| AU-61 | Pruebas verdes a la primera declaradas | 28.2 (y verify-report) | M |

## team-access (63)

| ID | Escenario | Tareas | Tipo |
|---|---|---|---|
| TA-1 | Valores del rol | 2.1 | A |
| TA-2 | El responsable ficha | 15.8, 24.3 | A+M |
| TA-3 | Usuario válido con correo normalizado | 2.2, 2.3 | A |
| TA-4 | Usuario sin responsable | 2.11 | A |
| TA-5 | Identificador vacío | 2.5 | A |
| TA-6 | Correo inválido | 2.6 | A |
| TA-7 | Nombre o hash no válidos | 2.7, 2.8 | A |
| TA-8 | Nombre con espacios exteriores | 2.4 | A |
| TA-9 | Normalización del correo | 2.3 | A |
| TA-10 | Rol no definido | 2.9 | A |
| TA-11 | Responsable no válido | 2.10 | A |
| TA-12 | Columnas | 5.2, 5.3 | A |
| TA-13 | Correo duplicado | 5.4 | A |
| TA-14 | Responsable inexistente | 5.4 | A |
| TA-15 | Borrado de un responsable con reportes | 5.4 | A |
| TA-16 | Responsable de sí mismo | 5.4 | A |
| TA-17 | Rol fuera de catálogo | 5.4 | A |
| TA-18 | Correo con mayúsculas | 5.4 | A |
| TA-19 | Ida y vuelta de un usuario | 6.1 | A |
| TA-20 | Búsqueda por correo | 6.2 | A |
| TA-21 | Reportes directos ordenados | 6.3 | A |
| TA-22 | Miembro del equipo | 6.3 | A |
| TA-23 | Límite del listado | 6.3 | A |
| TA-24 | Equipo con reportes | 3.8 | A |
| TA-25 | Sin jerarquía transitiva | 3.8, 6.3 | A |
| TA-26 | Equipo vacío (caso de uso) | 3.8 | A |
| TA-27 | Equipo de Marta | 15.3 | A |
| TA-28 | Sin datos sensibles | 15.3 | A |
| TA-29 | Máximo de 100 miembros | 3.9 (unitaria), 15.4 (API) | A |
| TA-30 | Equipo ajeno no visible | 15.3 | A |
| TA-31 | Equipo vacío (API) | 15.3 | A |
| TA-32 | Fichajes del miembro | 3.10, 15.5 | A |
| TA-33 | Más de 50 fichajes | 3.12, 15.5 | A |
| TA-34 | Miembro sin fichajes | 3.13, 15.5 | A |
| TA-35 | Solo los fichajes del miembro | 3.13, 15.5 | A |
| TA-36 | Solo lectura | 15.6 | A |
| TA-37 | Escritura sin token | 15.6 | A |
| TA-38 | Empleado en el listado | 15.2 | A |
| TA-39 | Empleado en los fichajes de un miembro | 15.2 | A |
| TA-40 | Sin token | 15.2 | A |
| TA-41 | Responsable permitido | 15.2 | A |
| TA-42 | Rol desconocido | 15.2 (verde por 11.4) | A |
| TA-43 | Miembro de otro responsable | 3.11, 15.7 | A |
| TA-44 | Miembro inexistente | 3.11, 15.7 | A |
| TA-45 | Ajeno e inexistente indistinguibles | 15.7 | A |
| TA-46 | El propio responsable | 3.11, 15.7 | A |
| TA-47 | Identificador que no es GUID | 15.7 | A |
| TA-48 | Caso de uso sin consulta de fichajes | 3.11 | A |
| TA-49 | Empleado solo ve los suyos | 15.8 | A |
| TA-50 | El responsable no ve los del equipo en su listado | 15.8 | A |
| TA-51 | Parámetros de empleado ignorados | 15.8 | A |
| TA-52 | El responsable no ficha por otro | 15.8 | A |
| TA-53 | Rutas y parámetro (contrato) | 15.1, 15.10 | A |
| TA-54 | Respuestas declaradas | 15.1, 15.10 | A |
| TA-55 | Sin employeeId | 15.10, 19.3 | A |
| TA-56 | Siembra activada | 16.2, 16.3, 20.4 | A+M |
| TA-57 | Contraseña como hash | 16.2, 16.3 | A |
| TA-58 | Idempotente | 16.2, 16.4 | A |
| TA-59 | Usuario previo conservado | 16.2, 16.3 | A |
| TA-60 | Siembra desactivada por defecto | 16.2, 16.4 | A |
| TA-61 | Login y equipo con usuarios sembrados | 16.2, 20.4 | A+M |
| TA-62 | Base no disponible con la siembra activa | 16.2, 16.3 | A |
| TA-63 | La contraseña no se registra | 16.2, 16.4 | A |

## web-session (41; verificación manual por decisión de la spec, sin bUnit)

| ID | Escenario | Tareas | Tipo |
|---|---|---|---|
| WS-1 | Inicio de sesión correcto | 23.2, 24.2 | M |
| WS-2 | Regreso a la ruta de origen | 23.2, 24.4 | M |
| WS-3 | Redirección abierta rechazada | 22.4, 23.2, 24.4 | M |
| WS-4 | Credenciales erróneas | 23.2, 24.2 | M |
| WS-5 | Demasiados intentos | 23.2, 24.5 **[R3]** | M |
| WS-6 | API caída (login) | 23.2, 24.5 | M |
| WS-7 | Campos vacíos | 23.2, 24.2 | M |
| WS-8 | Token en sessionStorage | 22.2, 22.4, 24.2 | A+M |
| WS-9 | Recarga de la página | 22.4, 24.2 | M |
| WS-10 | Copia en memoria | 22.4, 24.2 | M |
| WS-11 | El token no se expone | 22.2, 24.2 | A+M |
| WS-12 | Token vigente | 22.4, 24.3 | M |
| WS-13 | Token caducado al cargar | 22.4, 24.4 | M |
| WS-14 | Token mal formado | 22.4, 24.4 | M |
| WS-15 | Firma alterada | 24.4 (backend 11.3) | M |
| WS-16 | /fichajes sin sesión | 23.1, 24.2 | M |
| WS-17 | /equipo sin sesión | 23.1, 24.2 | M |
| WS-18 | Empleado en /equipo | 23.4, 24.2 | M |
| WS-19 | Responsable en /equipo | 23.4, 24.3 | M |
| WS-20 | Cabecera Bearer | 22.5, 24.2 | M |
| WS-21 | Login sin cabecera | 22.5, 24.2 | M |
| WS-22 | 401 con sesión | 22.5, 24.4 | M |
| WS-23 | Expiración local | 22.4, 24.4 | M |
| WS-24 | 403 no cierra la sesión | 22.5, 24.4 | M |
| WS-25 | Menú sin sesión | 23.1, 24.2 | M |
| WS-26 | Menú del empleado | 23.1, 24.2 | M |
| WS-27 | Menú del responsable | 23.1, 24.3 | M |
| WS-28 | Lista de miembros | 23.4, 24.3 | M |
| WS-29 | Fichajes de un miembro | 23.4, 24.3 | M |
| WS-30 | Cambio de miembro | 23.5 **[R4]**, 24.3 | M |
| WS-31 | Solo lectura | 23.4, 24.3 | M |
| WS-32 | Equipo vacío | 23.4, 24.3 | M |
| WS-33 | Miembro que ya no es del equipo | 23.5 **[R4]**, 24.3 | M |
| WS-34 | Cierre de sesión | 23.1, 24.2 | M |
| WS-35 | Ruta protegida tras cerrar | 23.1, 24.2 | M |
| WS-36 | Textos en español | 23.2–23.4, 24.2, 24.5 | M |
| WS-37 | Error inesperado | 24.5 | M |
| WS-38 | Compilación y formato | 23.6, 25.1 | C |
| WS-39 | Sin URL fija ni referencias | 22.2, 23.6 | A |
| WS-40 | Flujo manual del empleado | 24.2 | M |
| WS-41 | Flujo manual del responsable | 24.3 | M |

## startup-configuration (24)

| ID | Escenario | Tareas | Tipo |
|---|---|---|---|
| SC-1 | Falta la clave | 8.3 | A |
| SC-2 | Clave en blanco | 8.3 | A |
| SC-3 | Clave demasiado corta | 8.3 | A |
| SC-4 | Clave en el límite | 8.3 | A |
| SC-5 | El valor no se imprime (clave) | 8.3 | A |
| SC-6 | No versionada | 8.4 | A |
| SC-7 | Valor por defecto de la siembra | 16.1 | A |
| SC-8 | Valor no booleano | 16.1 | A |
| SC-9 | Siembra activa sin contraseña | 16.1 | A |
| SC-10 | Contraseña de demostración demasiado corta | 16.1 | A |
| SC-11 | Contraseña de demostración en el límite | 16.1 | A |
| SC-12 | Siembra desactivada ignora la contraseña | 16.1 | A |
| SC-13 | El valor no se imprime (contraseña) | 16.1 | A |
| SC-14 | Production con configuración completa | 12.1, 12.2 | A |
| SC-15 | Development con configuración completa | 12.1, 12.2 | A |
| SC-16 | Una clave Identity heredada no tiene efecto | 12.1, 12.2 | A |
| SC-17 | Valor tomado del entorno | 12.3 (prueba vigente adaptada) | A |
| SC-18 | Ningún secreto versionado | 8.4 | A |
| SC-19 | Claves documentadas | 20.1 | A |
| SC-20 | Claves de autenticación presentes | 20.1 | A |
| SC-21 | Sin identidad provisional | 20.1 | A |
| SC-22 | Valores de las claves de base de datos | 20.1 | A |
| SC-23 | Sin claves de IA activas | 20.1 | A |
| SC-24 | Puerto distinto | 20.5 | M |

## solution-architecture (27)

| ID | Escenario | Tareas | Tipo |
|---|---|---|---|
| SA-1 | Paquetes en su proyecto | 1.2, 1.3 | A |
| SA-2 | Application sigue sin paquetes | 1.3 (y prueba vigente) | A |
| SA-3 | Sin ASP.NET Core Identity | 1.2 | A |
| SA-4 | Sin vulnerabilidades | 1.3, 13.1, 21.1, 28.1 | C |
| SA-5 | Búsqueda sin coincidencias | 12.1, 12.2, 20.2, 27.1, 27.3 (Tandas 3a, 3b y 5) | A |
| SA-6 | Clases eliminadas | 12.1, 12.2 | A |
| SA-7 | Construcción de la imagen | 20.3 | M |
| SA-8 | Salud dentro del contenedor | 20.3 | M |
| SA-9 | Sin secretos en la imagen | 20.3 | M |
| SA-10 | Servicios definidos | 20.2 | A+M |
| SA-11 | Configuración de postgres | 20.2 | A |
| SA-12 | Puerto de la API solo local | 20.2, 20.4 | A+M |
| SA-13 | Claves de autenticación en compose | 20.2 | A |
| SA-14 | Sin credenciales en el archivo | 20.2 | A |
| SA-15 | Arranque ordenado y login de demostración | 20.4 | M |
| SA-16 | Build limpio | 4.1, 13.1, 21.1, 25.1, 28.1 | C |
| SA-17 | Pruebas en verde | 28.1 | C |
| SA-18 | Formato verificado | 4.1, 13.1, 21.1, 25.1, 28.1 | C |
| SA-19 | Web con sesión y fichajes | 22.2, 23.6 | A |
| SA-20 | ADR completos | 26.1, 26.2 **[R5]** | A |
| SA-21 | ADR 0009 sustituido | 26.1, 26.2 | A |
| SA-22 | Contenido de los ADR nuevos | 26.1, 26.2 **[R5]** | A |
| SA-23 | README ejecutable | 27.1, 27.2, 28.1 | A+M |
| SA-24 | README cubre la novedad | 27.1, 27.2, 27.3 | A |
| SA-25 | README sin identidad provisional | 27.1, 27.2 | A |
| SA-26 | Contrato generado en build | 8.3, 12.4, 19.3 | C |
| SA-27 | Sin efecto en la ejecución normal | 12.4 | A |

## time-tracking (14)

| ID | Escenario | Tareas | Tipo |
|---|---|---|---|
| TK-1 | El empleado sale del token | 12.1, 12.2 | A |
| TK-2 | Un employeeId del cliente se ignora | 12.1, 12.3 | A |
| TK-3 | Una cabecera de empleado se ignora | 12.1, 12.3 | A |
| TK-4 | Sin token | 12.1, 12.2 | A |
| TK-5 | El adaptador lee el claim sub | 9.3 | A |
| TK-6 | El adaptador sin usuario autenticado | 9.3 | A |
| TK-7 | El contrato no expone employeeId | 10.1, 19.3 | A |
| TK-8 | Entrada correcta | 12.3 | A |
| TK-9 | Entrada sin Location y coherente con el listado | 12.3 | A |
| TK-10 | Salida correcta | 12.3 | A |
| TK-11 | Minutos truncados en la salida | 12.3 | A |
| TK-12 | Listado con total | 3.2, 12.3 | A |
| TK-13 | Token caducado | 12.1, 14.1 (Tanda 3a/3b) | A |
| TK-14 | Contrato OpenAPI de fichajes | 10.1, 19.1, 19.3 (Tanda 3a/3b) | A |

## time-entries-persistence (14)

| ID | Escenario | Tareas | Tipo |
|---|---|---|---|
| PE-1 | Sin FK en time_entries | 5.5 | A |
| PE-2 | Fichaje huérfano aceptado | 5.5 | A |
| PE-3 | Sin cambios pendientes | 5.3 | A |
| PE-4 | Migración sobre base vacía | 5.2, 5.3 | A |
| PE-5 | AddUsers sobre una base con fichajes | 5.6 | A |
| PE-6 | Herramienta local | 5.3 (prueba vigente) | A |
| PE-7 | Bandera activada | 12.3 | A |
| PE-8 | Bandera por defecto | 12.3 (prueba vigente adaptada) | A |
| PE-9 | Migración fallida impide el arranque | 12.3 (prueba vigente adaptada) | A |
| PE-10 | Salud sin base de datos | 12.3 (prueba vigente) | A |
| PE-11 | Imagen fija | 20.2 | A |
| PE-12 | Suite verde con Docker | 28.1 | C |
| PE-13 | Correos únicos por prueba | 6.4 | A |
| PE-14 | README documenta el requisito | 27.1, 27.2 | A |

## time-entries-ui (13; verificación manual por decisión de la spec)

| ID | Escenario | Tareas | Tipo |
|---|---|---|---|
| UI-1 | Carga inicial con datos | 23.3, 24.2 | M |
| UI-2 | Sin fichajes | 23.3, 24.2 | M |
| UI-3 | Sin sesión | 23.1, 24.2 | M |
| UI-4 | Solo los fichajes propios | 23.3, 24.2 | M |
| UI-5 | Doble entrada | 22.3, 24.2 | M |
| UI-6 | Salida sin entrada | 22.3, 24.2 | M |
| UI-7 | API caída | 22.3, 24.5 | M |
| UI-8 | Sesión caducada en un botón | 22.5, 24.4 | M |
| UI-9 | ApiBaseUrl ausente o inválida | 24.5 | M |
| UI-10 | URL base | 24.1 | M |
| UI-11 | Sin referencias de proyecto | 22.2, 23.6 | A |
| UI-12 | Token en las peticiones | 22.5, 24.2 | M |
| UI-13 | Flujo de navegador registrado | 24.2, 24.3 | M |

## api-health (3)

| ID | Escenario | Tareas | Tipo |
|---|---|---|---|
| AH-1 | API en marcha | 11.6 | A |
| AH-2 | Método no permitido (cliente autenticado) | 11.6 | A |
| AH-3 | Método no permitido sin token | 11.6 | A |

## Resultado de la trazabilidad

**Escenarios sin mapear: ninguno.** Totales: 61 + 63 + 41 + 24 + 27 + 14 + 14 + 13 + 3 = **260**. Por tipo: **A** 189 · **A+M** 8 · **M** 55 · **C** 8.

Escenarios cuyo cumplimiento depende de un recorte: AU-35–AU-39 (R3), AU-40–AU-43 (R2), AU-56 (R1), WS-5 (R3), WS-30 y WS-33 (R4, parcial), SA-20 y SA-22 (R5).

Escenarios que se completan en más de una tanda: AU-55 y TK-14 (3a/3b), TK-13 y AU-47 (estado en 3a, `problem+json` en 3b), AU-22 (sonda sobre `/me` en 3a, literal sobre `/api/team/members` en 3b), AU-24 (sonda sobre `/me` en 11.4 y literal sobre `GET /api/time-entries` en 12.1, ambas en 3a), SA-5 (3a, 3b y 5); ver D12.

Escenarios que solo se pueden comprobar a mano: los 55 de tipo M (Web, imagen Docker, compose, puerto, AU-61). No tienen prueba automática por decisión de las specs (Web) o por no ser comprobables con `WebApplicationFactory` (puerto, compose).

Requisitos eliminados (sin escenarios propios) y su cobertura: «Identidad provisional mediante ICurrentUser» (`time-tracking`) → TK-1–TK-4, TK-7, SA-6; «Identidad provisional solo en Development» (`startup-configuration`) → SC-14–SC-16, SA-5. Requisitos sin escenario propio y su prueba: no registrar contraseñas ni hashes (10.3, 10.6, 16.4), `Application` sin paquetes (1.3).

# Resumen de tareas

| Tanda | Fases | Tareas | De ellas recortables |
|---|---|---|---|
| Tanda 1 | 1–4 | 31 | 0 |
| Tanda 2 | 5–7 | 12 | 0 |
| Tanda 3a | 8–13 | 26 | 0 |
| Tanda 3b | 14–21 | 31 | 17.1–17.3 (R3), 18.1–18.2 (R2), 19.1–19.2 en parte (R1) |
| Tanda 4 | 22–25 | 18 | 23.5 (R4) |
| Tanda 5 | 26–28 | 9 | 26.2 en parte (R5) |
| **Total** | **28** | **127** | |

Orden y paralelismo: dentro de cada tanda todo es secuencial por las dependencias de TDD y por haber un único escritor. Excepciones independientes: 8.4 respecto a 8.3; 10.1 y 15.1 (RED de contrato) pueden escribirse en cualquier momento tras 8.3; 16.1 no depende de la fase 15; las fases 17 y 18 son independientes entre sí y de las fases 15 y 16 una vez hechas 14.1 y 14.2; 20.1 y 20.2 solo necesitan 8.3 y pueden hacerse tras la fase 14; 20.4 exige las fases 15, 16 y 20.3; la Tanda 4 (salvo 24.x) no depende de 17–20; los ADR (26.x) pueden redactarse tras 21.2. El README (27.2) se cierra tras 20.4 y 24.x, que validan sus comandos.

# Desviaciones del diseño

- **D1.** Los paquetes `JwtBearer` y `Components.Authorization` se añaden en la Tanda 1 (1.2, 1.3), como dice la tabla de tandas del diseño §16 (V1/V2 en la Tanda 1), y no en 3a/4 como indica el §3.
- **D2.** `AspNetPasswordHasher`, `JwtAccessTokenIssuer` y `HttpContextCurrentUser` llevan archivos de prueba propios (`Authentication/AspNetPasswordHasherTests.cs`, `JwtAccessTokenIssuerTests.cs`, `HttpContextCurrentUserTests.cs`) que el diseño no lista; son necesarios para AU-1–AU-3, AU-14, AU-15–AU-17, TK-5 y TK-6.
- **D3.** Archivos de prueba no listados en el diseño §3: `Security/{FallbackPolicyTests,OpenApiExposureTests,SigningKeyLeakTests,AccessErrorsTests,LoginRateLimitTests,SecurityHeadersTests}.cs`, `Persistence/{UserSchemaTests,UserConstraintsTests,TimeEntriesForeignKeyTests}.cs` y `Startup/LegacyIdentityKeyTests.cs`. Este último contiene la clave heredada de SC-16 y la prueba de restos lo **exime por nombre** (la spec solo exime «la prueba que comprueba esta ausencia»). Se adaptan además pruebas existentes que el diseño no menciona: `HealthEndpointTests`, `StartupConfigurationTests`, `DatabaseStartupTests`, `DatabaseMigrationStartupTests`, `ErrorHandlingTests`, `TimeEntryEndpointsTests`, `TimeEntryConcurrencyTests`.
- **D4.** `UserRepository.FindByEmailAsync` normaliza de nuevo el correo (idempotente, además de `LoginUseCase`) para que TA-20 se cumpla literalmente con una entrada en mayúsculas; el diseño asume que el puerto recibe el correo ya normalizado.
- **D5.** En `LoginUseCase` «en blanco» se aplica también a la contraseña (`IsNullOrWhiteSpace`), como dice la spec (AU-7); el diseño (DD-7) hablaba de contraseña «vacía».
- **D6.** El ADR 0009 pasa a `- Estado: Sustituido por 0011` (texto literal del escenario SA-21); el diseño §13 proponía «Sustituido por el ADR 0011». El cuerpo no se reescribe.
- **D7.** El `FakeTimeProvider` (`Clock`) vive en la base `ApiFactory`, no solo en `PostgresApiFactory`: los tokens de prueba y la validación deben compartir reloj (DD-11) también en las pruebas sin base de datos.
- **D8.** `TestTokens` crece respecto al diseño: `subject` textual (para `no-es-un-guid`), algoritmo (HS512), `notBefore`/`expires` explícitos y `ReplacePayload`; se construye a mano con HMAC para ser independiente del emisor de producción.
- **D9.** `UserSeed` usa un hash calculado una vez y compartido (101 reportes en 15.4 serían lentos con PBKDF2) e incorpora `InsertRawAsync` (restricciones y hash ilegible).
- **D10.** AU-14 (hash ilegible → 401) no figuraba en el diseño: se añade por decisión del usuario a 9.1 (adaptador) y 10.3 (API); el `try/catch` del adaptador solo se escribe si el RED lo exige.
- **D11.** El límite de 100 miembros (TA-29) se parte en dos tareas por decisión del usuario: 3.9 (unitaria) y 15.4 (API).
- **D12.** Algunos escenarios se prueban en dos pasos porque su infraestructura llega en tandas distintas: AU-55 y TK-14 (200/400/401 en 3a; 429 y Bearer en 3b), AU-51/TK-13/AU-47 (estado en 3a; `problem+json` con `UseStatusCodePages` en 3b), AU-22 (sobre `/me` en 11.3 y sobre `/api/team/members` en 15.9) y AU-24 (sobre `/me` en 11.4 y sobre `GET /api/time-entries` en 12.1).
- **D13.** Copias de reversión: `..\TimeTracker.rollback\tanda-1`, `-2` y `-4` ya existen con las copias del cambio archivado. 1.1 las aparta a `..\TimeTracker.rollback\fichaje-extremo-a-extremo\` (movimiento, no borrado). `Save-Rollback` no sobrescribe y cada tanda copia el estado vigente al empezar, incluidos archivos creados por tandas anteriores.
- **D14.** Respaldo de V17: si, tras el limitador, alguna clase hace más de 10 logins por fábrica, `ApiFactory` incorpora un filtro de arranque de prueba que fija la IP remota desde `X-Test-Remote-Ip` (17.1).
- **D15.** Detalles que el diseño no fija y se deciden aquí: clave de `sessionStorage` `timetracker.accessToken`; nombres visibles sembrados «Marta Ejemplo», «Luis Ejemplo», «Ana Ejemplo» y «Pablo Ejemplo»; `/me` lleva `RequireAuthorization()` explícito en 10.5 y lo pierde en 11.6 (lo cubre el respaldo); `EnvironmentIsolation` deja de borrar `Identity__` y pasa a `Jwt__` y `Seed__`.
- **D16.** Las dudas abiertas del diseño §20 quedan resueltas: V13 se acepta (specs `authentication` y `api-health`), DD-24 se mantiene (spec `startup-configuration`: `.env.example` con `Seed__DemoPassword` vacía) y la previsión es 2.400–3.150 líneas.

# Riesgos

- **Calendario:** 12 días hasta el plazo del 2026-10-25 con una previsión de 6 a 8 veces el presupuesto de revisión. El orden de recorte R1–R5 no toca el backend ni las pruebas de seguridad; el disparador de 21.2 evita llegar tarde a la decisión.
- **Copias de reversión:** el apartado de las copias antiguas (1.1) es un paso con efecto fuera del repositorio; si falla, se para y se consulta.
- **Docker y rendimiento:** la integración exige Docker; PBKDF2 en pruebas (11 logins en 17.2, siembra, `UserSeed`) alarga la suite; el hash compartido de `UserSeed` mitiga el coste de los 101 reportes.
- **Limitador en pruebas (V17):** `TestServer` no tiene IP remota; una fábrica compartida con más de 10 logins daría `429` intermitentes. Una fábrica por prueba en `LoginRateLimitTests` y el filtro de IP de prueba como respaldo.
- **Comportamiento de librerías por confirmar:** `TimeProvider` en JwtBearer (V6), tiempos de `JsonWebTokenHandler` (V15), `Microsoft.OpenApi` 2.7.6 (V7), 400 del cuerpo roto y del cuerpo ausente sin `Content-Type` en Minimal API (V18), `StartAsync` frente a `StartingAsync` (V16). Cada una tiene respaldo previsto.
- **V13:** las rutas inexistentes y `POST /health` sin token responden `401`; aceptado y reflejado en las specs.
- **Colación del orden alfabético (6.3):** el orden por `DisplayName` depende de la colación de PostgreSQL; las pruebas usan nombres ASCII y la regla puede variar con acentos.
- **Web sin pruebas automáticas:** la lógica de `ReturnUrl.IsSafe` y de `JwtAuthenticationStateProvider` solo se verifica a mano (24.4). Sugerencia que requiere decisión del usuario (no incluida): enlazar `ReturnUrl.cs` en `AR` con `Compile Include` para probar `IsSafe` sin referenciar la Web.
- **`.env` no se lee:** la verificación de compose depende de variables de sesión; un `.env` antiguo con `Identity__DevelopmentEmployeeId` no tiene efecto (SC-16).
- **Prueba de restos:** `Code_HasNoProvisionalIdentityLeftovers` debe excluir `bin/` y `obj/` (los ensamblados y los `.json` generados contienen «Identity» y rutas antiguas).
- **`GetDocument.Insider`:** cada validador nuevo puede romper el build del contrato (lección D4 archivada); por eso `DocumentGeneration` se actualiza en la misma tarea que el validador.
- **Aceptados por el diseño:** XSS con token en `sessionStorage` (ADR 0012), sin refresh token (ADR 0011), contraseña de demostración pública (siembra desactivada por defecto), sin FK en `time_entries` (fichajes huérfanos; `docker compose down -v`), un usuario borrado conserva su token hasta 30 minutos, CI sin ejecutar y pruebas de entrega basadas en texto (sin analizador YAML).
- **Duplicación de pruebas:** candidatas a consolidar si falta tiempo, solo con aprobación del usuario: las pruebas de texto de `DeliveryArtifactsTests` y los casos repetidos entre 3.x (unitarias) y 15.x (API).

