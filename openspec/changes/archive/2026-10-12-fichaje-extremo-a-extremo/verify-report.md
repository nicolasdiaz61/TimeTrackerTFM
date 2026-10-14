```yaml
schema: gentle-ai.verify-result/v1
verdict: pass_with_warnings
blockers: 0
critical_findings: 0
requirements: 40/40 con evidencia (39 CUMPLE, 1 PARCIAL [SA-R7, CI])
scenarios: 124/124 con evidencia (122 CUMPLE, 2 PARCIAL [SA-21, SA-24], 0 INCUMPLE, 0 SIN EVIDENCIA)
test_command: dotnet test TimeTracker.sln -c Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults
test_exit_code: 0
test_output_hash: sha256:dbf0eb16a5a4df83ab403e8c6aee5697a90acc632195816f40f5586211a7033e
build_command: dotnet build TimeTracker.sln -c Release --no-restore -warnaserror
build_exit_code: 0
build_output_hash: sha256:74706c8c44db647464c8f4857e10413a103a4f582cb2f8d2541cf0d884c38485
```

# Informe de verificación: fichaje-extremo-a-extremo

**Cambio**: fichaje-extremo-a-extremo · **Modo**: openspec, Strict TDD · **Fecha**: 2026-10-12 (UTC 2026-10-13) · **Idioma**: español
**Alcance**: verificación independiente: los comandos se ejecutaron de nuevo en esta sesión (no se reutilizó la evidencia de `tasks.md` para lo que se puede ejecutar). No se modificó código ni especificaciones; solo este informe. No se ejecutó ningún comando Git (ni `git --version`); no existe `.git` en el repositorio. No se leyó ni imprimió `.env`.
**Persistencia**: este archivo (modo openspec). La sección "Verificación manual de UI" del informe preliminar se conserva íntegra al final.

## Veredicto

**PASS WITH WARNINGS. Se puede archivar.** 0 CRITICAL, 8 WARNING, 5 SUGGESTION.

Todas las puertas de calidad terminan en 0 (151 pruebas en verde, Domain 100 % y Application 100 %, 0 avisos, 0 vulnerabilidades), la imagen y Compose se comportan como exige la especificación, el flujo clock-in / 409 / clock-out / GET funciona contra PostgreSQL 18 real y el contrato OpenAPI regenerado es idéntico al versionado. Los avisos son límites ya admitidos (CI sin ejecutar, UI sin automatizar, recortes acordados) y dos textos de especificación que quedaron desfasados tras el recorte de `xmin`.

## 1. Completitud de tareas

| Métrica | Valor |
|---|---|
| Tareas con casilla en `tasks.md` | Todas `[x]`; 0 sin marcar (7.8, 9.7 y 10.11 figuran como «retirada» y no cuentan) |
| Requisitos contados en las 5 especificaciones | 40 (time-tracking 11, time-entries-persistence 9, time-entries-ui 5, solution-architecture 10, startup-configuration 5) |
| Escenarios contados | 124 (48 + 21 + 13 + 27 + 15); coincide con el recuento de `tasks.md` y con la tabla de trazabilidad (0 sin mapear) |
| Recortes acordados | Sin `xmin` (DD-9, PE-8/9/10, V12) y sin escenarios CORS (TT-46, SC-15..18). Coherentes en specs, design y tasks |

## 2. Ejecución real de comandos

Entorno comprobado limpio antes de ejecutar (ninguna variable `Cors__`, `ConnectionStrings__`, `Identity__`, `Database__`, `ASPNETCORE_*`, `DOTNET_ENVIRONMENT`). SDK 10.0.302. Docker 29.7.2.

| Comando | Código | Resultado |
|---|---|---|
| `dotnet restore TimeTracker.sln` | 0 | Correcto |
| `dotnet format TimeTracker.sln --verify-no-changes --no-restore` | 0 | Sin cambios |
| `dotnet build TimeTracker.sln -c Release --no-restore -warnaserror` | 0 | 0 advertencias, 0 errores |
| `dotnet test TimeTracker.sln -c Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults` (Docker en marcha, Testcontainers `postgres:18-alpine`) | 0 | Domain.Tests 27/27, Application.Tests 14/14, ArchitectureTests 44/44, Api.IntegrationTests 66/66 = **151 en verde, 0 fallos, 0 omitidas** (ningún `Skip` en `tests/`) |
| `pwsh scripts/check-coverage.ps1 … -Package TimeTracker.Domain -MinimumPercent 100` | 0 | líneas 100,0 %, ramas 100,0 % |
| `pwsh scripts/check-coverage.ps1 … -Package TimeTracker.Application -MinimumPercent 80` | 0 | líneas 100,0 %, ramas 100,0 % |
| `dotnet list TimeTracker.sln package --vulnerable --include-transitive` (`DOTNET_CLI_UI_LANGUAGE=en`) | 0 | «has no vulnerable packages» en los 9 proyectos; 0 apariciones de `has the following vulnerable packages` |
| `docker build -t timetracker-api:verify .` | 0 | Correcto |
| `dotnet build src/TimeTracker.Api -c Release --no-incremental -warnaserror` (regenera el contrato) | 0 | `docs/api/openapi.json` con SHA-256 idéntico antes y después (`102395ca…db8d`) |

