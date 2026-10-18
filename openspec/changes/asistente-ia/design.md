# Diseño: asistente-ia

Fecha: 2026-10-18 · Fase: sdd-design · Modo: openspec · Base: `proposal.md` (alternativa A, decisiones 1-8), `exploration.md`, código vigente de `src/` y `tests/`, y valores fijados por el orquestador: en v1 el responsable consulta **miembro a miembro** (sin agregados de equipo) y, con el asistente deshabilitado, la página muestra un **aviso en español** y el enlace del menú sigue visible. Las specs delta se redactan en paralelo; la sección 12 recoge los puntos que deben coincidir.

Convención: **DD-n** = decisión de diseño; **[V-n]** = comprobación que apply hace antes de dar el paso por bueno (sección 11). No se pudieron consultar NuGet ni la documentación en esta fase: las firmas de los SDK quedan como V-n.

## 1. Enfoque técnico

Alternativa A. Application gana el resumen por día en hora de España, la validación de la pregunta y dos puertos (`IAssistantEngine`, `IAssistantQuota`), sin paquetes. Infrastructure aloja las tres `AIFunction`, el bucle `FunctionInvokingChatClient` acotado, el prompt, las opciones `Ai:*`, la fábrica Anthropic/Ollama y la cuota diaria en memoria. Api expone `POST /api/asistente` y reordena el limitador. La Web añade `/asistente`. La autorización por objeto la siguen aplicando los casos de uso existentes, que **no cambian**.

```
Web /asistente ──POST /api/asistente {message, history}──> Api
  CORS → Authentication → Authorization (respaldo: autenticado) → RateLimiter("assistant", por sub, 5/min)
  AssistantEndpoints ──> AskAssistantUseCase (Application)
                           1 valida pregunta e historial ──────────────> 400
                           2 IAssistantEngine.IsEnabled = false ───────> 503 "deshabilitado"
                           3 IAssistantQuota.TryConsume(EmployeeId) ───> 429 + Retry-After (medianoche local)
                           4 IAssistantEngine.ReplyAsync ──> ChatAssistantEngine (Infrastructure)
                                 system prompt (hoy, rol, español, texto plano) + historial + pregunta
                                 FunctionInvokingChatClient (≤ N iteraciones, secuencial, sin detalle de errores)
                                   ├─ get_my_time_entries ──────────> GetTimeEntriesUseCase ─┐
                                   ├─ get_team_members (Manager) ───> GetTeamMembersUseCase  ├─> DailySummaryBuilder
                                   └─ get_team_member_time_entries ─> GetTeamMemberTimeEntriesUseCase (null → "no encontrado")
                                 proveedor: AnthropicClient.AsIChatClient | OllamaApiClient
                           null (fallo / tiempo agotado) ──────────────> 503 genérico
  <── 200 { reply } (texto plano)
```

Diagrama de secuencia (flujo con herramienta):

```
Web        Api(endpoint)   AskAssistantUseCase   ChatAssistantEngine   FICC            Proveedor     Herramienta/UseCase
 |--POST------>|                  |                      |                |                 |               |
 |             |--ExecuteAsync--->|--valida/cuota------->|                |                 |               |
 |             |                  |                      |--GetResponse-->|--mensajes+tools>|               |
 |             |                  |                      |                |<--FunctionCall--|               |
 |             |                  |                      |                |--InvokeAsync------------------->| (ownership)
 |             |                  |                      |                |<--resumen JSON------------------|
 |             |                  |                      |                |--resultado----->|               |
 |             |                  |                      |                |<--texto---------|               |
 |             |                  |<--reply / null-------|<--ChatResponse-|                 |               |
 |<--200/503---|<--Answered/...---|                      |  (log: metadatos, tokens, duración, nº herramientas)
```

## 2. Decisiones técnicas

