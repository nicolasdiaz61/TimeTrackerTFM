# Propuesta: autenticacion-y-roles

Fecha: 2026-10-12 · Fase: sdd-propose · Base: `exploration.md` (D1–D26) y valores por defecto de Q1–Q15, aceptados por delegación del usuario.

## Intención

TimeTracker ficha de extremo a extremo, pero con una identidad provisional que solo funciona en Development: no hay usuarios, contraseñas, roles ni control de acceso, y la API no puede desplegarse. Este cambio sustituye esa identidad por autenticación real (usuarios con contraseña y JWT) y añade el rol de responsable, que consulta en solo lectura los fichajes de su equipo. Es requisito previo de ausencias, del asistente de IA (que aplica la autorización del usuario autenticado) y del despliegue. Plazo TFM: 2026-10-25.

**Éxito:** con `docker compose up` y la Web local, un empleado de demostración inicia sesión en `/login`, ficha en `/fichajes` y solo ve sus fichajes; un responsable ve además `/equipo` con los fichajes de sus reportes directos; sin token la API responde 401, con rol insuficiente 403 y con un miembro ajeno 404.

### Hoja de ruta
`autenticacion-y-roles` (este cambio) → ausencias → asistente de IA → despliegue en Railway.

## Alcance

### Dentro
- **Domain:** `UserAccount` (`Id`, `Email` en minúsculas, `DisplayName`, `PasswordHash`, `Role`, `ManagerId?`) y `UserRole { Employee, Manager }`.
- **Application:** puertos `IUserRepository`, `IPasswordHasher`, `IAccessTokenIssuer`; `LoginUseCase`, `GetTeamMembersUseCase`, `GetTeamMemberTimeEntriesUseCase` (control por objeto, 404 idéntico); extracción de `TimeEntryListResult.From`. `ICurrentUser` no cambia.
- **Infrastructure:** tabla `users` (índice único `email`, FK autorreferencial `manager_id` RESTRICT, CHECK de `role` y `manager_id <> id`), repositorio y migración `AddUsers`.
- **Api:** `JwtBearer` HS256 (30 min, validación estricta), `PasswordHasher<object>`, `HttpContextCurrentUser` (claim `sub`); `POST /api/auth/login`, `GET /api/auth/me`, `GET /api/team/members`, `GET /api/team/members/{memberId}/time-entries`; política de respaldo autenticada y política `Manager`; `AllowAnonymous` solo en `/health`, login y `MapOpenApi`; `Jwt:SigningKey` obligatoria (≥ 32 bytes); siembra opcional `Seed:DemoUsers`/`Seed:DemoPassword`; rate limiting del login (10/min por IP); cabeceras de seguridad; `UseStatusCodePages`; esquema Bearer en OpenAPI; compose en `127.0.0.1:5274`; retirada de la identidad provisional.
- **Web:** `Login.razor`, `Team.razor`, `[Authorize]` en `/fichajes`, `JwtAuthenticationStateProvider`, `BearerTokenHandler`, token en `sessionStorage` con copia en memoria, menú con `AuthorizeView`.
- **Pruebas:** unitarias, integración con Testcontainers, `TestTokens`, `JwtStartupTests`, contrato y entrega actualizados.
- **Documentación:** ADR 0011 y 0012, README con usuarios de prueba inventados, `.env.example`, `config.yaml`.

### Fuera
Refresh token · 2FA · registro público · recuperación de contraseña · FK de `time_entries.employee_id` a `users` · jerarquías (solo reportes directos) · Web en compose · `ForwardedHeaders` · hosting estático de la Web con CSP · tablas de equipos o empleados · ausencias, IA y despliegue.

## Capacidades

### Nuevas
- `authentication`: login, hash de contraseñas, emisión y validación de JWT, `GET /api/auth/me`, rate limiting, cabeceras, siembra de demostración.
- `team-access`: rol `Manager`, listado del equipo y lectura de fichajes de un miembro con control por objeto.
- `web-session`: sesión en la Web, proveedor de estado, handler Bearer, rutas protegidas, `/login` y `/equipo`.

