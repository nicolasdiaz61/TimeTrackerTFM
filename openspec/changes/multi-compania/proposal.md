# Propuesta: multi-compañía con aislamiento y autorregistro (multi-compania)

## Intención

Hoy TimeTracker asume una sola empresa: el correo es único global, la configuración del asistente es una fila única y una instantánea global (ADR 0018/0019), y nada impide que un Manager vea o modifique datos de otra organización si la instancia llegara a compartirse. Se quiere servir a varias compañías en una misma instancia y base de datos: cada Manager se registra y crea la suya, da de alta a sus empleados y nadie ve datos ajenos. El ADR 0011 dejó diferida la FK `time_entries.employee_id` → `users` hasta añadir el alta de usuarios; este cambio la cierra. Detalles y hechos verificados en `exploration.md`.

## Alcance

### Dentro del alcance
- Tabla `companies` y `company_id` en `users`, `time_entries`, `assistant_settings` (PK) y `assistant_settings_audit`, con FK compuestas `(company_id, manager_id)` y `(company_id, employee_id)`.
- Aislamiento en tres capas: FK compuestas, filtro global `HasQueryFilter` que falla cerrado con `ICompanyContext`, y pruebas A/B más pruebas de arquitectura.
- Claims `cid` y `cname` en el JWT (`ICurrentUser.CompanyId`; `cname` es el nombre de la compañía, solo de presentación, para que `GET /api/auth/me` no consulte la base de datos); `RejectInvalidClaims` exige `cid`; la compañía nunca sale de ruta, consulta, cuerpo ni cabecera.
- Configuración del asistente por compañía: una fila por compañía, auditoría con `company_id`, registro de instantáneas por compañía, poller global, cuotas y topes por compañía (`Ai__RequestsPerMinutePerCompany` 30, `Ai__DailyRequestsPerCompany` 500 por día natural de Europe/Madrid, `Ai__MaxLoadedCompanies` 100); compañía sin configuración = asistente deshabilitado; `Ai__ApiKey` solo para la compañía demo.
- Autorregistro `POST /api/auth/register-company` con código de invitación secreto (`Registration__InviteCodes`: lista separada por comas, 16 a 128 caracteres, reutilizables y rotables por el entorno), bandera `Registration__Enabled` (apagada por defecto), límite de tasa de 5 por hora por IP, contraseña de 12 a 128 caracteres, tope de empleados por compañía (`Company__MaxEmployees`, 50 por defecto, rango 1 a 1000, exacto bajo concurrencia).
- Alta de empleados por un Manager (`POST /api/employees`) con contraseña temporal de un solo uso, `MustChangePassword`, regeneración de la contraseña temporal solo antes del primer acceso y endpoint de cambio de contraseña (`POST /api/auth/change-password`, 5 por minuto por usuario).
- Compañía demo aislada; siembra idempotente; al abrir el registro se apaga la siembra de usuarios demo.
- Migración expandir/contraer con compañía por defecto de id fijo, plan de copia de seguridad y ensayo; la migración A va en la PR 1 y la B en la PR 1b (despliegues distintos).
- Web: `/registro`, gestión de equipo y alta de personas (`/equipo/alta`), cambio de contraseña, aviso de configuración con el nombre de la compañía.
- ADR 0020 (aislamiento) y 0021 (sustituye parcialmente 0018/0019 en «fila única/toda la instancia») en la PR 4a, y ADR 0022 (autorregistro y contraseñas) en la PR 5; actualizar `AGENTS.md` §1, §6 y §7, README y runbook.

### Fuera del alcance
- Jornadas de trabajo (cambio `jornadas-de-trabajo`, depende de este).
- Correo: verificación de correo, recuperación de contraseña, proveedor de correo.
- RLS de PostgreSQL (diferida; ver riesgos).
- Supresión y exportación de datos RGPD (posposición explícita).
- Pertenencia a varias compañías por persona, jerarquías nuevas, rol Admin de plataforma, baja o suspensión de compañías desde la interfaz, revocación de tokens, cuotas compartidas entre instancias.

## Capacidades