| # | Decisión | Elección | Alternativas descartadas | Justificación |
|---|---|---|---|---|
| DD-1 | Limitador por minuto (a) | Mover `UseRateLimiter` **tras `UseAuthorization`**: orden nuevo SecurityHeaders → ExceptionHandler → StatusCodePages → CORS → Authentication → Authorization → RateLimiter → endpoints. Política `assistant`: ventana fija por claim `sub`, `Ai:RequestsPerMinute` (5), `QueueLimit = 0`. Un único `AddRateLimiting()` (renombra `AddLoginRateLimiting`) registra `login` y `assistant`; `OnRejected` elige el detalle según `EnableRateLimitingAttribute.PolicyName` del endpoint | Filtro de endpoint con `PartitionedRateLimiter` propio; mantener el orden y particionar por IP | Con el orden actual `HttpContext.User` aún está vacío y no se puede particionar por usuario. Tras autorización, un anónimo recibe 401 sin consumir permisos de nadie. CORS sigue antes, así que el 429 conserva `Access-Control-Allow-Origin` (motivo original de DD-21 archivada). La política `login` es por IP y anónima: el cambio no la altera (lo confirman `LoginRateLimitTests`). Cambia la spec `authentication` (orden del pipeline). |
| DD-2 | Tope diario (a) | Puerto `IAssistantQuota` (Application) e `InMemoryAssistantQuota` (Infrastructure, singleton): `ConcurrentDictionary<Guid, (DateOnly Day, int Count)>` por empleado, día natural de España (`SpainTime.DateOf(TimeProvider)`), incremento atómico; el exceso da 429 con `Retry-After` = segundos hasta la medianoche local. **Valor por defecto: 50 consultas por usuario y día** (`Ai:DailyRequestsPerUser`). Se consume **después** de validar y de comprobar que el asistente está habilitado | Segunda ventana fija de 24 h en el limitador (temporizador real: no se prueba con `FakeTimeProvider` y la ventana empieza en la primera petición, no a medianoche); persistir contadores en PostgreSQL (migración y escritura en un cambio de solo lectura) | Determinista y probable con reloj falso; un 400 o un 503 por deshabilitado no gastan cupo. **Limitación asumida y documentada en ADR 0017:** el contador vive en memoria de cada instancia; se reinicia con cada despliegue o reinicio y se multiplica por el número de réplicas (Railway ejecuta una). El tope real de gasto es el límite de presupuesto de la consola del proveedor (AGENTS §7). Estimación: con 6 llamadas al modelo por consulta como máximo y `MaxOutputTokens` 800, 50 consultas acotan el coste por usuario y día a un orden de céntimos con un modelo económico. |
| DD-3 | Rol en el contexto (b) | `ICurrentUser` gana `UserRole Role { get; }` (Domain ya es dependencia de Application). `HttpContextCurrentUser` lee el claim `role` con `Enum.TryParse` y lanza si falta (ya lo garantiza `RejectInvalidClaims`). `FixedCurrentUser(Guid id, UserRole role = UserRole.Employee)` en Application.Tests; único doble afectado | Pasar el rol desde el endpoint al motor | Un único puerto de identidad, siempre del token (AGENTS §6); Infrastructure no conoce `HttpContext`; el motor no recibe identidad por parámetro, así no hay camino por el que el modelo o el cliente la fijen. Impacto mínimo: un doble y el adaptador (+ su test). |
| DD-4 | `SpainTime` (c) | Se mueve de `Application/DemoData` a `Application/Time` (namespace `TimeTracker.Application.Time`) y gana `ToLocal(DateTimeOffset) → DateTime` y `StartOfDay(DateOnly) → DateTimeOffset`. Sus pruebas se mueven a `Application.Tests/Time` | `TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid")` (la imagen Alpine no trae tzdata); dejarlo en `DemoData` | Ya no es solo para la demo; el traslado solo añade `using` en 2 archivos de producción (`SeedDemoTimeEntriesUseCase`, `DemoScheduleGenerator`) y 3 de pruebas, y mueve `SpainTimeTests`. Europe/Madrid fija en v1 (decisión 2 de la propuesta). |
| DD-5 | Rango de fechas local (c) | `LocalDateRange.TryCreate(from, to, today, out range, out error)` en `Application/TimeTracking`: `to >= from`, máximo **31 días** inclusive, `from <= today` («No se pueden consultar periodos futuros.»); `ToWindow()` = `[StartOfDay(from), StartOfDay(to + 1))`, que siempre pasa `TimeWindow.TryCreate` | Recibir instantes UTC del modelo; reutilizar `TimeWindow` con 32 días | El modelo razona en fechas; el servidor convierte. La medianoche en España nunca es ambigua (los cambios son a las 02:00/03:00), así que los límites son exactos y los días de cambio duran 23 h o 25 h; 31 días locales son a lo sumo 31 d + 1 h < 32 d. |
| DD-6 | Resumen por día (c) | `DailySummaryBuilder.Build(TimeEntryListResult, LocalDateRange, now, truncated)` puro en `Application/TimeTracking`: recorta cada intervalo **cerrado** a cada día local del rango (`[StartOfDay(d), StartOfDay(d+1))`), minutos = `floor(segundos/60)` por tramo, totales = suma de tramos (coherentes por construcción, como DD-4 de fichajes-vista-mensual). Solo días con actividad. El intervalo **abierto** no suma: se expone en `Open` (tipo, fecha y hora local de inicio, minutos transcurridos hasta `now`) si está entre los devueltos; `Status` siempre sale del listado (DD-3 archivada). `Truncated = Items.Count >= MaxWindowEntries` | Que el modelo sume intervalos; incluir el abierto hasta «ahora» en los totales | Cálculo en código, probado y cubierto (≥ 80 %); el modelo recibe una salida compacta. Los totales pueden diferir en ≤ 1 min por intervalo partido respecto a `/api/time-entries` (recorte por día); documentado. Sin agregados de equipo en v1. |
| DD-7 | Herramientas y registro por rol (d) | `AssistantToolset` (Infrastructure, scoped) crea las `AIFunction` con `AIFunctionFactory.Create` y devuelve `get_my_time_entries` siempre y `get_team_members` / `get_team_member_time_entries` **solo si `ICurrentUser.Role == Manager`**. Descripciones en español. Serialización con `AIJsonUtilities.DefaultOptions` + `JsonStringEnumConverter` (enums como texto) | Registrar todas y confiar en el caso de uso; un `switch` en una sola herramienta | Defensa en profundidad: un empleado no puede ni nombrar las herramientas de equipo (si el modelo las inventa, `FunctionInvokingChatClient` responde «función no encontrada»). La autorización real sigue en los casos de uso. |
| DD-8 | Validación de argumentos (d) | Parámetros `string`: fechas `yyyy-MM-dd` con `DateOnly.TryParseExact(InvariantCulture)`; `memberId` con `Guid.TryParse`. Los errores **no** son excepciones: la herramienta devuelve `{ "error": "<motivo en español>" }`. En la herramienta de miembro se validan **primero las fechas** y después el miembro | Excepciones (cuentan como errores consecutivos y, sin detalle, el modelo no sabe corregir); tipos `DateOnly`/`Guid` en la firma (el fallo de enlace lo genera el SDK, con mensajes en inglés) | El modelo puede corregirse con un mensaje claro; el orden evita que un error de fechas revele si el miembro es del equipo (misma regla que el 400 de `/api/team`). |
| DD-9 | «No encontrado» uniforme (d) | `memberId` no GUID, inexistente, ajeno o el propio responsable → exactamente `{ "error": "No hay ningún miembro de tu equipo con ese identificador." }` (misma cadena, mismo tiempo de ruta: siempre pasa por `FindDirectReportAsync` salvo el GUID ilegible). `DisplayName` saneado en `get_team_members`: sin caracteres de control, espacios colapsados, máx. 60 caracteres | Mensajes distintos por caso | Equivalente al 404 idéntico (DD-15 archivada, BOLA/IDOR); el saneado reduce la inyección indirecta por nombres (OWASP LLM01). |
| DD-10 | Bucle de herramientas (e) | Pipeline `new ChatClientBuilder(inner).UseFunctionInvocation(loggerFactory: null, c => …).Build()` con `MaximumIterationsPerRequest = Ai:MaxToolIterations` (5), `MaximumConsecutiveErrorsPerRequest = 2`, `IncludeDetailedErrors = false`, `AllowConcurrentInvocation = false`, `TerminateOnUnknownCalls = false`. `ChatOptions { Tools, ToolMode = Auto, MaxOutputTokens = Ai:MaxOutputTokens (800), ModelId = Ai:Model }`. Respuesta vacía (p. ej. tope de iteraciones) → texto fijo «No he podido completar la consulta. Pruebe a reformularla.» con 200 | Valores por defecto del SDK; invocación concurrente | No fiarse de los valores por defecto (OWASP LLM10). Secuencial porque las herramientas comparten el `DbContext` del ámbito (no es seguro entre hilos). Sin `loggerFactory` el FICC no registra argumentos (privacidad). [V-3] |
| DD-11 | Tiempo, cancelación y fallos (e) | Tiempo máximo `Ai:TimeoutSeconds` (25) con `new CancellationTokenSource(timeout, TimeProvider)` enlazado al `RequestAborted`. Cliente que aborta → se propaga la cancelación (sin respuesta). Tiempo agotado o cualquier otra excepción del proveedor/herramientas → el motor devuelve `null` → **503** con detalle genérico. Reintentos del SDK de Anthropic limitados a 1 [V-2] | 504 para el tiempo agotado; dejar la excepción al `ExceptionHandler` (500) | Un solo código para «no disponible» (decisión 5 de la propuesta); 25 s queda por debajo del borde de Railway. Probable con `FakeTimeProvider`. |
| DD-12 | Registro solo de metadatos (e) | `LoggerMessage` (source generator) en Infrastructure: por herramienta `{Tool} {EmployeeId} {Outcome: ok/invalid/not-found/error} {ElapsedMs}`; por consulta `{EmployeeId} {Provider} {Model} {Outcome} {ElapsedMs} {InputTokens} {OutputTokens} {ToolCalls}`; en fallos, solo el **nombre del tipo** de la excepción. Nunca prompt, respuesta, historial, argumentos ni `ex.Message` | `UseLogging`/OpenTelemetry con datos sensibles | AGENTS §6 (auditar qué, quién, cuándo sin datos). Comprobado con un logger de captura. |
| DD-13 | Opciones y fábrica (f) | `AiOptions` (sección `Ai`) + `AiOptionsValidator : IValidateOptions<AiOptions>` + `ValidateOnStart()` en Infrastructure, como `ConnectionStringOptionsValidator`. `ChatClientFactory.Create(AiOptions)` devuelve `null` (deshabilitado), `new AnthropicClient { ApiKey = … }.AsIChatClient(model)` o `new OllamaApiClient(new Uri(endpoint), model)`. `AssistantChatClient` (singleton, desechable) guarda el pipeline ya montado o `null`; `ChatAssistantEngine.IsEnabled` lo consulta | Un `IChatClient` registrado en DI (no admite «nulo»); validar en el endpoint | Proveedor solo por configuración (AGENTS §7); la API arranca sin `Ai:Provider` y responde 503; las pruebas de integración sustituyen `AssistantChatClient` por uno montado sobre `FakeChatClient`, ejercitando el pipeline real. |
| DD-14 | Paquetes (f) | `Microsoft.Extensions.AI` **10.10.0** (fija `config.yaml`; 10.11.0 no aporta nada necesario), `Anthropic` 12.58.0 (SDK oficial), `OllamaSharp` 5.5.0, todos solo en `TimeTracker.Infrastructure.csproj`. `Microsoft.Extensions.AI.Abstractions` llega transitivo | 10.11.0; `tryAGI.Anthropic` | Versión acordada salvo motivo; si la restauración da NU1903/NU1605 con `-warnaserror`, se eleva la transitiva con un `PackageVersion` comentado, como `Microsoft.OpenApi` [V-1]. |
| DD-15 | Contrato y validación (g) | Petición `{ message: string, history?: [{ role: "user"\|"assistant", text: string }] }` → `AskAssistantCommand(string? Message, IReadOnlyList<AssistantTurnInput>? History)` (patrón de `LoginCommand`). Reglas en `AskAssistantUseCase`: mensaje recortado 1-500 caracteres; historial ≤ 10 elementos, cada texto 1-2000, total ≤ 8000; rol exactamente `user` o `assistant` (cualquier otro, incluidos `system` y `tool`, → 400 «El historial solo admite mensajes de los roles user y assistant.»). Respuesta 200 `{ reply: string }`. Cuerpo limitado a 32 KiB en el endpoint [V-5] | Validar en Api; aceptar `system` y descartarlo en silencio | La validación es lógica de aplicación y queda cubierta en Application.Tests. El historial es **no confiable**: un `assistant` falsificado solo influye en la conversación; los datos siguen protegidos por las herramientas. |
| DD-16 | Endpoint (g) | `AssistantEndpoints.MapAssistantEndpoints`: `POST /api/asistente`, tag `Assistant`, protegido por la política de respaldo, `.RequireRateLimiting("assistant")`, `.ProducesProblem(400/401/429/503)`. Resultados: `Answered` → 200; `Rejected` → 400; `QuotaExceeded` → 429 + `Retry-After`; `Unavailable(Disabled)` → 503 «El asistente no está disponible en este entorno.»; `Unavailable` → 503 «El asistente no está disponible en este momento. Inténtelo de nuevo más tarde.» | Excepciones de aplicación mapeadas por un manejador | `Results<…>` tipados como en `TeamEndpoints`; el resultado de `AskAssistantUseCase` es una jerarquía de `record` con pattern matching. OpenAPI regenerado. |
| DD-17 | Web (h) | `Pages/Assistant.razor` (`@page "/asistente"`, `[Authorize]`), enlace «Asistente» en `NavMenu` para cualquier autenticado. `AssistantConversation` (clase pura en `Web/Assistant`): turnos en memoria, límite de 500, últimos 10 turnos y 8000 caracteres al construir la petición, estado ocupado, retirada del turno pendiente si falla. Respuesta en `<p class="assistant-text">` (texto escapado por Blazor, `white-space: pre-wrap` en `app.css`), sin `MarkupString`. Indicador `role="status"` «Consultando…». Con 503 se muestra el `detail` como aviso persistente (deshabilitado o caído) y el enlace sigue visible | Ocultar el enlace; endpoint de estado previo | Valores fijados por el orquestador; sin endpoint adicional (YAGNI): el aviso aparece al primer 503 con el mensaje de la API. |
| DD-18 | `ApiRequest` (h) | Nueva sobrecarga `SendAsync<T>(send, IReadOnlySet<HttpStatusCode> detailStatuses, ct)`; la existente delega con `{ Conflict }`. `AssistantApiClient` pasa `{ BadRequest, TooManyRequests, ServiceUnavailable }` | Cambiar la regla global del 409 | Los clientes de fichajes y equipo no cambian de comportamiento (siguen viendo el mensaje genérico salvo en 409). |
| DD-19 | Pruebas (i) | Ver sección 6: nuevo `tests/TimeTracker.Infrastructure.Tests` (xUnit v2, sin Docker) que enlaza (`<Compile Include Link>`) los dobles `InMemoryTimeEntryRepository`, `InMemoryUserRepository` y `FixedCurrentUser` de Application.Tests; `FakeChatClient` propio, enlazado también desde Api.IntegrationTests | Duplicar los dobles; un proyecto común de utilidades de prueba | Sin duplicación y sin proyecto nuevo extra; los dobles son `internal` y el enlace los compila en cada ensamblado. |
| DD-20 | Prompt del sistema | `AssistantPrompt.Build(today, role)` en español: ámbito (fichajes del usuario y, si es responsable, de sus reportes directos), fecha de hoy con día de la semana y «hora de España peninsular», usar solo las herramientas, no inventar datos, días ausentes = sin fichajes, totales sin el intervalo abierto, responder **en español** y en **texto plano**. Sin identificadores, secretos ni reglas de seguridad | Herramienta «hoy»; poner reglas de acceso en el prompt | La seguridad la imponen las herramientas (AGENTS §6); «hoy» resuelve fechas relativas sin una llamada más. |