### Modificadas
- `time-tracking`: la identidad sale del token; todos los endpoints exigen autenticación.
- `startup-configuration`: `Jwt:SigningKey` y `Seed:*` validadas; se retira `Identity:DevelopmentEmployeeId`.
- `solution-architecture`: compose en 5274, 12 ADR, exportación OpenAPI con `Jwt:SigningKey` ficticia, README sin `Identity__*`.
- `time-entries-persistence`: migración `AddUsers` y pruebas con filas en `users`.
- `time-entries-ui`: `/fichajes` requiere sesión; un 401 lleva a `/login`.

> **Aviso de deltas destructivos:** se ELIMINAN los requisitos «Identidad provisional mediante ICurrentUser» (`time-tracking`) e «Identidad provisional solo en Development» (`startup-configuration`), y se modifican escenarios archivados de compose (puerto), número de ADR y claves de `.env.example`. El archivo debe advertir antes de fusionarlos. `docker compose down -v` borra los fichajes de desarrollo anteriores, que quedan huérfanos.

## Enfoque

Implementación mínima propia (D1): sin ASP.NET Core Identity, cookies ni OIDC. Application solo conoce puertos; Api es el composition root y aloja los adaptadores de JWT, hash e identidad. Paquetes nuevos: `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.10 (Api) y `Microsoft.AspNetCore.Components.Authorization` 10.0.10 (Web); respaldo 10.0.12 si aparece NU1903. Pipeline: ExceptionHandler → StatusCodePages → CORS → RateLimiter → Authentication → Authorization. La Web solo oculta UI; los permisos viven en la API.

### Tandas (un solo cambio, `size:exception` aceptado)
| Tanda | Contenido | Punto de control |
|---|---|---|
| 1 | Domain + Application con TDD | build, test y cobertura 100 % / ≥ 80 % en verde |
| 2 | Infrastructure: `users`, repositorio, `AddUsers`, Testcontainers | pruebas de repositorio y migraciones en verde |
| 3a | Api núcleo: JWT, hash, `HttpContextCurrentUser`, login/me, política de respaldo, retirada de identidad provisional, migración de pruebas | integración 200/401 en verde; sin restos de `DevelopmentCurrentUser` |
| 3b | Api extra: equipo y `Manager`, siembra, limitador, cabeceras, StatusCodePages, OpenAPI, compose 5274, `.env.example` | 403/404/429 probados; `docker compose up` y login en `http://localhost:5274` |
| 4 | Web: sesión, proveedor, handler, login, equipo, rutas protegidas | flujo manual de empleado y responsable registrado |
| 5 | Documentación: ADR 0011/0012, README, `config.yaml`, pruebas de entrega | pruebas de entrega en verde (12 ADR) |

### ADR (`docs/adr/`)
| N.º | Decisión |
|---|---|
| 0011 | Autenticación con JWT propio, roles y equipo; sustituye al 0009 (estado «Sustituido»); registra sin refresh token y sin FK con sus disparadores |
| 0012 | Sesión en la Web (`sessionStorage`) y riesgo XSS aceptado |

## Áreas afectadas

| Área | Impacto | Descripción |
|---|---|---|
| `src/TimeTracker.Domain`, `.Application` | Modificado | `UserAccount`, puertos, casos de uso de login y equipo |
| `src/TimeTracker.Infrastructure` | Modificado | `users`, repositorio, migración `AddUsers` |
| `src/TimeTracker.Api` | Modificado | Autenticación, autorización, endpoints, siembra, middleware; se eliminan los archivos de identidad provisional |
| `src/TimeTracker.Web` | Modificado | Login, equipo, sesión, handler, rutas protegidas |
| `tests/*` | Modificado | `ApiFactory`, `PostgresApiFactory`, `TestTokens`, `JwtStartupTests`, contrato y entrega |
| `Directory.Packages.props` | Modificado | Dos paquetes nuevos |
| `docker-compose.yml`, `.env.example`, `README.md`, `openspec/config.yaml` | Modificado | Puerto 5274, claves `Jwt__*`/`Seed__*`, usuarios de prueba |
| `docs/adr/0009`, `0011`, `0012`, `docs/api/openapi.json` | Modificado/Nuevo | ADR y contrato |

## Riesgos

