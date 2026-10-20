# Diseño: multi-compania

Fecha: 2026-10-20 · Fase: sdd-design · Modo: openspec · Base: `proposal.md`, `exploration.md` (fuente), `AGENTS.md`, ADR 0011, 0013, 0017, 0018 y 0019, `openspec/specs/` y el código vigente de `src/` citado en cada decisión. Rama: `docs/multi-compania-propuesta`.

Convención: **DD-n** = decisión de diseño; **V-n** = verificación que apply (o una persona) hace antes de dar un paso por bueno (sección 14); **Q-n** = pregunta abierta residual (sección 15). Lo que no se comprobó en esta fase se marca **«no verificado»**. Los identificadores van en inglés; los nombres de variables de entorno nuevos son propuestas que fijan las specs y las tareas.

Estado del código leído: `UserAccount` (inmutable, `Create` con `managerId`), `UserConfiguration` (`ux_users_email`, `fk_users_users_manager_id`, `ix_users_manager_id`), `TimeEntryConfiguration` (sin FK a `users`, `xmin`, `ux_time_entries_employee_id_open`), `AssistantSettingsConfiguration` (PK `id`, `ck_assistant_settings_singleton`, `xmin`), `AssistantSettingsAuditConfiguration` (`ix_assistant_settings_audit_occurred_at`), migración `AddAssistantAuditImmutability` (función `assistant_settings_audit_append_only` y dos triggers), `UserRepository`, `TimeEntryRepository`, `DemoTimeEntryStore`, `AssistantSettingsStore`, `AssistantSettingsLoader`, `AssistantSettingsPoller`, `InMemoryAssistantQuota`, `AesGcmCredentialProtector` (AAD `tt-assistant-credential|v1|{provider}|{version}`), `AuthenticationServiceCollectionExtensions.RejectInvalidClaims`, `JwtAccessTokenIssuer`, `HttpContextCurrentUser`, `AuthEndpoints`, `RateLimitingServiceCollectionExtensions`, `DemoUsersSeeder`. Versiones fijadas en `Directory.Packages.props`: EF Core 10.0.12 y Npgsql EF 10.0.3.

Hallazgo que corrige a la exploración: `ForwardedHeaders` **no está activo por defecto**; el ADR 0013 lo define opcional (`ForwardedHeaders__Enabled`, apagado por defecto, `ForwardedHeaders__Hops`). Que esté activo en producción es una comprobación pendiente (V-7), no un hecho (DD-19).

## 1. Enfoque técnico

Una base de datos compartida; cada fila de negocio lleva `company_id`. El aislamiento es híbrido y cada capa cubre el fallo de la anterior:

1. **Esquema (verdad estructural):** claves foráneas compuestas `(company_id, …)` hacia `users(company_id, id)`; ninguna fila puede apuntar a un usuario de otra compañía aunque el código falle.
2. **Código (filtro global que falla cerrado):** `HasQueryFilter` aplicado por convención a todo tipo de entidad con `company_id`, alimentado por `ICompanyContext`; sin compañía no hay filas. Las escrituras pasan por una guarda en `SaveChanges`. Las rutas que necesitan saltarse el filtro son una lista cerrada, vigilada por pruebas de arquitectura.
3. **Pruebas:** dos compañías A y B con la misma forma de datos recorren todos los endpoints, las herramientas del asistente y las restricciones.

La compañía sale solo del claim `cid` del JWT. La configuración del asistente y su runtime pasan de singleton a «una por compañía». La migración es expandir/contraer. El registro público queda tras `Registration__Enabled` (apagada), un código de invitación y un límite por IP.

```
Cliente ──JWT(sub, role, cid, [mcp])──> Api
  UseAuthentication ─ RejectInvalidClaims (sub, role, cid válidos)
  CompanyContextMiddleware ──> ICompanyContext (ámbito, escritura única) <── cid
  PasswordChangePendingMiddleware ──> 403 salvo endpoints marcados (mcp)
  UseAuthorization ─ RateLimiter ─ endpoint ─ caso de uso (ICurrentUser.CompanyId)
                                                  │
                                    Repositorios (DbContext con filtro global)
                                                  │
        TimeTrackerDbContext ── filtro: company_id = CurrentCompanyId (Guid.Empty si no hay) ── guarda de escritura
                                                  │
        PostgreSQL: FK compuestas, PK company_id, triggers de auditoría intactos

Rutas fuera del HTTP autenticado (lista cerrada, DD-9):
  login (correo) · nombre de compañía en login · versiones del poller · registro · siembra · cargador por compañía
        └─ IgnoreQueryFilters (3 sitios) o ICompanyScopeFactory.CreateScope(companyId)
```

## 2. Decisiones de diseño

### DD-1. Una BD compartida con `company_id`; capas y dónde vive cada pieza

| Capa | Cambios | Notas |
|---|---|---|
| Domain | `Company` (id, nombre, `NormalizeName`, estado, `UsesPlatformAssistant`, `CreatedAt`); constante `Company.DefaultId`; `CompanyId` en `UserAccount` y `TimeEntry` (explícito); `UserAccount.MustChangePassword` y `ChangePassword(hash)` | Reglas de nombre y normalización con cobertura 100 % (AGENTS §5). Sin EF ni ASP.NET. |
| Application | `ICurrentUser.CompanyId`, `ICompanyContext`, `PasswordPolicy`, `RegisterCompanyUseCase`, `CreateTeamMemberUseCase`, `ResetTemporaryPasswordUseCase`, `ChangePasswordUseCase`, puertos `ICompanyRegistrationStore`, `ICompanyRepository`, `ITemporaryPasswordGenerator`, `IInviteCodeValidator`; `IAssistantQuota` y `IAssistantRuntimeReloader` reciben `companyId`; `IAssistantCredentialProtector` recibe `companyId` | Cobertura >= 80 %. Los casos de uso toman la compañía de `ICurrentUser`, nunca de un comando. |
| Infrastructure | filtro global, guarda de escritura, `ICompanyContextSetter`, `ICompanyScopeFactory`, repositorios, `AssistantRuntimeRegistry`, cuota, AAD v2, migraciones A y B | Sin umbral; integración con PostgreSQL real. |
| Api | middleware de compañía y de cambio de contraseña pendiente, claims, endpoints nuevos, políticas de tasa, validadores de opciones, siembra | Composition root. |
| Web | `/registro`, `/equipo`, `/cambiar-contrasena`, aviso con el nombre de la compañía | Sin reglas de negocio ni identificadores de compañía. |

Alternativas descartadas: esquema por compañía o base por compañía (migraciones por inquilino y pool de conexiones por compañía; desproporcionado para el volumen previsto y rompe el arranque único de Railway); solo filtro de código sin FK compuestas (una consulta olvidada filtra datos sin red de seguridad); solo RLS (DD-12).

### DD-2. Modelo de datos

Nombres en `snake_case`, como el esquema actual.

**`companies`** (tabla nueva)

| Columna | Tipo | Restricción |
|---|---|---|
| `id` | uuid | PK; `Guid.CreateVersion7` salvo la compañía por defecto (DD-3) |
| `name` | varchar(100) | NOT NULL, `CHECK char_length(btrim(name)) >= 2` |
| `name_normalized` | varchar(100) | NOT NULL, índice único `ux_companies_name_normalized` |
| `status` | varchar(16) | NOT NULL DEFAULT `'Active'`, `CHECK status IN ('Active','Suspended')` |
| `uses_platform_assistant` | boolean | NOT NULL DEFAULT `false` (DD-13) |
| `created_at` | timestamptz | NOT NULL |

`name_normalized` = recorte, espacios internos colapsados a uno, normalización Unicode `FormKC` y `ToLowerInvariant`, calculado solo en `Company.NormalizeName` (una regla, como `UserAccount.NormalizeEmail`). Solo se impide el duplicado exacto normalizado. `status` no se cambia desde la interfaz (fuera de alcance); un operador lo cambia por SQL y el login lo respeta (DD-6).

**`users`**

| Cambio | Detalle |
|---|---|
| `company_id uuid` | NOT NULL tras la migración B; FK `fk_users_companies_company_id` a `companies(id)` |
| `must_change_password boolean` | NOT NULL DEFAULT `false` (se añade en A) |
| Clave alternativa `(company_id, id)` | Restricción UNIQUE `ux_users_company_id_id` (EF `HasAlternateKey`: es la forma de declarar el destino de una FK compuesta; en PostgreSQL equivale al índice único pedido) |
| FK compuesta `fk_users_users_company_id_manager_id` | `(company_id, manager_id)` -> `users(company_id, id)`, `NO ACTION`, **MATCH SIMPLE** (valor por defecto de PostgreSQL: con `manager_id` nulo no se comprueba nada). Sustituye a `fk_users_users_manager_id` |
| Índice | `ix_users_manager_id` se sustituye por `ix_users_company_id_manager_id` (consulta de reportes directos) |
| Se conservan | `ux_users_email` único global (una persona, una compañía), `ck_users_*` |

**`time_entries`**

| Cambio | Detalle |
|---|---|
| `company_id uuid` | NOT NULL tras B |
| FK compuesta `fk_time_entries_users_company_id_employee_id` | `(company_id, employee_id)` -> `users(company_id, id)`, `NO ACTION`. Cierra el hueco del ADR 0011 (disparador: llega el alta de usuarios). Antes de B se comprueban los huérfanos (DD-4, V-1) |
| Se conservan | `xmin`, `ux_time_entries_employee_id_open`, `ix_time_entries_employee_id_clock_in_at` (el filtro por compañía es un predicado extra sobre esos índices; sin índice nuevo) |

**`assistant_settings`**: PK `company_id` (uuid, FK a `companies(id)`); se eliminan `id` y `ck_assistant_settings_singleton`; se conservan todas las demás columnas y CHECK y **`xmin`** como token de concurrencia. El nombre de la PK vigente se lee de la migración `AddAssistantSettings` al escribir la migración B.