### Capacidades nuevas
- `company-isolation`: compañía, `company_id`, FK compuestas, filtro global que falla cerrado, `cid` en el token, `ICompanyContext`, lista cerrada de `IgnoreQueryFilters`, openapi sin `companyId`, migración expandir/contraer.
- `company-registration`: autorregistro con código de invitación, bandera, límite por IP, política de contraseñas, nombre de compañía único.
- `employee-management`: alta de empleados con contraseña temporal, `MustChangePassword`, cambio de contraseña, regeneración de la contraseña temporal antes del primer acceso (delta que añade `tasks.md`, tarea 6.2), tope por compañía, creación de Managers dentro de la compañía.

### Capacidades modificadas
(13 capacidades modificadas; las 16 specs de `specs/` son estas 13 más las 3 nuevas.)
- `team-access`: equipo y 404 idéntico acotados a la compañía; alta de usuarios deja de estar fuera de alcance; FK de `time_entries` a `users`.
- `authentication`: claims `cid` y `cname`, `me` y login devuelven la compañía, `MustChangePassword`, registro público deja de estar fuera de alcance.
- `time-tracking`: `TimeEntry.Start` con `companyId` (`Start(companyId, employeeId, type, at)`); la compañía del fichaje sale del token y se hereda en la pausa.
- `assistant-settings`: una configuración por compañía; auditoría y concurrencia por compañía; recarga por compañía; aviso nombra la compañía.
- `assistant-credential-protection`: AAD con `company_id` (formato v2, lectura v2 y v1).
- `assistant-configuration`: `Ai__ApiKey` de plataforma solo para la compañía demo.
- `assistant-tools`, `assistant-endpoint`: herramientas, cuota y límites por compañía; inyección con GUID ajeno.
- `time-entries-persistence`: sustituye «Sin clave foránea de fichajes a usuarios»; `company_id` en `time_entries`.
- `demo-data`: compañía demo aislada e idempotente.
- `web-session`: páginas nuevas, cambio forzado de contraseña, menú.
- `startup-configuration`: `Registration__*`, `Company__MaxEmployees`, exclusión entre siembra demo y registro, `.env.example` (los topes del asistente por compañía los especifica `assistant-configuration`).
- `solution-architecture`: pruebas de arquitectura nuevas (filtros, `IgnoreQueryFilters`).

## Enfoque

Una BD compartida con `company_id` y aislamiento híbrido (esquema + filtro global + pruebas). La compañía sale solo del `cid` del token (el nombre visible viaja en `cname`). La migración va en dos pasos (A expandir en la PR 1, B contraer en la PR 1b, en despliegues distintos) sin tocar el trigger de auditoría: columna con `DEFAULT` constante y luego sin él. El runtime del asistente pasa de singleton a un registro de instantáneas por compañía, con carga perezosa, sustitución por `xmin`, tope y expulsión. El registro queda detrás de `Registration__Enabled` hasta tener fusionadas y desplegadas las PR 1, 1b, 2, 3, 4a y 4b con sus pruebas de aislamiento en verde.

## Áreas afectadas

| Área | Impacto |
|---|---|
| `src/TimeTracker.Domain/Users`, `TimeTracking` | Modificado: compañía, `CompanyId` explícito en `TimeEntry` (`TimeEntry.Start(companyId, ...)`, capacidad `time-tracking`), `MustChangePassword` |
| `src/TimeTracker.Application` (identidad, auth, equipo, asistente) | Modificado/Nuevo: `ICurrentUser.CompanyId`, casos de uso de registro y alta |
| `src/TimeTracker.Infrastructure/Persistence` (+ migraciones A y B) | Modificado/Nuevo |
| `src/TimeTracker.Infrastructure/Assistant` | Modificado: registro de instantáneas, poller, cuota, AAD |
| `src/TimeTracker.Api` (auth, seeding, endpoints, límites) | Modificado/Nuevo |
| `src/TimeTracker.Web` | Nuevo/Modificado |
| `docs/adr/0020`, `0021`, `0022`, `AGENTS.md`, README, `.env.example`, `docs/api/openapi.json` | Nuevo/Modificado |
| Tests (tokens, usuarios, fábricas, arquitectura) | Modificado: separar en su propio PR |