## 3. Interfaces

```csharp
// Application/Identity
public interface ICurrentUser { Guid EmployeeId { get; } UserRole Role { get; } }

// Application/TimeTracking
public sealed record DaySummary(DateOnly Date, TimeOnly FirstStart, TimeOnly LastEnd, int WorkedMinutes, int BreakMinutes, int MealBreakMinutes);
public sealed record OpenEntrySummary(TimeEntryType Type, DateOnly SinceDate, TimeOnly SinceTime, int ElapsedMinutes);
public sealed record DailySummaryResult(DateOnly From, DateOnly To, IReadOnlyList<DaySummary> Days,
    int WorkedMinutes, int BreakMinutes, int MealBreakMinutes, TimeTrackingStatus Status, OpenEntrySummary? Open, bool Truncated);

// Application/Assistant
public sealed record AssistantTurnInput(string? Role, string? Text);
public sealed record AskAssistantCommand(string? Message, IReadOnlyList<AssistantTurnInput>? History);
public enum ConversationRole { User, Assistant }
public sealed record ConversationTurn(ConversationRole Role, string Text);
public sealed record AssistantConversation(IReadOnlyList<ConversationTurn> History, string Message);
public interface IAssistantEngine { bool IsEnabled { get; } Task<string?> ReplyAsync(AssistantConversation conversation, CancellationToken ct); } // null = no disponible
public interface IAssistantQuota { QuotaDecision TryConsume(Guid employeeId); }
public sealed record QuotaDecision(bool Allowed, TimeSpan RetryAfter);
public abstract record AskAssistantResult { /* Answered(string Reply) | Rejected(string Error) | QuotaExceeded(TimeSpan RetryAfter) | Unavailable(bool Disabled) */ }
```

