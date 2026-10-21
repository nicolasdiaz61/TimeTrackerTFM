# Progreso de apply: multi-compania

Fase: sdd-apply · Modo: openspec · **Strict TDD** · Rama de la PR 1: `feat/multi-compania-pr1-modelo` (sale de `main` 9e572b8) · Rama de la PR 1b: `feat/multi-compania-pr1b-contraer` (apilada sobre la anterior) · Rama de la PR 3: `feat/multi-compania-pr3-aislamiento` (apilada sobre la PR 2) · Fecha: 2026-10-21

## Estado acumulado

| PR | Tareas | Estado |
|---|---|---|
| PR 1 | 1.1, 1.5 a 1.13 | **Hechas** (11 de 15) |
| PR 1 | 1.2 (V-1), 1.3 (V-4, parte Railway), 1.4 (V-6), 1.4b (V-3) | **Pendientes del responsable** (puertas previas al despliegue; apply no accede a producción ni a Railway) |
| PR 1b | 1b.1, 1b.4 a 1b.10 | **Hechas** (10 de 12; ver la sección «PR 1b» al final) |
| PR 1b | 1b.2 (puertas previas de despliegue), 1b.3 (reconfirmación de V-3 con el responsable; la parte local está hecha) | **Pendientes del responsable** (apply no accede a producción ni a Railway) |
| PR 2 | 2.1 a 2.11 (con 2.9b) | **Hechas** (12 de 12; ver la sección PR 2 al final; sin tareas de producción) |
| PR 3 | 3.1 a 3.12 | **Hechas** (12 de 12; ver la sección PR 3 al final; sin tareas de producción) |
| PR 4a a PR 8, tarea 9 | todas | Sin empezar (fuera del alcance de este lote) |

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