Cobertura: 4 informes `coverage.cobertura.xml`. El de `Domain.Tests` muestra `TimeTracker.Domain` con `line-rate=1` y `branch-rate=1`; el de `Application.Tests` muestra `TimeTracker.Application` con `1` y `1` (el umbral se cumple solo con pruebas unitarias, V17). Infrastructure 95,2 % de líneas y Api 43,3 % (informe de integración), sin umbral por diseño. No hay `ExcludeFromCodeCoverage` en el código fuente propio (solo en archivos generados de `obj/`).

## 3. Docker y Compose (ejecutado en esta sesión)

| Comprobación | Resultado |
|---|---|
| Imagen: usuario | `User=1654` (no root) |
| Imagen: `HEALTHCHECK` | Presente (`wget … /health`) |
| Imagen: variables y capas | Solo `ASPNETCORE_HTTP_PORTS=8080` y las del marco; `docker history --no-trunc`: 0 coincidencias de `POSTGRES_PASSWORD`, `ConnectionStrings`, `Identity__` ni de la contraseña ficticia |
| `docker run` sin variables | Sale con código **139**; `AggregateException` nombra `Cors:AllowedOrigins` y `ConnectionStrings:Default` |
| **API en `Production`** con Cors, cadena e identidad definidas | Sale con código **139**; `OptionsValidationException: Identity: la identidad provisional solo está permitida en el entorno Development; configure la autenticación real.`; ninguna línea «Now listening» |
| `docker compose config --services` | `postgres`, `api` (exactamente dos) |
| `docker compose up -d --build --wait` | rc 0; `postgres` healthy y después `api` healthy; puertos `127.0.0.1:5432` y `127.0.0.1:8080` solamente |
| Valores ficticios | `POSTGRES_DB/USER/PASSWORD` y el GUID de identidad pasados por variables del proceso y un archivo `override` en la carpeta temporal de la sesión (Cors `https://app.example.test`); `.env` no se leyó |
| Migraciones | `\dt`: `__EFMigrationsHistory` y `time_entries`; 1 migración registrada; `\d time_entries`: 4 columnas, índice `ix_time_entries_employee_id_clock_in_at (employee_id, clock_in_at DESC)`, `ux_time_entries_employee_id_open` UNIQUE `WHERE clock_out_at IS NULL`, `CHECK ck_time_entries_clock_out_after_clock_in` |
| `GET /health` | 200 `{"status":"Healthy"}` |
| `POST clock-in` | **201** `application/json`, `clockInAt` terminado en `Z`, `clockOutAt` y `workedMinutes` nulos, **sin cabecera `Location`** |
| `POST clock-in` (segunda) | **409** `application/problem+json`, `title` «Ya existe un fichaje abierto», `detail` en español, sin traza ni `employeeId` |
| `POST clock-out?employeeId=…` con cabecera `X-Employee-Id` | 200 (ambos se ignoran: cierra el fichaje propio) |
| `POST clock-out` (sin abierta) | **409**, «No hay ningún fichaje abierto» |
| `GET /api/time-entries` | 200, un elemento cerrado, `workedMinutes` 0, `totalWorkedMinutes` 0 |
| Registros de la API | Solo `Regla de negocio incumplida: OpenTimeEntryAlreadyExistsException` a nivel `info`, sin `employeeId` |
| Limpieza | `docker compose down -v` (contenedores, red y volumen `timetracker_pgdata` eliminados); imágenes `timetracker-api:verify` y `:local` eliminadas; `TestResults/` eliminada; sin contenedores ni imágenes del proyecto. No se tocaron los contenedores ni las imágenes ajenos |

## 4. Matriz de cumplimiento por requisito (40)

Criterio: un escenario es CUMPLE si una prueba automática lo cubre y pasó en esta ejecución, o si la propia especificación (o `tasks.md`) lo declara de verificación manual y existe evidencia registrada. «M-previa» indica evidencia manual de apply no repetida en esta sesión (por falta de navegador o por ser una demostración única revertida).