Claves `Ai:*` (variables `Ai__*`): `Provider` (vacío/`None`, `Anthropic`, `Ollama`; otro valor → fallo al arrancar), `Model` (obligatoria si está habilitado, ≤ 100), `ApiKey` (obligatoria con Anthropic), `Endpoint` (obligatoria con Ollama, URI absoluta http/https), `MaxOutputTokens` 800 (64-4096), `MaxToolIterations` 5 (1-10), `TimeoutSeconds` 25 (5-55), `RequestsPerMinute` 5 (1-60), `DailyRequestsPerUser` 50 (1-1000). Mensajes en español con el nombre de la clave, nunca el valor.

## 4. Cambios por PR (l)

| PR | Archivos | Líneas previstas |
|---|---|---|
| **1 Consultas y contexto** (Application) | Mover `Application/DemoData/SpainTime.cs` → `Application/Time/SpainTime.cs` (+`ToLocal`, `StartOfDay`) y `using` en `SeedDemoTimeEntriesUseCase`, `DemoScheduleGenerator` y pruebas; `Identity/ICurrentUser.cs`, `Api/Authentication/HttpContextCurrentUser.cs` (+ `HttpContextCurrentUserTests`); `TimeTracking/LocalDateRange.cs`, `DailySummary.cs`, `DailySummaryBuilder.cs`; `Assistant/` (comando, conversación, puertos, `AskAssistantUseCase`, resultados); pruebas en `Application.Tests/Time`, `TimeTracking`, `Assistant` y `Fakes/FixedCurrentUser.cs` | ~420 |
| **2 Herramientas y orquestación** (Infrastructure) | `Directory.Packages.props` (`Microsoft.Extensions.AI`); `Infrastructure.csproj`; `Infrastructure/Assistant/`: `AssistantToolset.cs`, `ToolResults.cs`, `AssistantPrompt.cs`, `AssistantChatPipeline.cs`, `AssistantChatClient.cs`, `ChatAssistantEngine.cs`, `InMemoryAssistantQuota.cs`, `AiOptions.cs`, `AiOptionsValidator.cs`, `AssistantLog.cs`; nuevo `tests/TimeTracker.Infrastructure.Tests` (csproj, `FakeChatClient`, pruebas de herramientas, pipeline, motor, cuota y validador); `TimeTracker.sln`; `RepositoryConventionsTests` (proyecto nuevo, paquete de IA solo en Infrastructure) | ~480 (punto de corte si se exige ≤ 400: 2a herramientas + proyecto de pruebas; 2b motor, opciones y cuota) |
| **3 Endpoint y proveedores** (Api y docs) | `Directory.Packages.props` (`Anthropic`, `OllamaSharp`); `Infrastructure/Assistant/ChatClientFactory.cs`, `AssistantServiceCollectionExtensions.cs`; `Api/Assistant/AssistantEndpoints.cs`, `AssistantRequest.cs`, `AssistantResponse.cs`; `Api/Security/RateLimitingServiceCollectionExtensions.cs` (política `assistant`, `OnRejected` por política); `Program.cs` (orden, registro, mapeo); `docs/api/openapi.json`; integración (`Assistant/AssistantEndpointTests.cs`, `AssistantRateLimitTests.cs`, `Startup/AiStartupTests.cs`, enlace de `FakeChatClient`); `docs/adr/0017-asistente-ia.md`; `DeliveryArtifactsTests` (17 ADR, `Ai__ApiKey` vacía); `.env.example`, `docker-compose.yml`, `README.md` | ~420 |
| **4 Web** | `Web/Assistant/AssistantApiClient.cs`, `AssistantDtos.cs`, `AssistantConversation.cs`; `Pages/Assistant.razor`; `Layout/NavMenu.razor`; `Http/ApiRequest.cs`; `Program.cs` (cliente con `BearerTokenHandler`); `wwwroot/css/app.css`; `Web.Tests` (`AssistantConversationTests`, `ApiRequestTests`) | ~300 |

