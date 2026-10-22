# Progreso de apply: multi-compania

Fase: sdd-apply · Modo: openspec · **Strict TDD** · Rama de la PR 6: `feat/multi-compania-pr6-empleados` (apilada sobre la PR 5, `feat/multi-compania-pr5-registro`, PR #42) · Rama de la PR 5: `feat/multi-compania-pr5-registro` (apilada sobre la PR 4b, `feat/multi-compania-pr4b-runtime`, PR #41) · Rama de la PR 1: `feat/multi-compania-pr1-modelo` (sale de `main` 9e572b8) · Rama de la PR 1b: `feat/multi-compania-pr1b-contraer` (apilada sobre la anterior) · Rama de la PR 3: `feat/multi-compania-pr3-aislamiento` (apilada sobre la PR 2) · Fecha: 2026-10-21

## Estado acumulado

| PR | Tareas | Estado |
|---|---|---|
| PR 1 | 1.1, 1.5 a 1.13 | **Hechas** (11 de 15) |
| PR 1 | 1.2 (V-1), 1.3 (V-4, parte Railway), 1.4 (V-6), 1.4b (V-3) | **Pendientes del responsable** (puertas previas al despliegue; apply no accede a producción ni a Railway) |
| PR 1b | 1b.1, 1b.4 a 1b.10 | **Hechas** (10 de 12; ver la sección «PR 1b» al final) |
| PR 1b | 1b.2 (puertas previas de despliegue), 1b.3 (reconfirmación de V-3 con el responsable; la parte local está hecha) | **Pendientes del responsable** (apply no accede a producción ni a Railway) |
| PR 2 | 2.1 a 2.11 (con 2.9b) | **Hechas** (12 de 12; ver la sección PR 2 al final; sin tareas de producción) |
| PR 3 | 3.1 a 3.12 | **Hechas** (12 de 12; ver la sección PR 3 al final; sin tareas de producción) |
| PR 4a | 4a.1 a 4a.14 | **Hechas** (ver la sección PR 4a al final) |
| PR 4b | 4b.1 a 4b.13 | **Hechas** (13 de 13; ver la sección PR 4b al final; sin tareas de producción) |
| PR 5 | 5.1 a 5.8 (con 5.6b y 5.7b), 5.10, 5.11, 5.11b, 5.12, 5.13, 5.14 | **Hechas** (16 de 17; ver la sección PR 5 al final) |
| PR 5 | 5.9 (V-7, `ForwardedHeaders` en producción) | **Pendiente del responsable** (puerta previa a la apertura del registro; apply no accede a Railway) |
| PR 6 | 6.1 a 6.16 (con 6.15b) | **Hechas** (17 de 17; ver la sección PR 6 al final; sin tareas de producción) |
| PR 7 | 7.1 a 7.8 (con 7.3b y 7.4b) | **Hechas** (ver la sección PR 7) |
| PR 8 | 8.1 a 8.9 | **Hechas** (ver la sección PR 8 al final; documentación, sin tareas de producción) |
| Tarea 9 | todas | Sin empezar (despliegue y apertura: responsable) |

## Lo que queda al responsable (puertas previas, sin marcar en `tasks.md`)

- **1.2 V-1** — contenido de producción (solo lectura). Es puerta de la **1b** (huérfanos, Q-1); no bloquea la PR 1.
- **1.3 V-4 (Railway)** — versión mayor de PostgreSQL de Railway (>= 11) y `pg_dump` compatible. La parte local está hecha y anotada en `design.md`. **Puerta previa del despliegue de la PR 1.**
- **1.4 V-6** — valor de `Database__ApplyMigrationsOnStartup` en producción, solape de instancias, `source.repo = null`, healthcheck `/health`. **Puerta previa del despliegue de la PR 1.**
- **1.4b V-3** — vía real para ejecutar scripts y `Down` en producción con `PSQL`. **Bloquea el despliegue de la PR 1.**
- Antes de desplegar la PR 1 conviene además decidir si se repite el ensayo (V-11 parte 1) con una copia real (tarea 9.3); el ensayo local con datos sembrados ya está hecho y es repetible (`AddCompaniesRehearsalTests`).

## Commits de esta entrega

| Commit | Mensaje | Contenido |
|---|---|---|
| `56d5a37` | `feat(domain): entidad Company con normalización de nombre y compañía por defecto` | Tarea 1.5: `Company`, `CompanyStatus`, `Company.DefaultId`, pruebas de dominio |
| `ce3fa5a` | `feat(multi-compania): compañía en usuarios y fichajes con migración A «expandir»` | Tareas 1.6 a 1.12: dominio de `UserAccount`/`TimeEntry`, puertos, modelo y migración A, siembra, pruebas (modelo, migración, ensayo, contexto, siembra) y todos los llamadores |
| `6e313cc` | `test(multi-compania): restricciones de la tabla companies en la base migrada` | Escenarios de `companies` de la spec `company-isolation` |
| (este) | `docs(openspec): progreso de apply de la PR 1 de multi-compania` | `tasks.md`, `design.md` (resultados de V-n y aclaraciones), este archivo |

Tamaño (de `9e572b8` a los commits de código): **+2 344 / -188 líneas en 60 archivos**; sin el `Designer` generado (399) y el snapshot (141 + 7): **código de producción ≈ +442 / -28** (incluye la migración A escrita a mano, 74 líneas) y **pruebas ≈ +1 362 / -153**. Supera la previsión de 600-900 líneas porque el cambio de firma obliga a tocar unas 140 llamadas en pruebas existentes (mecánico) y porque las pruebas con datos (migración, ensayo, siembra) pesan más que el código; el código de producción cabe en el presupuesto.

## Evidencia de Strict TDD (ciclo por tarea)

| Tarea | Archivo de prueba | Capa | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 1.1 | — | — | N/A (copia) | N/A | `..\TimeTracker.rollback\pr-mc1\` (67 archivos) | N/A | N/A |
| 1.5 | `tests/TimeTracker.Domain.Tests/Companies/CompanyTests.cs` | Unitaria | N/A (nuevo) | CS0234 «`Companies` no existe en `TimeTracker.Domain`» | 85/85 | 4 casos de `NormalizeName`, límites 1/100/101, UTC, id vacío | Limpio |
| 1.6 | `tests/TimeTracker.Domain.Tests/UserAccountTests.cs` | Unitaria | 67/67 (suite de dominio de partida) | CS1061 «`UserAccount` no contiene `CompanyId`/`MustChangePassword`/`ChangePassword`/`RegenerateTemporaryPassword`» y CS1501 «`Create` no toma 7/8 argumentos» | 95/95 | Con y sin marca previa; hash vacío y en blanco en ambas operaciones sin modificar el usuario | `RequireHash` común |
| 1.7 | `tests/TimeTracker.Domain.Tests/TimeEntryTests.cs` | Unitaria | incluida en 67/67 | CS1501 «`Start` no acepta 4 argumentos» (27 llamadas) y CS1061 `CompanyId` | 95/95 | Compañía vacía, empleado vacío, `Pause`/`Resume` heredan `CompanyId` | Limpio |
| 1.7 (llamadores en `APP`) | `ClockInUseCaseTests`, `DemoScheduleGeneratorTests`, `SeedDemoTimeEntriesUseCaseTests` + 9 suites actualizadas | Unitaria | 324/324 | Error de compilación (`ICompanyContext`, `DemoEmployee` con 3 argumentos) | 331/331 | Compañía del contexto distinta de la por defecto; sin compañía lanza y no guarda; empleados de dos compañías | Limpio |
| 1.8 | `tests/TimeTracker.Api.IntegrationTests/Persistence/CompanyModelTests.cs` | Integración (Testcontainers `postgres:18-alpine`) | 538/538 | `PendingModelChangesWarning` en el arranque del fixture (el modelo cambió y faltaba la migración) | 11/11 | 4 tablas con `company_id`; FKs compuestas; `xmin` (dos contextos: el segundo falla con `DbUpdateConcurrencyException`); V-5 sobre la base creada desde el modelo | Limpio |
| 1.9 | `.../Persistence/AddCompaniesMigrationTests.cs` (+ ajustes de `MigrationTests` y `UserSchemaTests`) | Integración | 538/538 | Idem (migración inexistente) | 6/6 | Datos previos, compañía por defecto, `xmin` de la auditoría, ausencia de reescritura (V-4), código anterior sin `company_id`, `Down A` | Mutación: quitar el `UPDATE time_entries` de `Up` hace fallar 2 pruebas |
| 1.10 | `.../Persistence/FixedCompanyContextTests.cs` | Integración | 538/538 | Error de compilación (`FixedCompanyContext` inexistente) | 3/3 | Valor fijo, registro en el host, `clock-in` escribe `company_id` | Limpio |
| 1.11 | `.../Seeding/DemoSeedingCompanyTests.cs` | Integración | `DemoUsersSeederTests` y `DemoDataSeederTests` en verde | Error de compilación (`CompanyRepository`, `ICompanyRepository`) | 6/6 | Compañía ausente, dos arranques, correo demo en otra compañía (sin la contraseña y sin crear los otros), `23505` por carrera (interceptor `RacingSqlInterceptor`), compañía existente intacta | Limpio |
| 1.12 | `.../Persistence/AddCompaniesRehearsalTests.cs` | Integración | — | «Verde a la primera»: ensambla piezas ya probadas (`Up A` al arrancar la API, flujo mínimo, `Down A`); justificación y mutación: el mismo cambio de `Up` que rompe 1.9 rompe este ensayo | 1/1 | Un escenario completo | Limpio |
| (extra) | `.../Persistence/CompaniesTableTests.cs` | Integración | — | «Verde a la primera» (restricciones ya creadas por la migración del commit anterior); sin mutación temporal ejecutada: quitar un `CHECK` o el índice único haría fallar las pruebas de duplicado, estado y nombre en blanco | 8/8 | Defectos, duplicado, estado fuera de catálogo, nombre en blanco | Limpio |
| 1.13 | — | — | Suite completa | — | Puerta en verde (abajo) | — | `dotnet format` aplicado; sin código muerto; configuraciones sin duplicación |

### Resumen de pruebas

- Pruebas nuevas: dominio +28 (67 → 95), aplicación +7 (324 → 331), integración +35 (538 → 573: 11 + 6 + 3 + 6 + 1 + 8); infraestructura, arquitectura y Web sin cambios.
- Total tras la PR: 95 + 331 + 288 + 399 + 156 + 573 = **1 842 pruebas**, todas en verde.
- Capas: unitaria (dominio, aplicación) e integración con PostgreSQL real; ninguna llamada a un proveedor real de IA.

### Evidencia de unidad de trabajo

| Evidencia | Valor |
|---|---|
| Prueba focalizada | `dotnet test tests/TimeTracker.Api.IntegrationTests -c Release --no-build --filter "FullyQualifiedName~CompanyModelTests\|FullyQualifiedName~AddCompaniesMigrationTests\|FullyQualifiedName~AddCompaniesRehearsalTests\|FullyQualifiedName~FixedCompanyContextTests\|FullyQualifiedName~DemoSeedingCompanyTests\|FullyQualifiedName~CompaniesTableTests"` → 35/35 (más las suites de dominio y aplicación) |
| Arnés de ejecución | `AddCompaniesRehearsalTests`: la API arranca aplicando `Up A` sobre una base con la forma de la de producción y sirve login, equipo, listado y alta de fichaje, configuración del asistente y `/health`; después `Down A`. Es local (Testcontainers); no toca producción |
| Límite de reversión | Revertir los tres commits de código y restaurar `..\TimeTracker.rollback\pr-mc1\`; contra una base ya migrada, `Down` de `AddCompanies` (quita columnas y `companies`) |

## Puerta de la PR (todo en verde)

| Paso | Resultado |
|---|---|
| DOCKER-CHECK | `docker info` responde (29.7.2); solo lectura |
| Entorno limpio | Sin variables `Cors__`, `ConnectionStrings__`, `Database__`, `Jwt__`, `Seed__`, `Ai__`, `Registration__`, `Company__`, `ForwardedHeaders__`, `ASPNETCORE_`, `DOTNET_ENVIRONMENT` |
| RESTORE | Correcto |
| BUILD | `-warnaserror`: 0 advertencias, 0 errores |
| TEST | Domain 95, Application 331, Web 288, Infrastructure 399, Architecture 156, Api.Integration 573: 0 fallos |
| COV-A | Application: líneas 99,9 %, ramas 96,6 % (mínimo 80 %) |
| COV-D | Domain: líneas 100,0 %, ramas 100,0 % (mínimo 100 %) |
| FORMAT | `--verify-no-changes`: sin cambios |
| VULN | Sin `has the following vulnerable packages` |
| EF-PENDING | «No changes have been made to the model since the last migration» (con `--startup-project src/TimeTracker.Infrastructure`; ver desviación 6) |

La suite de integración se ejecutó 7 veces seguidas sin fallos tras corregir la agrupación de conexiones (desviación 8).

## Desviaciones del diseño y de las tareas

Detalle en `design.md`, sección 9, «Desviaciones y aclaraciones del diseño surgidas en la PR 1»:

1. La PK de `assistant_settings` sigue siendo `id` en la PR 1 (el cambio a `company_id`, `AssistantSettingsRecord` y el almacén van juntos en la 1b).
2. EF crea dos índices de apoyo no listados por el diseño (`ix_time_entries_company_id_employee_id`, `ix_assistant_settings_company_id`); la migración A no los crea y **la B debe crearlos**.
3. `TimeTrackerDbContext` no recibe todavía `ICompanyContext` (sin filtro hasta la PR 3; un parámetro sin leer no compila con `-warnaserror`); `DesignTimeDbContextFactory` no cambia.
4. Nace `ICompanyRepository.EnsureDefaultAsync` en la PR 1 (la 2 le añade `FindLoginInfoAsync`).
5. La siembra comprueba los cuatro correos demo antes de crear ninguno.
6. El comando `EF-PENDING` de `tasks.md` (`--startup-project src/TimeTracker.Api`) no funciona (también en `main`: la Api no referencia `Microsoft.EntityFrameworkCore.Design`); se usa `--startup-project src/TimeTracker.Infrastructure`.
7. Riesgo de solape: filas escritas con `company_id` nulo por el código anterior no son legibles por el nuevo hasta que la B las rellene (mitigación para el runbook en `design.md`).
8. `PostgresFixture.CreateEmptyDatabaseAsync` cierra pronto las conexiones inactivas para no agotar `max_connections`.
9. Rutas de las pruebas: las de migración y modelo están en `tests/TimeTracker.Api.IntegrationTests/Persistence/` (donde ya viven las de migraciones), no en `IT/Migrations/` como decía `tasks.md`.

## Notas para la PR 1b

- La migración B debe crear, además de lo listado en DD-4: `ix_time_entries_company_id_employee_id`, `ix_assistant_settings_company_id` (y retirarlo al pasar la PK a `company_id`), los índices `ix_users_company_id_manager_id` y `ix_assistant_settings_audit_company_id_occurred_at`, `ux_users_company_id_id`, las FK `fk_users_companies_company_id`, `fk_time_entries_companies_company_id`, `fk_assistant_settings_companies_company_id`, `fk_assistant_settings_audit_companies_company_id` y las dos compuestas; y retirar `fk_users_users_manager_id`, `ix_users_manager_id` e `ix_assistant_settings_audit_occurred_at`. El snapshot actual ya contiene todo eso (salvo la PK de `assistant_settings`).
- `AssistantSettingsRecord` y `AssistantSettingsAuditRecord` tienen hoy `CompanyId = Company.DefaultId` por defecto (transitorio, marcado en el código).
- El nombre de la PK vigente de `assistant_settings` es `PK_assistant_settings` (a reconfirmar en 1b.4).

## Riesgos abiertos

- Despliegue de la PR 1: ver «Lo que queda al responsable» y la desviación 7.
- Las tareas 1.2, 1.3, 1.4 y 1.4b siguen sin marcar a propósito.

---

# PR 1b — Migración B «contraer» y puente transitorio del asistente

Rama `feat/multi-compania-pr1b-contraer` (apilada sobre `feat/multi-compania-pr1-modelo`, PR #36; cadena `stacked-to-main`) · Strict TDD · Copia previa en `..\TimeTracker.rollback\pr-mc1b\` (73 archivos).

## Lo que queda al responsable (puertas previas, sin marcar en `tasks.md`)

- **1b.2** — puertas previas del despliegue de la 1b: V-1 resuelta (huérfanos y decisión de Q-1), PR 1 desplegada y observada, V-6 comprobada ese día y `Database__ApplyMigrationsOnStartup=false` aplicado antes, V-3 reconfirmada, copia `pg_dump -Fc` restaurada en local (9.3), healthcheck y rollback de código identificados. El código se puede revisar y fusionar: si quedan huérfanos, B aborta de forma segura con el número de filas.
- **1b.3 (reconfirmación)** — la vía y la versión de `psql` de Railway. La parte local está hecha (ver «V-3» en `design.md`).
- No se ha accedido a Railway ni a ninguna base real.

## Commits de esta entrega

| Commit | Mensaje | Contenido |
|---|---|---|
| `c9beeac` | `feat(multi-compania): migración B «contraer» y puente transitorio del asistente` | Tareas 1b.1, 1b.4 a 1b.9: migración `EnforceCompanyIsolation` (SQL a mano, `Designer`, snapshot), modelo y almacén del asistente (puente), pruebas nuevas y ajuste de las existentes |
| (este) | `docs(openspec): progreso de apply de la PR 1b de multi-compania` | `tasks.md`, `design.md` (resultados de V-n y desviaciones), este archivo |

Tamaño del commit de código (`git diff HEAD~1`): **+1 907 / -398 en 35 archivos**. Producción: +546 / -35 en 7 archivos, de los que el `Designer` generado son 390 líneas; sin él, **≈ +156 / -35** (migración escrita a mano 131 líneas, puente del almacén +18/-7, snapshot +1/-10). Pruebas: +1 361 / -363 en 28 archivos. La previsión era 400-650 líneas sin `Designer`: el código de producción cabe holgadamente; las pruebas pesan más (migración con datos, `Down`, scripts, FK, ensayo completo) y hay un ajuste de las pruebas existentes que usaban empleados sueltos.

## Evidencia de Strict TDD (ciclo por tarea)

| Tarea | Archivo de prueba | Capa | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 1b.1 | — | — | N/A (copia) | N/A | `..\TimeTracker.rollback\pr-mc1b\` | N/A | N/A |
| 1b.4 | `Persistence/EnforceCompanyIsolationMigrationTests` (`PrimaryKeyOfAssistantSettings`) | Integración | N/A | Lectura de `AddAssistantSettings` y snapshot: `PK_assistant_settings` sobre `id`; la prueba fija el nombre antes y después de B | incluido en 1b.5 | Antes de B (`id`) y después (`company_id`) | Limpio |
| 1b.5 | `Persistence/EnforceCompanyIsolationMigrationTests` | Integración (Testcontainers `postgres:18-alpine`) | Pruebas de migración y modelo de la PR 1 en verde | 8 pruebas, 7 en rojo: esperaba `time_entries huérfanos: 1`, `is_nullable` «YES» en vez de «NO», `23502` no lanzado (el `DEFAULT` seguía), índices y restricciones inexistentes | 8/8 | Huérfano (1 y 2), sin huérfano, relleno de filas con `company_id` nulo, `DEFAULT` retirado, triggers, índices y FK, `HasPendingModelChanges` | Limpio. Una prueba pasó a la primera (`Up_KeepsTheAppendOnlyTriggersPresentAndActive`): ya se cumplía tras A; la cubre además `Up_RemovesTheAuditDefault...` |
| 1b.6 | `Persistence/CompositeForeignKeyTests` | Integración | N/A (nuevo) | 5 de 8 en rojo (sin FK no había `23503`) | 8/8 | Responsable ajeno, propio y nulo; fichaje de otra compañía, propio y de empleado inexistente; compañía inexistente; borrado de usuario con fichajes | Limpio |
| 1b.6b | `Persistence/UsersTableConstraintsTests`, `Persistence/TimeEntriesColumnsTests` | Integración | `UserConstraintsTests`/`UserSchemaTests` previos (se fusionan) | 6 de 14 en rojo (`company_id` anulable, FK con el nombre antiguo, sin `23502`) | 14/14 | Correo duplicado en la misma y otra compañía, rol, mayúsculas, `confdeltype`, ida y vuelta de `UserAccount` y `TimeEntry` | Limpio |
| 1b.7 | `Persistence/EnforceCompanyIsolationDownTests` | Integración | N/A | 5 de 6 en rojo (`Down` sin comprobación ni restauración de la clave `id`) | 6/6 | Una compañía con y sin fila de configuración; dos compañías con una fila y con dos; tres compañías sin filas (la condición no depende de las filas); secuencia completa | Limpio |
| 1b.7b (V-3) | `Persistence/EnforceCompanyIsolationScriptTests` | Integración | N/A | «Verde a la primera»: el comportamiento ya existía y se descubrió ensayándolo a mano con `psql`; la prueba lo fija. Mutación temporal: sustituir la comprobación de huérfanos por una versión que no aborta (no ejecutada para esta prueba; sí se ejecutó una variante sintáctica que confirmó que el `DO` anidado funciona en el script idempotente) | 3/3 | Script idempotente de `Up B` con huérfano (sin cambios parciales, incluido el relleno residual), sin huérfano (y repetido), `Down B` con dos y con una compañía | Limpio |
| 1b.8 | `Startup/ApplyMigrationsOnStartupTests` | Integración | `DatabaseMigrationStartupTests` | 2/2 en rojo (la API no aplicaba B) | 2/2 | A y B antes de servir; B aborta y la API no arranca | Limpio |
| 1b.8b | `Assistant/AssistantBridgeTests` | Integración (`FakeChatClient`) | `AssistantSettingsStoreTests`, `...ExternalSaveTests`, `...MultiInstanceTests` | 2 de 4 en rojo (almacén contra la clave `id`) | 4/4 | Fila de la compañía por defecto con clave `company_id`; fila ajena ignorada y auditoría con la compañía por defecto; guardado por API que llega a otra instancia tras su sondeo; sin fila gana el entorno y con clave guardada gana la guardada | Limpio |
| 1b.9 | `Persistence/EnforceCompanyIsolationRehearsalTests` | Integración (ensayo) | N/A | «Verde a la primera»: ensambla piezas ya probadas (misma justificación que 1.12); quitar el relleno residual o la comprobación de huérfanos lo rompería | 1/1 | Un escenario completo | Limpio |
| 1b.10 | — | — | Suite completa | — | Puerta de la PR (abajo) | — | `dotnet format` sin cambios; sin código muerto |

### Resumen de pruebas

- Pruebas nuevas: 46 (8 + 6 + 3 + 8 + 11 + 3 + 2 + 4 + 1); retiradas por sustitución: 11 (`UserConstraintsTests` 6, `UserSchemaTests` 1, `TimeEntriesForeignKeyTests` 2, `AddCompaniesRehearsalTests` 1, `Columns_AreExactlySix` 1). Integración: 573 → 608.
- Total tras la PR: 95 + 331 + 399 + 156 + 288 + 608 = **1 877 pruebas**; 1 872 en verde y **5 con fallo de entorno** (ver más abajo).
- Capas: integración con PostgreSQL real (Testcontainers) y ensayo con `psql` en un contenedor propio y desechable; ninguna llamada a un proveedor real de IA.

### Evidencia de unidad de trabajo

| Evidencia | Valor |
|---|---|
| Prueba focalizada | `dotnet test tests/TimeTracker.Api.IntegrationTests -c Release --no-build --filter "FullyQualifiedName~EnforceCompanyIsolation\|FullyQualifiedName~CompositeForeignKey\|FullyQualifiedName~UsersTableConstraints\|FullyQualifiedName~TimeEntriesColumns\|FullyQualifiedName~ApplyMigrationsOnStartup\|FullyQualifiedName~AssistantBridge"` → 47/47 (el comando de `tasks.md`, solo `FullyQualifiedName~EnforceCompanyIsolation`, da 18/18) |
| Arnés de ejecución | `EnforceCompanyIsolationRehearsalTests` (API sobre una base con la forma de la de producción: `Up A`, aborto por huérfano, `Up B`, `Down B` con una y con dos compañías, `Down A`, de nuevo `Up A` y `Up B` al arrancar, flujo mínimo) y la ejecución manual de los scripts con `psql 18.6 -v ON_ERROR_STOP=1 --single-transaction` en un contenedor propio (retirado al terminar). Nada contra producción |
| Límite de reversión | Revertir `c9beeac` y restaurar `..\TimeTracker.rollback\pr-mc1b\`; contra una base ya migrada con B, `Down` de `EnforceCompanyIsolation` (solo con una compañía y antes de abrir el registro) |

## Puerta de la PR

| Paso | Resultado |
|---|---|
| DOCKER-CHECK | `docker info` responde (29.7.2); solo lectura; los contenedores existentes no se tocaron |
| Entorno limpio | Sin variables `Cors__`, `ConnectionStrings__`, `Database__`, `Jwt__`, `Seed__`, `Ai__`, `Registration__`, `Company__`, `ForwardedHeaders__`, `ASPNETCORE_`, `DOTNET_ENVIRONMENT` |
| RESTORE | Correcto |
| BUILD | `-warnaserror`: 0 advertencias, 0 errores |
| TEST | Domain 95, Application 331, Web 288, Infrastructure 399, Architecture 156: 0 fallos. Api.Integration 608: 603 superadas y **5 con fallo de entorno** (puerto 5000 de Windows reservado: `AssistantBodyLimitTests` ×3 y `BodyOver8KiB_Returns413_AndNothingIsReadOrWritten` ×2, con `Failed to bind to address http://localhost:5000`); con `ASPNETCORE_URLS=http://127.0.0.1:0` solo en ese comando de diagnóstico, pasan 5/5 |
| COV-A | Application: líneas 99,9 %, ramas 96,6 % (mínimo 80 %) |
| COV-D | Domain: líneas 100,0 %, ramas 100,0 % (mínimo 100 %) |
| FORMAT | `--verify-no-changes`: sin cambios |
| VULN | Sin `has the following vulnerable packages` |
| EF-PENDING | «No changes have been made to the model since the last migration» (`--startup-project src/TimeTracker.Infrastructure`) |

## Desviaciones del diseño y de las tareas

Detalle en `design.md`, sección 9, «Desviaciones y aclaraciones del diseño surgidas en la PR 1b»:

1. El `Down` de B deja `assistant_settings.id` con `DEFAULT 1` (lo pide la spec; A lo dejó sin valor por defecto).
2. El snapshot casi no cambia en B (A ya reflejaba el modelo final); `ix_assistant_settings_company_id` nunca llega a existir.
3. El puente del almacén usa la constante `Company.DefaultId` sin cambiar el constructor (no lee `ICompanyContext`); el cargador y el poller no cambian porque ya leen a través del almacén.
4. Las pruebas existentes crean ahora el usuario de cada fichaje (la FK compuesta lo exige); se sustituyen cuatro clases de prueba cuyos escenarios cambian de signo.
5. Cinco pruebas de integración dependen del puerto 5000, reservado en esta máquina: bloqueo de entorno, sin relación con el cambio.
6. Hallazgo para el runbook (V-3): el script de EF lleva `START TRANSACTION`/`COMMIT` y, bajo `--single-transaction`, `psql` avisa; es inocuo con un script por migración y no hay que concatenar migraciones.
7. La nota de la tarea 1b.6 («se anota en el ADR 0020 que la baja de usuarios queda pendiente») se traslada a la PR 4a, donde nace el ADR 0020 (DD-24); el diseño (DD-2) ya la recoge.

## Riesgos abiertos

- Despliegue de la PR 1b: ver «Lo que queda al responsable». Hasta que la V-1 esté resuelta no se sabe si hay huérfanos; B los detecta y aborta (fusionar es seguro, desplegar no).
- El rollback de un clic de Railway contra el esquema B no es válido (los `INSERT` de `time_entries` fallan por `company_id NOT NULL` y el almacén antiguo lee `assistant_settings.id`): ejecutar `Down` antes de redesplegar el código anterior.
- Las tareas 1.2, 1.3, 1.4, 1.4b, 1b.2 y 1b.3 siguen sin marcar a propósito.

---

# PR 2 — Claim de compañía en el token

Rama `feat/multi-compania-pr2-claim` (apilada sobre `feat/multi-compania-pr1b-contraer`, PR #37; cadena `stacked-to-main`) · Strict TDD · Copia previa en `..\TimeTracker.rollback\pr-mc2\` (63 archivos).

## Lo que queda al responsable

Nada de producción en esta PR. Efecto a comunicar al desplegar: los tokens vigentes no llevan `cid` y devuelven `401`; las personas vuelven a iniciar sesión (caducan como mucho a los 30 minutos). Un rollback de código no revierte el esquema (ejecutar `Down` antes de redesplegar; ver DD-4).

## Commits de esta entrega

| Commit | Mensaje | Contenido |
|---|---|---|
| `879e4c3` | `test(multi-compania): TestTokens fabrica tokens con cid, cname y mcp` | Tarea 2.4: `TestTokens` (+ `TestTokensTests`), sin cambiar aserciones |
| `2c0ede8` | `feat(multi-compania): claims cid, cname y mcp y compañía en la sesión` | 2.5 a 2.8 (claims e identidad): `JwtDefaults`, `RejectInvalidClaims`, emisor, `SessionIssuer`, `FindLoginInfoAsync`, `ICurrentUser.CompanyId`, `me`, DTO y contrato |
| `62633ed` | `feat(multi-compania): contexto de compañía desde el token y ámbitos por compañía` | 2.9, 2.10: `AmbientCompanyContext`, middleware, `ICompanyScopeFactory`, puente y siembras por `CreateScope(Company.DefaultId)` |
| `51ac1db` | `test(multi-compania): pruebas de arquitectura del contexto de compañía y de CreateScope` | 2.8 (arquitectura), 2.9b |
| (este) | `docs(openspec): progreso de apply de la PR 2 de multi-compania` | `tasks.md`, `design.md` (V-10, V-15, V-16 y desviaciones), este archivo |

Tamaño (`git diff ab215ef HEAD` de los commits de código): 52 archivos, +1 550 / -127 líneas; el código de producción es una fracción pequeña (≈ +330 / -50 sin el contrato OpenAPI) y el resto son pruebas. El volumen de pruebas supera la previsión de 400-600 líneas.

## Evidencia de Strict TDD (ciclo por tarea)

| Tarea | Archivo de prueba | Capa | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 2.1 | — | — | N/A (copia) | N/A | `..\TimeTracker.rollback\pr-mc2\` | N/A | N/A |
| 2.2, 2.3 | — (decisiones V-15 y V-16) | — | N/A | N/A | Anotadas en `design.md` | N/A | N/A |
| 2.4 | `IT/TestTokensTests.cs` | Integración (sin Docker) | 90/90 de autenticación | Infraestructura de pruebas: los parámetros `companyId`/`companyName` no existían; las pruebas existentes siguen en verde con el emisor antiguo | 4/4 | Por defecto, explícito, omitido (`null`) y sin firma | Limpio |
| 2.5 | `IT/Authentication/CompanyClaimTests` | Integración (sin Docker) | 87 de autenticación | 4 de 11 en rojo: «esperado 401» sin `cid`, con `cid` vacío o mal formado (`200`/`500`) | 11/11 | Sin cid, tres valores inválidos, payload alterado (verde a la primera: ya lo impedía la firma), `mcp` no `true`, sin `sub`/rol | Limpio |
| 2.6 | `AT/LoginUseCaseTests`, `AT/SessionIssuerTests` | Unitaria | 331/331 | CS0246 (`CompanyLoginInfo`, `SessionIssuer`) y CS0535 (`Issue(user, companyName)`) | 337/337 | Compañía activa, suspendida (contraseña verificada una vez), inexistente, con cambio pendiente | Limpio |
| 2.6 (repositorio) | `IT/Persistence/CompanyLoginInfoTests` | Integración | N/A (nuevo) | Error de compilación (`FindLoginInfoAsync` inexistente) | 4/4 | Por defecto (`Demo`, plataforma), nueva, suspendida, desconocida | Nombres de compañía únicos por el índice del nombre normalizado |
| 2.7 | `IT/Authentication/TokenContentTests`, `JwtAccessTokenIssuerTests` | Integración | 90/90 | Error de compilación y pruebas de login con token sin `cid` en rojo | 7/7 + 3 del emisor | Compañía no por defecto, `mcp`, sin `companyId` en el cuerpo, login suspendido idéntico al de credenciales erróneas, `me` sin base de datos | Limpio |
| 2.8 | `IT/Authentication/HttpContextCurrentUserTests`, `AR/CompanyContextPlacementTests` | Integración / arquitectura | 8/8 | CS0535 «`HttpContextCurrentUser` no implementa `ICurrentUser.CompanyId`» (con el archivo de producción revertido temporalmente) | 9 nuevas en verde | Válido, anónimo, sin contexto, tres inválidos, sin claim, cabecera/consulta/cuerpo ignorados | Las pruebas de arquitectura pasan a la primera (la estructura ya existía); su validez la respalda la mutación de la 2.9b |
| 2.9 | `IT/Pipeline/MiddlewareOrderTests`, `IT/Security/AmbientCompanyContextTests`, `CompanyContextMiddlewareTests`, `IT/Persistence/CompanyContextHostTests`, `IFT/Persistence/CompanyScopeFactoryTests`, `IFT/.../AssistantSettingsLoaderTests` | Integración / unitaria | 399 de infraestructura y 645 de integración | CS0246 (`AmbientCompanyContext`, `ICompanyScopeFactory`, `ICompanyContextSetter`) | 402 de infraestructura y 659 de integración | Token válido, sin cid/vacío/mal formado, anónimo; escritura única; fichaje con compañía distinta de la por defecto y con la por defecto | Se elimina `FixedCompanyContext` y su prueba |
| 2.9b | `AR/CreateScopeClosedListTests` | Arquitectura | 156/156 | «Verde a la primera» (la estructura ya cumplía la lista). Mutación comprobada una vez: una llamada temporal en `TimeEntryRepository` hace fallar la prueba indicando el archivo; al retirarla vuelve a pasar | 11/11 | Lista de llamadores, entradas sin uso y 9 casos del escáner | Limpio |
| 2.10 | `IT/Seeding/DemoSeedingCompanyTests` | Integración | 6/6 | Error de compilación (`ICompanyScopeFactory` sin registrar que observar) | 7/7 | Dos sembradores, siempre con la compañía por defecto | Limpio |
| 2.11 | — | — | Suite completa | — | Puerta de la PR (abajo) | — | Sin literales de `cid`, `cname` ni `mcp` fuera de `JwtDefaults`; `dotnet format` aplicado |

### Resumen de pruebas

- Total tras la PR: 95 + 337 + 402 + 175 + 288 + 659 = **1 956 pruebas** (antes 1 877), todas en verde. Nuevas: aplicación +6, infraestructura +3, arquitectura +19, integración +51.
- Ninguna llamada a un proveedor real de IA.

### Evidencia de unidad de trabajo

| Evidencia | Valor |
|---|---|
| Prueba focalizada | `dotnet test tests/TimeTracker.Api.IntegrationTests -c Release --no-build --filter "FullyQualifiedName~Authentication\|FullyQualifiedName~MiddlewareOrder\|FullyQualifiedName~CompanyContext\|FullyQualifiedName~CompanyLoginInfo"` más `T-AT` y `T-AR` (todo en verde) |
| Arnés de ejecución | `CompanyContextHostTests` y `TokenContentTests`: la API real con PostgreSQL (Testcontainers) emite el token en el login, lo valida y escribe el fichaje con la compañía del token; `AssistantBridgeTests` sigue en verde con el puente por `CreateScope(Company.DefaultId)`. Nada contra producción |
| Límite de reversión | Revertir los cuatro commits de código y restaurar `..\TimeTracker.rollback\pr-mc2\`; los tokens nuevos con `cid` los acepta el código anterior (ignora claims desconocidos) |

## Puerta de la PR

| Paso | Resultado |
|---|---|
| DOCKER-CHECK | `docker info` responde (29.7.2); solo lectura |
| Entorno limpio | Sin variables `Cors__`, `ConnectionStrings__`, `Database__`, `Jwt__`, `Seed__`, `Ai__`, `Registration__`, `Company__`, `ForwardedHeaders__`, `ASPNETCORE_`, `DOTNET_ENVIRONMENT` |
| RESTORE | Correcto |
| BUILD | `-warnaserror`: 0 advertencias, 0 errores |
| TEST | Domain 95, Application 337, Web 288, Infrastructure 402, Architecture 175, Api.Integration 659: 0 fallos (con `ASPNETCORE_URLS=http://127.0.0.1:0` solo en ese comando, por el puerto 5000 reservado) |
| COV-A | Application: líneas 99,9 %, ramas 96,6 % (mínimo 80 %) |
| COV-D | Domain: líneas 100,0 %, ramas 100,0 % (mínimo 100 %) |
| FORMAT | `--verify-no-changes`: sin cambios |
| VULN | Sin `has the following vulnerable packages` |
| EF-PENDING | «No changes have been made to the model since the last migration» (`--startup-project src/TimeTracker.Infrastructure`) |

## Desviaciones

Detalle en `design.md`, sección 9, «Desviaciones y aclaraciones del diseño surgidas en la PR 2»: `mcp` distinto de `true` no invalida el token (prevalece la tarea y la spec sobre DD-6); `SessionIssuer` como emisor único; `me` devuelve `companyName` vacío si falta `cname`; la lista cerrada de `CreateScope` no incluye al sondeo (no abre ámbitos); `SkipCompanyContext` se difiere a la PR 5.

## Riesgos abiertos

- 401 masivo en el despliegue (los tokens vigentes caducan como mucho a los 30 minutos): comunicarlo en la descripción de la PR.
- Sin filtro de consulta todavía (PR 3): el aislamiento de lectura aún no está activo; el contexto ya es el del token.

---

# PR 3 — Aislamiento de consultas

Rama `feat/multi-compania-pr3-aislamiento` (apilada sobre `feat/multi-compania-pr2-claim`, PR #38; cadena `stacked-to-main`) · Strict TDD · Copia previa en `..\TimeTracker.rollback\pr-mc3\` (22 archivos).

## Lo que queda al responsable

Nada de producción en esta PR (sin migración ni cambio de esquema). Al desplegarla el filtro queda activo: el puente del asistente y la siembra siguen funcionando con `CreateScope(Company.DefaultId)`; si una consulta olvidara el contexto, el síntoma sería «no veo datos» (falla cerrado), no una fuga. Es la capa 2 de la regla de apertura del registro (punto 4 del encabezado de `tasks.md`): la apertura sigue exigiendo las PR 1 a 4b fusionadas y desplegadas.

## Commits de esta entrega

| Commit | Mensaje | Contenido |
|---|---|---|
| `02202e2` | `feat(multi-compania): filtro global que falla cerrado y guarda de escritura por compañía` | 3.2 a 3.5: filtro por convención, guarda en `SaveChanges`, `CompanyScopeViolationException`, spike V-2/V-9, `TwoCompanyFixture`, pruebas de fallo cerrado, guarda y modelo |
| `1e80196` | `test(multi-compania): lista cerrada de IgnoreQueryFilters y APIs que evitan el filtro` | 3.6: sitio 1 (`FindByEmailAsync`) y prueba de arquitectura |
| `f9a2b9e` | `test(multi-compania): guardas de arquitectura de que la compañía sale solo del token` | 3.7 |
| `6e15ea9` | `test(multi-compania): pruebas A/B de equipo, fichajes, endpoints existentes, correo global y siembra` | 3.8 a 3.11 |
| `2215b3a` | `style(multi-compania): orden de los using en AssistantSettingsStoreTests` | Ajuste de `dotnet format` |
| (este) | `docs(openspec): progreso de apply de la PR 3 de multi-compania` | `tasks.md`, `design.md` (V-2, V-9 y desviaciones), este archivo |

Tamaño (`git diff c9cb0d5 HEAD` de los commits de código): 19 archivos, **+1 516 / -8**; producción **+86 / -3** (contexto, excepción, un `IgnoreQueryFilters` y el `InternalsVisibleTo`) y pruebas **+1 430 / -5**. Muy por debajo de la previsión de 700-1000 líneas en producción; las pruebas pesan más (dos compañías, mutaciones, escáneres de arquitectura).

## Evidencia de Strict TDD (ciclo por tarea)

| Tarea | Archivo de prueba | Capa | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 3.1 | — | — | N/A (copia) | N/A | `..\TimeTracker.rollback\pr-mc3\` | N/A | N/A |
| 3.2 | `IT/Persistence/QueryFilterSpikeTests` | Integración (Testcontainers) | Compilación y suite de integración de la PR 2 en verde | CS1729 «`TimeTrackerDbContext` no contiene un constructor que tome 2 argumentos» (`CurrentCompanyId` aún no existía) | 4/4 tras el filtro; la hipótesis de V-9 resultó falsa (`Find` sí aplica el filtro) y la prueba pasó a documentar el resultado real | Dos contextos con compañías distintas y consulta repetida; cambio de compañía entre consultas; `ExecuteUpdate`/`ExecuteDelete` | Limpio |
| 3.3 | `IT/Persistence/FailClosedFilterTests` | Integración | Idem | Compilación (ver 3.2); mutación comprobada: sin el bucle del filtro fallan 6 de 7 | 7/7 | Sin compañía y con `Guid.Empty`; compañía A y B; unión, join y agrupación; `companies` sin filtro; puente del almacén con y sin compañía | Limpio |
| 3.4 | `AR/CompanyIsolationModelTests` | Arquitectura | 156 de arquitectura | «Verde a la primera» (el filtro de 3.3 ya cubría el modelo); la comprobación se valida con un contexto temporal con `company_id` sin filtro (detectado) y otro con filtro (no) | 4/4 | Modelo real, cuatro tablas, lista de excepciones vacía, tipo sin filtro | Limpio |
| 3.5 | `IT/Persistence/WriteGuardTests` | Integración | Idem | CS0246 «`CompanyScopeViolationException` no se encontró» | 11/11 | `Add`/`Update`/`Remove` ajenos; cambio de `CompanyId` (a otra compañía y a la del contexto; mutación comprobada: sin la comprobación del valor original falla la segunda); sin compañía y vacía; síncrono; mensaje sin datos; 500 por HTTP con registro sin datos personales | Limpio |
| 3.6 | `AR/IgnoreQueryFiltersClosedListTests` | Arquitectura | 156 de arquitectura | 2 de 24 en rojo (el sitio 1 no existía: recuento 0 en vez de 1) | 24/24 | Escáner con y sin comentario, comentario lejano o sin motivo, APIs prohibidas; mutación comprobada: un `IgnoreQueryFilters` temporal en `TimeEntryRepository` hace fallar la prueba indicando el archivo | Limpio |
| 3.7 | `AR/CompanyGuardsTests` | Arquitectura | Idem | «Verde a la primera» (el contrato ya cumplía); mutación comprobada: un literal `"cid"` temporal en un repositorio hace fallar la prueba indicando el archivo; el comprobador de firmas se prueba con tipos sintéticos | 11/11 | OpenAPI (parámetros y esquemas), firmas, repositorios, literales | Limpio |
| 3.8 | `IT/CompanyIsolation/TeamAndTimeEntriesIsolationTests` | Integración | Pruebas de equipo y fichajes en verde | «Verde a la primera» parcial: los identificadores ya acotaban; mutación comprobada: sin el filtro fallan el token inconsistente (cid de A con la identidad de B) y los fichajes propios | 8/8 | A y B, mismo 404 (estado, tipo, cabeceras y cuerpo sin `traceId`), ventana futura, `companyId` por consulta, cabecera y cuerpo ignorado | Limpio |
| 3.9 | `IT/CompanyIsolation/ExistingEndpointsMatrixTests` | Integración | Idem | «Verde a la primera»; mutación comprobada: sin el filtro y quitando `ManagerId` de `FindDirectReportAsync` fallan los dos casos de Manager | 4/4 | Empleado y Manager de A y de B (fichajes, equipo, `me`) | Limpio |
| 3.10 | `IT/Persistence/UserLookupTests` | Integración | `UserRepositoryTests` en verde | «Verde a la primera» (el sitio 1 vino con 3.6); mutación comprobada: sin `IgnoreQueryFilters` fallan las pruebas de login | 4/4 | `23505` de `ux_users_email`, búsqueda de correo con contexto A, B, vacío y sin compañía, búsquedas acotadas, `managerId` ajeno | Limpio |
| 3.11 | `IT/Seeding/DemoSeedingCompanyTests` (+1) | Integración | 7/7 de siembra de la PR 2 | «Verde a la primera»: las pruebas de las PR 1 y 2 ya corrían con el filtro activo; se añade el caso con compañías ajenas presentes | 8/8 | Dos arranques, compañías ajenas intactas, fichajes demo con la compañía demo | Limpio |
| 3.12 | — | — | Suite completa | — | Puerta de la PR (abajo) | — | `dotnet format` aplicado (orden de `using`) |

### Resumen de pruebas

- Total tras la PR: 95 + 337 + 402 + 214 + 288 + 698 = **2 034 pruebas** (antes 1 956), todas en verde. Nuevas: arquitectura +39, integración +39.
- Ninguna llamada a un proveedor real de IA.

### Evidencia de unidad de trabajo

| Evidencia | Valor |
|---|---|
| Prueba focalizada | `ASPNETCORE_URLS=http://127.0.0.1:0 dotnet test tests/TimeTracker.Api.IntegrationTests -c Release --no-build --filter "FullyQualifiedName~CompanyIsolation\|FullyQualifiedName~QueryFilterSpike\|FullyQualifiedName~FailClosedFilter\|FullyQualifiedName~WriteGuard\|FullyQualifiedName~UserLookup"` más `T-AR` (todo en verde). El comando de `tasks.md` (`~CompanyIsolation`) solo cubre las dos clases de la carpeta `CompanyIsolation`; el resto vive en `Persistence/` |
| Arnés de ejecución | La API real con PostgreSQL (Testcontainers) y dos compañías: lecturas y escrituras de todos los endpoints existentes con tokens de A y de B; `AssistantBridgeTests` y `AssistantSettings*` siguen en verde con el filtro activo. Nada contra producción |
| Límite de reversión | Revertir los cinco commits de código y restaurar `..\TimeTracker.rollback\pr-mc3\`; no hay migración (las FK compuestas siguen protegiendo el esquema) |

## Puerta de la PR (todo en verde)

| Paso | Resultado |
|---|---|
| DOCKER-CHECK | `docker info` responde (29.7.2); solo lectura |
| Entorno limpio | Sin variables `Cors__`, `ConnectionStrings__`, `Database__`, `Jwt__`, `Seed__`, `Ai__`, `Registration__`, `Company__`, `ForwardedHeaders__`, `ASPNETCORE_`, `DOTNET_ENVIRONMENT` |
| RESTORE | Correcto |
| BUILD | `-warnaserror`: 0 advertencias, 0 errores |
| TEST | Domain 95, Application 337, Web 288, Infrastructure 402, Architecture 214, Api.Integration 698: 0 fallos (con `ASPNETCORE_URLS=http://127.0.0.1:0` solo en ese comando, por el puerto 5000 reservado) |
| COV-A | Application: líneas 99,9 %, ramas 96,6 % (mínimo 80 %) |
| COV-D | Domain: líneas 100,0 %, ramas 100,0 % (mínimo 100 %) |
| FORMAT | `--verify-no-changes`: sin cambios |
| VULN | Sin `has the following vulnerable packages` |
| EF-PENDING | «No changes have been made to the model since the last migration» (`--startup-project src/TimeTracker.Infrastructure`) |

## Desviaciones

Detalle en `design.md`, sección 9, «Desviaciones y aclaraciones del diseño surgidas en la PR 3»: V-9 (`FindAsync` sí aplica el filtro en 10.0.12; las prohibiciones se mantienen), V-2 (existen filtros con nombre; no se adoptan), el constructor `internal` del contexto para el diseño, la guarda que también rechaza escrituras sin compañía, `InternalsVisibleTo` a las pruebas de integración, la tolerancia transitoria de la lista cerrada hasta 4b.7 y el efecto del filtro sobre el almacén puente.

## Riesgos abiertos

- La tolerancia `TransitionallyUnused` de `IgnoreQueryFiltersClosedListTests` debe retirarse en la 4b.7 (sitio 2, `ListVersionsAsync`).
- Cada consulta fuera de contexto falla cerrado (devuelve vacío): un olvido se nota como «no veo datos».

---

# PR 4a — Configuración del asistente por compañía: datos y casos de uso

Rama `feat/multi-compania-pr4a-datos` (apilada sobre `feat/multi-compania-pr3-aislamiento`, PR #39; cadena `stacked-to-main`) · Strict TDD · Copia previa en `..\TimeTracker.rollback\pr-mc4a\` (473 archivos, incluye restos de `bin`/`obj` de las pruebas de arquitectura).

## Lo que queda al responsable

Nada de producción en esta PR (sin migración ni cambio de esquema; no se accede a Railway). Al desplegarla: la clave del asistente que se guarde desde ahora se cifra en formato v2 (vinculada a la compañía); **revertir el código tras un guardado v2 exige volver a introducir la clave** (ADR 0021). El asistente de cualquier compañía distinta de la demo queda deshabilitado (`503`) hasta la PR 4b, aunque guarde su configuración. Si el operador cambia `uses_platform_assistant` de la compañía por defecto a falso (Q-1), el cargador deja de usar el entorno para ella tras la siguiente carga (arranque o guardado).

## Commits de esta entrega

| Commit | Mensaje | Contenido |
|---|---|---|
| `941b6fe` | `feat(multi-compania): texto cifrado v2 vinculado a la compañía` | 4a.5, 4a.6: protector con AAD v2 (v1 solo en la compañía por defecto), puerto con `companyId`, casos de uso con `ICurrentUser.CompanyId` |
| `0761b99` | `feat(multi-compania): configuración del asistente por compañía` | 4a.3, 4a.4, 4a.7, 4a.9 (código): resolver con `platformDefaults`, `AssistantCompany`, casos de uso, almacén generalizado, recargador, cargador y guarda del motor, dobles de pruebas |
| `57bc6fd` | `test(multi-compania): la tolerancia de la lista cerrada de IgnoreQueryFilters caduca sola` | Prueba autoexpirable de `TransitionallyUnused` |
| `e02a6e7` | `test(multi-compania): pruebas A/B de la configuración, el restablecimiento y las herramientas del asistente` | 4a.8, 4a.8b, 4a.9, 4a.10 |
| `096fdf4` | `docs(adr): ADR 0020 de aislamiento multiempresa y ADR 0021 de configuración del asistente por compañía` | 4a.12, 4a.13, `DeliveryArtifactsTests`, índice del README |
| (este) | `docs(openspec): progreso de apply de la PR 4a de multi-compania` | `tasks.md`, `design.md` (V-18, DD-13, DD-14 y desviaciones), este archivo |

Tamaño (`git diff 3d004fe HEAD` de los cinco commits de código y ADR): 51 archivos, **+1 982 / -151**; producción (`src`) **+223 / -81**, pruebas **+1 666 / -68**, documentación (ADR y README) **+93 / -2**. La previsión era de 600-900 líneas; el volumen de pruebas A/B y de dobles es lo que pesa.

## Evidencia de Strict TDD (ciclo por tarea)

| Tarea | Archivo de prueba | Capa | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 4a.1 | — | — | N/A (copia; la línea base de 156/156 de configuración del asistente en Application y 33/33 del protector se tomó antes de la tarea 4a.3) | N/A | `..\TimeTracker.rollback\pr-mc4a\` | N/A | N/A |
| 4a.2 | — (decisión V-18) | — | N/A | N/A | Anotada en `design.md` | N/A | N/A |
| 4a.3 | `AT/Assistant/Configuration/AssistantSettingsResolverTests` | Unitaria | 156/156 | CS7036 «falta el argumento `platformDefaults`» (las 9 pruebas existentes adaptadas) y `SettingSource.Default` inexistente | 15/15 | Sin fila con/sin plataforma, fila con clave de entorno del mismo proveedor, modelo solo de la fila, «sin fila» frente a `None` guardado | Limpio |
| 4a.4 | `AT/.../AssistantUseCasesCompanyTests` | Unitaria | 156/156 | CS7036 (los casos de uso no recibían `ICompanyRepository`) | 12/12 | Demo y otra compañía en `Get`, prueba con clave de entorno / guardada propia / ajena / candidata, `Save`/`Reset` recargan la compañía del contexto, fallo de recarga, firmas sin compañía por reflexión | `FindCredential` sin `Guid companyId` (la guarda `CompanyGuardsTests` lo detectó) |
| 4a.5 | `IFT/Assistant/AesGcmCredentialProtectorV2Tests` | Unitaria | 33/33 | CS1501 «`Protect` no toma 3 argumentos» | 7 nuevas + 33 existentes en verde | v2 ida y vuelta con AAD escrito a mano, otra compañía, otro proveedor, v1 solo en la compañía por defecto, `Protect` siempre v2, manipulación | Limpio |
| 4a.6 | `AT/.../SaveAssistantSettingsReEncryptTests` | Unitaria | 156/156 | «Verde a la primera» tras cambiar la firma (el recifrado ya existía); mutación comprobada: sin `Protect` en `ReEncryptOrKeep` falla el recifrado v1 → v2 | 5/5 | v1 en la compañía por defecto, v2 en otra, v1 en otra (no legible: rechazo), sin protector, lectura sin escritura | Limpio |
| 4a.7 | `IT/Assistant/AssistantSettingsStorePerCompanyTests` | Integración (Testcontainers) | 41/41 (almacén, puente, filtro) | CS7036 (`AssistantSettingsStore` sin `ICompanyContext`) tras adaptar los constructores; mutación comprobada con `IgnoreQueryFilters` temporal | 9/9 | Tres compañías, sin fila, `xmin` de B intacto, auditoría con la compañía, `23505` → `Conflict`, `xmin` obsoleto, token ajeno, sin compañía | Limpio |
| 4a.8 | `IT/.../AssistantConfigurationEndpointsCompanyTests` | Integración | 177/177 de asistente | «Verde a la primera» (la generalización de 4a.3 y 4a.7 ya lo cubría); mutación: `UsesPlatformAssistant` fijado a verdadero hace fallar 3 de 11 | 11/11 | Sin fila, demo con entorno, A/B, segundo Manager, clave ajena en la prueba, sin identificador de compañía (cuerpo, consulta, cabecera), Employee 403 | Limpio |
| 4a.8b | `IT/.../AssistantResetAndPriorityTests` | Integración | 177/177 | «Verde a la primera»; mutación de la guarda del motor hace fallar 2 de 7 | 7/7 | Restablecer en otra compañía y en la demo, solo la propia, token obsoleto (almacén), tabla ausente, guardado gana al entorno, `None`, sin fila | Limpio |
| 4a.9 | `IT/.../AssistantEndpointDisabledTests` + `IFT/.../ChatAssistantEngineTests` | Integración / unitaria | 177/177 y 33/33 | Motor: pruebas de `IsEnabled`/`ReplyAsync` para otra compañía en rojo (el singleton servía a todas); mutación de la guarda: 5 de 11 de integración fallan | 4/4 + 3 de motor | Otra compañía sin fila, con fila propia (sin instantánea ni clave de plataforma), demo conserva el comportamiento | Limpio |
| 4a.9 (puente) | `IFT/.../AssistantSettingsLoaderTests`, `AssistantRuntimeReloaderTests` | Unitaria | 33/33 | CS0246/errores de compilación (`AssistantSnapshot.DisabledVersion`, `Companies`) | 418/418 de infraestructura | No plataforma sin fila, con fila (sin clave ni modelo del entorno), versión del sondeo, `42P01` de `companies`, otro fallo, recarga solo de la compañía por defecto | Limpio |
| 4a.10 | `IT/.../AssistantToolsIsolationTests` | Integración | 7/7 de herramientas (Infrastructure) | «Verde a la primera» (el filtro y el alcance por responsable ya aislaban); controles positivos: el propio miembro devuelve 60 min | 7/7 | Esquemas sin compañía, equipo, mis fichajes, GUID de B idéntico al inexistente y sin consulta a `time_entries`, argumentos extra, fechas antes que miembro, registro sin `memberId` ni nombres de B | Limpio |
| 4a.11 | — | — | Suite completa | — | `AssistantAuditAppendOnlyTests` y auditoría con `company_id` en verde | — | Sin duplicación |
| 4a.12, 4a.13 | `AR/DeliveryArtifactsTests` | Arquitectura | 36/36 | 4 de 40 en rojo: faltaban los ADR 0020 y 0021, el recuento de 21 y el índice del README | 40/40 | Contenido de cada ADR, huella SHA-256 de 0017, 0018 y 0019 (no se editan), marcas en el índice | Limpio |
| 4a.14 | — | — | Suite completa | — | Puerta de la PR (abajo) | — | — |

Además, `IgnoreQueryFiltersClosedListTests.TheTransitionalTolerance_ExpiresAsSoonAsAnEntryGetsAUse` (arquitectura, 25/25): verde a la primera; mutación comprobada una vez (un `IgnoreQueryFilters` temporal en `AssistantSettingsStore` la hace fallar con el mensaje esperado).

### Resumen de pruebas

- Total tras la PR: 95 + 360 + 418 + 219 + 288 + 736 = **2 116 pruebas** (antes 2 034), todas en verde. Nuevas: aplicación +23, infraestructura +16, arquitectura +4, integración +38 (más la prueba autoexpirable en arquitectura).
- Ninguna llamada a un proveedor real de IA; valores de prueba inventados (`clave-prueba-123456`).

### Evidencia de unidad de trabajo

| Evidencia | Valor |
|---|---|
| Prueba focalizada | `T-AT --filter "FullyQualifiedName~Assistant"` (en verde), `T-IFT --filter "FullyQualifiedName~AesGcm"` (40) y `ASPNETCORE_URLS=http://127.0.0.1:0 T-IT --filter "FullyQualifiedName~Assistant"` (177 en verde, Docker) |
| Arnés de ejecución | La API real con PostgreSQL (Testcontainers) y dos compañías: `GET`/`PUT`/`DELETE`/prueba de `/api/asistente/configuracion` con tokens de A, de B y de la demo, el chat con el puente y el almacén con `xmin`; fábrica y probador falsos. Nada contra producción |
| Límite de reversión | Revertir los cinco commits de código y ADR y restaurar `..\TimeTracker.rollback\pr-mc4a\`; sin migración. Tras un guardado v2, volver a introducir la clave (ADR 0021) |

## Puerta de la PR (todo en verde)

| Paso | Resultado |
|---|---|
| DOCKER-CHECK | `docker info` responde (29.7.2); solo lectura |
| Entorno limpio | Sin variables `Cors__`, `ConnectionStrings__`, `Database__`, `Jwt__`, `Seed__`, `Ai__`, `Registration__`, `Company__`, `ForwardedHeaders__`, `ASPNETCORE_`, `DOTNET_ENVIRONMENT` |
| RESTORE | Correcto |
| BUILD | `-warnaserror`: 0 advertencias, 0 errores |
| TEST | Domain 95, Application 360, Web 288, Infrastructure 418, Architecture 219, Api.Integration 736: 0 fallos (con `ASPNETCORE_URLS=http://127.0.0.1:0` solo en ese comando, por el puerto 5000 reservado) |
| COV-A | Application: líneas 99,9 %, ramas 96,9 % (mínimo 80 %) |
| COV-D | Domain: líneas 100,0 %, ramas 100,0 % (mínimo 100 %) |
| FORMAT | `--verify-no-changes`: sin cambios |
| VULN | Sin `has the following vulnerable packages` |
| EF-PENDING | «No changes have been made to the model since the last migration» (`--startup-project src/TimeTracker.Infrastructure`); sin modelo ni migración nuevos |

## Desviaciones

Detalle en `design.md`, sección 9, «Desviaciones y aclaraciones del diseño surgidas en la PR 4a»: la spec manda sobre DD-13 (el modelo y el esfuerzo del entorno solo valen para la compañía de plataforma), origen `default`, lectura de la compañía en los casos de uso, guarda del puente por identificador (el indicador lo respeta el cargador), recarga solo de la compañía por defecto, `TransitionallyUnused` mantenida con prueba autoexpirable, herramientas probadas sobre el conjunto de herramientas con repositorios reales (el chat de B llega con la 4b), «restablecer con token obsoleto» a nivel de almacén y dobles enlazados.

## Riesgos abiertos

- La tolerancia `TransitionallyUnused` se retira en la 4b.7 (la prueba autoexpirable falla en cuanto `ListVersionsAsync` use `IgnoreQueryFilters`).
- La Web muestra el origen `default` sin traducir hasta la PR 7.
- El puente solo sirve a la compañía por defecto: una compañía registrada que guarde su configuración no tendrá asistente hasta la PR 4b (esperado; el registro sigue cerrado).
- Revertir tras un guardado v2 obliga a volver a introducir la clave (ADR 0021).
- Q-8 (custodia de la clave maestra) sigue abierta y gana importancia: protege a todas las compañías.

---

# PR 4b — Runtime por compañía

Rama `feat/multi-compania-pr4b-runtime` (apilada sobre `feat/multi-compania-pr4a-datos`, PR #40; cadena `stacked-to-main`) · Strict TDD · Copia previa en `..\TimeTracker.rollback\pr-mc4b\` (183 archivos).

## Lo que queda al responsable

Nada de producción en esta PR (sin migración ni cambio de esquema; no se accede a Railway). Al desplegarla:

- Las claves `Ai__RequestsPerMinutePerCompany`, `Ai__DailyRequestsPerCompany`, `Ai__MaxLoadedCompanies` y `Ai__RuntimeIdleMinutes` tienen valores por defecto (30, 500, 100 y 60): no hace falta definirlas. Un valor fuera de rango o no numérico impide arrancar.
- Los contadores de cuota siguen en memoria de la instancia (ADR 0017): se reinician al desplegar.
- Rollback antes de abrir el registro: revertir la PR vuelve al runtime singleton de la compañía de plataforma. Tras abrirlo no hay `Down` ni se revierte esta PR (fix-forward).
- Es la última de las cuatro condiciones de apertura del registro (PR 1, 1b, 2, 3, 4a y 4b fusionadas y desplegadas, tareas 9.4 y 9.6). El registro sigue apagado.

## Commits de esta entrega

| Commit | Mensaje | Contenido |
|---|---|---|
| `5de50b7` | `feat(multi-compania): topes del asistente por compañía validados al arrancar y documentados` | 4b.3 y 4b.12: `AiOptions`, validador, `AiOptionsBinder`, `.env.example` |
| `b7d74fd` | `feat(multi-compania): ritmo por minuto y tope diario del asistente por compañía` | 4b.2 (V-17), 4b.9, 4b.10: `IAssistantQuota`, `InMemoryAssistantQuota`, caso de uso y endpoint |
| `a4bc5ca` | `feat(multi-compania): registro de instantáneas del asistente por compañía, sondeo global y retirada del puente` | 4b.4 a 4b.8, 4b.11 y la retirada del puente: registro, lector, resolución, sondeo, motor, `ListVersionsAsync`, listas cerradas, medición V-8 |
| (siguiente) | `test(multi-compania): la medición de memoria solo exige una cota superior` | Ajuste de la prueba de V-8 |
| (este) | `docs(openspec): progreso de apply de la PR 4b de multi-compania` | `tasks.md`, `design.md`, este archivo y las correcciones de documentación de la PR 4a (ADR 0021, tarea 4a.3, fila 4a.1) |

Tamaño (`git diff 097cd2a HEAD`, tres commits de código): `src` 28 archivos, **+1 001 / -316**; pruebas 34 archivos, **+2 418 / -763**; `.env.example` +8. El código de producción queda por debajo de la previsión de 700-1000 líneas netas; el volumen de pruebas (registro, expulsión, sondeo, cuotas, A/B) es lo que pesa.

## Evidencia de Strict TDD (ciclo por tarea)

| Tarea | Archivo de prueba | Capa | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 4b.1 | — | — | N/A (copia) | N/A | `..\TimeTracker.rollback\pr-mc4b\` | N/A | N/A |
| 4b.2 | — (decisión V-17) | — | N/A | N/A | Anotada en `design.md` | N/A | N/A |
| 4b.3 | `IFT/AiOptionsCompanyLimitsTests`, `IT/Startup/AiCompanyLimitsStartupTests`, `IT/Assistant/CompanyLimitsNotEditableTests` | Unitaria / integración | 93 de opciones y 70 de arranque | CS0117 (las propiedades no existían); el valor `muchas` se imprimía en la excepción del enlazador (4 en rojo) | 97/97 y 25/25 | Cuatro claves × (0, mínimo, máximo, máximo + 1), sin proveedor, proveedor desconocido, no numérico, `appsettings` | Limpio. `CompanyLimitsNotEditable`: verde a la primera (el cuerpo estricto de la 4a ya lo rechazaba); mutación no ejecutada |
| 4b.4 | `IFT/AssistantRuntimeRegistryTests`, `CompanySettingsReaderTests` | Unitaria (reloj falso) | 457 de asistente en Infrastructure | CS0246 (`AssistantRuntimeRegistry`, `AssistantOptionsResolver`, `ICompanySettingsReader`); mutación comprobada: la plataforma para todas las compañías hace fallar 3 | 30 del registro y 7 del lector | Concurrencia (una sola lectura), A/B, clave propia, sin fila con y sin plataforma, primera fila inválida / no descifrable / no permitida / fábrica que falla, fallo y plazo agotado sin cachear, cancelación del llamante, recarga, versión repetida, conservar la última válida con un aviso por versión, pérdida y recuperación del indicador | Limpio |
| 4b.5 | `IFT/AssistantRuntimeMemoryTests` | Unitaria (medición) | N/A (nueva) | CS0246 | 3/3 | 1, 20 y 100 compañías: 4 KiB, 203 KiB y 266 KiB | Cota solo superior (la medición puntual es ruidosa) |
| 4b.6 | `IFT/AssistantRuntimeEvictionTests` | Unitaria (reloj falso) | Registro 30/30 | CS0246 (como 4b.4) | 11/11 | Expulsión de la menos usada, el uso reciente protege, recarga tras expulsión, petición en curso y gracia de 120 s, deshabilitadas no cuentan, un guardado que habilita pasa a contar, concurrencia, caducidad por inactividad (incluye deshabilitadas), aviso con límite de frecuencia, cierre | Margen de 1 s de `AskAsync` corregido en la prueba |
| 4b.7 | `IT/AssistantSettingsListVersionsTests`, `IFT/AssistantSettingsPollerTests`, `AR/IgnoreQueryFiltersClosedListTests` | Integración (Testcontainers) / unitaria / arquitectura | 223 de arquitectura y 12 de sondeo | CS1061 (`ListVersionsAsync`) y la lista cerrada con una entrada sin uso (tolerancia retirada) | 4/4, 13/13 y 223/223 | Una consulta sin entidades y sin compañía en el contexto, seguimiento de guardado y borrado, indicador, sin tablas; solo compañías cargadas, versión `"0"`, versión rechazada, máximo de 4 recargas en paralelo, errores sin propagar con aviso por racha y línea al recuperarse, precarga acotada | `TransitionallyUnused` retirada: la lista queda con 2 sitios y ninguna entrada sin uso |
| 4b.8 | `IFT/ChatAssistantEngineTests`, `ChatAssistantEngineSnapshotTests`, `AT/AskAssistantUseCaseTests` | Unitaria | 339 + 132 de motor y 38 del caso de uso | CS0535 (`GetAvailabilityAsync`) y constructor del motor | Verde | Compañía del token, una sola lectura por petición, fallo de carga = no disponible sin cachear, instantánea intercambiada a mitad | Puente y guarda `ServesCompany` retirados |
| 4b.9 | `IFT/InMemoryAssistantQuotaTests` | Unitaria (reloj falso) | 8 de cuota | CS1061 (`TryConsumeDaily` / `TryConsumeMinute`) y `QuotaScope` | 18/18 | Tope de compañía, ámbito de usuario, el rechazo de un ámbito no consume el otro, 22:00 → 7200 s y reinicio, concurrencia (300 usuarios, 25 permitidos), ventana de un minuto, purga de un solo barrido | Limpio |
| 4b.10 | `IT/AssistantCompanyRateLimitTests`, `AT/AskAssistantUseCaseTests` | Integración / unitaria | 23 de límites | Compilación (cuota) | 11/11 y 39/39 | 429 con `Retry-After`, `detail` propio y CORS; reinicio de ventana; el rechazo por usuario no consume; anónimos; `400` y `503` cuentan; diario y reinicio; A no afecta a B | Limpio |
| 4b.11 | `IT/AssistantRuntimeIsolationTests` | Integración (Testcontainers, sondeo con reloj falso) | Suite de asistente 177 | «Verde a la primera» tras compilar el registro; la corrección fue de una expectativa (la demo precarga su instantánea) | 7/7 | A no habilita B, la clave de plataforma nunca sirve a otra compañía, clave propia, guardado externo con sondeo, dos instancias, borrado de fila, pérdida del indicador de plataforma | Limpio |
| 4b.12 | `AR/DeliveryArtifactsTests` | Arquitectura | 40 | 5 de 18 en rojo (claves y comentario ausentes) | 45/45 | Cuatro claves comentadas con su valor y comentario de que no las edita un Manager | Limpio |
| 4b.13 | — | — | Suite completa | — | Puerta de la PR (abajo) | — | `dotnet format` sin cambios |

### Resumen de pruebas

- Total tras la PR: 95 + 366 + 473 + 223 + 288 + 782 = **2 227 pruebas** (antes 2 116), todas en verde. Se retiran las del cargador singleton (sustituidas por las del registro) y dos del puente.
- Ninguna llamada a un proveedor real de IA; valores de prueba inventados (`clave-prueba-123456`, `clave-plataforma-123456`). Sin `Thread.Sleep`: el tiempo va con `FakeTimeProvider` y las esperas con señales (`TaskCompletionSource`, `PollCompleted`).

### Evidencia de unidad de trabajo

| Evidencia | Valor |
|---|---|
| Prueba focalizada | `T-IFT --filter "FullyQualifiedName~Assistant"` (en verde), `ASPNETCORE_URLS=http://127.0.0.1:0 T-IT --filter "FullyQualifiedName~Assistant"` (en verde, Docker) y `T-AR` |
| Arnés de ejecución | La API real con PostgreSQL (Testcontainers), el registro y el sondeo reales y dos compañías: guardar en A y preguntar, B sin fila, clave de plataforma solo para la demo, guardado externo por SQL, dos instancias sobre la misma base, pérdida del indicador. Nada contra producción |
| Límite de reversión | Revertir los tres commits de código y restaurar `..\TimeTracker.rollback\pr-mc4b\`; sin migración. Antes de abrir el registro vuelve el runtime singleton; tras abrirlo, fix-forward |

## Puerta de la PR (todo en verde)

| Paso | Resultado |
|---|---|
| DOCKER-CHECK | `docker info` responde (29.7.2); solo lectura |
| Entorno limpio | Sin variables `Cors__`, `ConnectionStrings__`, `Database__`, `Jwt__`, `Seed__`, `Ai__`, `Registration__`, `Company__`, `ForwardedHeaders__`, `ASPNETCORE_`, `DOTNET_ENVIRONMENT` |
| RESTORE | Correcto |
| BUILD | `-warnaserror`: 0 advertencias, 0 errores |
| TEST | Domain 95, Application 366, Web 288, Infrastructure 473, Architecture 223, Api.Integration 782: 0 fallos (con `ASPNETCORE_URLS=http://127.0.0.1:0` solo en ese comando) |
| COV-A | Application: líneas 99,7 %, ramas 97,0 % (mínimo 80 %) |
| COV-D | Domain: líneas 100,0 %, ramas 100,0 % (mínimo 100 %) |
| FORMAT | `--verify-no-changes`: sin cambios |
| VULN | Sin `has the following vulnerable packages` |
| EF-PENDING | «No changes have been made to the model since the last migration»; sin modelo ni migración nuevos |

## Desviaciones

Detalle en `design.md`, sección 9, «Desviaciones y aclaraciones del diseño surgidas en la PR 4b»: estructura del runtime (lector, resolvedor y soporte por compañía), `IAssistantEngine.GetAvailabilityAsync`, sondeo con `LEFT JOIN` que incluye el indicador de plataforma, sondeo interno, tope que cuenta solo compañías con proveedor, `AiOptionsBinder` para el valor no numérico, el `400` por cuerpo ilegible no cuenta para el minuto, guardar con la compañía no cargada y cambios de pruebas.

Avisos de la revisión de la PR 4a: (1) **resuelto** (la compañía que pierde `uses_platform_assistant` degrada a deshabilitado aunque su fila se rechace, y el cambio del indicador recarga); (2) **resuelto** (ADR 0021: desviación deliberada de la convención «Sustituido por» del ADR 0001, un guardado que conserve la clave recifra a v2 y revertir obliga a volver a introducir la clave; nota «ver desviación 1» en la tarea 4a.3; fila 4a.1 de la tabla de evidencia de la PR 4a).

## Riesgos abiertos

- Si el indicador pasa de falso a verdadero y la fila de esa compañía queda rechazada, el sondeo la vuelve a leer en cada vuelta (sin repetir el aviso).
- Los contadores de cuota y las instantáneas son por instancia (ADR 0017): con réplicas, cada una cuenta aparte y converge por el sondeo.
- Con mucha actividad simultánea el coste real son las conexiones abiertas al proveedor (V-8 mide en reposo).
- Q-8 (custodia de la clave maestra) sigue abierta.

---

# PR 5 — Autorregistro con código de invitación

Rama `feat/multi-compania-pr5-registro` (apilada sobre `feat/multi-compania-pr4b-runtime`, PR #41; cadena `stacked-to-main`) · Strict TDD · Copia previa en `..\TimeTracker.rollback\pr-mc5\` (82 archivos; la carpeta no existía y se creó) · `Registration__Enabled` **no se activa en ningún entorno**.

## Lo que queda al responsable

Nada de producción en esta PR (sin migración ni cambio de esquema; no se accede a Railway). Puertas previas a la **apertura del registro** (tarea 9.6), no al despliegue de esta PR:

- **5.9 V-7** — confirmar en Railway `ForwardedHeaders__Enabled=true` y `ForwardedHeaders__Hops >= 1` (el valor por defecto en código es 2: «cliente, borde»). Sin ello el límite por IP es una partición global o falseable. El validador de arranque lo exige en `Production` con el registro abierto, así que la API no arrancaría; queda **sin marcar** en `tasks.md`.
- Al desplegar con el registro cerrado (`Registration__Enabled` sin definir o `false`) la ruta **no existe**: `401` sin token y `404` con token. No hay que definir `Registration__InviteCodes` hasta abrirlo; entonces, de 16 a 128 caracteres por código, solo en Railway, y para rotarlos se sustituye la lista completa.
- Orden de apertura (runbook, PR 8): `Seed__DemoUsers=false`, comprobar que no se recrean, retirar los usuarios demo, definir los códigos y, al final, `Registration__Enabled=true`. Q-1 (qué hacer con la compañía Demo si tiene usuarios reales) sigue siendo decisión previa del responsable; el validador de arranque (5.11b) impide abrir el registro en `Production` mientras haya correos `@example.test` en la compañía por defecto.

## Commits de esta entrega

| Commit | Mensaje | Contenido |
|---|---|---|
| `80fcb07` | `feat(multi-compania): política de contraseñas, código de invitación y caso de uso del autorregistro` | 5.2 a 5.4: `PasswordPolicy`, `InviteCodeValidator`, `RegisterCompanyUseCase`, puerto `ICompanyRegistrationStore`, predicados de dominio, pruebas de arquitectura |
| `ac78cda` | `feat(multi-compania): almacén del autorregistro con compañía y Manager en un solo guardado` | 5.5: `CompanyRegistrationStore` y lista cerrada de `CreateScope` |
| `a874dce` | `feat(multi-compania): validadores de arranque del registro y de los usuarios de demostración` | 5.10, 5.11, 5.11b |
| `3d7baad` | `feat(multi-compania): endpoint de autorregistro con código de invitación, límite por IP y contrato` | 5.6, 5.6b, 5.7, 5.7b, 5.8 y `docs/api/openapi.json` |
| `bbedb72` | `docs(multi-compania): claves del registro en .env.example y ADR 0022` | 5.12, 5.13 |
| `83ee1ae` | `fix(multi-compania): el fallo de ForwardedHeaders:Hops explica por qué el límite por IP lo necesita` | mensaje de arranque (5.11) |
| (este) | `docs(openspec): progreso de apply de la PR 5 de multi-compania` | `tasks.md`, `design.md` (V-7, V-12 y desviaciones), este archivo |

Tamaño (`git diff 58f6557 HEAD` de los seis commits de código y documentación): 63 archivos, **+3 657 / -20**; producción (`src`) **+654 / -13** (30 archivos), pruebas **+2 822 / -6** (29 archivos), documentación (`.env.example`, README y ADR 0022) **+181 / -1**, y `docs/api/openapi.json` generado **+118**. La previsión era de 500-800 líneas: el código de producción cabe; el volumen son las pruebas de una superficie pública (privacidad, límites, arranque).

## Evidencia de Strict TDD (ciclo por tarea)

| Tarea | Archivo de prueba | Capa | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 5.1 | — | — | N/A (copia) | N/A | `..\TimeTracker.rollback\pr-mc5\` (82 archivos) | N/A | N/A |
| 5.2 | `AT/PasswordPolicyTests`, `AT/LoginUseCaseTests` | Unitaria | 17/17 de login | CS0103 («`PasswordPolicy` no existe») | 15/15 y 18/18 | 11/12/13/128/129 caracteres, sin composición, solo espacios, espacios exteriores contados, nulo y vacío, campo nombrado y contraseña nunca repetida; el login acepta 8 caracteres. Mutación: aplicar la política en el login hace fallar 2 pruebas | `FieldError` común |
| 5.3 | `AT/Companies/InviteCodeValidatorTests`, `AR/RegistrationApplicationTests` | Unitaria / arquitectura | N/A (nuevo) | CS0246 («`InviteCodeValidator` no existe») | 18/18 y 26/26 | Cada código, ausente, en blanco, incorrecto, mayúsculas, 127/128/129, recorte, vacío configurado, sin códigos, `MatchesAny` visita todos aunque coincida el primero. Mutaciones: cortocircuito (falla la prueba de recorrido), sin tope de 128 (falla la de código sobredimensionado) y `==` (falla el escáner de arquitectura) | `MatchesAny` pura e inyectable |
| 5.4 | `AT/Companies/RegisterCompanyUseCaseTests`, `AR/RegistrationApplicationTests`, `DT` | Unitaria / dominio | suite de aplicación en verde | CS0246 (`RegisterCompanyUseCase`, `ICompanyRegistrationStore`) | 26/26 (+16 de dominio) | Orden código, campos, hash y almacén; cada campo inválido aislado y todos juntos; límites de nombres; duplicados distintos; fallo del almacén sin sesión; emisor común y compañía no activa. Mutaciones: sin comprobar el código y rol `Employee` hacen fallar 5 y 1 | Predicados `IsValid*` compartidos con `Create`; `Created` lleva `CompanyId` (RED: CS1061) |
| 5.5 | `IT/Registration/CompanyRegistrationStoreTests` | Integración (Testcontainers) | N/A (nuevo) | CS0246 (`CompanyRegistrationStore`) | 7/7 | Un guardado, ámbito de la compañía nueva, nombre y correo duplicados con atomicidad, acento distinto, carrera de dos registros iguales, violación ajena no mapeada, Manager de otra compañía. Mutaciones: intercambiar `DuplicateName` y no añadir el Manager hacen fallar 1 y 3 | `CreateScopeClosedListTests` (RED: llamador fuera de la lista) |
| 5.10 | `IT/Startup/RegistrationOptionsValidatorTests` | Integración (sin base de datos) | pruebas de arranque existentes en verde | CS0246 (`RegistrationOptions`) | 21/21 | Defecto, no booleano, sin códigos o en blanco, cortos, elemento en blanco, 129 caracteres, 16/128, varios, recorte, duplicados, cerrado ignora los códigos, valor no impreso (una prueba nació roja por la spec: ver desviación 1). Mutaciones: rango del código y validar con el registro cerrado hacen fallar 4 y 3 | — |
| 5.11 | `IT/Startup/RegistrationStartupGuardsTests` | Integración | pruebas de opciones existentes en verde | 4 de 13 en rojo (sin guardas); la de `Hops = 0` nació «verde a la primera» (ya lo detenía `AddForwardedHeadersBehindProxy`; la ampliación del mensaje tuvo su propio RED antes del commit `83ee1ae`) | 13/13 | Siembra más registro, `DemoData`, solo siembra, solo registro, `Production` con y sin `ForwardedHeaders` (`Hops` 1, 2 y por defecto), fuera de `Production`, cerrado. Cada fallo se comprueba contra una base vacía con migraciones activadas que sigue sin tablas. Mutaciones: sin guarda de siembra y sin comprobar el entorno hacen fallar 2 y 2 | Lectura de las opciones al componer la aplicación |
| 5.11b | `IT/Startup/DemoUsersStartupGuardTests`, `IT/Persistence/UserEmailDomainTests` | Integración | N/A (nuevo) | CS1061 (`ExistsWithEmailDomainAsync`) | 7/7 y 4/4 | Con usuario demo falla sin correos ni códigos; sin usuarios arranca (tras las migraciones); registro cerrado; fuera de `Production`; correo demo en otra compañía; otro dominio; ámbito de la compañía por defecto; comparación literal del final del correo y acotado por compañía. Mutaciones: guarda inactiva y `Contains` fijo hacen fallar 1 y 3 | `DemoUsers.EmailDomain` |
| 5.6 | `IT/Registration/RegisterCompanyEndpointTests`, `RegistrationPrivacyTests` | Integración (Testcontainers; Kestrel para el `413`) | N/A (nuevo) | Compilación (el endpoint no existía) | 21/21 y 5/5 | `201` con el cuerpo del login y token de la compañía nueva; la persona queda dentro; hash con espacios y login real; ambos códigos y reutilización; `403` idéntico (cuatro causas) y antes que el `409`; nombre y correo repetidos; acento; carrera; campos aislados; 11/12/128/129; cuerpo ausente y roto; propiedades desconocidas; `413`; asistente `503` en la compañía nueva; privacidad de registros y respuestas con marcador; misma forma con el segundo código. Mutaciones: código en el registro, `403` distinto por causa, sin `AllowAnonymous` y sin tope de cuerpo hacen fallar 1, 1, 29 y 1 | `ProblemDetails` común |
| 5.6b | `IT/Registration/RegisterWithTokenTests`, `AR/SkipCompanyContextTests` | Integración / arquitectura | suite de arquitectura en verde | CS0246 (`SkipCompanyContextMetadata`) | 5/5 y 7/7 | Token de A: compañía nueva distinta, datos de A intactos, único `Set` con la compañía nueva; sin token; marcador solo en el registro; middleware con y sin marcador. Mutación: sin `SkipCompanyContext` hace fallar 2 | — |
| 5.7 | `IT/Registration/RegistrationClosedTests`, `AR/OpenApiContractTests` | Integración / arquitectura | pruebas de contrato existentes en verde | Compilación y contrato sin la ruta | 5/5 y 24/24 | Cerrado: `401` sin token y `404` problem+json con token, igual que una ruta inexistente, sin evaluar el código ni leer la base de datos; ruta ausente de la tabla de endpoints (sin definir y `false`); `Production`; abierto, la ruta existe solo con `POST`. Contrato: `post` anónimo con `201/400/403/409/429`, cinco propiedades, sin código ficticio ni ejemplo. Mutación: mapeo siempre activo hace fallar 5 | `DocumentGeneration` fuerza el registro solo al exportar |
| 5.7b | `IT/Assistant/AssistantContractAndAuthorizationCoverageTests` | Integración | suite de asistente en verde | «Verde a la primera» (ajusta cobertura; el registro ya existía): anónimos tres con el registro cerrado y cuatro con él abierto, `GET /api/asistente` da `405`, contrato vivo de `/api/asistente`; mutación: quitar `AllowAnonymous` del registro hace fallar la cobertura | 5/5 | — | — |
| 5.8 | `IT/Registration/RegistrationRateLimitTests` | Integración | pruebas de límites existentes en verde | Compilación | 7/7 | Cinco `403` y sexto `429` sin evaluar el código (contador); los éxitos también cuentan; CORS y cuatro cabeceras de seguridad; otra IP; `X-Forwarded-For` antepuesto con un salto; sin `ForwardedHeaders`, partición única; independiente del login. Mutación: límite 6 hace fallar 6 | — |
| 5.12 | `AR/DeliveryArtifactsTests` | Arquitectura | 45 | 5 de 6 en rojo (claves y comentarios ausentes); la de compose nació «verde a la primera» (mutación: añadir `Registration__` al compose la hace fallar) | 51/51 | `Registration__Enabled=false` única línea activa, códigos y tope comentados sin valor, comentarios exigidos, sin valores de claves maestras, V-12 (`ExportedDocument_KeepsPasswordAndCompanyNameAsLegitimateNames`) | — |
| 5.13 | `AR/DeliveryArtifactsTests` | Arquitectura | 45 | 3 en rojo (ADR inexistente, recuento de 22, índice) | 51/51 | Contenido del ADR, sin valores secretos, índice del README | — |
| 5.14 | — | — | Suite completa | — | Puerta de la PR (abajo) | — | `dotnet format` sin cambios |

### Resumen de pruebas

- Total tras la PR: 111 + 426 + 288 + 473 + 268 + 889 = **2 455 pruebas** (antes 2 227; +228: dominio +16, aplicación +60, arquitectura +45, integración +107), todas en verde.
- Ninguna llamada a un proveedor real de IA; valores inventados (`codigo-prueba-0123456789`, `contrasena-prueba-12345`, `ana@example.test`). Sin `Thread.Sleep` nuevo: la carrera se prueba con dos peticiones simultáneas y el límite sin esperar a que caduque la ventana.

### Evidencia de unidad de trabajo

| Evidencia | Valor |
|---|---|
| Prueba focalizada | `T-AT` (resultado y casos de uso del registro), `ASPNETCORE_URLS=http://127.0.0.1:0 T-IT --filter "FullyQualifiedName~Registration"` (todas en verde, Docker), `T-AR` (268) |
| Arnés de ejecución | La API real con PostgreSQL (Testcontainers): registro con código, token de la compañía nueva usado en `me`, equipo y fichajes, dos compañías A/B con un token vigente de A, límite por IP con y sin `ForwardedHeaders`, Kestrel real para el `413`, arranque en `Production` contra una base vacía con migraciones. Nada contra producción |
| Límite de reversión | `Registration__Enabled=false` cierra el alta al instante; revertir la PR (seis commits de código y documentación) y restaurar `..\TimeTracker.rollback\pr-mc5\` no toca datos ni esquema |

## Puerta de la PR (todo en verde)

| Paso | Resultado |
|---|---|
| DOCKER-CHECK | `docker info` responde (29.7.2); solo lectura |
| Entorno limpio | Sin variables `Cors__`, `ConnectionStrings__`, `Database__`, `Jwt__`, `Seed__`, `Ai__`, `Registration__`, `Company__`, `ForwardedHeaders__`, `ASPNETCORE_`, `DOTNET_ENVIRONMENT` |
| RESTORE | Correcto |
| BUILD | `-warnaserror`: 0 advertencias, 0 errores |
| TEST | Domain 111, Application 426, Web 288, Infrastructure 473, Architecture 268, Api.Integration 889: 0 fallos (con `ASPNETCORE_URLS=http://127.0.0.1:0` solo en ese comando) |
| COV-A | Application: líneas 99,6 %, ramas 96,8 % (mínimo 80 %) |
| COV-D | Domain: líneas 100,0 %, ramas 100,0 % (mínimo 100 %) |
| FORMAT | `--verify-no-changes`: sin cambios |
| VULN | Sin `has the following vulnerable packages` |
| EF-PENDING | «No changes have been made to the model since the last migration»; sin modelo ni migración nuevos |
| Contrato | `dotnet build src/TimeTracker.Api --no-incremental`: el `docs/api/openapi.json` conserva el mismo SHA-256 (comprobado a mano) |

## Desviaciones

Detalle en `design.md`, sección 9, «Desviaciones y aclaraciones del diseño surgidas en la PR 5»: el ejemplo `corto-secreto-prueba` de la spec mide 20 caracteres (la prueba usa uno de 15), `Hops < 1` ya lo detenía el registro de `ForwardedHeaders` (solo se amplía el mensaje), las opciones se validan al componer la aplicación, `Created` lleva `CompanyId` para el registro de metadatos, predicados de dominio compartidos, un `Content-Type` no JSON anónimo recibe `401` (no llega al endpoint, como el login), la comprobación del SHA-256 del contrato se hizo a mano, dos entradas nuevas en la lista cerrada de `CreateScope`, aislamiento del entorno de pruebas ampliado y registro del rechazo por límite.

## Riesgos abiertos

- **V-7 sin comprobar en Railway** (5.9): bloquea la apertura del registro, no el código ni su despliegue con el registro cerrado.
- **Enumeración por `409`** y **registro sin verificación de correo** (aceptados, ADR 0022): un código filtrado permite altas hasta que se sustituya la lista completa.
- **Los contadores del limitador son por instancia** (ADR 0017): con réplicas, cada una cuenta aparte y reinicia al desplegar.
- **Límite de cuerpo de 4 KiB** (`413`) es una decisión de diseño no fijada por la spec.
- Q-1, Q-8 y Q-10 (proceso operativo de apertura y rotación) siguen abiertas.

---

# PR 6 — Alta de empleados, contraseña temporal y cambio de contraseña

Rama `feat/multi-compania-pr6-empleados` (apilada sobre `feat/multi-compania-pr5-registro`, PR #42; cadena `stacked-to-main`) · Strict TDD · Copia previa en `..\TimeTracker.rollback\pr-mc6\` (24 archivos) · Sin migración ni cambio de esquema (el token `xmin` ya existía desde la PR 1) · `Registration__Enabled` **no se activa en ningún entorno**.

## Lo que queda al responsable

Nada de producción en esta PR (sin migración, sin cambio de esquema; no se accede a Railway). Para tener en cuenta al desplegar y operar:

- Desplegable tras la PR 5 sin puertas nuevas. Efecto a comunicar: los tokens vigentes siguen valiendo (el cambio forzado solo afecta a quien tenga `must_change_password = true`, es decir, a las personas dadas de alta con esta PR).
- `Company__MaxEmployees` es opcional (50 por defecto, de 1 a 1000, solo en Railway si se quiere otro valor); un valor fuera de rango o no numérico impide el arranque nombrando la clave.
- **Runbook (PR 8):** revisar periódicamente las cuentas con `must_change_password` antigua (la contraseña temporal no caduca) y recordar que un token `mcp` ya emitido no se invalida al regenerar (30 minutos). Ambos riesgos están aceptados en el ADR 0022.
- **Pendiente aparte (no se toca aquí):** el límite por IP del registro frente a IPv6 y `ForwardedHeaders` es una PR de seguimiento. Esta PR solo añade límites por usuario del token (no por IP) y no modifica el reparto por IP ni `ForwardedHeaders`.

## Commits de esta entrega

| Commit | Mensaje | Contenido |
|---|---|---|
| `4bdd856` | `feat(multi-compania): casos de uso del alta de empleados, la regeneración y el cambio de contraseña` | 6.3 a 6.6: `CreateTeamMemberUseCase`, `ResetTemporaryPasswordUseCase`, `ChangePasswordUseCase`, `TemporaryPasswordGenerator`, `SessionIssuer.IsCompanyActiveAsync`, `UserRepository` (alta bajo tope en `Serializable`, lecturas seguidas, guardado con `xmin`) y su spike V-14 |
| `84d874f` | `feat(multi-compania): tope de empleados por entorno y cambio de contraseña obligatorio por middleware` | 6.9 y 6.11: `CompanyOptions`, validador, `ICompanyLimits`; `PasswordChangePendingMiddleware` y su marcador |
| `f70d2a2` | `feat(multi-compania): endpoints del alta de empleados, la regeneración y el cambio de contraseña, con contrato` | 6.7, 6.8, 6.10, 6.12 a 6.15b: endpoints, políticas de tasa, contrato `openapi.json`, pruebas HTTP, de arquitectura y de contrato; corrección del conflicto de serialización envuelto; prueba del lado aceptado de 4 KiB |
| `874b66c` | `docs(adr): ampliación del ADR 0022 con la contraseña temporal, el cambio forzado, el tope exacto y los riesgos aceptados` | 6.16: ADR 0022 y su prueba de contenido |
| (este) | `docs(openspec): progreso de apply de la PR 6 de multi-compania` | `tasks.md`, `design.md` (V-13, V-14 y desviaciones), este archivo |

Tamaño (`git diff 0d3ac25 HEAD` de los cuatro commits de código y ADR): 59 archivos, **+4 040 / -12**; producción (`src`) **+926 / -5** (30 archivos), pruebas **+2 750 / -5** (27 archivos), ADR 0022 **+15 / -2** y `docs/api/openapi.json` generado **+349**. La previsión era de 500-800 líneas: el código de producción cabe; el volumen son las pruebas de una superficie de seguridad (aislamiento, carreras, privacidad, contrato).

## Evidencia de Strict TDD (ciclo por tarea)

| Tarea | Archivo de prueba | Capa | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 6.1 | — | — | N/A (copia) | N/A | `..\TimeTracker.rollback\pr-mc6\` (24 archivos) | N/A | N/A |
| 6.2 (V-13) | — | — | N/A | N/A | Rutas y specs comprobadas (design.md, «Resultados de V-n») | N/A | N/A |
| 6.3 (V-14) | `IT/Employees/UserCapRepositoryTests`, `IT/Employees/UserRepositoryConcurrencyTests` | Integración (Testcontainers) | N/A (nuevo) | CS0535 (`UserRepository` no implementa `AddWithinCapAsync`) | 8/8 y 4/4 | Cinco altas simultáneas con barrera asíncrona (un `Created` y cuatro `CapReached`), **control sin transacción que supera el tope (7 con tope 3)**, bajo el tope y otra compañía, correo repetido, responsable ajeno (`23503`), reintentos acotados a 3 (`Busy`), reintento que se resuelve y fallo envuelto en dos niveles. Mutaciones: `ReadCommitted`, 5 reintentos y un solo nivel de excepción hacen fallar 1, 1 y 1 | Recorrido de toda la cadena de excepciones internas (RED real: un `500` esporádico con cinco altas HTTP) |
| 6.4 | `AT/Team/CreateTeamMemberUseCaseTests`, `AT/TemporaryPasswordGeneratorTests` | Unitaria | suite de aplicación en verde | CS0246 (`CreateTeamMemberUseCase`, `ITemporaryPasswordGenerator`, `AddUserOutcome`, `ICompanyLimits`) | 470/470 de aplicación | Rol por defecto y explícito (4 formas), responsable por defecto y de la misma compañía, ajeno, inexistente y no Manager con el mismo resultado, nueve campos inválidos aislados y todos juntos, correo repetido, tope, `Busy`; generador: 50 distintas y con política, alfabeto sin ambigüedades y completo. Mutaciones: sin `mustChangePassword`, rol del responsable sin comprobar | — |
| 6.5 | `AT/Team/ResetTemporaryPasswordUseCaseTests` | Unitaria | idem | CS0246 (`ResetTemporaryPasswordUseCase`) | idem | Regeneración correcta, el mismo `NotFound` para ajeno/inexistente/el propio Manager, ya cambiada → `Conflict`, conflicto de concurrencia → `Conflict`, generador real. Mutación: sin la comprobación de `MustChangePassword` | — |
| 6.6 | `AT/ChangePasswordUseCaseTests`, `AT/SessionIssuerTests` | Unitaria | idem | CS0246 (`ChangePasswordUseCase`, `IsCompanyActiveAsync`) | idem | Cambio correcto, compañía suspendida **sin verificar ni hashear**, usuario inexistente, actual errónea/vacía/enorme (sin hashear), nueva de 11/129, igual a la actual, ambas mal, conflicto `xmin`. Mutaciones: sin la comprobación de compañía y sin la regla «distinta de la actual» | — |
| 6.7 | `IT/Employees/EmployeeCreationEndpointTests` | Integración (HTTP + Testcontainers) | N/A (nuevo) | 46 de 98 en rojo antes de los endpoints (rutas inexistentes) | 16/16 | `201` con las cinco propiedades exactas, hash que verifica, entrada con `mcp`, tres contraseñas distintas, `401`/`403` antes de evaluar el cuerpo, `companyId`/`passwordHash` ignorados, responsable ajeno/inexistente/Employee con huella idéntica, responsable Manager de la misma compañía, `409` genérico entre compañías, cinco campos inválidos, límite 30/min con `Retry-After` y CORS y otro Manager sin afectar, sin eco en registros. Mutación: límite 31 | `Cache-Control` ya lo fija `SecurityHeadersMiddleware`: se retiró la cabecera redundante |
| 6.8 | `IT/Employees/EmployeeCapTests` | Integración | idem | idem | 5/5 | Tope en el límite y por debajo, **cinco `POST` simultáneos: un `201` y cuatro `409` (3 compañías por ejecución; 12 ejecuciones seguidas sin fallo)**, otra compañía sin afectar, tope por defecto de 50. Mutación: ver 6.3 | — |
| 6.9 | `IT/Employees/CompanyOptionsValidatorTests` | Integración (sin base de datos) | pruebas de opciones existentes en verde | CS0246 (`CompanyOptions`) | 9/9 | Defecto 50, 1/1000/3, 0/1001/-5 sin imprimir el valor, `muchos` sin imprimir el valor, sin clave en `appsettings*.json`. Mutaciones: rango máximo 2000, mínimo 0 y sin la comprobación previa del enlazador | — |
| 6.10 | `IT/Employees/RegenerateTemporaryPasswordEndpointTests` | Integración | N/A (nuevo) | rojo antes del endpoint | 7/7 | `200` con una sola propiedad, la anterior deja de entrar, `401`/`403`, `404` idéntico para ajeno/inexistente/otro Manager/uno mismo **con control del responsable que sí puede**, `409` por cambio previo, **carrera real con SQL entre lectura y guardado (`409` y la elegida no se pisa)**, sin eco, 30/min con contador propio. Mutaciones: regenerar sin filtrar por responsable y sin `IsConcurrencyToken` | — |
| 6.11 | `IT/Employees/PasswordChangePendingMiddlewareTests`, `AR/PasswordChangePendingMarkerTests` | Integración / arquitectura | pruebas de pipeline en verde | 3 de 14 en rojo (los controles pasaban) | 14/14 y 7/7 | Time-entries, equipo, asistente y fichaje → `403` problem+json sin base de datos; mismos endpoints sin `mcp` servidos (control); siete valores de `mcp` distintos de `"true"` tratados como sin cambio; `me` permitido; anónimos exentos; ruta inexistente `403` solo con `mcp`; orden con sonda en la autorización. Mutaciones: presencia en vez de valor, comparación sin distinguir mayúsculas, sin la exención anónima y middleware tras la autorización | — |
| 6.12 | `IT/Employees/ChangePasswordEndpointTests` | Integración | N/A (nuevo) | rojo antes del endpoint | 14/14 | Cambio correcto con token sin `mcp` y acceso (control: antes `403`), cinco contraseñas inválidas, 11 → `400` y 12 → `200`, `401`, compañía suspendida frente a activa, **carrera real con el Manager (`409`)**, 5/min con otro usuario libre, `userId`/`companyId` ignorados, sin eco, la temporal deja de valer. Mutaciones: sin `AllowWhilePasswordChangePending`, sin la política de tasa, sin la comprobación de compañía | — |
| 6.13 | `IT/Employees/ManagerCreationIsolationTests` | Integración | N/A (nuevo) | rojo antes de los endpoints | 3/3 | Manager nuevo con responsable, rol y `cid` del creador y configuración del asistente de A (nunca la de B), sin jerarquía transitiva, no puede crear bajo un Manager de B | — |
| 6.14 | `AR/OpenApiContractTests` | Arquitectura | contrato de la PR 5 en verde | rojo (rutas ausentes) | 7 pruebas nuevas en verde | Rutas, respuestas, Bearer, campos exactos, sin `companyId`/`passwordHash`/`employeeId`, `temporaryPassword` solo en dos esquemas, sin operación del tope, `companyName` solo donde corresponde. SHA-256 del contrato regenerado = versionado (a mano) | Sustituye la prueba «DoesNotYetContain…» de la PR 5 |
| 6.15 | `IT/Seeding/DemoUsersSeederTests` | Integración | siembra en verde | «Verde a la primera» (la siembra ya dejaba `must_change_password = false`); mutación: sembrar con la marca la hace fallar | 1 nueva en verde | Usuarios demo sin cambio pendiente, token sin `mcp`, contraseña errónea y correo desconocido con la misma huella | — |
| 6.15b | `IT/Authentication/PasswordStorageAndFallbackPolicyTests`, `IT/Security/AuthorizationCoverageTests` | Integración | cobertura de autorización en verde | «Verde a la primera» en lo ya cubierto; recorrido nuevo en rojo hasta el endpoint | 12 y 2 nuevas en verde | Hash/sal, recorrido registro → alta → regeneración → login → cambio con `temporaryPassword` solo en dos respuestas y sin contraseñas en registros, nueve rutas protegidas sin token (con y sin `Content-Type` JSON) y control `415` autenticado, endpoints de empleados con política Manager y de cambio con respaldo | — |
| 6.16 | `AR/DeliveryArtifactsTests` | Arquitectura | 51 | 1 en rojo (ADR sin las decisiones de la PR 6) | verde | Contenido del ADR sin valores secretos | — |

### Resumen de pruebas

- Total tras la PR: 111 + 470 + 288 + 473 + 281 + 985 = **2 608 pruebas** (antes 2 455; +153: dominio +0, aplicación +44, arquitectura +13, integración +96), todas en verde.
- Ninguna llamada a un proveedor real de IA; valores inventados (`contrasena-prueba-12345`, `ana@example.test`, `clave-de-prueba-123`). Sin `Thread.Sleep`: las carreras se provocan con una barrera asíncrona y con SQL concurrente justo antes del guardado (`OneShotRacingInterceptor`); el único retardo del código de producción es el escalonado de reintentos del alta (15 ms por intento).

### Evidencia de unidad de trabajo

| Evidencia | Valor |
|---|---|
| Prueba focalizada | `T-AT` (470), `ASPNETCORE_URLS=http://127.0.0.1:0 T-IT --filter "FullyQualifiedName~Employees"` (Docker; 100 en verde en 1 min), `T-AR` (281) |
| Arnés de ejecución | La API real con PostgreSQL (Testcontainers): alta, inicio de sesión con la temporal, cambio forzado, regeneración, tope con cinco peticiones simultáneas, carreras con SQL entre lectura y guardado, compañía suspendida con sesión vigente, dos compañías A/B. Nada contra producción |
| Límite de reversión | Revertir los cuatro commits de código y ADR y restaurar `..\TimeTracker.rollback\pr-mc6\`: sin cambios de esquema ni de datos; los usuarios creados con la PR conservan su hash y su marca |

## Puerta de la PR (todo en verde)

| Paso | Resultado |
|---|---|
| DOCKER-CHECK | `docker info` responde (29.7.2); solo lectura |
| Entorno limpio | Sin variables `Cors__`, `ConnectionStrings__`, `Database__`, `Jwt__`, `Seed__`, `Ai__`, `Registration__`, `Company__`, `ForwardedHeaders__`, `ASPNETCORE_`, `DOTNET_ENVIRONMENT` |
| RESTORE | Correcto |
| BUILD | `-warnaserror`: 0 advertencias, 0 errores |
| TEST | Domain 111, Application 470, Web 288, Infrastructure 473, Architecture 281, Api.Integration 985: 0 fallos (con `ASPNETCORE_URLS=http://127.0.0.1:0` solo en ese comando; integración 4 min 27 s) |
| COV-A | Application: líneas 99,5 %, ramas 96,7 % (mínimo 80 %) |
| COV-D | Domain: líneas 100,0 %, ramas 100,0 % (mínimo 100 %) |
| FORMAT | `--verify-no-changes`: sin cambios |
| VULN | Sin `has the following vulnerable packages` |
| EF-PENDING | «No changes have been made to the model since the last migration»; sin modelo ni migración nuevos |
| Contrato | `dotnet build src/TimeTracker.Api -c Release --no-incremental`: el `docs/api/openapi.json` conserva el mismo SHA-256 (`76489e10...b1a90a`), comprobado a mano |

## Desviaciones

Detalle en `design.md`, sección 9, «Desviaciones y aclaraciones del diseño surgidas en la PR 6»: resultado `Busy` propio cuando los reintentos se agotan, lecturas seguidas (`ForUpdate`) y `SaveChangesAsync` en el repositorio de usuarios, `ICompanyLimits` en Application, comprobación previa del valor no numérico del tope, el middleware exime los endpoints anónimos y deniega por defecto una ruta inexistente con `mcp`, el rol del alta no distingue mayúsculas, un único mensaje para responsable inexistente/ajeno/no Manager, el contrato incorpora las tres rutas, las pruebas de los avisos de la PR 5 (lado aceptado de 4 KiB; `Content-Type` no JSON anónimo → `401`), la cabecera `no-store` ya la pone la capa común y el SHA-256 del contrato. Hallazgo de la PR: con altas simultáneas reales EF envuelve el fallo de serialización en dos niveles (un `500` esporádico en la primera versión), corregido y blindado con prueba.

## Riesgos abiertos

- **La contraseña temporal no caduca** y **un token `mcp` emitido no se invalida al regenerar** (aceptados, ADR 0022): el runbook (PR 8) debe incluir la revisión periódica de las cuentas con `must_change_password` antigua.
- **El cambio forzado se apoya en un claim de 30 minutos**: no consulta la base de datos por petición.
- **Bajo carrera extrema el alta puede responder `409` («demasiadas altas simultáneas») sin haber alcanzado el tope** si se agotan los tres reintentos; el tope nunca se supera y el cliente repite la operación.
- **Cualquier Manager de la compañía puede crear Managers** y editar la configuración del asistente de su compañía (aceptado en la spec).
- **Pendiente aparte:** límite por IPv6 y `ForwardedHeaders` del registro (PR de seguimiento); V-7 sigue sin comprobar en Railway (5.9).
- Q-1, Q-8 y Q-10 (proceso operativo de apertura y rotación) siguen abiertas.

---

# PR 7 — Web: registro, gestión de equipo, cambio de contraseña y aviso

Rama `feat/multi-compania-pr7-web` (apilada sobre `feat/multi-compania-pr6-empleados`; cadena `stacked-to-main`) · Strict TDD · Copia previa en `..\TimeTracker.rollback\pr-mc7\` (34 archivos) · Sin cambios en la API, el esquema ni el contrato (`docs/api/openapi.json` conserva el SHA-256 `76489e10...`) · `Registration__Enabled` **no se activa en ningún entorno** (solo en el proceso local de la verificación manual).

## Lo que queda al responsable

Nada de producción en esta PR (solo Web; no se accede a Railway). Para tener en cuenta:

- Desplegable tras la PR 6: la página `/registro` muestra «El registro no está disponible.» mientras la API la mantenga cerrada, y el menú no enlaza a ella.
- Una **pasada humana breve con Chrome visible** (registro por URL directa, alta, regeneración, primer inicio) sigue siendo recomendable: la tarea 7.8 se ejecutó con Chrome headless dirigido por CDP (ver `verify-report.md`).
- Pendiente de seguimiento (ya anotado antes): el límite por IP/IPv6 del registro y `ForwardedHeaders`; V-7 sin comprobar en Railway; Q-1, Q-8 y Q-10.

## Commits de esta entrega

| Commit | Mensaje | Contenido |
|---|---|---|
| `7374da4` | `feat(web): formulario de registro de compañía, cliente tipado y base común de formularios` | 7.2: `RegisterForm`, `ApiCall`, `FormFeedback`, `AuthApiClient.RegisterCompanyAsync` |
| `fff3eb1` | `feat(web): inicio de sesión con cambio pendiente, cambio de contraseña y redirección por claim mcp` | 7.3 y 7.3b: `LoginForm`, `ChangePasswordForm`, `SessionClaims`, `PasswordChangeRedirect`, `JwtPayload`, `AccountApiClient` |
| `478d927` | `feat(web): alta de personas y regeneración de la contraseña temporal como clases puras` | 7.4 y 7.4b: `TeamMemberCreationForm`, `TeamMemberTemporaryPassword`, `TemporaryPasswordReveal`, `EmployeesApiClient` |
| `84fd785` | `feat(web): páginas de registro, cambio de contraseña y alta de personas, aviso con el nombre de la compañía y menú` | 7.5 a 7.7: páginas, `PasswordChangeGate`, `AssistantConfigurationNotice`, `NavMenu`, `Program.cs`, `RepositoryConventionsTests` y `CompanyGuardsTests` actualizados |
| `6f9b58d` | `refactor(web): el cliente de configuración del asistente reutiliza ApiCall` | REFACTOR (7.5/7.6) |
| (este) | `docs(openspec): progreso de apply de la PR 7 de multi-compania` | `tasks.md`, `design.md` (desviaciones), `verify-report.md`, este archivo |

Tamaño (`git diff 3949002 HEAD` de los cinco commits de código): 50 archivos, **+3 226 / -223**; producción (`src`) **+1 487 / -221** (34 archivos), pruebas **+1 739 / -2** (16 archivos). La previsión era de 600-900 líneas: la Web quedó por encima porque se extrajeron clases puras con sus clientes y pruebas (la lógica de presentación debe vivir fuera de las páginas, sin bUnit) y se rehízo `NavMenu.razor`. Se declara `size:exception` igual que en las PR 5 y 6.

## Evidencia de Strict TDD (ciclo por tarea)

| Tarea | Archivo de prueba | Capa | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 7.1 | — | — | N/A (copia) | N/A | `..\TimeTracker.rollback\pr-mc7\` (34 archivos) | N/A | N/A |
| 7.2 | `WT/Registration/RegisterFormTests`, `RegisterCompanyClientTests` | Unitaria (Web) | 288 de la Web en verde | CS0246/CS0234 (`RegisterForm`, `IRegistrationApi`, `ApiCallResult`, `ISessionSignIn`) | 19/19 | Recorte de campos y contraseña intacta; `201` con sesión y destino `/`; confirmación distinta sin petición; `400` por campo; `403` genérico; `409` con `detail` y datos conservados; `429` con pista; `401`/`404` cerrado sin sesión; `500`/red genérico; doble envío ignorado; cliente: cinco campos exactos, sin `Authorization`, errores por campo, `Retry-After`, red caída. Mutaciones: sin vaciar secretos y `403` con el `detail` de la API hacen fallar 9 y 1 | `FormFeedback` y `ApiCall` compartidos |
| 7.3 | `WT/Account/ChangePasswordFormTests`, `AccountApiClientTests`, `SessionClaimsTests`, `PasswordChangeRedirectTests` | Unitaria (Web) | 307 en verde | CS0234 (`TimeTracker.Web.Account`), `ILoginApi`, `IPasswordChangeApi` | 67/67 de `Account` | Token nuevo sustituye la sesión; `returnUrl` seguro e inseguro; `400` por campo y campos vaciados; `429`; `409`; `403`; `401` sin mensaje; confirmación; `mcp` solo con el valor exacto `true`; redirección de cualquier ruta salvo `/cambiar-contrasena` y `/login` (sin distinguir mayúsculas, no por prefijo), sin `returnUrl` inseguro. Mutaciones: sin la exención de `/login`, `mcp` por presencia y sin vaciar contraseñas hacen fallar 2, 4 y 7 | `JwtPayload` extraído del proveedor (pruebas del proveedor intactas) |
| 7.3b | `WT/Account/LoginFormTests` | Unitaria (Web) | idem | CS0246 (`LoginForm`) | idem | Sin cambio pendiente: Inicio, `/equipo`, `//ejemplo.test`, `https://ejemplo.test`, ruta con barra invertida, ruta sin barra; con cambio pendiente: `/cambiar-contrasena` con o sin `returnUrl` seguro; `401` idéntico para contraseña errónea y correo inexistente; `429`; `500`/red genérico; campos vacíos sin petición; contraseña con espacios intacta. Mutación: ignorar el claim `mcp` hace fallar 4 | `LoginForm` sustituye la clase privada de `Login.razor` |
| 7.4 | `WT/Team/TeamMemberCreationFormTests`, `EmployeesApiClientTests` | Unitaria (Web) | 374 en verde | CS0246 (`TeamMemberCreationForm`, `IEmployeesApi`, `CreateEmployeeRequestDto`) | 36/36 de `Team` | Cuerpo de cuatro campos sin compañía; responsable solo de la lista del equipo; contraseña temporal una vez y desaparece al cerrar o al empezar otra operación; `400` por campo, `409` con `detail` (correo o tope), `403`, `401` sin mensaje, `429`, genérico; Employee sin llamada a la API; sin miembros de baja/edición; doble envío. Mutaciones: aceptar cualquier responsable, no descartar la contraseña previa y quitar la guarda de Employee hacen fallar 1, 1 y 1 | `TemporaryPasswordReveal` compartido |
| 7.4b | `WT/Team/TemporaryPasswordRegenerationTests` | Unitaria (Web) | idem | CS0246 (`TeamMemberTemporaryPassword`) | idem | Confirmación previa sin llamada; confirmar con el identificador; sin solicitud no hace nada; cancelar; descartar; `409` con `detail` y sin contraseña; `404`, `403`, `401`, `429`, genérico; doble confirmación. Mutaciones: `Dismiss` inerte y confirmar sin solicitud hacen fallar 4 y 2 | — |
| 7.5 | `WT/Assistant/AssistantConfigurationNoticeTests`, `AssistantConfigurationFormTests` | Unitaria (Web) | 410 en verde | CS0103 (`AssistantConfigurationNotice`) | 420/420 | Texto de la spec con el nombre, nombre como texto literal (`<b>x</b>`), sin nombre «su compañía», nunca «toda la empresa»/«toda la instancia», restablecer nombra la compañía y conserva «aunque otro responsable»; `SourceLabel("default")` | `AssistantConfigurationApiClient` reutiliza `ApiCall` (pruebas del cliente intactas) |
| 7.6 | `AR/RepositoryConventionsTests` | Arquitectura | 281 de arquitectura en verde | 8 en rojo (páginas, menú, servicios y literales ausentes) | 289/289 | Sin `MarkupString`/almacenamiento/`IJSRuntime` en las páginas nuevas ni en sus clases; contraseñas enmascaradas; sin contraseña/código/temporal en `NavigateTo`; textos en español sin términos técnicos (solo el marcado visible); sin controles de baja o edición; sin identificador de compañía en la Web (verde a la primera, vigila); sin enlace a `/registro`. Mutaciones: `localStorage` en `Register.razor`, `href="registro"` en el menú, sin `PasswordChangeGate`, «toda la empresa», «Editar» y sin `alertdialog` hacen fallar 2, 1, 1, 1, 1 y 1 | — |
| 7.7 | (convenciones de 7.6) | Arquitectura | idem | idem | idem | `CompanyGuardsTests` pasa a permitir el literal `cname` en `SessionClaims.cs` (RED real: falló al crear el archivo) | — |
| 7.8 | — | Manual | N/A | N/A | `verify-report.md`: 31/31 y 2/2 con Chrome headless contra la API local | — | — |

### Resumen de pruebas

- Total tras la PR: 111 + 470 + 420 + 473 + 289 + 985 = **2 748 pruebas** (antes 2 608; +140: Web +132, arquitectura +8), todas en verde.
- Ninguna llamada a un proveedor real de IA ni a producción; valores inventados (`contrasena-prueba-12345`, `codigo-prueba-0123456789`, `ana@example.test`).

### Evidencia de unidad de trabajo

| Evidencia | Valor |
|---|---|
| Prueba focalizada | `dotnet test tests/TimeTracker.Web.Tests -c Release` (420 en verde) y `dotnet test tests/TimeTracker.ArchitectureTests --filter FullyQualifiedName~RepositoryConventionsTests` (92 en verde) |
| Arnés de ejecución | Web y API reales en local (PostgreSQL 18 en Docker) con Chrome headless dirigido por CDP: registro con código (incorrecto y correcto), alta, regeneración, primer inicio con redirección, cambio de contraseña, aviso de configuración, menú por rol y sin sesión, registro cerrado. Nada contra producción |
| Límite de reversión | Borrar las páginas, clientes y clases nuevas, restaurar `NavMenu`, `Program.cs`, `App.razor`, `Login.razor`, `Team.razor`, `AssistantConfiguration.razor` y `RepositoryConventionsTests` desde `..\TimeTracker.rollback\pr-mc7\`; sin cambios de API, esquema ni datos |

## Puerta de la PR (todo en verde)

| Paso | Resultado |
|---|---|
| DOCKER-CHECK | `docker info` responde (29.7.2); solo lectura |
| Entorno limpio | Sin variables `Cors__`, `ConnectionStrings__`, `Database__`, `Jwt__`, `Seed__`, `Ai__`, `Registration__`, `Company__`, `ForwardedHeaders__`, `ASPNETCORE_`, `DOTNET_ENVIRONMENT` |
| RESTORE | Correcto |
| BUILD | `-warnaserror`: 0 advertencias, 0 errores |
| TEST | Domain 111, Application 470, Web 420, Infrastructure 473, Architecture 289, Api.Integration 985: 0 fallos (con `ASPNETCORE_URLS=http://127.0.0.1:0` solo en ese comando; integración 4 min 42 s) |
| COV-A | Application: líneas 99,5 %, ramas 96,7 % (mínimo 80 %) |
| COV-D | Domain: líneas 100,0 %, ramas 100,0 % (mínimo 100 %) |
| FORMAT | `--verify-no-changes`: sin cambios |
| VULN | Sin `has the following vulnerable packages` |
| EF-PENDING | N/A: sin modelo ni migración (PR solo de Web) |
| Contrato | `dotnet build src/TimeTracker.Api -c Release --no-incremental`: `docs/api/openapi.json` conserva el mismo SHA-256 (`76489e10...`) |

## Desviaciones

Detalle en `design.md`, sección 9, «Desviaciones y aclaraciones del diseño surgidas en la PR 7»: `RepositoryConventionsTests` vive en `ArchitectureTests`, puertos pequeños (`I…Api`, `ISessionSignIn`) para las clases puras, `ApiCall`/`FormFeedback` compartidos, cambio pendiente leído del token, `PasswordChangeGate` que también guarda el Inicio y conserva el `returnUrl`, código de invitación enmascarado, `401` sin mensaje en los formularios autenticados, lista «Personas a su cargo» con la regeneración, aviso de configuración con `AssistantConfigurationNotice`, `Equipo` con `Match=All`, `cname` permitido en `SessionClaims.cs`, `exp` textual aceptado por el lector del payload y la verificación manual automatizada con Chrome headless.

## Riesgos abiertos

- **La verificación 7.8 fue automatizada** (Chrome headless por CDP): falta una pasada visual humana y el flujo con dos compañías en el navegador (cubierto por las pruebas de integración de las PR 1 a 6).
- **La contraseña temporal está en el DOM** mientras se muestra el aviso (inevitable para entregarla); no se guarda en almacenamiento ni URL y se retira al cerrar o navegar. No hay botón de copiar (sin JS interop): se selecciona de un clic (`user-select: all`).
- **El cambio forzado de la Web es de presentación**: la protección real es el `PasswordChangePendingMiddleware` de la API.
- **La contraseña temporal no caduca** y **un token `mcp` emitido no se invalida al regenerar** (aceptados, ADR 0022; runbook en la PR 8).
- V-7, Q-1, Q-8 y Q-10 siguen abiertas.

## PR 8: documentación, runbook y especificaciones vigentes (8.1 a 8.9)

Rama `feat/multi-compania-pr8-docs` (sobre `feat/multi-compania-pr7-web`). Copia previa en `..\TimeTracker.rollback\pr-mc8\` (AGENTS, README, `.env.example`, compose, `docs/adr/`, `config.yaml`, spec de solution-architecture y artefactos de openspec).

### Commits

| Commit | Mensaje | Tareas |
|---|---|---|
| `d9d47e1` | `test(docs): pruebas en rojo ...` | 8.2 (RED: 17 de 69 en rojo) |
| `0a49c7c` | `docs(agents): ...` | 8.3 |
| `42ddfed` | `docs(runbook): ...` | 8.4, 8.5, 8.7 (señal en README y ADR 0021, 0018/0019 intactos) |
| `f0aa284` | `docs(specs): ...` | 8.6 |
| `c260a6e` | `docs(openspec): cierre de V-1 a V-18 ...` | 8.8 |
| (este) | `docs(openspec): progreso de apply de la PR 8` | tasks.md, este archivo |

### Evidencia de Strict TDD

| Tarea | Prueba | RED | GREEN | Notas |
|---|---|---|---|---|
| 8.2 a 8.8 | `AR/DeliveryArtifactsTests` (+17 pruebas, una existente actualizada a ADR 0021) | 17 en rojo | 69/69 | AGENTS §1/§6/§7, runbook (orden de apertura, V-3/V-6/V-7 como pendientes), README, índice de 22 ADR, ausencia de secretos y enlaces de sesión, spec vigente, cierre V-1..V-18, advertencias de archivo. Las huellas de 0017/0018/0019 siguen en verde |
| 8.9 | — | — | Puerta | Sin refactor adicional |

### Puerta

RESTORE correcto; BUILD `-warnaserror` 0/0; TEST Domain 111, Application 470, Web 420, Infrastructure 473, Architecture 306, Api.Integration 985, todo en verde (`ASPNETCORE_URLS=http://127.0.0.1:0` solo en ese comando); COV-D 100,0 %/100,0 %; COV-A 99,5 %/96,7 %; FORMAT sin cambios; VULN sin vulnerables; `openapi.json` sin cambios (SHA-256 `76489e10...`); EF-PENDING N/A (sin modelo).

### Desviaciones y pendientes

- README: la sección «Activar el autodespliegue» pasa a «Despliegue manual y autodespliegue» (no hay autodespliegue). `config.yaml` no se tocó.
- Las specs vigentes solo se corrigen en la cifra de ADR; los demás deltas se listan en `design.md` para el informe de archivo.
- Pendientes del responsable: V-1, V-3, V-4 (Railway), V-6, V-7, Q-1, Q-8; Q-9 queda resuelta por el ADR 0021 y el índice (confirmar con el usuario) y Q-10 descrita en el runbook. Seguimiento abierto: límite IPv6 /64 y `ForwardedHeaders` (PR aparte).