| # | Capacidad · Requisito | Estado | Evidencia |
|---|---|---|---|
| TT-R1 | time-tracking · Inicio de un fichaje en el dominio | CUMPLE | `TimeEntryTests` (Start_*) |
| TT-R2 | Cierre de un fichaje en el dominio | CUMPLE | `TimeEntryTests` (Close_*) |
| TT-R3 | Cálculo de minutos trabajados truncados | CUMPLE | `TimeEntryTests` (CalculateWorked_*) |
| TT-R4 | Caso de uso ClockIn | CUMPLE | `ClockInUseCaseTests` |
| TT-R5 | Caso de uso ClockOut | CUMPLE | `ClockOutUseCaseTests` |
| TT-R6 | GetTimeEntries y total | CUMPLE | `GetTimeEntriesUseCaseTests` |
| TT-R7 | Identidad provisional (`ICurrentUser`) | CUMPLE | `TimeEntryIsolationTests`, `OpenApiContractTests`, comprobación manual con `?employeeId=` y `X-Employee-Id` |
| TT-R8 | Aislamiento entre empleados | CUMPLE | `TimeEntryIsolationTests`, `ClockIn/ClockOutUseCaseTests` |
| TT-R9 | Contrato REST de fichajes | CUMPLE | `TimeEntryEndpointsTests`, `OpenApiContractTests`, flujo en Compose |
| TT-R10 | Errores 409 como ProblemDetails en español | CUMPLE | `TimeEntryEndpointsTests`, `ErrorHandlingTests`, flujo en Compose |
| TT-R11 | Flujo completo y pruebas | CUMPLE | `Flow_ClockInListClockOutList`; cobertura Domain 100 / Application 100 |
| PE-R1 | persistence · Modelo de datos con instantes UTC | CUMPLE | `MigrationTests`, `TimeEntryRepositoryTests`, `\d time_entries` |
| PE-R2 | Índices de consulta y de entrada abierta única | CUMPLE | `MigrationTests`, `\d time_entries` |
| PE-R3 | Traducción de conflicto de entrada duplicada | CUMPLE | `TimeEntryRepositoryTests` (incluye «otra violación única no se traduce») |
| PE-R4 | Repositorio acotado por empleado | CUMPLE | `TimeEntryRepositoryTests` |
| PE-R5 | Migraciones reales y sin cambios pendientes | CUMPLE | `MigrationTests`, `DotnetTools_DeclaresDotnetEf_10_0_12` |
| PE-R6 | Bandera de migraciones al arrancar | CUMPLE | `DatabaseMigrationStartupTests`; Compose aplicó la migración |
| PE-R7 | Conexión perezosa y salud independiente | CUMPLE | `HealthEndpointTests` |
| PE-R8 | Carrera de doble fichaje | CUMPLE | `AddAsync_TenParallelOpenInserts…`, `ClockIn_TenParallel_OneCreatedNineConflict` |
| PE-R9 | Pruebas con Testcontainers | CUMPLE (con W5) | `PostgresFixtureTests`, suite verde con Docker |
| UI-R1 | ui · Página /fichajes con listado y total | CUMPLE | M-previa (UI-1, UI-2), `Web_HasTimeEntriesPageClientAndApiBaseUrl` |
| UI-R2 | Botones de fichaje | CUMPLE | M-previa (UI-3, UI-4) |
| UI-R3 | Presentación en UTC y «en curso» | CUMPLE | M-previa (UI-5, UI-6) |
| UI-R4 | Mensajes de error en español | CUMPLE | M-previa (UI-7, UI-8, UI-9) |
| UI-R5 | Cliente tipado y URL base | CUMPLE | M-previa + `Web_DoesNotHardcodeApiUrl`, `Web_HasNoProjectReferences` |
| SA-R1 | solution-architecture · Docker Compose con API y PostgreSQL | CUMPLE | `DeliveryArtifactsTests`, Compose ejecutado en esta sesión |
| SA-R2 | Regla de dependencias por referencias de proyecto | CUMPLE | `DependencyRuleTests`, `RepositoryConventionsTests`; M-previa (`dotnet list reference`) |
| SA-R3 | Prueba de arquitectura por reflexión | CUMPLE | `DependencyRuleTests`, `Domain_And_Application_DeclareNoPackageOrFrameworkReferences` |
| SA-R4 | Puerta de calidad local | CUMPLE | restore, format, build y test en 0 |
| SA-R5 | Informe de cobertura | CUMPLE | `check-coverage.ps1` Domain y Application en 0 |
| SA-R6 | Imagen Docker de la API | CUMPLE | Sección 3 |
| SA-R7 | Flujo de CI | PARCIAL | Pasos y orden verificados por texto y lectura de `ci.yml`; no ejecutada, sin analizador YAML (W3) |
| SA-R8 | ADR y README | CUMPLE (con W1) | 10 ADR con 4 secciones (`Adr_Directory_HasTenAdrsWithFourSections`), README verificado por prueba y lectura |
| SA-R9 | Exportación OpenAPI sin base de datos ni identidad real | CUMPLE | Build en entorno limpio rc 0 y contrato idéntico; `Production` falla |
| SA-R10 | Entrada RENAMED «Docker Compose con solo la API» a «Docker Compose con API y PostgreSQL» (cuenta como encabezado de requisito en el delta; no es un requisito distinto de SA-R1) | CUMPLE | Nombre nuevo en uso; ver SA-R1 |
| SC-R1 | startup-configuration · Cadena de conexión validada al arrancar | CUMPLE | `DatabaseStartupTests` |
| SC-R2 | Identidad provisional solo en Development | CUMPLE | `IdentityStartupTests`; `docker run` en `Production` |
| SC-R3 | Bandera de migraciones al arrancar | CUMPLE | `DatabaseStartupTests` |
| SC-R4 | `.env.example` (MODIFIED) | CUMPLE | `DeliveryArtifactsTests` (5 pruebas de `.env.example`) |
| SC-R5 | Puerto configurable (MODIFIED) | CUMPLE | M-previa (SC-19, tarea 13.2) |