Total ~1.620 líneas (dentro de la estimación de la propuesta). PR 2 supera el presupuesto de 400: se recomienda el corte 2a/2b si la estrategia de entrega no admite excepción.

## 5. ADR 0017 (j) — `docs/adr/0017-asistente-ia-con-herramientas-de-solo-lectura.md`

Cuatro secciones (las exige `Adr_Directory_…`): **Contexto** (consultas en lenguaje natural; regla de dependencias; RGPD). **Decisión**: alternativa A; `IChatClient` + `FunctionInvokingChatClient` con topes; tres herramientas de solo lectura y registro por rol; identidad solo del token; Europe/Madrid fija; Anthropic (nube) y Ollama (local) por configuración, deshabilitado sin `Ai:Provider`; limitador tras autorización y cupo diario en memoria; 503 genérico; registro de metadatos. **Consecuencias**: datos personales (nombres, horas) enviados al proveedor en la nube, minimizados y con datos de demostración inventados; cupo que se reinicia con cada despliegue y no se comparte entre réplicas; presupuesto en la consola del proveedor; modelos locales pequeños pueden fallar con herramientas o español; cambia el orden del pipeline respecto a DD-21 archivada. **Alternativas**: B (Application con abstracciones de IA), C (lógica en Api), D (volcar el mes en el prompt), streaming, conversaciones persistidas, cupo en PostgreSQL.