## Riesgos

| Riesgo | Prob. | Mitigación |
|---|---|---|
| Runtime por compañía (4b): memoria, carreras, expulsión | Alta | PR propio, tope de compañías activas, pruebas de instantánea por compañía |
| Fuga por consulta sin filtro o `IgnoreQueryFilters` mal usado | Media | FK compuestas, filtro que falla cerrado, prueba sobre `GetEntityTypes()`, lista cerrada |
| Clave de plataforma usada por compañías nuevas | Media | Asistente deshabilitado por defecto; clave solo para la demo |
| Trigger de auditoría frente al relleno de `company_id` | Media | `DEFAULT` constante, sin `UPDATE` |
| Rollback de producción (el código no revierte el esquema) | Media | `Down` antes de redesplegar, copia de seguridad, ensayo con datos copiados; hasta que el ADR 0020 exista (PR 4a), las descripciones de las PR 1 a 3 llevan la advertencia de rollback |
| `ForwardedHeaders` no activo por defecto (ADR 0013): el límite por IP sería una partición global | Media | Validador de arranque que lo exige con el registro abierto en producción; comprobación previa a la apertura |
| Huérfanos en `time_entries` bloquean la FK | Media | Comprobar producción antes (no verificado) |
| Abuso del registro sin verificar correo | Media | Código secreto, bandera apagada, límite por IP |
| Enumeración de correos (409 en registro) | Baja | Aceptado y documentado |
| Volumen de cambios en tests | Alta | PR separado de fábricas y tokens |
| Token de 30 min: compañía suspendida o rol obsoleto | Baja | Aceptado, documentado |
| Pruebas de arquitectura con literales fijados | Media | Actualizarlas en el PR que cambia el aviso |
| Rol de BD propietario (sin RLS) | Media | RLS diferida y anotada en el ADR 0020 |
| Filtros con nombre de EF Core 10.0.12 | Baja | Spike previo (no verificado) |

## Plan de reversión

- Las PR 5 a 7 se revierten sin tocar datos; `Registration__Enabled=false` cierra el alta al instante.
- Esquema: la migración B (PR 1b; `Down` restaura el singleton solo si hay una sola fila) y luego la A (PR 1). Un rollback de código NO revierte el esquema: ejecutar `Down` antes de redesplegar el código antiguo. Las PR 1 a 3 llevan esta advertencia en su descripción hasta que el ADR 0020 entre en la PR 4a.
- Antes de aplicar en producción: copia de seguridad y ensayo con datos copiados. Con `Database__ApplyMigrationsOnStartup=true` la migración se aplica al arrancar el nuevo código.
- Con varias compañías ya creadas, el `Down` pierde la separación: revertir solo antes de abrir el registro.

## Dependencias

- Para abrir el registro: PR 1, 1b, 2, 3, 4a y 4b fusionadas y desplegadas con sus pruebas de aislamiento en verde.
- Clave maestra de cifrado en Railway (nunca en el repo); `ForwardedHeaders` **no está activo por defecto** (ADR 0013): hay que activarlo (`ForwardedHeaders__Enabled=true`, `Hops` correctos) antes de abrir el registro, y un validador de arranque lo exige con el registro abierto en producción.
- Verificaciones previas: contenido de la BD de producción (huérfanos, usuarios no demo) y filtros con nombre de EF Core 10.0.12.

## Criterios de éxito

- [ ] Con dos compañías A y B, ningún endpoint, herramienta del asistente ni consulta devuelve o modifica datos de la otra; el id ajeno da el mismo 404 que el inexistente.
- [ ] El esquema rechaza (`23503`) un `ManagerId` de otra compañía y un fichaje con `company_id` distinto del de su empleado.
- [ ] Un `DbContext` sin compañía no devuelve filas; un token sin `cid` recibe 401.
- [ ] La configuración y la clave de una compañía no son legibles ni usables por otra; una compañía nueva ve el asistente deshabilitado.
- [ ] `openapi.json` no contiene `companyId`; las pruebas de arquitectura (filtros, `IgnoreQueryFilters`) están en verde.
- [ ] La migración conserva los datos existentes, los triggers de auditoría siguen activos y el `Down` funciona.
- [ ] El registro con código de invitación crea compañía y Manager; con la bandera apagada no existe la ruta efectiva; el empleado con contraseña temporal debe cambiarla.
- [ ] La siembra demo queda apagada al abrir el registro.