### Escenarios (124)

**time-tracking (48):**

| IDs | Estado | Prueba |
|---|---|---|
| TT-1 a TT-16 | CUMPLE | `TimeEntryTests` (14 métodos / Theory) |
| TT-17 a TT-27 y nivel unitario de TT-33 a TT-35 | CUMPLE | `ClockIn/ClockOut/GetTimeEntriesUseCaseTests` |
| TT-28 a TT-30, TT-32 a TT-35 | CUMPLE | `TimeEntryIsolationTests` (5) y `IdentityStartupTests`; comprobado a mano en Compose |
| TT-31, TT-41 | CUMPLE | `OpenApiContractTests` (3); `openapi.json`: 0 apariciones de `employeeId`, 2 `date-time`, 2 respuestas `409` |
| TT-36 a TT-40 | CUMPLE | `TimeEntryEndpointsTests` (9); sin `Location` confirmado a mano |
| TT-42 a TT-45, TT-47 | CUMPLE | `TimeEntryEndpointsTests`, `ErrorHandlingTests` (2) |
| TT-48 | CUMPLE | `Flow_ClockInListClockOutList` |
| TT-49 | CUMPLE | `check-coverage.ps1` (100 / 100) |

**time-entries-persistence (21):** PE-1, PE-2, PE-4, PE-5, PE-6, PE-13, PE-14 CUMPLE (`MigrationTests`, 8); PE-3, PE-7, PE-11, PE-12, PE-20 CUMPLE (`TimeEntryRepositoryTests`, 8); PE-15 CUMPLE (`RepositoryConventionsTests`); PE-16 a PE-18 CUMPLE (`DatabaseMigrationStartupTests`, 4); PE-19 CUMPLE (`HealthEndpointTests`); PE-21 CUMPLE (`TimeEntryConcurrencyTests`; depende del entrelazado de hilos, S3); PE-22 CUMPLE (fixture y compose usan `postgres:18-alpine`, `DeliveryArtifactsTests`); PE-23 CUMPLE (suite en verde con Docker); PE-24 CUMPLE (`Readme_DocumentsFichajesDockerRequirementAndRequiredVariables` y lectura del README).

**time-entries-ui (13):** UI-1 a UI-10 y UI-13 CUMPLE (manual, M-previa: Chrome real, detalle en la sección «Verificación manual de UI»; UI-9 con la salvedad allí indicada). UI-11 y UI-12 CUMPLE (automática y manual: `Web_DoesNotHardcodeApiUrl`, `Web_HasNoProjectReferences`).

**solution-architecture (27):**