## 6. Estrategia de pruebas (i)

| Capa | Qué | Cómo |
|---|---|---|
| Application.Tests | `SpainTime` (movido, `ToLocal`); `LocalDateRange` (orden, 31 días, futuro, formato de ventana en marzo y octubre: 23 h/25 h); `DailySummaryBuilder` (medianoche, recorte en bordes, días de cambio de hora, abierto fuera de totales con `Open`, `Truncated`, vacío); `AskAssistantUseCase` (cada regla de validación, roles `system`/`tool`, deshabilitado sin consumir cupo, cupo agotado sin llamar al motor, `null` → no disponible, éxito) | Dobles en memoria, `FakeTimeProvider`; cuenta para el 80 % |
| Infrastructure.Tests (nuevo) | Seguridad por herramienta (AGENTS §5): empleado solo lo suyo; sin herramientas de equipo para `Employee`; responsable solo reportes directos; ajeno, inexistente, uno mismo y GUID ilegible → **misma cadena**; fechas validadas antes que el miembro; `DisplayName` saneado. Pipeline: tope de iteraciones con un `FakeChatClient` que siempre pide herramienta; errores consecutivos; sin detalle de la excepción de la herramienta en el siguiente mensaje al modelo; invocación secuencial. Motor: prompt con fecha y rol, historial mapeado, tiempo agotado y excepción → `null`, registro sin prompt ni respuesta. Cuota: por usuario, reinicio a medianoche local, `Retry-After`. `AiOptionsValidator`: cada regla, mensajes sin valores | `FakeChatClient` guionizado que registra mensajes y `ChatOptions`; `AIFunction.InvokeAsync` directo; logger de captura |
| Api.IntegrationTests (Docker) | 401 anónimo; 200 con `FakeChatClient` para empleado y responsable; 400 por regla; 429 al sexto envío en un minuto (detalle en español y cabecera CORS); 429 diario con `Ai__DailyRequestsPerUser=2`; anónimo no consume cupo del limitador; 503 deshabilitado (configuración por defecto) y por fallo del proveedor sin texto de la excepción; arranque falla con `Ai:*` inválida; `LoginRateLimitTests` siguen en verde con el nuevo orden; `AuthorizationCoverageTests` sin anónimos nuevos | `WebApplicationFactory` sustituyendo `AssistantChatClient` |
| ArchitectureTests | `DependencyRuleTests` sin cambios en el mapa (ya prohíbe IA en Application); `RepositoryConventionsTests`: `Infrastructure.Tests` en `ProjectPaths`, paquetes de IA declarados solo en Infrastructure; `DeliveryArtifactsTests`: 17 ADR, `Ai__ApiKey` vacía en `.env.example`; `OpenApiContractTests` con el contrato regenerado | Igual que hoy |
| Web.Tests | `AssistantConversation` (límites, recorte de historial, fallo); sobrecarga de `ApiRequest` (409 en otros clientes intacto; 429/503 con detalle) | xUnit sin navegador; página verificada a mano en Chrome (`verify-report.md`) |