**`assistant_settings_audit`**: `company_id uuid NOT NULL` con FK a `companies(id)`; el índice `ix_assistant_settings_audit_occurred_at` se sustituye por `ix_assistant_settings_audit_company_id_occurred_at (company_id, occurred_at DESC)`. Los triggers de solo inserción no se tocan (DD-5). Sin FK de `actor_id` (igual que hoy: la auditoría no depende del ciclo de vida del usuario).

**Tabla de tokens:** no hay. La «contraseña temporal de un solo uso» es una contraseña normal con `must_change_password = true`: se sustituye por la que elige la persona y la marca vuelve a `false` (DD-21). No se guarda ningún secreto temporal aparte del hash. La baja de usuarios sigue fuera de alcance: la FK es `NO ACTION` y un borrado falla con `23503`, de modo que la futura «baja» deberá decidir entre baja lógica y borrado en cascada (se anota en el ADR 0020).

### DD-3. Compañía por defecto de id fijo y compañía de plataforma

La migración A crea una compañía de id fijo `Company.DefaultId = 00000000-0000-7000-8000-000000000001`, nombre ASCII `Demo` y `name_normalized = 'demo'` (ASCII para que `lower()` de SQL y `NormalizeName` de C# coincidan sin depender del idioma de la base de datos; una prueba compara el literal de la migración con la constante). Todo dato existente pasa a ella. Es la «compañía demo» **y** la única con `uses_platform_assistant = true` (A lo fija a `true` para conservar el comportamiento actual: la clave `Ai__ApiKey` sigue sirviendo a los datos existentes). El registro público crea siempre `uses_platform_assistant = false`; ningún endpoint lo modifica.

| Opción para «la clave de plataforma solo para la demo» | Ventaja | Inconveniente | Decisión |
|---|---|---|---|
| Columna `uses_platform_assistant` | Explícita y auditable por SQL; permite que el product owner active otra compañía sin código | Una columna más | **Elegida** |
| Comparar con `Company.DefaultId` en código | Sin columna | Acopla el comportamiento a una constante; no sirve si producción tiene datos reales en la compañía por defecto | Descartada |
| Variable `Ai__PlatformCompanyId` | Sin esquema | Un identificador más en el entorno; se desincroniza de la base de datos | Descartada |

Si producción contiene usuarios que no son de demostración (V-1), la compañía por defecto los aloja igualmente; el nombre y la bandera se corrigen por SQL antes de abrir el registro (Q-1).

### DD-4. Migración expandir/contraer (A y B)

Dos migraciones escritas a mano (SQL) sobre el modelo destino. El `Designer` y el snapshot de A reflejan el modelo final (las propiedades `CompanyId` son `Guid` no nulo): EF solo compara el snapshot con el modelo, no con la base de datos, y así `dotnet ef migrations has-pending-model-changes` queda vacío tras B (V-5).

**Migración A, `AddCompanies` (expandir; segura para el código anterior)**

1. `CREATE TABLE companies` y `INSERT` de la compañía por defecto (`uses_platform_assistant = true`).
2. `users`: `ADD COLUMN company_id uuid NULL`, `ADD COLUMN must_change_password boolean NOT NULL DEFAULT false`; `UPDATE users SET company_id = '<DefaultId>'`.
3. `time_entries`: `ADD COLUMN company_id uuid NULL`; `UPDATE` a la compañía por defecto (también los huérfanos: la comprobación es de B).
4. `assistant_settings`: `ADD COLUMN company_id uuid NULL`; `UPDATE` de la fila singleton.
5. `assistant_settings_audit`: `ADD COLUMN company_id uuid NOT NULL DEFAULT '<DefaultId>'`, **sin `UPDATE`** (DD-5).
6. Sin FKs ni PK nuevas: el código anterior, que no conoce `company_id`, sigue funcionando (las filas que escriba en la ventana quedan con `NULL` y la auditoría recibe el valor por defecto).

**Migración B, `EnforceCompanyIsolation` (contraer)**, en este orden, cada migración en su transacción (el DDL de PostgreSQL es transaccional):

1. Relleno residual de las filas escritas por código anterior: `UPDATE ... SET company_id = '<DefaultId>' WHERE company_id IS NULL` en `users`, `time_entries` y `assistant_settings`.
2. **Comprobación de huérfanos que aborta con un mensaje claro:** `DO $$ ... RAISE EXCEPTION 'time_entries huérfanos: %', n` si hay fichajes cuyo `employee_id` no existe en `users`. B nunca borra ni inventa usuarios. Qué hacer con los huérfanos lo decide una persona antes (exportar y borrar, o reasignar a un usuario real; V-1). El ADR 0011 anticipaba justo este coste.
3. `SET NOT NULL` en las tres columnas y `ALTER COLUMN company_id DROP DEFAULT` en la auditoría (ya no puede haber escritores antiguos).
4. `users`: UNIQUE `(company_id, id)`; FK a `companies`; se elimina `fk_users_users_manager_id`; FK compuesta del responsable; intercambio de índice.
5. `time_entries`: FK compuesta del empleado.
6. `assistant_settings`: se elimina el CHECK del singleton, se sustituye la PK por `company_id`, se elimina la columna `id`, FK a `companies`.
7. Auditoría: FK a `companies` e índice `(company_id, occurred_at DESC)`.

**Reversibilidad y `Down`**

| Migración | `Down` | Condición y pérdida |
|---|---|---|
| B | Elimina FKs compuestas, UNIQUE y FKs a `companies`, devuelve las columnas a NULL, restaura `id smallint DEFAULT 1` con PK y `ck_assistant_settings_singleton`, repone índices antiguos | **Aborta con `RAISE EXCEPTION` si `assistant_settings` tiene más de una fila.** Con varias compañías ya creadas el `Down` pierde la separación: revertir solo antes de abrir el registro |
| A | Elimina las columnas `company_id` y `must_change_password` y la tabla `companies` | Pierde el vínculo de compañía y la marca de contraseña temporal |

El `Down` de A no viola el trigger de auditoría: `ALTER TABLE ... DROP COLUMN` no dispara triggers por fila. Una prueba de migración con datos (sección 11) ejecuta `Up A`, `Up B`, `Down B`, `Down A` y comprueba que los triggers siguen presentes (`pg_trigger`) y que un `UPDATE`/`DELETE`/`TRUNCATE` sobre la auditoría sigue fallando.

**Orden de despliegue de las migraciones (separación real de A y B).** Con `Database__ApplyMigrationsOnStartup=true` todas las migraciones pendientes se aplican en el mismo arranque, así que A y B en una misma publicación no se separarían. Se publican en despliegues distintos: A en PR 1; B en una PR 1b posterior (DD-24), cuando el código de PR 1 ya escribe `company_id` en todo `INSERT`. Así, si Railway solapa la instancia nueva y la antigua durante unos segundos (V-6), el código antiguo nunca choca con un `NOT NULL`.

**Copia de seguridad, ensayo y ejecución en producción** (procedimiento; nada de esto está verificado en Railway):

1. Antes de cada despliegue con migración: copia lógica (`pg_dump -Fc`) y comprobación de que se restaura en un PostgreSQL local (misma versión mayor que la de Railway; V-4). La copia gestionada de Railway depende del plan y no se da por hecha (V-3).
2. Ensayo: restaurar la copia en un contenedor local, ejecutar `Up A`, comprobaciones de huérfanos, `Up B`, `Down B`, `Down A`, y arrancar la API contra el resultado con la suite de integración mínima. Si no hay copia utilizable, ensayo con la base sembrada por `DemoUsersSeeder`/`DemoDataSeeder` más filas de auditoría y un fichaje huérfano creado a propósito.
3. Consultas previas (V-1): usuarios que no son de demostración, `time_entries` huérfanos, filas de `assistant_settings` y recuento de auditoría.
4. Cómo aplicar:

| Vía | Cuándo | Estado |
|---|---|---|
| `Database__ApplyMigrationsOnStartup=true` al desplegar | Migración A (aditiva, tras copia y ensayo) | Mecanismo existente (`DatabaseMigrationHostedService`); valor actual en producción **no verificado** |
| Script `dotnet ef migrations script --idempotent` aplicado con `psql` mediante `railway connect` (o proxy TCP público temporal) | Migración B, cualquier `Down` y cualquier caso con huérfanos | **No verificado** (acceso desde fuera y versión de `psql`) |
| `railway run dotnet ef database update` desde un equipo local | Alternativa | **No verificado**: `railway run` inyecta las variables del servicio, pero `${{Postgres.PGHOST}}` apunta a la red privada (`*.railway.internal`), que no se resuelve fuera de Railway; haría falta el proxy TCP público (mayor superficie) |

   Antes de B se pone `Database__ApplyMigrationsOnStartup=false` para que el despliegue no la aplique sin el control manual, y se vuelve a activar después.

**Procedimiento de rollback de producción (un rollback de código NO revierte el esquema):**

1. `Registration__Enabled=false` (si estaba abierta). Con más de una compañía real, **no** ejecutar `Down`: se restaura la copia o se acepta el aviso (el `Down` de B ya aborta).
2. Dejar de servir tráfico de escritura (escalar la API a 0 o retirar el despliegue).
3. Copia nueva.
4. Ejecutar `Down B` y luego `Down A` (hasta `AddAssistantAuditImmutability`) por la vía de script.
5. Solo entonces redesplegar el código antiguo, con `Database__ApplyMigrationsOnStartup=false`.
6. Comprobar `/health`, login y un fichaje.

**Advertencia:** el rollback de un solo clic de Railway devuelve el binario antiguo contra el esquema nuevo (los `INSERT` de `time_entries` fallan por `company_id NOT NULL`, el almacén antiguo lee `assistant_settings.id`). Se documenta en el runbook y en el ADR 0020: no usarlo con B aplicada sin `Down` previo.

### DD-5. Auditoría: relleno de `company_id` sin `UPDATE`

El trigger `trg_assistant_settings_audit_no_update_delete` rechaza todo `UPDATE` de fila, así que un `UPDATE ... SET company_id` fallaría. Solución: `ADD COLUMN company_id uuid NOT NULL DEFAULT '<DefaultId>'` (PostgreSQL 11 o superior almacena el valor como metadato sin reescribir la tabla ni ejecutar `UPDATE`; la versión del servidor de Railway **no está verificada**, V-4) y quitar el `DEFAULT` en B. No se tocan la función ni los triggers (ni `DISABLE TRIGGER`, que exigiría privilegio de propietario y dejaría una ventana). Mientras el `DEFAULT` existe, el código anterior sigue insertando auditoría sin conocer la compañía y esas filas reciben la compañía por defecto, que es la única existente en ese momento.

### DD-6. Identidad: claim `cid`, `ICurrentUser.CompanyId` y `RejectInvalidClaims`

- `JwtAccessTokenIssuer` añade `cid = user.CompanyId` (constante `JwtDefaults.CompanyClaim = "cid"`) y, solo con `must_change_password`, `mcp = "true"` (DD-21). El nombre de la compañía **no** va en el token.
- `ICurrentUser` gana `Guid CompanyId`; `HttpContextCurrentUser` lo lee del claim ya validado.
- `RejectInvalidClaims` exige además `cid` GUID no vacío (y, si existe `mcp`, que valga `"true"`). Un token sin `cid` falla con 401 (falla cerrado). **Efecto del despliegue:** todos los tokens vigentes caducan como máximo a los 30 minutos y las personas vuelven a iniciar sesión; la Web ya trata el 401 como sesión caducada.
- Login: tras verificar la contraseña y antes de emitir el token, si la compañía no está `Active` se responde el mismo 401 genérico (interruptor de operador por SQL; el tiempo de verificación de contraseña se mantiene constante porque el hash se verifica siempre antes, DD-6 de `LoginUseCase`).
- `LoginResult`/`UserSummary` incorporan `CompanyName` y `MustChangePassword`; `me` devuelve `companyName` y `mustChangePassword`. **`me` deja de ser «sin base de datos»:** una consulta por clave primaria al nombre de la compañía (filtrada por `ICompanyContext`), para no inflar el token ni mostrar un nombre obsoleto.
- Nunca se expone `companyId` en ninguna respuesta ni petición (prueba sobre `openapi.json`, DD-9).
- **Límites aceptados y documentados** (ADR 0020): con token de 30 minutos, una compañía suspendida o un rol cambiado conservan acceso hasta que caduque el token; no hay revocación (fuera de alcance).

### DD-7. `ICompanyContext`, ámbito HTTP y guarda de escritura

`ICompanyContext { Guid? CompanyId { get; } }` vive en `Application/Identity`. Su implementación `AmbientCompanyContext` (Infrastructure, ámbito `Scoped`) es de **escritura única**:

- `ICompanyContextSetter.Set(Guid)` (Infrastructure; no es un puerto de Application) asigna la compañía una sola vez; un segundo `Set` con otro valor lanza `InvalidOperationException`.
- En HTTP, `CompanyContextMiddleware` (tras `UseAuthentication`, antes de `UseAuthorization`) lee `cid` del principal y llama a `Set`. Una petición anónima o con token inválido deja el contexto vacío.
- Fuera de HTTP, `ICompanyScopeFactory.CreateScope(companyId)` crea un `IServiceScope` nuevo y hace `Set`; lo usan el cargador del asistente, la siembra y la prueba de integración. Hay una sola vía para «actuar como una compañía».

`TimeTrackerDbContext` recibe `ICompanyContext` y expone `Guid CurrentCompanyId => companyContext.CompanyId ?? Guid.Empty`. `Guid.Empty` no coincide con ninguna fila (la FK a `companies` lo impide): **sin compañía no se devuelve nada**. `DesignTimeDbContextFactory` y las pruebas usan un contexto vacío o fijo.

**Guarda de escritura:** el `DbContext` sobrescribe `SaveChanges`/`SaveChangesAsync`; para cada entrada `Added`, `Modified` o `Deleted` de un tipo con `CompanyId` (o `Company`) exige `CompanyId == CurrentCompanyId` y que nadie modifique la propiedad `CompanyId`; si no, lanza `CompanyScopeViolationException` (un 500 genérico y un registro sin datos). Cierra el hueco de `Update(entidadDesconectada)`, que evita el filtro de lectura. El registro de compañía hace `Set(nuevoId)` antes de insertar, así que **no existe ninguna ruta de escritura exenta**.

| Opción | Decisión |
|---|---|
| Holder de escritura única alimentado por middleware | **Elegida**: Infrastructure no depende de ASP.NET; un único punto de verdad |
| `ICompanyContext` que lee `IHttpContextAccessor` | Descartada: no sirve fuera de HTTP (poller, siembra) |
| Pasar `companyId` a cada método de repositorio | Descartada: un parámetro olvidado es justo la fuga que se quiere evitar; el filtro lo hace por convención |

### DD-8. Filtro global que falla cerrado, por convención

Un único bucle en `OnModelCreating`, después de `ApplyConfigurationsFromAssembly`, recorre `modelBuilder.Model.GetEntityTypes()` y a cada tipo con propiedad `CompanyId` le aplica `HasQueryFilter(e => EF.Property<Guid>(e, "CompanyId") == CurrentCompanyId)` (expresión construida con una referencia al contexto, como el patrón documentado de multiinquilino). `Company` recibe `c.Id == CurrentCompanyId`. Ninguna configuración por entidad aplica el filtro a mano, así que una entidad nueva con `CompanyId` lo hereda. Una prueba de modelo (sección 11) falla si algún tipo del modelo no tiene filtro, salvo una lista de excepciones que arranca **vacía**.

Puntos a probar (V-2, V-9): que el filtro construido por reflexión se parametriza por la instancia del contexto actual (dos contextos con compañías distintas no se contaminan); que `ExecuteUpdate`/`ExecuteDelete` respetan el filtro (la prueba de arquitectura los prohíbe en `src/` igualmente, DD-9); que `Find`/`FindAsync` **no** lo aplican (se prohíben).

### DD-9. Rutas explícitas permitidas y vigilancia por pruebas de arquitectura

**Lista cerrada de `IgnoreQueryFilters` (3 sitios en `src/`):**

| # | Sitio | Motivo | Lo que devuelve |
|---|---|---|---|
| 1 | `UserRepository.FindByEmailAsync` | El login no tiene compañía: el correo es único global | `UserAccount` (con `CompanyId`) |
| 2 | `CompanyRepository.FindLoginInfoAsync(companyId)` | Nombre y estado de la compañía del usuario recién autenticado, antes de existir contexto | `(Name, Status)` por PK |
| 3 | `AssistantSettingsStore.ListVersionsAsync` | El poller global lee `(company_id, xmin)` de todas las compañías | Solo identificador y versión; sin entidades con seguimiento |

La resolución de compañía para `me`, las pruebas de unicidad del registro (se apoyan en las restricciones `ux_companies_name_normalized` y `ux_users_email` y capturan `23505`), el cargador (`CreateScope`) y la siembra (`CreateScope` + `FindByEmailAsync`) **no** necesitan saltarse el filtro.

**Prueba de arquitectura de la lista cerrada:** analiza el código fuente de `src/` (el mismo enfoque que `Src_DoesNotHardcodeModelNames`) y exige que `IgnoreQueryFilters` aparezca solo en esos tres archivos y con el recuento exacto, cada uno con el comentario `// company-filter-bypass: <motivo>`. Añadir un cuarto uso exige tocar la prueba y revisar la decisión.

**APIs que evitan el filtro, prohibidas en `src/` fuera de `Migrations/`:** `.Find(`, `.FindAsync(` (no aplican el filtro), `FromSql`, `FromSqlRaw`, `FromSqlInterpolated`, `ExecuteSql*`, `SqlQuery*`, `ExecuteUpdate`, `ExecuteDelete`, `Database.GetDbConnection` y el uso de `Npgsql` en código de consulta (el `catch` de `PostgresException` en los almacenes sigue permitido). Las migraciones son SQL controlado y quedan exentas.

**Otras guardas de arquitectura:** (a) `openapi.json` no contiene un parámetro ni una propiedad llamados `companyId` (sin distinguir mayúsculas; `companyName` es válido); (b) ningún tipo de petición del Api ni comando de Application declara `CompanyId` (barrido por reflexión); (c) el literal `"cid"` se usa solo en `JwtDefaults`, el emisor, el validador y `HttpContextCurrentUser`/el middleware; (d) los repositorios y almacenes no reciben `Guid companyId` como parámetro de consulta (salvo `CompanyRepository.FindLoginInfoAsync`, el registro de ejecución y el protector); (e) `AssistantAuditAppendOnlyTests` sigue vigente.

### DD-10. Filtros de consulta con nombre de EF Core 10: spike, sin depender de ellos

EF Core 10 documenta filtros con nombre (varios filtros por entidad con `IgnoreQueryFilters` selectivo); que 10.0.12 los ofrezca **no está verificado** (V-2). Decisión: este cambio usa **un único filtro sin nombre** y el diseño funciona igual con o sin esa característica. El spike (primera tarea de la PR 3, con prueba en rojo) comprueba la API contra la versión fijada; si existe, se registra y se valora adoptarla en un cambio posterior (por ejemplo, para combinar con borrado lógico) sin cambiar el contrato. No se sube de versión de paquetes por este motivo.

### DD-11. 404 idéntico y comportamiento de los repositorios

`FindDirectReportAsync(managerId, memberId)`, `ListDirectReportsAsync`, `FindDisplayNameAsync`, `TimeEntryRepository` y `DemoTimeEntryStore` no cambian de firma: el filtro los acota a la compañía. Un identificador de otra compañía es, para la consulta, un identificador inexistente, de modo que el caso de uso devuelve el mismo `null` y el endpoint el mismo 404 con el mismo cuerpo (`ProblemDetails` idéntico; prueba que compara código, cabeceras relevantes y cuerpo). `ICurrentUser.CompanyId` se pasa solo donde se **crea** una fila (`TimeEntry.Start...`, `UserAccount.Create`), y la guarda lo comprueba. Las herramientas del asistente (`AssistantToolset`) heredan el aislamiento: un GUID de B inyectado en el mensaje se resuelve contra el contexto de A y da el mismo resultado que un GUID inexistente.

### DD-12. RLS de PostgreSQL: diferida

El rol de la aplicación es propietario de las tablas (ADR 0019): un propietario no está sometido a RLS salvo `FORCE ROW LEVEL SECURITY`, y además puede deshabilitar triggers y políticas. No se aborda ahora porque exige un rediseño de roles y del pool. **Camino futuro (se documenta en el ADR 0020):** (1) dos roles: `tt_migrator` (propietario, solo migraciones) y `tt_app` (sin `BYPASSRLS`, sin propiedad); (2) `ALTER TABLE ... ENABLE` y `FORCE ROW LEVEL SECURITY` con políticas `USING (company_id = current_setting('app.company_id', true)::uuid)`; (3) un `DbConnectionInterceptor` de Npgsql que ejecute `set_config('app.company_id', ..., false)` al abrir la conexión y la reponga (`RESET`/`DISCARD`) al devolverla al pool, porque el pool reutiliza sesiones; (4) las rutas de la lista cerrada (login, poller, versiones) pasan a funciones `SECURITY DEFINER` o a un rol con `BYPASSRLS` acotado; (5) cadena de conexión de migración distinta de la de la aplicación (hoy es una sola). Mientras tanto, la protección de la tercera capa es el esquema y las pruebas.

### DD-13. Asistente por compañía: almacén, resolución y compañía de plataforma

- `AssistantSettingsStore` (con ámbito) no cambia de firma: `GetAsync`, `GetVersionAsync`, `SaveAsync` y `DeleteAsync` leen/escriben la fila de **su** compañía por el filtro; `SaveAsync` fija `row.CompanyId = ICompanyContext.CompanyId` al insertar y la guarda lo verifica. «Sin fila» sigue siendo token `"0"`; un `INSERT` concurrente de la misma compañía viola la PK (`23505`) y da `Conflict`, como hoy. La auditoría se inserta con la misma compañía en la misma transacción. `xmin` se conserva.
- `AssistantSettingsResolver.Resolve(env, stored, storedCredentialUsable)` gana el argumento `platformDefaults` (= `Company.UsesPlatformAssistant`). Sin fila: plataforma -> el entorno de hoy; no plataforma -> **proveedor `None` con origen `none` y `credentialConfigured = false`** (asistente deshabilitado). Con fila: gana la fila; la clave de entorno `Ai__ApiKey` solo se usa si `platformDefaults` y el proveedor efectivo es el del entorno; modelo, tiempo máximo y tokens siguen heredando del entorno cuando la fila es nula (son valores no secretos). El resultado de «sin fila» y el de «`None` guardado» son indistinguibles para el chat (503 genérico).
- `Ai__AllowedProviders` sigue siendo una lista de **plataforma**: las compañías nuevas eligen entre ella, con su propia clave. Un proveedor `Ollama` en la lista da a todas las compañías acceso al `Ai__Endpoint` del operador; es una decisión explícita del operador al incluirlo (riesgo anotado).
- La prueba de conexión usa solo la configuración efectiva de la compañía propia: un candidato sin clave nueva usa la clave guardada de **esa** compañía o la de plataforma solo si `platformDefaults`; nunca la de otra. Los límites de tasa por persona (3 por minuto, `sub`) no cambian.
- `GET`/`PUT`/`DELETE` quedan acotados por el filtro: no existe un identificador de compañía en el contrato. Cualquier Manager de la compañía edita la configuración (**riesgo aceptado**, ahora acotado a la compañía; se documenta en el ADR 0021 y en el aviso de la página).

### DD-14. Runtime: registro de instantáneas por compañía

El singleton `AssistantChatClient` + `AssistantSettingsLoader` se sustituye por `AssistantRuntimeRegistry` (singleton, Infrastructure):

```
ChatAssistantEngine ──GetAsync(companyId)──> Registry ──hit──> CompanyRuntime(holder AssistantChatClient, LastUsed)
                                                 │ miss: carga única por compañía (Lazy<Task>)
                                                 └─> ICompanyScopeFactory.CreateScope(companyId) ─> store.GetAsync ─> resolver ─> fábrica ─> Swap
Poller ──ListVersionsAsync()──> para cada CompanyRuntime cargada: si versión distinta -> ReloadAsync(companyId)
```

| Aspecto | Diseño |
|---|---|
| Construcción perezosa | La primera pregunta de una compañía crea su `CompanyRuntime` con una carga protegida por un `Lazy<Task>` por compañía (una sola carga concurrente). Una compañía sin fila y sin plataforma obtiene la instantánea `Disabled` (sin pipeline ni `HttpClient`) |
| Sustitución por `xmin` | Igual que hoy por compañía: `Swap(inner, options, version)`, publicación con `Interlocked.Exchange`, **gracia de 120 s** para liberar la instantánea anterior (toda petición dura <= 55 s) |
| Conservar la última válida | Si la fila de una compañía es inválida, no descifrable o con proveedor no permitido, no hay `Swap` y se registra un aviso **una vez por versión rechazada y compañía** (`LastRejectedVersion` por compañía), sin secretos |
| Memoria | Un pipeline y un `HttpClient` propios (`OwnedHttpClientChatClient`) por compañía **con proveedor activo**. Tope `Ai__MaxLoadedCompanies` (propuesta de valor por defecto 20, a medir, V-8). Las `Disabled` apenas pesan y no cuentan para el tope |
| Expulsión | Al superar el tope se expulsa la compañía menos usada recientemente (LRU por `LastUsed` con `TimeProvider`): `Swap(Disabled)` + retirada del diccionario; el pipeline anterior se libera tras la gracia de 120 s, por lo que una petición en curso termina bien. Además, caducidad por inactividad (`Ai__RuntimeIdleMinutes`, propuesta 60) para todas las entradas. La compañía expulsada se recarga sola en su siguiente pregunta |
| Sobrecarga | Si más compañías activas que el tope compiten, hay expulsiones y recargas repetidas (latencia de una lectura y de una construcción de cliente). Se registra un aviso con límite de frecuencia; el operador sube el tope. No se rechazan preguntas por capacidad |
| Escritura local | Tras el `Save`/`Delete`, el caso de uso llama a `IAssistantRuntimeReloader.ReloadAsync(companyId, ct)`; si la compañía no está cargada, no hace nada (carga perezosa). Si la recarga falla, el guardado ya está confirmado: 200 y aviso, como hoy |
| Motor | `ChatAssistantEngine` obtiene la instantánea una vez por petición con `registry.GetAsync(currentUser.CompanyId)` y deja de usar el singleton |

Compañía de plataforma: la carga resuelve el entorno completo; la clave de plataforma nunca llega a una compañía que no sea de plataforma.

### DD-15. Poller global

`AssistantSettingsPoller` se mantiene como un único `BackgroundService` (cada `Ai__SettingsPollSeconds`, 30 por defecto). Su vuelta: `store.ListVersionsAsync()` (sitio 3 de la lista cerrada, una sola consulta `SELECT company_id, xmin FROM assistant_settings` sin entidades con seguimiento) y, para cada `CompanyRuntime` **ya cargada**, compara su versión con la de la fila (`"0"` si no hay fila; una fila nueva para una entrada `"0"` cuenta como cambio) y recarga solo las distintas, con un máximo de recargas en paralelo y sin propagar errores (aviso una vez por racha, como hoy). Las compañías no cargadas se ignoran: cargarán la fila fresca en su primera pregunta. También aplica la expulsión por inactividad. La primera carga acotada en `StartAsync` (10 s) se conserva: precarga solo la compañía de plataforma (para que el primer chat de la demo use la fila ya guardada) y deja el resto en lazy. Consistencia eventual <= `SettingsPollSeconds`. Alternativa descartada: comprobar la versión de la compañía en cada pregunta (una consulta por pregunta, contra el principio de poller ya decidido).

### DD-16. Cuotas y límites por compañía

| Límite | Hoy | Después |
|---|---|---|
| Cuota diaria | `InMemoryAssistantQuota`: contador por `employeeId`, tope `Ai__DailyRequestsPerUser` | Clave `(companyId, employeeId)` **más un tope agregado diario por compañía** `Ai__CompanyDailyRequests`; se consume primero el de la compañía y, si la persona ya agotó el suyo, se devuelve el permiso a la compañía (decremento con comparación e intercambio). `QuotaDecision` indica el ámbito (persona o compañía) para el registro; el mensaje al cliente sigue siendo genérico |
| Tasa por minuto | Política `assistant`, ventana fija por `sub` (`Ai__RequestsPerMinute`) | Se mantiene por `sub` y se añade en `IAssistantQuota` una ventana fija de un minuto **por compañía** (`Ai__CompanyRequestsPerMinute`), porque una política de `RateLimiter` admite una sola partición por endpoint y no se encadenan limitadores en `AddPolicy` |
| Prueba de conexión | 3 por minuto y persona | Sin cambios |
| Empleados | no existía | `Company__MaxEmployees` (tope por compañía; DD-21) |

Todos son variables de **entorno**, no editables por el Manager (ni por API). Siguen **en memoria por instancia** (ADR 0017): se pierden al reiniciar y no se comparten entre réplicas; Railway ejecuta una instancia. Para que el diccionario no crezca sin límite con muchas compañías, las entradas de días anteriores se purgan con un barrido al cambiar de día (un único barrido, protegido por un `Interlocked`). Con un tope agregado, una compañía no puede agotar la clave de plataforma de la demo más allá de su cupo.

### DD-17. Cifrado con AAD `company_id` (formato v2)

`AssociatedData` pasa a `tt-assistant-credential|v2|{companyId:D}|{provider}|{keyVersion}`. `Protect(plaintext, provider, companyId)` cifra siempre en v2. `TryUnprotect(stored, provider, companyId, out plaintext)` prueba **v2** y, si falla la autenticación, **v1 solo cuando `companyId == Company.DefaultId`**: los textos cifrados en v1 solo existen en la compañía por defecto, así que una fila v1 que aparezca en otra compañía (copia manual entre filas) no se descifra. No cambia el formato de almacenamiento (`credential_ciphertext`, `credential_nonce`, `credential_key_version`): la versión del AAD no se guarda, se deduce probando. `ReEncryptOrKeep` (ya existente en `SaveAssistantSettingsUseCase`) recifra en v2 en el primer guardado que conserve la clave; el cargador es de solo lectura y no migra el formato. Efecto: un texto cifrado de una compañía no se puede trasplantar a otra (mejora, no requisito de bloqueo; las claves maestras siguen siendo globales por entorno, DD-6 de `asistente-config-responsable`).

### DD-18. Página de configuración y aviso por compañía

El texto fijo «única para toda la empresa» pasa a nombrar la compañía: «Esta configuración es única para toda la compañía «{nombre}» y cualquier responsable de la compañía puede cambiarla.» El nombre sale de la respuesta de login/`me` (no de `localStorage`). El `DELETE` mantiene el aviso de que puede borrar un cambio ajeno; el texto de restablecimiento nombra la compañía. Hay pruebas de arquitectura y de la Web con los literales anteriores (`RepositoryConventionsTests`, `AssistantConfiguration.razor`): se actualizan en la PR 7 junto con el cambio del texto, no antes. La página nunca muestra ni envía un identificador de compañía.

### DD-19. Autorregistro con código de invitación

`POST /api/auth/register-company`, anónimo, bajo `Registration__Enabled` (apagada por defecto).

| Aspecto | Diseño |
|---|---|
| Existencia de la ruta | `MapRegistrationEndpoint` solo se invoca si `Registration:Enabled` es `true` al arrancar; apagada, la ruta no existe y responde el 404 genérico de una ruta desconocida. El cambio de valor exige reiniciar (una variable de Railway ya redespliega). `DocumentGeneration` fuerza `Registration:Enabled=true` al exportar el contrato, para que `openapi.json` documente siempre el endpoint |
| Petición | `companyName`, `managerName`, `email`, `password`, `inviteCode`; `JsonUnmappedMemberHandling.Disallow` y cuerpo máximo 4 KiB (patrón de `asistente-config-responsable`) |
| Orden | (1) forma mínima y longitudes máximas (código <= 128), (2) **código de invitación**, (3) validación de campos, (4) hash de la contraseña, (5) inserción. El código va antes que el resto para que solo quien lo posee pueda aprovechar el 409 de correo repetido |
| Código de invitación | `Registration__InviteCodes__0..n` (lista; cada uno de 24 caracteres como mínimo, validado al arrancar con la bandera abierta; bandera abierta sin códigos = fallo de arranque). Comparación en **tiempo constante**: SHA-256 del candidato frente al de cada código con `CryptographicOperations.FixedTimeEquals`, recorriendo siempre **todos** los códigos sin cortocircuito. Rechazo: 403 con detalle genérico. Se registra el **índice** del código aceptado (no el valor) para poder retirar uno filtrado. Los códigos son reutilizables y rotables editando la lista (Q-5) |
| Transacción | Un `SaveChanges` crea `companies` (`uses_platform_assistant=false`, `status='Active'`) y el Manager (`MustChangePassword=false`) tras `Set(nuevoId)`. Las colisiones se resuelven por restricción: `ux_companies_name_normalized` -> 409 «nombre de compañía ya registrado»; `ux_users_email` -> 409 «correo ya registrado» (se distinguen por `PostgresException.ConstraintName`) |
| Respuesta | `201` con el mismo cuerpo que el login (token con `cid`, usuario, `companyName`); la persona queda dentro |
| Límite por IP | Política `register` por IP (ventana fija, propuesta 10 por hora, constante de código como `login`, cuentan todos los intentos). Depende de `ForwardedHeaders` (ADR 0013); sin él todas las peticiones comparten la IP del borde y quedan en **una partición global** (falla seguro pero un atacante agota el cupo de todos). Con `Registration:Enabled=true` en `Production`, un validador de arranque exige `ForwardedHeaders:Enabled=true` (V-7) |
| Enumeración | El 409 por correo o nombre repetidos revela existencia; **riesgo aceptado** (solo accesible con un código válido, límite por IP), se documenta en el ADR 0022 |
| Compañía inicial | Sin configuración del asistente: deshabilitado; sin clave de plataforma (DD-13) |

Alternativas descartadas: registro abierto sin código (abuso y coste del asistente); código de un solo uso con tabla (más piezas; ver Q-5); verificación de correo (fuera de alcance, sin proveedor de correo).

### DD-20. Política de contraseñas

`PasswordPolicy` (Application, pura): mínimo **12**, máximo **128** (el tope existente de `LoginUseCase.MaxPasswordLength`), medido en caracteres, **sin reglas de composición**, rechaza una contraseña formada solo por espacios y una igual al correo normalizado. Se aplica al registro, al cambio de contraseña y a la contraseña elegida por la persona; la contraseña temporal generada por el servidor la cumple por construcción (DD-21). El login no la aplica (solo vacía y <= 128) para no excluir a quien ya tiene una contraseña corta del sistema anterior (los usuarios de demostración y los existentes); el cambio de contraseña sí la exige. Hash: `IPasswordHasher` existente (`AspNetPasswordHasher`). Los mensajes nombran el campo y nunca incluyen la contraseña.

### DD-21. Alta de empleados, contraseña temporal y cambio de contraseña

| Endpoint | Autorización | Petición | Respuesta | Errores |
|---|---|---|---|---|
| `POST /api/equipo` | Manager; política `employee-create` por `sub` (propuesta 30/min) | `email`, `displayName`, `role` (`Employee` por defecto o `Manager`), `managerId` opcional | `201` `{ member, temporaryPassword }` con `Cache-Control: no-store` | 400 por campo, 401, 403, 409 (correo ya existente **sin revelar la compañía**; tope de empleados), 429 |
| `POST /api/equipo/{memberId}/contrasena-temporal` | Manager; misma política | — | `200` `{ temporaryPassword }` | 401, 403, 404 (ajeno o inexistente, idéntico), 409 si la persona ya cambió su contraseña |
| `POST /api/auth/change-password` | Autenticado, **permitido con `mcp`**; política `password-change` por `sub` (propuesta 5/min) | `currentPassword`, `newPassword` | `200` con el cuerpo del login (token nuevo sin `mcp`) | 400 por campo (incluye contraseña actual incorrecta y nueva igual a la actual), 401, 429 |
| `GET /api/auth/me` | Autenticado, permitido con `mcp` | — | `userId`, `email`, `name`, `role`, `companyName`, `mustChangePassword` | 401 |

- **Quién y dónde:** `CompanyId` sale de `ICurrentUser`. `managerId` por defecto es el Manager que crea; si se indica otro, debe existir **en la misma compañía** (el filtro lo oculta si es ajeno -> 400 «responsable no válido» sin distinguir inexistente de ajeno) y tener rol `Manager`; además la FK compuesta lo garantiza (`23503` se traduce al mismo 400, defensa en profundidad). Un Manager creado por otro Manager queda con `managerId` nulo por defecto: no se crean jerarquías nuevas (Q-4).
- **Contraseña temporal:** la genera el servidor (`ITemporaryPasswordGenerator`, 16 caracteres de un alfabeto sin ambigüedades con `RandomNumberGenerator`, unas 80 bits), se devuelve **una sola vez** en la creación y no se guarda en claro, ni se escribe en registros, ni se incluye en el contrato más allá del campo de respuesta (V-12 comprueba que ninguna prueba del contrato la prohíbe por nombre). Que la genere el servidor evita que un Manager fije contraseñas débiles o reutilizadas.
- **Regenerar** la temporal solo mientras `must_change_password = true` (nunca ha entrado): sin ella, una persona que pierde la contraseña temporal quedaría bloqueada, porque el correo ya existe (409) y no hay recuperación por correo. Es una ampliación mínima de alcance (Q-6).
- **Cambio forzado de verdad:** el token de una persona con `must_change_password` lleva `mcp=true`. `PasswordChangePendingMiddleware` (tras la autenticación) responde **403** `ProblemDetails` («Debe cambiar su contraseña antes de continuar») a todo endpoint que no lleve el marcador de metadatos `AllowWhilePasswordChangePending` (solo `change-password` y `me`). Se hace con middleware y no con una política porque las políticas por rol (`Manager`) no lo heredarían. Tras el cambio se emite un token nuevo sin `mcp`; el antiguo sigue bloqueado hasta caducar (como máximo 30 minutos). `ChangePassword` verifica la actual con el hasher (tiempo constante en la verificación), aplica `PasswordPolicy` y pone `MustChangePassword=false`.
- **Tope de empleados:** `Company__MaxEmployees` (propuesta de valor por defecto 50; no editable por el Manager). Se cuenta con el filtro y se inserta; no hay bloqueo entre contar e insertar, así que peticiones concurrentes pueden superarlo por unas pocas filas (acotado por la política por persona): tope blando, aceptado. La consulta de recuento se hace en `IUserRepository`, sin SQL crudo.
- **Rol y alta:** un Manager puede crear otros Managers **solo en su compañía** (la guarda y la FK lo imponen). No hay baja de usuarios ni cambio de rol en este cambio.
- El correo pasa por `UserAccount.NormalizeEmail` y el alta falla por `ux_users_email` (`23505`) -> 409 sin pre-consulta (sin carrera).

### DD-22. Siembra y compañía demo idempotente

`DemoUsersSeeder` y `DemoDataSeeder` trabajan dentro de `ICompanyScopeFactory.CreateScope(Company.DefaultId)`. La compañía por defecto la crea la migración A; el sembrador la «crea o encuentra» de forma idempotente (si faltara, la inserta con el id fijo y trata `23505` como existente). Para cada usuario de demostración usa `FindByEmailAsync` (sitio 1 de la lista): si existe y su compañía no es la demo, **no lo toca** y avisa (el arranque no falla). Los fichajes de demostración llevan `CompanyId` de la demo. **Apagado de la siembra pública al abrir el registro:** `RegistrationOptionsValidator` hace fallar el arranque (mensaje con ambas claves, sin valores) si `Registration:Enabled=true` y `Seed:DemoUsers=true`. Los usuarios de demostración que ya existan en producción (contraseña pública) **siguen existiendo**: antes de abrir el registro se retiran o se cambian por SQL (borrando antes sus fichajes por la FK) o se reinicia la base de datos si solo contiene datos de demostración (V-1, Q-1). La demo conserva la clave de plataforma, acotada por `Ai__CompanyDailyRequests`.

### DD-23. Opciones de entorno nuevas (nombres propuestos)

| Variable | Defecto | Uso | Validación al arrancar |
|---|---|---|---|
| `Registration__Enabled` | `false` | Abre `register-company` (DD-19) | Con `true`: códigos presentes y largos; `Seed:DemoUsers=false`; `ForwardedHeaders:Enabled=true` en `Production` |
| `Registration__InviteCodes__0..n` | vacío | Códigos de invitación (secreto) | >= 24 caracteres, sin duplicados; nunca en el repositorio ni en `.env.example` con valor |
| `Company__MaxEmployees` | 50 | Tope de usuarios por compañía | entero 1-10000 |
| `Ai__CompanyDailyRequests` | 500 | Tope agregado diario por compañía | entero >= 1 |
| `Ai__CompanyRequestsPerMinute` | 30 | Tope agregado por minuto por compañía | 1-600 |
| `Ai__MaxLoadedCompanies` | 20 | Tope de compañías con pipeline cargado | 1-1000 |
| `Ai__RuntimeIdleMinutes` | 60 | Caducidad por inactividad de una instantánea | 1-1440 |

Los valores por defecto numéricos son propuestas a confirmar con el product owner (Q-7). Todos los mensajes nombran la clave (`Registration:...`), nunca el valor. `.env.example` documenta las claves sin valores secretos; `DeliveryArtifactsTests` comprueba que `Registration__Enabled=false` figura en `.env.example`.

### DD-24. Estrategia de PR apiladas

Reglas: cada PR parte de pruebas en rojo (AGENTS §5), lleva sus pruebas y su documentación, y es desplegable por sí sola. Todas superan 400 líneas (proposal), por lo que se mantiene la excepción de PR apiladas; no se admite mezclar contenido de dos filas. Las migraciones se publican en despliegues distintos (DD-4).

| # | PR | Contenido | Depende de | Desplegable tras ella | Notas |
|---|---|---|---|---|---|
| 1 | Modelo y migración A | `Company`, `CompanyId` y `MustChangePassword` en Domain y configuraciones, migración A, `Company.DefaultId`, **contexto fijo** (`ICompanyContext` devuelve la compañía por defecto), escrituras que fijan `CompanyId` por defecto, ADR 0020 | main | Sí: comportamiento idéntico (una compañía); código nuevo escribe `company_id` | Se despliega y se deja un periodo de observación antes de 1b |
| 1b | Migración B | `EnforceCompanyIsolation` (FKs compuestas, NOT NULL, PK, comprobación de huérfanos) | 1 desplegada y V-1 resuelta | Sí | **Se separa de la PR 1 respecto a la lista de la propuesta** para que A y B no se apliquen en el mismo arranque (Q-2) |
| 2 | Token `cid` | Claim, `ICurrentUser.CompanyId`, `RejectInvalidClaims`, middleware y holder de compañía (sustituye al contexto fijo), `me` con `companyName`; fábricas y tokens de pruebas en su propio commit | 1 | Sí: las personas vuelven a iniciar sesión; sin filtro todavía | Volumen de pruebas alto |
| 3 | Aislamiento de consultas | Filtro por convención, guarda de escritura, lista cerrada, pruebas de arquitectura y de modelo, spike DD-10, pruebas A/B de equipo y fichajes, 404 idéntico | 1b y 2 | Sí | Es la capa 2; sin ella no se abre el registro |
| 4a | Configuración del asistente por compañía: datos y casos de uso | Store con ámbito, resolver con `platformDefaults`, AAD v2, auditoría con `company_id`, endpoints acotados, ADR 0021; **transitorio:** el runtime singleton carga la fila de `Company.DefaultId` mediante `CreateScope` y el poller consulta esa compañía | 3 | Sí (una sola compañía real todavía) | Sin este puente el guardado de la compañía por defecto no llegaría al runtime |
| 4b | Runtime por compañía | Registro de instantáneas, poller global, cuotas y topes por compañía, expulsión | 4a | Sí | Mayor riesgo técnico; PR propia |
| 5 | Autorregistro tras bandera | `register-company`, validaciones, código de invitación, límite por IP, validadores de arranque, ADR 0022 | 4b | Sí, con `Registration__Enabled=false` | **Regla:** la bandera sigue apagada hasta que 1-4b estén fusionadas **y desplegadas** con sus pruebas de aislamiento en verde |
| 6 | Alta de empleados y cambio de contraseña | Endpoints, `PasswordChangePendingMiddleware`, política de contraseñas, tope de empleados | 5 | Sí | Podría ir antes de 5 sin problema técnico; se mantiene el orden de la propuesta |
| 7 | Web | `/registro`, `/equipo`, `/cambiar-contrasena`, aviso de configuración, menú | 5 y 6 | Sí | Actualiza los literales de las pruebas de arquitectura de la Web |
| 8 | Docs | `AGENTS.md` §1, §6 y §7, README, runbook (backup, ensayo, rollback, Railway), `.env.example`, specs sincronizadas | 1-7 | Sí | Los ADR 0020, 0021 y 0022 entran con las PR 1, 4a y 5 |

Apertura del registro (acción del operador, no una PR): comprobar V-1, V-7, retirar los usuarios de demostración (DD-22), definir `Registration__InviteCodes`, activar `Registration__Enabled=true` y confirmar con una prueba de humo A/B en producción.

### DD-25. ADR y documentos

Los ADR 0018 y 0019 **no se reescriben ni se editan**; los sustituye parcialmente el 0021 (convención del ADR 0001 sobre cómo señalar la sustitución: Q-9).

| ADR | Contenido |
|---|---|
| **0020 Aislamiento multiempresa** | DD-1, DD-2, DD-6 a DD-12: BD compartida, tres capas, `cid`, filtro que falla cerrado, lista cerrada, RLS diferida con su camino futuro, migración y rollback, límites aceptados (token 30 min, rol propietario, baja de usuarios pendiente). Cierra el disparador del ADR 0011 (FK `time_entries` -> `users`) |
| **0021 Configuración del asistente por compañía** | Sustituye a 0018 y 0019 en «fila única / toda la instancia / cualquier Manager de cualquier equipo»: fila por compañía, compañía nueva deshabilitada, clave de plataforma solo para la compañía de plataforma, AAD v2, registro de instantáneas, poller global, cuotas por compañía. Mantiene 0018/0019 en cifrado, auditoría de solo inserción, catálogo y recarga |
| **0022 Autorregistro con código de invitación y contraseñas** | DD-19 a DD-21: código secreto, bandera, límite por IP, enumeración aceptada, política de 12/128, contraseña temporal de un solo uso con cambio forzado, correo único global |

Cambios en `AGENTS.md`: **§1** añadir que una instancia sirve a varias compañías con aislamiento por `company_id` (ADR 0020) y que el asistente se configura por compañía (ADR 0021); **§6** añadir que `CompanyId` sale siempre del claim `cid` (`ICurrentUser.CompanyId`) y nunca de ruta, consulta, cuerpo ni cabecera, que toda consulta pasa por el filtro global y que `IgnoreQueryFilters` solo existe en la lista cerrada vigilada por pruebas, que cada herramienta del asistente y cada endpoint se prueban con dos compañías, y la política de contraseñas (12/128 sin composición, contraseña temporal de un solo uso); **§7** sustituir «solo mediante configuración... o configuración guardada por un Manager (ADR 0018)» por «...guardada por un Manager de su compañía (ADR 0021)», y añadir que las migraciones usan expandir/contraer, que un rollback de código no revierte el esquema (ejecutar `Down` antes de redesplegar) y que toda tabla de negocio nueva lleva `company_id` con FK compuesta y filtro global.

## 3. Interfaces (firmas relevantes)

```csharp
// Application/Identity
public interface ICurrentUser { Guid EmployeeId { get; } Guid CompanyId { get; } UserRole Role { get; } }
public interface ICompanyContext { Guid? CompanyId { get; } }

// Infrastructure/Persistence (no es un puerto de Application)
public interface ICompanyContextSetter { void Set(Guid companyId); }           // escritura única
public interface ICompanyScopeFactory { AsyncServiceScope CreateScope(Guid companyId); }

// Application/Companies
public sealed record CompanyLoginInfo(string Name, CompanyStatus Status, bool UsesPlatformAssistant);
public interface ICompanyRepository { Task<CompanyLoginInfo?> FindLoginInfoAsync(Guid companyId, CancellationToken ct); } // sitio 2
public interface ICompanyRegistrationStore { Task<RegistrationOutcome> CreateAsync(Company company, UserAccount manager, CancellationToken ct); } // Created | DuplicateName | DuplicateEmail

// Application/Assistant
public interface IAssistantQuota { QuotaDecision TryConsume(Guid companyId, Guid employeeId); }
public interface IAssistantRuntimeReloader { Task ReloadAsync(Guid companyId, CancellationToken ct); }
public interface IAssistantCredentialProtector
{
    EncryptedCredential Protect(AssistantCredential plaintext, AssistantProvider provider, Guid companyId);
    bool TryUnprotect(EncryptedCredential stored, AssistantProvider provider, Guid companyId, out AssistantCredential? plaintext); // v2 y luego v1 (solo Company.DefaultId)
}
public interface IAssistantSettingsStore { /* GetAsync, GetVersionAsync, SaveAsync, DeleteAsync sin cambios de firma */
    Task<IReadOnlyList<CompanyVersion>> ListVersionsAsync(CancellationToken ct); }          // sitio 3 (poller)

// Infrastructure/Assistant
internal interface IAssistantRuntimeRegistry
{
    ValueTask<AssistantSnapshot> GetAsync(Guid companyId, CancellationToken ct);
    Task ReloadAsync(Guid companyId, CancellationToken ct);
    IReadOnlyList<(Guid CompanyId, string Version)> LoadedVersions();
}
```

## 4. Cambios de archivos (por PR)

| PR | Archivo | Acción | Descripción |
|---|---|---|---|
| 1 | `src/TimeTracker.Domain/Companies/Company.cs`, `CompanyStatus.cs` | Crear | Entidad y normalización |
| 1 | `src/TimeTracker.Domain/Users/UserAccount.cs`, `TimeTracking/TimeEntry.cs` | Modificar | `CompanyId`, `MustChangePassword`, `ChangePassword` |
| 1 | `src/TimeTracker.Infrastructure/Persistence/CompanyConfiguration.cs`, `UserConfiguration.cs`, `TimeEntryConfiguration.cs`, `AssistantSettings*Configuration.cs`, `AssistantSettings*Record.cs` | Crear/Modificar | Columnas, clave alternativa, FKs compuestas, PK |
| 1 | `.../Persistence/Migrations/<ts>_AddCompanies.cs` (+ `Designer`, snapshot) | Crear | DD-4 (A, SQL a mano) |
| 1b | `.../Migrations/<ts>_EnforceCompanyIsolation.cs` (+ `Designer`, snapshot) | Crear | DD-4 (B) |
| 1/2 | `.../Persistence/TimeTrackerDbContext.cs`, `DesignTimeDbContextFactory.cs` | Modificar | `ICompanyContext`, `CurrentCompanyId` |
| 2 | `src/TimeTracker.Application/Identity/ICurrentUser.cs`, `ICompanyContext.cs` | Modificar/Crear | DD-6, DD-7 |
| 2 | `src/TimeTracker.Api/Authentication/JwtDefaults.cs`, `JwtAccessTokenIssuer.cs`, `AuthenticationServiceCollectionExtensions.cs`, `HttpContextCurrentUser.cs`, `AuthEndpoints.cs`, `LoginResponse.cs`, `UserResponse.cs` | Modificar | `cid`, `mcp`, `me` |
| 2 | `src/TimeTracker.Api/Security/CompanyContextMiddleware.cs`, `.../Persistence/AmbientCompanyContext.cs`, `CompanyScopeFactory.cs` | Crear | DD-7 |
| 3 | `.../Persistence/TimeTrackerDbContext.cs` | Modificar | Filtro por convención y guarda de escritura |
| 3 | `.../Persistence/UserRepository.cs`, `CompanyRepository.cs`, `AssistantSettingsStore.cs` | Modificar/Crear | Sitios de la lista cerrada |
| 3 | `tests/TimeTracker.ArchitectureTests/CompanyIsolationTests.cs` | Crear | DD-9 |
| 4a | `src/TimeTracker.Application/Assistant/Configuration/*`, `AesGcmCredentialProtector.cs`, `AssistantSettingsStore.cs` | Modificar | DD-13, DD-17 |
| 4b | `src/TimeTracker.Infrastructure/Assistant/AssistantRuntimeRegistry.cs`, `CompanyRuntime.cs`, `AssistantSettingsPoller.cs`, `AssistantSettingsLoader.cs`, `InMemoryAssistantQuota.cs`, `ChatAssistantEngine.cs`, `AssistantServiceCollectionExtensions.cs`, `AiOptions.cs`, `AiOptionsValidator.cs` | Crear/Modificar | DD-14 a DD-16 |
| 5 | `src/TimeTracker.Application/Companies/*`, `Authentication/PasswordPolicy.cs`, `src/TimeTracker.Api/Authentication/RegistrationEndpoints.cs`, `Registration/*Options*.cs`, `RateLimitingServiceCollectionExtensions.cs`, `DocumentGeneration.cs`, `Program.cs` | Crear/Modificar | DD-19, DD-20 |
| 6 | `src/TimeTracker.Application/Team/*`, `src/TimeTracker.Api/Team/TeamEndpoints.cs`, `Security/PasswordChangePendingMiddleware.cs`, `ChangePasswordEndpoint` | Crear/Modificar | DD-21 |
| 6 | `src/TimeTracker.Api/Seeding/DemoUsersSeeder.cs`, `DemoDataSeeder.cs` | Modificar | DD-22 |
| 7 | `src/TimeTracker.Web/Pages/Register.razor`, `Team.razor`, `ChangePassword.razor`, `AssistantConfiguration.razor`, `Layout/NavMenu.razor`, clientes y DTO | Crear/Modificar | DD-18 y páginas nuevas |
| 8 | `AGENTS.md`, `README.md`, `.env.example`, `docs/adr/0020…0022*.md`, `docs/api/openapi.json`, runbook | Modificar/Crear | DD-25 |

## 5. Flujo de datos

```
Registro:  POST register-company ─ código (tiempo constante) ─ validación ─ hash ─ Set(nuevoId) ─ [companies + users] 1 transacción ─ token(cid)
Login:     correo ─ FindByEmail (sitio 1) ─ verificar hash ─ FindLoginInfo (sitio 2) ─ estado Active ─ token(cid, [mcp])
Petición:  token ─ RejectInvalidClaims ─ Set(cid) ─ caso de uso(ICurrentUser) ─ repositorio ─ SQL con company_id = @cid ─ FK compuestas
Asistente: pregunta ─ registry.GetAsync(cid) ─ pipeline de la compañía ─ herramientas (mismo DbContext filtrado) ─ cuota (cid, sub)
Guardado:  PUT config ─ store (fila de cid) ─ audit(cid) ─ ReloadAsync(cid); poller: ListVersions (sitio 3) ─ recarga solo las cargadas que cambiaron
```

## 6. Estrategia de pruebas

AGENTS §5 y §6: TDD (rojo antes que verde en cada PR), pruebas de seguridad del asistente por herramienta y dobles de `IChatClient`. Las pruebas de integración usan PostgreSQL real (Testcontainers) con una base propia por prueba. Todas las de aislamiento usan dos compañías A y B, cada una con un Manager y dos Employees (fábrica `TwoCompanyFixture`).

| # | Prueba imprescindible | Capa |
|---|---|---|
| T-1 | Equipo y fichajes: `GET /api/equipo`, `GET /api/equipo/{id}/fichajes` y los fichajes propios solo ven la compañía propia; un identificador de B desde A da **el mismo 404** (mismo estado, tipo, título y cuerpo) que un GUID inexistente | Integración |
| T-2 | Esquema: insertar un usuario con `manager_id` de otra compañía -> `23503`; un `time_entries` con `company_id` distinto del de su empleado -> `23503`; `manager_id` nulo se acepta (MATCH SIMPLE) | Integración |
| T-3 | Guarda de escritura: `Add`/`Update` de una entidad con `CompanyId` ajeno lanza `CompanyScopeViolationException` y no escribe; intentar cambiar `CompanyId` también | Integración |
| T-4 | Fallo cerrado: un `DbContext` sin compañía devuelve 0 filas en cada `DbSet`; con `Guid.Empty`, igual; no se lanza excepción en lectura | Integración |
| T-5 | Modelo: recorrer `context.Model.GetEntityTypes()` y exigir filtro en todos; lista de excepciones vacía; cada tipo con `company_id` lo tiene mapeado | Arquitectura/modelo |
| T-6 | `IgnoreQueryFilters` solo en los 3 sitios con recuento exacto; `Find`, `FindAsync`, SQL crudo y `ExecuteUpdate/Delete` ausentes en `src/` fuera de `Migrations/` | Arquitectura |
| T-7 | `openapi.json` sin `companyId`; ningún comando ni petición con `CompanyId`; `"cid"` solo en los archivos permitidos | Arquitectura |
| T-8 | Herramientas del asistente: con un mensaje que inyecta el GUID de un empleado de B, cada herramienta responde como si no existiera y no filtra datos de B; un Manager de A no ve el equipo de B | Integración con `IChatClient` falso |
| T-9 | Configuración: GET/PUT/DELETE solo sobre la fila propia; el `xmin` de la fila de B no cambia tras guardar o borrar la de A; la auditoría lleva `company_id` de A; la compañía sin fila ve el asistente deshabilitado (`provider None`, origen `none`) | Integración |
| T-10 | Runtime: la instantánea de A no atiende a B; una compañía no de plataforma nunca usa `Ai__ApiKey`; la prueba de conexión de B no usa la clave de A; expulsión por tope y por inactividad con `FakeTimeProvider`; la petición en curso termina durante la gracia; carga única concurrente; compañía expulsada se recarga | Unitaria e integración |
| T-11 | Poller: lee `(company_id, xmin)` de todas en una consulta, recarga solo las cargadas que cambiaron, aviso una vez por versión rechazada y compañía | Integración |
| T-12 | Cifrado: v2 ida y vuelta; un texto cifrado de A no se descifra con el `companyId` de B; v1 se lee solo con `Company.DefaultId`; `ReEncryptOrKeep` recifra a v2 | Unitaria |
| T-13 | Login y token: correo único global (409 al registrar un correo existente en otra compañía); token sin `cid` -> 401; `cid` inválido -> 401; `me` devuelve `companyName`; compañía `Suspended` -> 401 en login | Integración |
| T-14 | Registro: `Enabled=false` -> 404 y la ruta no existe; código incorrecto -> 403 antes de validar y sin revelar si el correo existe; código correcto crea compañía y Manager en una transacción; 409 por nombre y por correo; contraseña de 11, 12, 128 y 129 caracteres; tope por IP; comparación recorre todos los códigos (prueba del comparador); arranque falla con `Enabled` sin códigos, con `Seed:DemoUsers=true` o sin `ForwardedHeaders` en `Production` | Unitaria e integración |
| T-15 | Alta de empleados: `managerId` por defecto el creador; de otra compañía -> 400 sin distinguirlo de inexistente; FK compuesta como red; tope de empleados; contraseña temporal solo en la respuesta y ausente en registros; un Manager crea otro Manager solo en su compañía; Employee -> 403; correo repetido -> 409 sin nombrar compañía | Integración |
| T-16 | Contraseña temporal: con `mcp` toda ruta excepto `change-password` y `me` da 403; `change-password` con actual incorrecta / nueva igual / corta -> 400; éxito emite token sin `mcp` y desbloquea; regenerar solo con `must_change_password=true` | Integración |
| T-17 | Límites: política `register` por IP; cuota diaria y por minuto por compañía (agotar la de A no afecta a B); decremento de devolución; purga diaria del diccionario | Unitaria e integración |
| T-18 | Migración con datos: base sembrada (usuarios, fichajes, un huérfano, singleton con clave cifrada v1, auditoría) -> `Up A` conserva datos; `Up B` aborta con el huérfano y funciona sin él; triggers presentes y activos tras A y B; `Down B`, `Down A` ejecutan y `Down B` aborta con más de una fila; `has-pending-model-changes` vacío; el literal de la migración coincide con `Company.DefaultId` y `NormalizeName("Demo")` | Integración |
| T-19 | Siembra: idempotente en dos arranques; no toca un correo de otra compañía; fichajes demo con `CompanyId` demo | Integración |
| T-20 | Web: formularios (registro, alta, cambio) sin identificador de compañía, contraseña temporal limpiada al abandonar la página, textos con el nombre de la compañía | Web.Tests |
| T-21 | Dominio: `Company.NormalizeName` y sus validaciones (100 %); `UserAccount.ChangePassword` | Unitaria |

Las pruebas con proveedor real de IA siguen fuera (AGENTS §5).

## 7. Matriz de amenazas

No hay rutas de shell, subprocesos ni automatización de VCS (N/A). Amenazas propias del cambio y su tratamiento:

| Amenaza | Tratamiento | Prueba |
|---|---|---|
| IDOR entre compañías (id de B desde A) | Filtro global + FK compuestas + 404 idéntico | T-1, T-2 |
| Mass assignment de `companyId` | Ningún contrato lo acepta; `cid` solo del token | T-7 |
| Consulta sin filtro / `IgnoreQueryFilters` / `Find` | Convención central, lista cerrada, APIs prohibidas | T-5, T-6 |
| Escritura con compañía ajena | Guarda en `SaveChanges` | T-3 |
| Fuerza bruta del código de invitación | Comparación en tiempo constante, límite por IP, 403 genérico, bandera apagada | T-14 |
| Enumeración de correos o nombres | 409 aceptado, solo con código válido | ADR 0022 |
| Fuga de la contraseña temporal | Una vez, `no-store`, sin registros, cambio forzado real (`mcp`) | T-15, T-16 |
| Clave de plataforma usada por compañías nuevas | `uses_platform_assistant=false` por defecto; asistente deshabilitado sin fila | T-9, T-10 |
| Trasplante del texto cifrado entre compañías | AAD v2 con `company_id` | T-12 |
| Consumo abusivo (vecino ruidoso) | Cuota y tasa agregadas por compañía, tope de compañías cargadas | T-10, T-17 |
| Token obsoleto (compañía suspendida, rol) | Aceptado: 30 minutos, sin revocación | ADR 0020 |

## 8. Riesgos

| Riesgo | Mitigación |
|---|---|
| Runtime por compañía (memoria, carreras, expulsión) | PR 4b propia, tope, gracia de 120 s, pruebas con reloj falso; medir memoria (V-8) |
| Fuga por consulta no filtrada | Tres capas y pruebas A/B; la lista cerrada exige revisión explícita |
| Rol de BD propietario (sin RLS) | Diferida y documentada (DD-12) |
| Ventana de despliegue con instancias solapadas (A/B) | A y B en despliegues distintos; código nuevo escribe `company_id` antes de B (V-6) |
| Huérfanos en `time_entries` | Comprobación en B que aborta; decisión humana previa (V-1) |
| Rollback de Railway de un clic con B aplicada | Documentado: `Down` primero; ADR y runbook |
| `ForwardedHeaders` apagado en producción | Validador de arranque y V-7 |
| Usuarios demo con contraseña pública tras abrir el registro | Validador + retirada previa (DD-22) |
| Cualquier Manager edita la configuración de su compañía | Riesgo aceptado, aviso, auditoría de solo inserción y restablecer |
| Tope de empleados blando | Aceptado (pocas filas de margen) |
| Cuotas en memoria por instancia | Igual que ADR 0017; se revisa si hay réplicas |
| Volumen de cambios en pruebas (fábricas, tokens) | Commit/PR separado en la PR 2 |
| Literales fijados en pruebas de arquitectura | Se actualizan en la PR 7 con el cambio de texto |
| Nombre `Demo` reservado por la migración | Un registro con ese nombre recibe 409; aceptado |

## 9. Verificaciones pendientes (V-n)

| V | Comprobar | Si falla / acción |
|---|---|---|
| V-1 | Contenido de producción (**no verificado**): `SELECT count(*) FROM users`; usuarios cuyo correo no sea de demostración; `time_entries` huérfanos (`LEFT JOIN users`); filas de `assistant_settings` y recuento de `assistant_settings_audit` | Con huérfanos: exportar y borrar o reasignar antes de B. Con solo datos de demostración: valorar reiniciar la base antes de abrir el registro (Q-1) |
| V-2 | Spike en 10.0.12: ¿filtros con nombre?; el filtro por reflexión se parametriza por contexto; dos contextos con compañías distintas no se contaminan | Sin nombres: se mantiene el único filtro (DD-10). Si la parametrización falla, construir el filtro con la forma de captura documentada |
| V-3 | Cómo ejecutar migraciones y scripts contra la BD de producción en Railway (**no verificado**): `railway connect`, proxy TCP público, `railway run`, copias del plan | Documentar la vía real en el runbook antes de la PR 1b; mientras no se compruebe, ninguna migración B ni `Down` se aplica en producción |
| V-4 | Versión mayor de PostgreSQL en Railway (**no verificado**) >= 11 para `ADD COLUMN ... DEFAULT` sin reescritura; `pg_dump` compatible; `ADD COLUMN DEFAULT` no dispara el trigger de auditoría (prueba local con la misma versión) | Si fuera < 11, el relleno de la auditoría exigiría reescritura de tabla: rediseñar (sin `UPDATE` bajo trigger) |
| V-5 | EF: clave alternativa y FK compuesta generan lo esperado; `manager_id` nulo con `company_id` presente no valida nada; `has-pending-model-changes` vacío tras B; nombre real de la PK de `assistant_settings` | Ajustar la migración a mano |
| V-6 | ¿Railway solapa la instancia nueva y la antigua durante el despliegue?; valor actual de `Database__ApplyMigrationsOnStartup` en producción (**no verificado**) | Si solapa, mantener A y B en despliegues separados (ya lo exige DD-4); si no, se admite fusionarlos |
| V-7 | `ForwardedHeaders__Enabled=true` y `Hops` correctos en producción (**no verificado**; el ADR 0013 los define opcionales) | Activarlos antes de abrir el registro; sin ellos el límite por IP es una partición global |
| V-8 | Memoria de un pipeline + `HttpClient` por compañía bajo carga con N compañías (no medido) | Ajustar `Ai__MaxLoadedCompanies` y el valor por defecto |
| V-9 | `Find`/`FindAsync` no aplican el filtro y `ExecuteUpdate/Delete` sí, en EF 10.0.12 (prueba que lo demuestre) | Mantener las prohibiciones de DD-9 igualmente |
| V-10 | Orden del pipeline: `CompanyContextMiddleware` y `PasswordChangePendingMiddleware` van entre autenticación y autorización; el limitador de tasa y `/health` no dependen de compañía | Reordenar |
| V-11 | Ensayo completo con copia de datos (Up A, Up B, Down B, Down A) y arranque de la API sobre el resultado | Bloquea el despliegue de PR 1 y 1b |
| V-12 | Las pruebas del contrato (`OpenApiContractTests`, `DeliveryArtifactsTests`) no prohíben por nombre `temporaryPassword`/`password` y `companyName`; `.env.example` sin valores secretos nuevos | Ajustar las guardas con justificación |

## 10. Preguntas abiertas residuales

- [ ] **Q-1** Producción: ¿se migran los datos existentes a la compañía por defecto o se reinicia la base? Depende de V-1. Si contiene usuarios reales, hay que renombrar la compañía por defecto y decidir si conserva la clave de plataforma.
- [ ] **Q-2** ¿Se acepta partir la migración B en una PR 1b (desviación respecto a la lista de la propuesta) para que A y B vayan en despliegues distintos?
- [ ] **Q-3** ¿Se acepta la columna `uses_platform_assistant` (frente a comparar con el id de la demo)?
- [ ] **Q-4** Un Manager creado por otro Manager queda con `managerId` nulo (sin jerarquías nuevas). ¿Se confirma?
- [ ] **Q-5** ¿Códigos de invitación reutilizables y rotables por entorno (propuesta) o de un solo uso con tabla?
- [ ] **Q-6** Se añade «regenerar contraseña temporal» (solo antes del primer acceso) para no bloquear altas perdidas; amplía ligeramente el alcance. ¿Se acepta?
- [ ] **Q-7** Valores por defecto de `Company__MaxEmployees` (50), `Ai__CompanyDailyRequests` (500), `Ai__CompanyRequestsPerMinute` (30), `Ai__MaxLoadedCompanies` (20) y del límite de registro por IP (10 por hora).
- [ ] **Q-8** Custodia de la clave maestra (pendiente desde el ADR 0019): ahora protege a todas las compañías.
- [ ] **Q-9** Cómo señalar en 0018 y 0019 que 0021 los sustituye parcialmente sin reescribirlos (convención del ADR 0001).
- [ ] **Q-10** Quién puede abrir el registro y cómo se rotan los códigos de invitación (proceso operativo).
- RGPD (supresión y exportación) queda **fuera de alcance por decisión expresa** y se anota en el ADR 0022 como posposición; las jornadas de trabajo quedan para `jornadas-de-trabajo`.