| Riesgo | Prob. | Mitigación |
|---|---|---|
| Calendario (13 días, ≈ 1.490–1.955 líneas) | Alta | Tandas con punto de control; orden de recorte acordado |
| XSS roba el token de `sessionStorage` | Media | Expiración 30 min, cabeceras, sin HTML sin escapar; ADR 0012 |
| Sin refresh token (desviación de AGENTS §6) | Media | Aceptado; seguimiento en ADR 0011 |
| Fuga de la clave de firma | Baja | Solo por entorno, validada al arrancar, nunca en logs ni appsettings (prueba) |
| Contraseña de demostración pública | Media | Siembra desactivada por defecto; documentado no activarla en despliegue real |
| Enumeración de usuarios o fuerza bruta | Media | 401 genérico con hash ficticio; rate limiting del login |
| Rate limiting por IP tras proxy | Media | `ForwardedHeaders` antes de desplegar (fuera) |
| Sin FK en `time_entries`; fichajes huérfanos | Baja | ADR 0011 con disparador; `docker compose down -v` documentado |
| NU1903, PRF del hash, `TimeProvider` en JwtBearer, API OpenAPI, cuerpo del 429, CORS en 401/403/429 | Media | Validaciones V1–V12 en apply |
| CI sin ejecutar y con Docker | Alta | Reproducir localmente; documentar |

## Plan de reversión

Sin Git. Antes de cada tanda, apply copia los archivos existentes que va a modificar o eliminar en `..\TimeTracker.rollback\<tanda>\`, fuera del repositorio, conservando rutas relativas (`Program.cs`, `Directory.Packages.props`, proyectos Api/Web, `DevelopmentCurrentUser` y su opción/validador/extensión, `IdentityStartupTests`, `ApiFactory`, `PostgresApiFactory`, `docker-compose.yml`, `.env.example`, `README.md`, `config.yaml`, ADR 0009, `openapi.json`). Revertir: restaurar las copias de la tanda afectada y las posteriores, borrar los archivos nuevos (entidad, puertos, casos de uso, repositorio, migración `AddUsers`, adaptadores, páginas, pruebas, ADR 0011/0012) y ejecutar `docker compose down -v`. No hay datos reales que perder.

## Dependencias

- Restauración desde nuget.org: `JwtBearer` 10.0.10 y `Components.Authorization` 10.0.10 (respaldo 10.0.12).
- Docker Desktop en marcha para Testcontainers y compose.
- Sin software global nuevo.

## Criterios de éxito

- [ ] `dotnet build -warnaserror` y `dotnet format --verify-no-changes` limpios.
- [ ] `dotnet test` en verde, incluida la integración con PostgreSQL.
- [ ] Cobertura: Domain 100 %, Application ≥ 80 %.
- [ ] Login correcto → 200 con token; credenciales erróneas o correo inexistente → mismo 401 genérico.
- [ ] Sin token → 401; empleado en `/api/team/*` → 403; miembro ajeno o inexistente → 404 idéntico.
- [ ] Undécimo intento de login en un minuto desde la misma IP → 429.
- [ ] Sin `Jwt:SigningKey` válida, la API no arranca y nombra la clave sin el valor.
- [ ] Ninguna clase o configuración de la identidad provisional sigue en el código.
- [ ] `HasPendingModelChanges() == false`; `openapi.json` incluye las rutas nuevas y el esquema Bearer.
- [ ] `docker compose up`: login de demostración en `http://localhost:5274`.
- [ ] Flujo manual en la Web (empleado y responsable) registrado en `verify-report.md`.
- [ ] 12 ADR (0009 «Sustituido»), README y `.env.example` actualizados, con datos inventados.

## Previsión de carga de revisión

- Estimación: ≈ 1.490–1.955 líneas a mano (1: 390–500; 2: 55–75; 3a: 300–380; 3b: 300–380; 4: 300–400; 5: 140–200), sin migraciones generadas.
- Orden de recorte: esquema OpenAPI Bearer → cabeceras → rate limiting → pulido de equipo → fusionar ADR 0012 en 0011. Backend, control de acceso y pruebas de seguridad no se recortan.
- Decision needed before apply: No (`size:exception` aceptado)
- Chained PRs recommended: No (sin repositorio; las tandas actúan como puntos de control)
- 400-line budget risk: High

## Ronda de preguntas de propuesta

Resuelta por delegación (Q1–Q15, valores por defecto). Supuestos vigentes:
- Equipo = reportes directos por `ManagerId`; el responsable también ficha y solo lee los fichajes del equipo.
- Roles en inglés en el código y en español en la UI.
- Restricciones: sin Git, sin software global, datos inventados, `AGENTS.md` sin menciones ajenas al proyecto.