Las pruebas con modelo real quedan fuera (propuesta).

## 7. Despliegue (k)

- **Railway (`api`)**: `Ai__Provider=Anthropic`, `Ai__Model=<modelo económico elegido en la consola>`, `Ai__ApiKey` como secreto (`railway variables set … --stdin`); límites opcionales. Fijar un presupuesto de gasto en la consola de Anthropic. `web` y la CSP no cambian.
- **`.env.example`**: bloque `Ai__*` con `Ai__Provider=` y `Ai__ApiKey=` activas y vacías (deshabilitado por defecto), y comentadas `# Ai__Model=`, `# Ai__Endpoint=http://host.docker.internal:11434/` y los límites con sus valores por defecto.
- **Compose**: `api` ya lee `.env` (`env_file`); se añade `extra_hosts: ["host.docker.internal:host-gateway"]` para Ollama en el anfitrión Linux. Sin servicio `ollama` (opcional y pesado).
- **README**: activación local con Ollama (modelo con soporte de herramientas, p. ej. `qwen2.5:7b`) y en Railway; aviso de datos al proveedor.

## 8. Migración y reversión

Sin esquema ni migración. Desactivar: quitar `Ai__Provider` (503, resto intacto). Revertir PR 4 → 1; al revertir PR 3 vuelve el orden de pipeline anterior.

