# Exploración: autenticacion-y-roles

Fecha: 2026-10-13 · Fase: sdd-explore · Modo: openspec. Sigue a `esqueleto-solucion` y `fichaje-extremo-a-extremo` (archivados). El usuario delegó las decisiones: una recomendación por decisión y los valores por defecto de la sección 6 se aplican sin preguntar. Plazo TFM: 2026-10-25.

## 1. Estado actual (verificado en el código)
- `ICurrentUser { Guid EmployeeId }` en Application; adaptador provisional `DevelopmentCurrentUser` (lee `Identity:DevelopmentEmployeeId`, solo Development) con validador que impide arrancar fuera de Development. Los casos de uso solo conocen `ICurrentUser`.
- Api: sin autenticación, autorización, rate limiting ni cabeceras de seguridad. CORS ya permite `Authorization` (`AllowAnyHeader`), sin `AllowCredentials`. `/health` y `MapOpenApi` son anónimos: con política de respaldo autenticada hay que marcarlos `AllowAnonymous`.
- Datos: solo `time_entries` (`employee_id` GUID sin FK). Migración `InitialTimeEntries`; migración en arranque por bandera.
- `DocumentGeneration` (rama GetDocument.Insider) aporta valores ficticios incluido `Identity:DevelopmentEmployeeId` → cambiar por `Jwt:SigningKey`.
- Web: Blazor WASM con `/fichajes`, `TimeEntriesApiClient`, `HttpClient` propio (sin `Microsoft.Extensions.Http`). Sin login. `ApiBaseUrl` = `http://localhost:5274`; origen Web `http://localhost:5172`.
- Pruebas: `ApiFactory` y `PostgresApiFactory` inyectan identidad provisional; `ExportedDocument_DoesNotExposeEmployeeId` prohíbe `employeeId` en el contrato; `AppSettings_Contain...` prohíbe claves sensibles en `appsettings*.json`; `DeliveryArtifactsTests` fija claves de `.env.example`, puerto compose 8080, exactamente 10 ADR y mención de `Identity__DevelopmentEmployeeId` en README; `Web_DoesNotHardcodeApiUrl`.
- ADR 0009 dice que este cambio sustituirá el adaptador sin tocar Application. README: usuario de prueba "pendiente".