## Corte en PR apiladas (tamaños sin Designer)

| # | PR | ~Líneas |
|---|---|---|
| 1 | Modelo y migración A (sin ADR; descripción con advertencia de rollback) | 600-900 |
| 1b | Migración B (despliegue aparte; descripción con advertencia de rollback) | 300-500 |
| 2 | Token `cid`, `cname` y `mcp` | 400-600 |
| 3 | Aislamiento de consultas | 700-1000 |
| 4a | Asistente por compañía: datos y casos de uso; ADR 0020 y 0021 | 600-900 |
| 4b | Runtime por compañía | 700-1000 |
| 5 | Autorregistro tras bandera; ADR 0022 | 500-800 |
| 6 | Alta de empleados, contraseña temporal (con regeneración) y cambio | 500-800 |
| 7 | Web | 600-900 |
| 8 | Docs, runbook, README, specs vigentes (mejor repartido en cada PR) | 300-500 |

Son diez PR (1, 1b, 2, 3, 4a, 4b, 5, 6, 7, 8) más la tarea 9 (verificación final, despliegue por fases y apertura del registro; no es una PR). Todas superan el presupuesto de 400 líneas: se requiere estrategia de PR apiladas (`stacked-to-main`; el usuario fusiona). Estimación total: 15-20 jornadas.

## Decisiones tomadas

1. BD compartida con `company_id`. 2. Autorregistro con código de invitación secreto, sin correo, tras `Registration__Enabled`. 3. Una persona, una compañía (correo único global). 4. Compañía nueva con el asistente deshabilitado. 5. Contraseña temporal de un solo uso con cambio forzado. 6. Demo aislada; siembra demo apagada al abrir el registro. 7. Clave maestra solo en Railway. 8. Sin jornadas de trabajo. 9. Migración B en una PR 1b separada (A y B no pueden ir juntas con `Database__ApplyMigrationsOnStartup`). 10. Se incluye regenerar la contraseña temporal, solo antes del primer acceso. 11. Códigos de invitación reutilizables y rotables, de 16 a 128 caracteres. 12. Topes por defecto aceptados y configurables por entorno: 50 empleados por compañía, 30 por minuto y 500 por día del asistente por compañía, 100 compañías con asistente cargado, 5 registros por hora por IP, cambio de contraseña 5 por minuto por usuario. 13. Claim `cname` en el token (el nombre de la compañía no se consulta en `me`). 14. ADR 0020 y 0021 en la PR 4a. 15. RGPD fuera de alcance.

## Decisiones pendientes (con recomendación; requieren confirmación del usuario)

Las filas marcadas «(confirmado)» ya cuentan con la decisión del usuario recogida en `tasks.md`; se conservan por trazabilidad.

| Tema | Recomendación |
|---|---|
| ¿Cualquier Manager edita la configuración del asistente o solo el fundador? | Cualquiera; riesgo aceptado, ahora acotado a la compañía |
| ¿Un Manager puede crear otros Managers? | Sí, solo en su compañía, sin jerarquías nuevas; el responsable por defecto es el Manager que crea |
| Longitud mínima de contraseña | 12 (máximo 128), sin reglas de composición |
| Topes por compañía (empleados, cuota, tasa del asistente) | Configurables por entorno, no editables por el Manager (confirmado: 50 empleados con rango 1 a 1000; 30 por minuto y 500 por día del asistente; 100 compañías cargadas) |
| Supresión y exportación RGPD | Fuera de este cambio; posposición explícita (confirmado) |
| `CompanyId` explícito en `TimeEntry` | Columna explícita en Domain |
| Claim `cid` y nombre de la compañía | `cid` y `cname` en el token; `me` los lee de los claims sin consultar la base de datos (confirmado) |
| Poller global o comprobación por pregunta | Poller global |
| BD de producción: ¿reiniciar si solo hay datos de demo? | Decidir tras comprobar el contenido (no verificado) |