| IDs | Estado | Evidencia |
|---|---|---|
| SA-1 a SA-4 | CUMPLE | `Compose_*` (`DeliveryArtifactsTests`) y `docker compose config --services` = `api`, `postgres` |
| SA-5 | CUMPLE | `docker compose up -d --wait` ejecutado en esta sesión (healthy, migraciones, `/health` 200) |
| SA-6, SA-8 | CUMPLE | `RepositoryConventionsTests` y M-previa (`dotnet list … reference` / `package`) |
| SA-7, SA-10, SA-11 | CUMPLE | Demostraciones únicas revertidas (M-previa, tarea 4.3); SA-11 además por `Domain_And_Application_DeclareNoPackageOrFrameworkReferences` |
| SA-9 | CUMPLE | `DependencyRuleTests` |
| SA-12 a SA-14, SA-16, SA-17 | CUMPLE | Sección 2 |
| SA-15 | CUMPLE | `Web_HasTimeEntriesPageClientAndApiBaseUrl` |
| SA-18, SA-20 | CUMPLE | Sección 3 (reejecutado) |
| SA-19 | CUMPLE | M-previa (tarea 12.4: `/health` 200 y `healthy` en contenedor); no repetida |
| SA-21 | PARCIAL | `Ci_RunsApplicationCoverageAfterTestsAndBeforeAudit`; «válido como YAML» no se puede comprobar sin analizador (W3) |
| SA-22 | CUMPLE | Misma prueba y lectura de `ci.yml` |
| SA-23 | CUMPLE | `Adr_Directory_HasTenAdrsWithFourSections` y `config.yaml` actualizado |
| SA-24 | PARCIAL | Se ejecutaron los pasos, pero no literalmente el de `docker compose up -d postgres` con la contraseña del `.env` (W6) |
| SA-25 | CUMPLE | `Readme_Documents…` |
| SA-26, SA-27 | CUMPLE | Build limpio en entorno limpio; `Production` sin cadena falla (contenedor y M-previa) |

**startup-configuration (15):** SC-1 a SC-4 CUMPLE (`DatabaseStartupTests`); SC-5 CUMPLE (`AppSettings_ContainNoConnectionStringsIdentityOrDatabaseKeys`); SC-6 a SC-9 CUMPLE (`IdentityStartupTests`; SC-9 además `docker run` en `Production`, código 139 y mensaje con `Identity`); SC-10, SC-11 CUMPLE (`DatabaseStartupTests`); SC-12 a SC-14 CUMPLE (`DeliveryArtifactsTests`); SC-19 CUMPLE (M-previa, tarea 13.2; no repetida).

**Resumen**: 124 escenarios: 122 CUMPLE, 2 PARCIAL (SA-21, SA-24), 0 INCUMPLE, 0 SIN EVIDENCIA. Los de UI, SA-5/18/19/20, SA-6/7/10/11, SA-24, SA-26/27 y SC-19 llevan evidencia manual (sola o combinada); el resto, prueba automática. Requisitos: 40 encabezados en las especificaciones (SA-R10 es el encabezado RENAMED del delta); 39 CUMPLE y 1 PARCIAL (SA-R7).

## 5. Coherencia diseño y código

| Decisión | Coherente | Observación |
|---|---|---|
| DD-1 Identidad `ICurrentUser` + `DevelopmentCurrentUser`; validador siempre registrado | Sí | Solo se registra en Development; `Production` no arranca (reproducido) |
| DD-2/3/4/5 Modelo, constructor privado, UUID v7, truncado | Sí | El `Id` devuelto es versión 7 (`…-73bb-…`) |
| DD-6 `TimeProvider` | Sí | Application sin paquetes; Domain sin referencias |
| DD-7 Excepciones tipadas a `IExceptionHandler` único | Sí | `DomainRuleExceptionHandler` con un solo `is DomainRuleException` |
| DD-8 Índice único parcial y traducción por nombre | Sí | Filtro por `ConstraintName` |
| DD-9 Diferida | Sí | Sin `xmin` en código ni migración; `UpdateAsync` simple |
| DD-12 `IHostedLifecycleService.StartingAsync` | Sí | Probado también con `WebApplicationFactory` |
| DD-14/15 `DateTime` UTC con `Z`; 201 sin `Location` | Sí | Confirmado a mano |
| DD-16 `DocumentGeneration` | Sí | Contrato regenerado en entorno limpio |
| DD-17 Cliente con `HttpClient` propio | Sí | Verificado en UI |
| DD-18 Prueba de arquitectura reforzada | Sí | Pruebas presentes y rojos demostrados en apply |
| §8 Registro del error inesperado con `LoggerMessage` propio | Desviación aceptada | `ExceptionHandlerMiddleware` ya lo registra; anotada en `tasks.md` 10.10, no en `design.md` (S2) |
| §10 Compose | Sí | Puertos en `127.0.0.1`, referencias obligatorias a las variables `POSTGRES_*` sin literales, volumen `pgdata` montado en `/var/lib/postgresql` |

## 6. AGENTS.md y amenazas