## 2. Hechos verificados
- Runtime `Microsoft.AspNetCore.App` 10.0.10; `PasswordHasher<T>` está en el framework compartido (sin paquete).
- NuGet: `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.9–10.0.12; `Microsoft.AspNetCore.Components.Authorization` 10.0.10–10.0.12 (no es transitivo de Components.WebAssembly).
- `PasswordHasher`: 100.000 iteraciones por defecto; PRF en .NET 10 sin confirmar (HMAC-SHA256 vs SHA512) → verificar en apply (V3).
- Doc oficial de Blazor recomienda cookies; con tokens la seguridad es responsabilidad propia. `MapIdentityApi` emite tokens opacos no JWT. `AuthorizeView` solo oculta UI.

## 3. Decisiones recomendadas
- **D1 Mecanismo:** implementación mínima propia: tabla `users`, `PasswordHasher` (PBKDF2) y JWT HS256 firmado con clave simétrica por configuración, validado con `JwtBearer`. Descartados: Identity completo/`MapIdentityApi` (superficie y tablas innecesarias), cookies (orígenes distintos), OIDC externo (sobredimensionado).
- **D2 Paquetes:** `JwtBearer` 10.0.10 en Api; `Components.Authorization` 10.0.10 en Web (misma versión que el resto ASP.NET). Nada nuevo en Domain/Application/Infrastructure. Verificar NU1903 con `Microsoft.IdentityModel.*` (V1; respaldo 10.0.12).
- **D3 Hash:** `PasswordHasher<object>` tras un puerto `IPasswordHasher` (Application) con adaptador en Api. Login con correo inexistente verifica contra hash ficticio y devuelve el mismo 401 genérico.
- **D4 Token:** claims `sub`, `name`, `email`, `role`, `jti`, `iat`, `nbf`, `exp`; 30 min constante; sin refresh token (desviación consciente de AGENTS §6, seguimiento en ADR 0011); validación estricta (`ValidAlgorithms=[HS256]`, ClockSkew 30 s, `MapInboundClaims=false`).
- **D5 Configuración:** `Jwt:SigningKey` obligatoria (≥32 bytes UTF-8) con `ValidateOnStart`, mensaje que nombra la clave sin el valor. `Jwt__SigningKey=` vacía en `.env.example`, con comando PowerShell en README para generarla. Se retiran las claves `Identity__*`.
- **D6 Modelo:** `UserAccount` en Domain (`Id`, `Email` minúsculas, `DisplayName`, `PasswordHash`, `Role`, `ManagerId?`; constructor privado único + `Create`). Tabla `users`: índice único `email`, FK autorreferencial `manager_id` (RESTRICT), CHECK `manager_id <> id`, CHECK de `role`. Migración `AddUsers`. Sin tablas de equipos/empleados.
- **D7:** sin FK de `time_entries.employee_id` a `users` en este cambio (documentado en ADR 0011 con disparador). Datos antiguos de la identidad provisional quedan huérfanos (`docker compose down -v`).
- **D8 Roles:** `UserRole { Employee, Manager }`; el responsable también ficha. `FallbackPolicy = RequireAuthenticatedUser`, política `Manager`; `AllowAnonymous` solo en `/health`, `POST /api/auth/login` y `MapOpenApi`.
- **D9:** `ICurrentUser` no cambia; nuevo `HttpContextCurrentUser` (claim `sub`) en Api. Se eliminan `DevelopmentCurrentUser`, opciones, validador, extensión e `IdentityStartupTests`.
- **D10 Endpoints:** `POST /api/auth/login` → 200 `{accessToken, expiresAt, user}` o 401 ProblemDetails genérico; `GET /api/auth/me` (solo claims); `GET /api/team/members` (Manager); `GET /api/team/members/{memberId}/time-entries` (Manager, solo lectura). El parámetro se llama `memberId` (no `employeeId`); regla reinterpretada: la identidad del que llama nunca es parámetro.
- **D11 Casos de uso:** `LoginUseCase` (devuelve null → 401), `GetTeamMembersUseCase`, `GetTeamMemberTimeEntriesUseCase` con control por objeto (`FindTeamMemberAsync`; no miembro/inexistente → mismo 404). `IUserRepository`. Extraer `TimeEntryListResult.From` del caso de uso existente.
- **D12 Rate limiting:** política `login` ventana fija 10/min por IP (429); orden del pipeline: ExceptionHandler → StatusCodePages → CORS → RateLimiter → Authentication → Authorization. `ForwardedHeaders` tras proxy: seguimiento antes de desplegar.
- **D13 Cabeceras:** middleware en línea (`nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, CSP restrictiva en API, `Cache-Control: no-store` en `/api`). Sin HSTS/HTTPS (TLS en el ingreso).
- **D14 CORS:** sin cambios de código; prueba de preflight con `authorization`; verificar que 401/403/429 conservan CORS (V8).
- **D15 Errores:** `UseStatusCodePages()` para ProblemDetails en 401/403/404/429 (V9).
- **D16 Token en Web:** `sessionStorage` vía `IJSRuntime` con copia en memoria; riesgo XSS aceptado y documentado (ADR 0012).
- **D17:** `JwtAuthenticationStateProvider` (decodifica payload, comprueba `exp`, sin validar firma), `AuthorizeRouteView`, `AuthorizeView` en menú.
- **D18:** `BearerTokenHandler : DelegatingHandler` compartido; en 401 limpia sesión y navega a `/login`. Sin `Microsoft.Extensions.Http`.
- **D19 Páginas:** `Login.razor`, `Team.razor` (`[Authorize(Roles="Manager")]`), `[Authorize]` en `/fichajes`, NavMenu con `AuthorizeView`.
- **D20 Siembra:** `Seed:DemoUsers` (bool, por defecto `false`) y `Seed:DemoPassword` (obligatoria ≥12 caracteres si la bandera está activa). `DemoUsersSeeder` idempotente por correo. Usuarios inventados: `marta.responsable@example.test` (Manager), `luis.empleado@example.test` y `ana.empleada@example.test` (equipo de Marta), `pablo.empleado@example.test` (sin responsable). Compose activa la siembra; `.env.example` y README documentan la contraseña de demostración inventada; el resto de claves sensibles vacías.
- **D21 Pruebas existentes:** `ApiFactory`/`PostgresApiFactory` con `Jwt:SigningKey` ficticia y cabecera Bearer por defecto; `TestTokens.Create(userId, role, ...)`; `IdentityStartupTests` → `JwtStartupTests`; Testcontainers para pruebas con filas en `users`.
- **D22:** actualizar pruebas de arquitectura/entrega: claves `Jwt`/`Seed` prohibidas en appsettings, contrato con rutas nuevas (200/401/403/404) y esquema Bearer, claves de `.env.example`, puerto compose 5274, 12 ADR, README sin `Identity__...`.
- **D23:** transformador OpenAPI con esquema `Bearer` (verificar API de `Microsoft.OpenApi` 2.7.6, V7; recorte posible).
- **D24 Puertos/CORS:** compose publica `127.0.0.1:5274:8080` (la Web no cambia); README documenta `Cors__AllowedOrigins__0=http://localhost:5172`; actualizar test de compose. La Web no entra en compose.
- **D25 ADR:** 0011 «Autenticación con JWT propio, roles y equipo» (sustituye al 0009, cuyo estado pasa a «Sustituido») y 0012 «Sesión en la Web y riesgo XSS»; total 12 ADR.
- **D26 Specs:** nuevas `authentication`, `team-access`, `web-session`; MODIFIED en `time-tracking`, `startup-configuration`, `solution-architecture`, `time-entries-persistence`, `time-entries-ui`.

