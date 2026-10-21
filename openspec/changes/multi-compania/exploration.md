# Exploración: multi-compania

> Estado: superado por `proposal.md`, `design.md` y las specs (`specs/**`) en lo que contradiga. Este documento recoge la fase de exploración y no se actualiza con las decisiones posteriores.

Resumen de los hallazgos del explorador, revisados contra el código y las especificaciones el 2026-10-20, y de las decisiones firmes del usuario. Lo que no se pudo comprobar se marca «no verificado». Rama de trabajo: `docs/multi-compania-propuesta`.

## Decisiones firmes del usuario

1. Una sola base de datos compartida con columna `company_id` (aislamiento por código y esquema).
2. Autorregistro: cada Manager se registra y crea su compañía con un código de invitación secreto definido en el entorno (`Registration__InviteCodes`). Sin correo: no hay verificación de correo ni proveedor de correo en este cambio. El registro queda detrás de `Registration__Enabled`, apagada por defecto hasta que el aislamiento esté probado y fusionado.
3. Una persona pertenece a una sola compañía (correo único global).
4. Una compañía nueva sin configuración tiene el asistente deshabilitado; `Ai__ApiKey` de plataforma solo se usa, de forma explícita, para la compañía demo.
5. Alta de empleados por un Manager con contraseña temporal de un solo uso y cambio forzado en el primer inicio de sesión (`MustChangePassword` y endpoint de cambio de contraseña).
6. La compañía de demostración queda aislada; al abrir el registro se apaga la siembra de usuarios demo con contraseña pública. Si producción solo contiene datos de demo, valorar reiniciar la base de datos (pendiente de comprobar el contenido).
7. La clave maestra de cifrado vive solo en Railway, nunca en el repositorio.
8. Este cambio no incluye jornadas de trabajo (será `jornadas-de-trabajo`, que depende de este).

## Estado actual (verificado en el código y las specs)

| Hecho | Verificación |
|---|---|
| `users`: `Email` con índice único global `ux_users_email`; `Role`; `ManagerId` con FK autorreferencial sin comprobar compañía | Verificado (`UserConfiguration.cs`, spec `team-access`) |
| `time_entries.employee_id` sin FK a `users`; el ADR 0011 fijó como disparador «al añadir alta y baja de usuarios» | Verificado (ADR 0011, spec `time-entries-persistence`) |
| `assistant_settings` fila única con `ck_assistant_settings_singleton` (`id = 1`) y `xmin` como token de concurrencia | Verificado (`AssistantSettingsConfiguration.cs`, `AssistantSettingsStore.cs`) |
| `assistant_settings_audit` sin compañía; índice por `occurred_at`; triggers de solo inserción (migración `AddAssistantAuditImmutability`) | Verificado (`AssistantSettingsAuditConfiguration.cs`, ADR 0019) |
| 5 migraciones: `InitialTimeEntries`, `AddUsers`, `AddTimeEntryType`, `AddAssistantSettings`, `AddAssistantAuditImmutability` | Verificado (carpeta `Migrations`) |
| `ICurrentUser` expone solo `EmployeeId` y `Role` | Verificado |
| `JwtAccessTokenIssuer` emite `sub`, `name`, `email`, `role`, `jti` (más `iat`, `nbf`, `exp`, `iss`, `aud`) | Verificado |
| `RejectInvalidClaims` valida solo `sub` y `role` | Verificado |
| `AssistantSettingsStore`: `GetAsync`, `GetVersionAsync`, `SaveAsync` y `DeleteAsync` sin filtro (`FirstOrDefaultAsync` sobre la tabla entera) | Verificado |
| `UserRepository.ListDirectReportsAsync`/`FindDirectReportAsync` filtran por `ManagerId` (seguros solo si el manager es de la misma compañía); `FindDisplayNameAsync` busca por `userId` sin compañía | Verificado |
| `AssistantSettingsPoller` compara una sola versión (`xmin`) con `client.Current.Version`; `AssistantChatClient` con una instantánea global; `IAssistantQuota` es un singleton en memoria | Verificado (poller, `AssistantServiceCollectionExtensions.cs`); cuota por usuario y límites globales según ADR 0017 |
| `ConfigurationAssistantEnvironment` entrega `Ai__ApiKey` del entorno como valor por defecto | Verificado (línea 21) |
| Validación de contraseña en login: no vacía y ≤128; no existe política de contraseñas, alta de usuarios, gestión de equipo, cambio de contraseña ni infraestructura de correo | Verificado (`LoginUseCase`, specs) |
| Los identificadores se generan con `Guid.CreateVersion7` | Verificado |
| Versiones fijadas: EF Core 10.0.12, Npgsql EF 10.0.3 | Verificado (`Directory.Packages.props`) |
| `TimeEntryRepository` sin red de seguridad por compañía | Según el explorador; no releído en esta ronda |
| Filtros de consulta con nombre en EF Core 10 | No verificado (comprobar con un spike contra 10.0.12) |
| Contenido real de la BD de producción en Railway (fichajes huérfanos, usuarios no demo) | No verificado |
| Cómo ejecutar `dotnet ef`/migraciones contra la BD de producción (solo red privada; `railway run`) | No verificado |