| Regla | Resultado |
|---|---|
| Identidad desde el servidor (§6) | CUMPLE: ningún endpoint, parámetro, cabecera ni cuerpo con `employeeId`; contrato sin `employeeId` (0 apariciones); `?employeeId=` y `X-Employee-Id` ignorados (a mano y con prueba) |
| Errores sin trazas (§6) | CUMPLE: 409 y 500 genéricos (`ErrorHandlingTests`, flujo real); logs sin `EmployeeId`; `FromSqlRaw`, `ExecuteSqlRaw`, `EnableSensitiveDataLogging`, `NotImplementedException`: 0 en `src/` |
| Secretos fuera del repositorio | CUMPLE: imagen sin secretos; compose sin literales; `.env.example` sin valores reales (pruebas); `appsettings*.json` sin claves de conexión ni identidad |
| Clean Architecture | CUMPLE: Domain y Application sin paquetes ni marcos; Web sin referencias de proyecto |
| Sin menciones del curso ni datos de empresas reales | CUMPLE en código, pruebas, README, ADR, `docs/`, specs y `config.yaml`. Ver W7 por «TFM» en artefactos SDD del cambio |
| No existe `.git` | CUMPLE (no hay carpeta `.git`; solo `.gitignore`, `.gitattributes` y `.github`) |
| README veraz | CUMPLE: coincide con compose, variables y comandos; declara CI «pendiente», despliegue, vídeo y usuario de prueba «pendiente»; aviso de identidad provisional. No menciona el riesgo residual de `clock-out` (S1) |
| Límite de tasa (§6) | Diferido y declarado (riesgo residual aceptado en design §14) |

## 7. Strict TDD (plausibilidad)

`tasks.md` registra RED con mensaje real y GREEN en cada paso de Domain (2.1 a 2.14), Application, repositorio, arranque y endpoints; las pruebas que pasaron a la primera llevan mutación temporal con rojo y reversión (2.6, 2.13, 2.14, 3.4, 3.7, 3.8, 3.11, 7.9, 9.2, 9.4, 10.5 a 10.10, 12.2, 12.3). Plausible y coherente con el código: las pruebas existen con los nombres citados, no hay `Skip`, y el recuento final (27 + 14 + 44 + 66 = 151) coincide con 17.1. Hubo un ajuste honesto (10.6: la mutación no discriminaba con 30 s y se añadió el caso de 59 s). Los pasos de Web (14.x) no tienen RED automático por decisión de la spec (sin bUnit). No se reejecutaron las mutaciones en esta verificación.

## 8. Riesgo residual del recorte de `xmin` (DD-9)

Documentado en `design.md` (DD-9, §14, V12 retirada), `tasks.md` (recortes, D9, riesgos), ADR 0008 (consecuencias y alternativas), `config.yaml` y la nota de `time-entries-persistence`. Efecto: dos `clock-out` simultáneos del mismo fichaje pueden dar dos `200` y la última escritura fija `clock_out_at`; la `CHECK` y las reglas de Domain siguen cumpliéndose y no hay segunda fila. La entrada sigue protegida por el índice único parcial (probado con 10 inserciones paralelas y 10 `POST` paralelos). Verificado en código: sin columna de concurrencia (`Columns_AreExactlyFour`) y `UpdateAsync` simple. Severidad baja con un único cliente y sin edición de fichajes; ninguna prueba fija el comportamiento actual. Riesgo aceptado por el usuario (opción A, 2026-10-12).

## 9. Hallazgos

### CRITICAL

Ninguno.

### WARNING

- **W1.** `specs/solution-architecture/spec.md`, requisito «ADR y README»: sigue diciendo que el ADR 0008 cubre `xmin`, pero la concurrencia de la salida se retiró; el ADR 0008 solo lo cita como diferido. Corregir el texto al fusionar el delta (quitar `xmin` de la lista).
- **W2.** El `Purpose` de la spec vigente `openspec/specs/solution-architecture/spec.md` (excluye PostgreSQL, EF Core y la pantalla de marcajes) queda obsoleto: debe actualizarse al archivar (ya lo anota el delta). Además, al archivar hay que fusionar 3 specs nuevas y 2 deltas (con RENAMED de compose).
- **W3.** La CI nunca se ha ejecutado (sin remoto); `pwsh` de `ubuntu-latest`, `actions/checkout@v7` y `setup-dotnet@v6` sin verificar; no hay analizador YAML, así que SA-21 («válido como YAML») queda PARCIAL. Los pasos se reprodujeron en local.
- **W4.** La Web no tiene pruebas automáticas (sin bUnit ni E2E, por decisión de la spec); los 13 escenarios de UI descansan en la verificación manual del 2026-10-12, que esta sesión no repitió (sin navegador disponible). Sin verificar: aspecto visual, estado intermedio de botones deshabilitados y el panel de error de Blazor en producción (publicado). UI-9 se infiere de que la pantalla siguió operativa.
- **W5.** El fallo claro sin Docker (requisito «Pruebas con Testcontainers») solo se comprobó con un sustituto (imagen inexistente: `InvalidOperationException … requieren Docker en marcha`): un `DOCKER_HOST` inválido no lo reproduce porque Testcontainers 4.16 recurre a otro proveedor. No se omite en silencio (0 `Skip`).
- **W6.** SA-24 (README ejecutable): no se siguió literalmente el paso `docker compose up -d postgres` con la contraseña del `.env` temporal; se usó un PostgreSQL con `docker run` y la ruta `docker compose up` con valores propios. Esta verificación sí ejecutó `up -d --wait` con valores ficticios por entorno.
- **W7.** Los artefactos SDD del cambio (`exploration.md` línea 42, `proposal.md` línea 7, `tasks.md` D11) mencionan «TFM». El usuario aceptó la misma mención en el cambio archivado (W6 de aquel informe); no hay menciones en código, README, ADR, specs ni `config.yaml`. Decidir si se retira antes de archivar.
- **W8.** Limpieza pendiente fuera del control de esta verificación: el `.env` temporal (solo valores ficticios, según `tasks.md`; no se leyó) sigue en la raíz, porque `docker-compose.yml` lo exige en `env_file` y `up` funcionó; borrarlo a mano si no se necesita (está en `.gitignore` y `.dockerignore`). La carpeta `..\TimeTracker.rollback` (tandas 1 a 4) se conserva hasta archivar.