## 4. Tamaño y tandas
≈ 1.490–1.955 líneas a mano (`size:exception`). Recorte en orden: esquema OpenAPI Bearer → cabeceras → rate limiting → pulido de equipo → fusionar ADR 0012 en 0011. Backend, control de acceso y pruebas de seguridad no se recortan.
1. Domain + Application (390–500): `UserAccount`, puertos, `LoginUseCase`, casos de equipo, refactor `TimeEntryListResult.From`.
2. Infrastructure (55–75): `users`, repositorio, migración `AddUsers`, Testcontainers.
3a. Api núcleo (300–380): JWT, emisor, hasher, `HttpContextCurrentUser`, login/me, política de respaldo, retirar identidad provisional, migrar pruebas.
3b. Api extra (300–380): equipo y política Manager, siembra, limitador, cabeceras, StatusCodePages, OpenAPI, compose 5274, `.env.example`.
4. Web (300–400): sesión, proveedor, handler, login, equipo, rutas protegidas.
5. Documentación (140–200): ADR 0011/0012, README con usuarios de prueba, `config.yaml`, pruebas de entrega.

## 5. Riesgos
Calendario (12 días); XSS con token en `sessionStorage`; sin refresh token; clave de firma (solo por entorno, sin logs); contraseña de demostración pública (solo con siembra activa; no activar en despliegue real); rate limiting tras proxy; sin FK en `time_entries`; equipo = solo reportes directos; NU1903/mezcla de versiones; PRF de PasswordHasher, `TimeProvider` en JwtBearer, API de seguridad de Microsoft.OpenApi, cuerpo del 429 (validar en apply); pruebas dependientes de `TestTokens`; CI sin ejecutar y con Docker; volúmenes antiguos huérfanos.

## 6. Preguntas abiertas con respuesta por defecto (aplicadas sin preguntar)
Q1 motor propio JWT → sí · Q2 sin refresh, 30 min → sí · Q3 `sessionStorage` + memoria → sí · Q4 roles en inglés, UI en español → sí · Q5 equipo por `ManagerId` → sí · Q6 sin FK en `time_entries` → sí · Q7 404 idéntico → sí · Q8 `memberId` → sí · Q9 siembra con contraseña de demostración documentada → sí · Q10 rate limit en login y cabeceras (recortables) → sí · Q11 compose 5274 sin Web → sí · Q12 paquetes 10.0.10 → sí · Q13 ADR 0011 y 0012 → sí · Q14 compose en Development, `MapOpenApi` solo Development → sí · Q15 el responsable ficha y solo lee los fichajes del equipo → sí.

## 7. Validaciones para apply
V1 versiones/NU190x · V2 Components.Authorization 10.0.10 · V3 formato del hash · V4 claims `sub`/`role` sin mapeo · V5 401/403 · V6 `TimeProvider` en JwtBearer · V7 API de seguridad OpenAPI · V8 CORS en 401/403/429 y preflight · V9 ProblemDetails del 429 · V10 auditoría de vulnerables en CI · V11 `/health` anónimo y `HEALTHCHECK` · V12 compose con siembra y login en `http://localhost:5274`.

## 8. Listo para propuesta
Sí. La propuesta debe incluir plan de reversión sin Git, `size:exception`, aviso de deltas destructivos (se elimina el requisito de identidad provisional) y que `docker compose down -v` borra los fichajes de desarrollo anteriores.