## Puntos de fuga entre compañías

- `AssistantSettingsStore` (Get/Version/Save/Delete): un Manager podría leer, sobrescribir o borrar la configuración y la clave cifrada de otra compañía.
- Instantánea global del asistente: las preguntas de la compañía B se atenderían con la clave de la A.
- `UserRepository.ListDirectReports`/`FindDirectReport`: seguros solo si `ManagerId` pertenece a la misma compañía (hoy nada lo impone).
- `FindDisplayNameAsync`: por `userId` sin compañía.
- `TimeEntryRepository`: sin red de seguridad por compañía.
- `InMemoryAssistantQuota`: por empleado, con límites globales; las políticas de tasa son por `sub` sin tope agregado por compañía.
- Auditoría sin `company_id`.
- AAD del cifrado sin compañía: el texto cifrado se puede mover entre compañías.

## Diseño recomendado

**Modelo.**
- `companies` (`id`, `name`, `name_normalized` único, `status`, `created_at`).
- `users.company_id` NOT NULL, índice único `(company_id, id)` y FK compuesta `(company_id, manager_id)` → `users(company_id, id)` (MATCH SIMPLE: un manager nulo no exige nada).
- `time_entries.company_id` NOT NULL con FK compuesta `(company_id, employee_id)` → `users(company_id, id)`. Cierra el hueco del ADR 0011 (antes, comprobar huérfanos en producción).
- `assistant_settings` con PK `company_id` (se eliminan `id` y el singleton; `xmin` se mantiene).
- `assistant_settings_audit.company_id` NOT NULL con índice `(company_id, occurred_at desc)`.

**Aislamiento híbrido en tres capas.**
1. FK compuestas (verdad estructural).
2. Filtro global `HasQueryFilter` que falla cerrado (sin compañía no devuelve filas) con `ICompanyContext`; lista cerrada de usos de `IgnoreQueryFilters` vigilada por una prueba de arquitectura, más una prueba que recorre `context.Model.GetEntityTypes()` y exige filtro en toda entidad con `company_id`.
3. Pruebas A/B con dos compañías.

**Identidad.** La compañía sale solo del claim `cid` del JWT (`ICurrentUser.CompanyId`); `RejectInvalidClaims` exige un `cid` válido (los tokens vigentes caducan a los 30 minutos tras desplegar; falla cerrado). Nunca de ruta, consulta, cuerpo ni cabecera (prueba de arquitectura: `openapi.json` sin parámetro ni propiedad `companyId`). Límite conocido: con token de 30 minutos, una compañía suspendida conserva el acceso hasta que caduque (aceptado).

**RLS de PostgreSQL: diferida.** El rol de la aplicación es propietario de las tablas (ADR 0019); haría falta un rol distinto, `FORCE ROW LEVEL SECURITY` y `app.company_id` por transacción con el pool de Npgsql. Se anota como endurecimiento futuro.

**Configuración del asistente por compañía.**
- Una fila por compañía; auditoría con `company_id`.
- GOTCHA: el `UPDATE` de relleno de `company_id` sobre `assistant_settings_audit` lo bloquea el trigger. La migración añade la columna con `DEFAULT` constante (id fijo de la compañía por defecto) y luego lo quita, sin `UPDATE` ni tocar el trigger.
- AAD del cifrado con `company_id` (formato v2; se prueba v2 y luego v1; `ReEncryptOrKeep` recifra al guardar): mejora, no bloqueo.
- Runtime: el singleton pasa a un registro de instantáneas por compañía, construidas de forma perezosa y sustituidas por versión `xmin`. El poller lee todas las `(company_id, xmin)` en una consulta y recarga las que cambiaron; conserva la gracia de 120 s y «conservar la última válida». Con tope y expulsión de compañías poco usadas (un pipeline y un `HttpClient` por compañía activa).
- Cuotas y límites por compañía: tope agregado no editable por el Manager; siguen en memoria por instancia (ADR 0017).
- `/asistente/configuracion`: el aviso «única para toda la empresa» pasa a nombrar la compañía (hay pruebas de arquitectura con literales fijados que cambiarán). El riesgo aceptado pasa a «cualquier Manager de la misma compañía».

**ADR.** 0018 y 0019 no se reescriben. Nuevo ADR 0020 (aislamiento multiempresa) y ADR 0021 que sustituye al 0018/0019 en lo de «fila única/toda la instancia»; actualizar `AGENTS.md` §1 y §7. Posible ADR de autorregistro con código de invitación.