### SUGGESTION

- **S1.** Mencionar en el README (o en un seguimiento) el riesgo residual del doble `clock-out` simultáneo.
- **S2.** Reflejar en `design.md` las desviaciones registradas solo en `tasks.md` (D1, D4, D5, D7 y el logging del 500 en §8) para que el diseño no quede desactualizado; AGENTS §8 trata la documentación obsoleta como defecto.
- **S3.** PE-21 depende del entrelazado de hilos; pasó 3 ejecuciones en apply y 1 en esta sesión. Si apareciera intermitencia, basta con la variante determinista PE-20 (ya existe).
- **S4.** Abrir el cambio de seguimiento para `xmin` (DD-9) y para el límite de tasa antes de cualquier despliegue; fijar con una prueba el comportamiento actual de doble salida si se decide aceptarlo de forma permanente.
- **S5.** El presupuesto de revisión se superó de lejos (previsión ≈ 2.380 a 3.060 líneas; real ≈ 690 + 510 + 1.445 + 590 por tanda) con `size:exception` aceptado; para el siguiente cambio conviene dividir en entregas más pequeñas.

## 10. ¿Se puede archivar?

**Sí.** No hay CRITICAL ni escenarios INCUMPLE o SIN EVIDENCIA. Antes de archivar, conviene: corregir W1 en la spec, actualizar el `Purpose` (W2), decidir sobre W7 y borrar el `.env` temporal si procede (W8). El archivado debe advertir de que fusiona deltas con RENAMED/MODIFIED (regla de `config.yaml`).

---

# Verificación manual de UI (informe preliminar conservado)

Estado: **incorporada** al informe completo. El texto siguiente es el del informe preliminar (tarea 14.6), sin cambios.

## Verificación manual de UI

Fecha: 2026-10-12 (UTC 2026-10-13 00:43). Ejecutada de verdad, con valores ficticios.

### Entorno

- PostgreSQL: contenedor `postgres:18-alpine` (`docker run`, `127.0.0.1:5433`, contraseña ficticia). **Desviación:** no se usó `docker compose up -d postgres` para no leer ni depender del `.env` temporal de la Tanda 3; el contenedor se llamó `tt-ui-pg` y se eliminó al terminar.
- API: `dotnet run --project src/TimeTracker.Api --launch-profile http --no-build` (`Development`, `http://localhost:5274`) con `ConnectionStrings__Default` (apunta a `localhost:5433`), `Identity__DevelopmentEmployeeId` (GUID nuevo), `Database__ApplyMigrationsOnStartup=true` y `Cors__AllowedOrigins__0=http://localhost:5172`.
- Web: `dotnet run --project src/TimeTracker.Web --launch-profile http` (`http://localhost:5172`).
- Navegador: las herramientas `mcp__claude-in-chrome` **no estaban disponibles** para este ejecutor. **Respaldo aplicado:** Google Chrome real en modo headless (perfil temporal propio) controlado con el protocolo Chrome DevTools (Node, script en la carpeta temporal de la sesión; sin instalar nada). Se leyeron el DOM renderizado por Blazor WebAssembly, la consola y la red del navegador. Es ejecución real de la Web en un navegador, no una simulación; no es una captura de pantalla ni una revisión estética.

### Resultados por escenario