## 9. Matriz de amenazas

N/A: sin shell, subprocesos, automatización de VCS/PR ni clasificación de ejecutables. Las amenazas de la ruta HTTP nueva (OWASP API/LLM) se tratan en DD-1, DD-2, DD-7 a DD-12 y DD-15.

## 10. Riesgos

Cupo en memoria (DD-2); modelos locales con herramientas; firmas de SDK sin verificar en esta fase (V-1 a V-5); PR 2 por encima de 400 líneas.

## 11. Validaciones para apply

| V | Comprobar | Si falla |
|---|---|---|
| V-1 | `dotnet restore` + `build -warnaserror` con los tres paquetes (NU1903/NU1605/NU1608) | Elevar transitiva con `PackageVersion` comentado; si hace falta 10.11.0, registrar el motivo |
| V-2 | `Anthropic` 12.58.0: nombre de la propiedad de clave (`ApiKey`/`APIKey`), `AsIChatClient(string model)`, opción de reintentos/tiempo | Ajustar la fábrica; el diseño no cambia |
| V-3 | `FunctionInvokingChatClient` 10.10.0: `MaximumIterationsPerRequest`, `MaximumConsecutiveErrorsPerRequest`, `IncludeDetailedErrors`, `AllowConcurrentInvocation`, `TerminateOnUnknownCalls`; qué devuelve al llegar al tope | Ajustar nombres; mantener el texto fijo ante respuesta vacía |
| V-4 | `OllamaApiClient(Uri, string)` implementa `IChatClient` con la versión de Abstractions resuelta | Usar el adaptador que exponga OllamaSharp 5.5.0 |
| V-5 | `RequestSizeLimitAttribute` como metadato en Minimal API (32 KiB) | Validar `Content-Length` en el endpoint |

## 12. Alineación con las specs en paralelo

Deben coincidir: límites de DD-15 (500/10/2000/8000), valores por defecto de `Ai:*`, textos 429/503, «no encontrado» de DD-9, 31 días de DD-5, el nuevo orden del pipeline (spec `authentication`), 17 ADR y `Infrastructure.Tests` (spec `solution-architecture`), aviso en la página sin ocultar el enlace (spec `assistant-web`). Si una spec fija otro valor, apply sigue la spec y actualiza este diseño.