**Autorregistro sin correo.** `POST /api/auth/register-company` anónimo (`companyName`, `managerName`, `email`, `password`, `inviteCode`):
- una transacción crea compañía y Manager;
- política de tasa por IP (depende de `ForwardedHeaders`, que el ADR 0013 define opcional y apagado por defecto; que esté activo en Railway no está verificado, ver `design.md` V-7);
- código de invitación comparado en tiempo constante;
- nombre de compañía normalizado y único (solo duplicado exacto);
- contraseña de 12 a 128 caracteres sin reglas de composición;
- tope de empleados por compañía configurable por entorno;
- riesgo residual aceptado y documentado: enumeración de correos (409 por correo repetido).

**Alta de empleados.** El Manager crea al empleado con contraseña temporal de un solo uso. `ManagerId` por defecto es el Manager que crea; otro solo de la misma compañía (caso de uso y FK compuesta). Correo ya existente: 409 sin revelar la compañía.

**Migración en dos pasos (expandir/contraer).**
- A) Crear `companies`, la compañía por defecto de id fijo, `company_id` nullable en `users`, `time_entries`, `assistant_settings` y auditoría (rellenar; la auditoría por `DEFAULT` constante).
- B) NOT NULL, índice único `(company_id, id)`, FK compuestas, índices, PK de `assistant_settings` a `company_id` y retirada del singleton.
- Reversibilidad: el `Down` de B restaura el singleton solo si hay una sola compañía (aborta con más de una). Copia de seguridad previa. `Database__ApplyMigrationsOnStartup=true` aplica al arrancar (valor real en producción no verificado; Railway no tiene autodespliegue, solo `railway up` manual: ver `design.md`); un rollback de código no revierte el esquema (ejecutar `Down` antes de redesplegar el código antiguo). Ensayo con datos copiados.

**Siembra.** `DemoUsersSeeder` y `DemoDataSeeder` crean o encuentran la compañía demo de forma idempotente.

## Corte en PR apiladas (tamaños sin Designer)

| # | PR | ~Líneas |
|---|---|---|
| 1 | Modelo y migración | 600-900 |
| 2 | Token `cid` | 400-600 |
| 3 | Aislamiento de consultas | 700-1000 |
| 4a | Configuración del asistente por compañía: datos y casos de uso | 600-900 |
| 4b | Runtime del asistente por compañía (mayor riesgo técnico) | 700-1000 |
| 5 | Autorregistro con código de invitación tras bandera | 500-800 |
| 6 | Alta de empleados con contraseña temporal y cambio de contraseña | 500-800 |
| 7 | Web (`/registro`, gestión de equipo, cambio de contraseña, aviso) | 600-900 |
| 8 | Docs, ADR, runbook, README, specs (mejor actualizar en cada PR) | 300-500 |

Estimación total: 15-20 jornadas (sin PR de correo, ahorro de 3-4). `Registration__Enabled` permanece apagada hasta que 1-4b estén fusionadas con sus pruebas de aislamiento.

## Pruebas de aislamiento imprescindibles (dos compañías A y B, cada una con un Manager y dos Employees)

- API de equipo y fichajes: mismo 404 para ids de otra compañía que para inexistentes.
- Esquema: `23503` por `ManagerId` de otra compañía y por fichaje con `company_id` distinto.
- Herramientas del asistente: inyección con un GUID de B en el mensaje.
- Configuración: GET/PUT/DELETE solo sobre la fila propia, `xmin` de la otra intacto, auditoría con `company_id`, compañía sin fila ve el asistente deshabilitado, la instantánea de A no atiende a B, la prueba de conexión no usa clave ajena.
- Login y token: correo único global, token sin `cid` → 401, `me` devuelve la compañía.
- Fallo cerrado: `DbContext` sin compañía no devuelve nada.
- Arquitectura: lista cerrada de `IgnoreQueryFilters`, todas las entidades con `company_id` con filtro, `openapi.json` sin `companyId`.
- Migración con datos: triggers de auditoría activos, `Down` funciona.
- Límites de tasa y cuotas por compañía.

## Riesgos principales

Runtime por compañía (4b); fuga residual por consulta sin filtro o `IgnoreQueryFilters` mal usado; clave de plataforma usada por compañías nuevas (mitigado: deshabilitado por defecto); trigger de auditoría frente al relleno; rollback de producción; huérfanos en `time_entries`; abuso del registro sin verificación de correo (mitigado por código, bandera y límite por IP); volumen de cambios en tests (tokens, usuarios, fábricas); tokens de 30 minutos; pruebas de arquitectura con literales fijados; rol de BD propietario.

## Pendientes de decisión (con recomendación)

Ver `proposal.md`, sección «Decisiones pendientes». Verificaciones previas: contenido real de la BD de producción y filtros con nombre de EF Core 10.0.12.

## Ronda de preguntas de la propuesta

Las decisiones firmes (1 a 8) llegan del usuario; no se hizo una ronda interactiva adicional. Las decisiones pendientes quedan listadas con recomendación en `proposal.md` para su revisión.