| ID | Escenario | Pasos y resultado | Estado |
|---|---|---|---|
| UI-2 | Sin fichajes | Abrir `/fichajes` con la base vacía: sin filas; «No hay fichajes registrados.» y «Total trabajado: 0 minutos (0:00)». | Verificado |
| UI-3 | Fichar entrada | Pulsar «Fichar entrada»: fila nueva `2026-10-13 00:43 UTC` / `en curso` / sin minutos. | Verificado |
| UI-7 | Doble entrada | Pulsar «Fichar entrada» de nuevo: alerta «Registre la salida antes de fichar una nueva entrada.» (detail de la API); el listado no cambia. | Verificado |
| UI-4 | Fichar salida | Pulsar «Fichar salida»: la fila muestra hora de salida y `0` minutos (entrada y salida en el mismo minuto); la alerta desaparece. | Verificado |
| UI-8 | Salida sin entrada | Pulsar «Fichar salida» sin abierta: alerta «Registre la entrada antes de fichar la salida.» | Verificado |
| UI-1 | Carga inicial con datos | Dos cerrados y uno abierto de la sesión de fichajes, más dos cerrados sembrados con SQL (08:00:00→09:30:59 y 10:00:00→10:45:00 del 2026-10-10). Orden idéntico al de la API (más reciente primero); total `135 minutos (2:15)` = `totalWorkedMinutes` de la API (90 + 45; el abierto no suma). | Verificado |
| UI-5 | Hora etiquetada | Se lee `2026-10-10 08:00 UTC` / `2026-10-10 09:30 UTC` (API: `…T08:00:00Z`), sin desplazamiento de zona. | Verificado |
| UI-6 | Fichaje en curso | Fila abierta: salida `en curso`, sin minutos, fuera del total. | Verificado |
| UI-13 | Flujo de navegador / CORS (V11) | Consola del navegador sin errores de CORS: `GET`, `POST clock-in` y `POST clock-out` con `Access-Control-Allow-Origin: http://localhost:5172`; también las respuestas **409** llevan esa cabecera (la Web mostró el `detail` en español, lo que no ocurriría si el navegador hubiera bloqueado la respuesta). La única entrada de consola con nivel error es el aviso estándar de Chrome «Failed to load resource: 409» (esperado). Los `POST` sin cuerpo no generan preflight. V11 resuelta. | Verificado |
| UI-11 | URL base | Pestaña Red: todas las llamadas van a `http://localhost:5274/api/time-entries…`; `appsettings.json` y `appsettings.Development.json` se piden a la Web. Prueba automática `Web_DoesNotHardcodeApiUrl` en verde. | Verificado |
| UI-9 | API caída | Con la API detenida: al cargar `/fichajes` y tras pulsar «Fichar entrada» aparece «No se pudo completar la operación. Inténtelo de nuevo más tarde.»; sin trazas; los botones siguen activos y no aparece el panel de error de Blazor. Nota: la instantánea tras pulsar es idéntica a la de la carga, de modo que el efecto del clic se infiere de que la pantalla siguió operativa, no de un cambio visible. | Verificado (con la salvedad indicada) |
| UI-10 | `ApiBaseUrl` ausente o inválida | Con `"ApiBaseUrl": "no-es-una-url"` y con la clave ausente (en los dos `appsettings`): la Web no arranca, aparece el panel «An unhandled error has occurred» y la consola muestra `Falta la configuración «ApiBaseUrl» o no es una URL absoluta http/https. Defínala en wwwroot/appsettings.json.` Blazor WebAssembly envuelve la excepción en una `AggregateException` en la consola; el mensaje en español con `ApiBaseUrl` se conserva. Archivos restaurados a `http://localhost:5274`. | Verificado |
| UI-12 | Sin referencias de proyecto | `dotnet list src/TimeTracker.Web/TimeTracker.Web.csproj reference` → «No hay ninguna referencia Proyecto a proyecto». Prueba automática `Web_HasNoProjectReferences` en verde. | Verificado |

### No verificado

- Aspecto visual (maquetación, estilos, responsive): no se revisó con capturas; solo se comprobó el DOM, los textos y el estado de los botones.
- Botones deshabilitados durante la petición (DEBERÍA, 14.5): con un servidor local la petición dura milisegundos, así que el estado intermedio no se observó; el código fija `disabled` mientras `busy`.
- Texto de depuración del panel de error de Blazor en producción (publicado): no se probó fuera del servidor de desarrollo.

### Limpieza

API y Web detenidas (puertos 5172 y 5274 libres), contenedor `tt-ui-pg` eliminado, `docker compose down -v` ejecutado, Chrome headless cerrado, variables de entorno solo en el proceso hijo (la sesión no las define).
