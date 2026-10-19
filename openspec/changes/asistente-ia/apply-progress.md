# Progreso de apply: asistente-ia

## PR 1 — Consultas y contexto (Application): completado (tareas 1.1-4.2)

Rama `feat/asistente-ia-pr1-resumen-diario`, con Strict TDD (RED por error de compilación o prueba fallida, luego GREEN).

| Tarea | Resultado |
|---|---|
| 1.1 | Copia previa en `..\TimeTracker.rollback\pr-1\` (11 archivos). |
| 1.2 | `ICurrentUser.Role`; `HttpContextCurrentUser` lee el claim `role` (lanza si no hay usuario, falta o no es `Employee`/`Manager`); `FixedCurrentUser(id, role = Employee)`. 8 pruebas nuevas. |
| 1.3 | `SpainTime` movido a `Application/Time` con `ToLocal` y `StartOfDay`; 7 pruebas nuevas (verano, invierno, 2026-03-29, 2026-10-25). |
| 2.1 | `LocalDateRange` (recorte del fin futuro antes de contar 31 días); 11 pruebas. `ToWindow()` reutiliza `TimeWindow.TryCreate`, sin duplicar reglas. |
| 2.2 | `DaySummary`, `OpenEntrySummary`, `DailySummaryResult`, `DailySummaryBuilder`; 13 pruebas. |
| 3.1-3.2 | Puertos, comandos, resultados y `AskAssistantUseCase`; 36 pruebas. |
| 4.1-4.2 | `DependencyRuleTests` y suite en verde; Application sin paquetes nuevos. |

## Puerta de unidad (reales)

- `RESTORE` y `BUILD -warnaserror`: 0 advertencias, 0 errores.
- `TEST`: Domain 67, Application 195 (eran 138 al inicio de la sesión: +57), Web 180, Architecture 74, Api.IntegrationTests 323 (Docker): todas en verde.
- `COV-A`: líneas 100,0 %, ramas 99,3 % (mínimo 80). `COV-D`: 100,0 % / 100,0 %.
- `FORMAT`: sin cambios. `VULN`: sin paquetes vulnerables.

## Desviaciones

1. `DailySummaryBuilder.Build(list, range, now)` no recibe `truncated`: calcula `Truncated = Items.Count >= MaxWindowEntries` por dentro (la spec y la tarea 2.2 lo exigen y así es verificable en Application). PR 2a debe llamar con tres argumentos.
2. `LastEnd` de un día cuyo último tramo llega a la medianoche local se expone como 23:59 (`TimeOnly` no admite las 24:00).
3. Mensajes de rechazo (texto fijo, en `AskAssistantUseCase`): mensaje 1-500, historial máx. 10, texto de turno 1-2000, suma 8000, y el de roles de la spec. Si el endpoint los necesita en otro idioma de contrato, ya están como constantes privadas; los límites son constantes públicas.
4. El caso de uso no usa `TimeProvider` (la tarea citaba `FakeTimeProvider`): no depende del tiempo; el reloj entra con la cuota (PR 2b).
5. **Tamaño**: el diff real es de 1.097 inserciones + 11 borrados (≈ 1.108 líneas, 23 archivos) frente a las ≈ 410 previstas; supera con mucho el umbral de 450. Ver informe del apply.

## Para PR 2a / 2b

- `SpainTime.DateOf(DateTimeOffset)` existe; la cuota de 2b necesitará un `DateOf` desde `TimeProvider` (o `GetUtcNow()`), no añadido aquí.
- `AskAssistantResult` es una jerarquía cerrada (`Answered`, `Rejected`, `QuotaExceeded`, `Unavailable(bool Disabled)`).
- `docs/api/openapi.json` se regenera al compilar con saltos de línea distintos (solo ruido); restaurar con `git checkout` si aparece modificado.

---

## PR 2a — Herramientas y proyecto de pruebas (Infrastructure): completado (tareas 1.1-3.1)

Rama `feat/asistente-ia-pr2a-herramientas` (desde `cf5f8cb`), con Strict TDD (RED por error de compilación o por pruebas fallidas, luego GREEN y REFACTOR). Ninguna prueba llama a un modelo: solo `FakeChatClient`. Docker en marcha para `Api.IntegrationTests`.

| Tarea | Resultado |
|---|---|
| 1.1 | Copia previa en `..\TimeTracker.rollback\pr-2a\` (`Directory.Packages.props`, `TimeTracker.Infrastructure.csproj`, `TimeTracker.sln`, `RepositoryConventionsTests.cs`). |
| 1.2 | V-1 (parte 1) correcto: `Microsoft.Extensions.AI` 10.10.0 + Abstractions 10.10.0, 0 advertencias con `-warnaserror`, sin vulnerables. Detalle en `design.md` sección 11. |
| 1.3 | V-3 resuelto contra el paquete restaurado y una prueba de caracterización desechable: nombres correctos; al tope de iteraciones hay 1 + `MaximumIterationsPerRequest` llamadas, el SDK retira las herramientas en la última y la respuesta puede quedar con `Text` vacío y una llamada pendiente; el error de herramienta se relanza al superar `MaximumConsecutiveErrorsPerRequest` (la excepción original). Detalle en `design.md` sección 11. |
| 1.4 | `Microsoft.Extensions.AI` en `Directory.Packages.props` (comentado) y referencia sin `Version` en Infrastructure; `InternalsVisibleTo` hacia `TimeTracker.Infrastructure.Tests`. |
| 1.5 | `tests/TimeTracker.Infrastructure.Tests` (en la solución, dobles enlazados de `Application.Tests`, `FakeChatClient`, `CapturingLogger`, `ThrowingTimeEntryRepository`); `RepositoryConventionsTests` con el proyecto en `ProjectPaths` y `AiPackages_AreDeclaredOnlyInInfrastructureWithCentralVersion` (mutación temporal: un `PackageReference` de IA en Web la hace fallar). |
| 2.1 | `AssistantJson`, `ToolResults`, `DisplayNameSanitizer`, `ToolOutcome` y `AssistantLog` (`LoggerMessage`, solo metadatos). |
| 2.2-2.4 | `AssistantToolset` (`get_my_time_entries`, `get_team_members`, `get_team_member_time_entries`; las de equipo solo para `Manager`) y refactor (`SummarizeAsync`/`RunAsync` compartidos). |
| 3.1 | Puerta de unidad en verde (abajo). |

### Puerta de unidad (reales)

- `RESTORE` y `BUILD -warnaserror`: 0 advertencias, 0 errores.
- `TEST`: Domain 67, Application 195, Web 180, **Infrastructure.Tests 54 (nuevo)**, Architecture 77 (eran 74: +3), Api.IntegrationTests 326 (Docker): todas en verde.
- `COV-A`: líneas 100,0 %, ramas 99,3 % (mínimo 80). `COV-D`: 100,0 % / 100,0 %. Infrastructure sin umbral.
- `FORMAT`: sin cambios. `VULN`: sin paquetes vulnerables.

### Desviaciones y decisiones

1. **Tamaño**: el diff real es de 1.156 líneas (285 `src`, 854 pruebas, 17 props y `.sln`; sin docs de openspec) frente a las ≈ 265 previstas. Supera el umbral de 450: ver propuesta de corte en el informe del apply. Las pruebas de seguridad por herramienta (tabla de «no visibles», registro de auditoría sin datos, registro por rol) son la parte grande y están exigidas por las specs.
2. `AssistantToolset` es `public` y recibe `GetTimeEntriesUseCase`, `GetTeamMembersUseCase`, `GetTeamMemberTimeEntriesUseCase`, `ICurrentUser`, `TimeProvider` e `ILogger<AssistantToolset>`; `CreateTools()` devuelve las `AIFunction` del rol (PR 2b lo llamará una vez por petición). El resto de tipos nuevos son `internal` con `InternalsVisibleTo`.
3. Cualquier excepción inesperada dentro de una herramienta (salvo la cancelación) se convierte en `{ "error": "No se ha podido completar la consulta en este momento." }` (sin texto ni tipo) y se registra con resultado `error`. Por eso el límite de errores consecutivos del SDK solo actuará en fallos de enlace de argumentos.
4. `AssistantJson.Options` copia `AIJsonUtilities.DefaultOptions` (que indenta) y fija `WriteIndented = false` para gastar menos tokens.
5. Mensaje de fechas inválidas: «Las fechas deben tener el formato yyyy-MM-dd, por ejemplo 2026-10-18.»; los demás errores de fechas son los de `LocalDateRange`.
6. Orden de tareas: la referencia al paquete se añadió antes de la verificación 1.2 (no se puede restaurar sin declararlo); el resultado es el mismo.

### Para PR 2b

- `AssistantChatPipeline`: con `MaximumIterationsPerRequest` = N hay N + 1 llamadas como máximo y la última va sin herramientas; respuesta vacía o con llamada pendiente → texto fijo. La excepción relanzada por el SDK conserva el mensaje original: registrar solo `GetType().Name`.
- `AIFunction.JsonSerializerOptions` ya lleva `AssistantJson.Options`; el motor solo debe pasar `Tools = toolset.CreateTools()` en `ChatOptions`.
- `FakeChatClient` guioniza turnos con `Func<IReadOnlyList<ChatMessage>, ChatResponse>` y guarda `Calls` (mensajes y `ChatOptions`); para «modelo que siempre pide herramienta» basta pasar N veces el mismo turno. PR 3 lo enlazará desde `Api.IntegrationTests`.
- `TimeProvider` con `FakeTimeProvider` ya se usa en el arnés (`ToolHarness`), reutilizable para el tiempo máximo del motor.
- `docs/api/openapi.json` sigue regenerándose con ruido de saltos de línea tras compilar: restaurar con `git checkout`.

---

## PR 2b — Motor, opciones y cuota (Infrastructure): completado (tareas 1.1-3.2)

Rama `feat/asistente-ia-pr2b-motor` (desde `origin/main` con PR 1 y PR 2a), con Strict TDD. Ninguna prueba llama a un modelo: solo `FakeChatClient` y `HangingChatClient` (proveedor que no responde, para tiempo y cancelación). Ninguna clave en disco: `clave-secreta-prueba` es un literal ficticio de las pruebas. La API no registra el asistente (PR 3).

### Evidencia del ciclo TDD

| Tarea | Archivo de pruebas | RED (mensaje real) | GREEN | Triangulación | REFACTOR |
|---|---|---|---|---|---|
| 1.1 | n/a | n/a | Copia en `..\TimeTracker.rollback\pr-2b\` (`src/.../Assistant/*` de 2a, 6 archivos) | n/a | n/a |
| 1.2 | `AiOptionsValidatorTests` | `CS0246: El nombre del tipo o del espacio de nombres 'AiOptions' no se encontró` | 40/40 | Sin proveedor (4 formas), proveedor desconocido, clave/endpoint/modelo, 20 casos de borde de rango, el valor nunca se imprime | Un único `CheckRange`; sin cambios de comportamiento |
| 1.3 | `InMemoryAssistantQuotaTests` | `CS0246: ... 'InMemoryAssistantQuota' no se encontró` | 8/8 | Tope 3, por usuario, medianoche local, 7200 s a las 22:00, día de 25 h (24,5 h), día español vs UTC, 200 tareas concurrentes → exactamente 10 | Bucle de comparación e intercambio (`TryAdd`/`TryUpdate`), sin bloqueos |
| 2.1 | `AssistantPromptTests` | `CS0103: El nombre 'AssistantPrompt' no existe en el contexto actual` | 9/9 | 3 fechas con su día de la semana, ambos roles, sin claves ni `SigningKey` | Fecha con `InvariantCulture` |
| 2.2 | `AssistantChatPipelineTests` | `CS0103: El nombre 'AssistantChatPipeline' no existe en el contexto actual` | 6/6 | Opciones enviadas, tope (4 llamadas con 3 iteraciones), 3 llamadas con 2 errores tolerados, error sin texto ni tipo, secuencial, función desconocida | Mutación temporal: `AllowConcurrentInvocation = true` e `IncludeDetailedErrors = true` hacen fallar 2 pruebas; restaurado |
| 2.3 | `ChatAssistantEngineTests` (16), `EngineLifetimeTests` (2) | `CS0246: El nombre del tipo o del espacio de nombres 'ChatAssistantEngine' no se encontró` | 18/18 | Un solo `system`, historial como `user`, herramientas por rol, texto fijo (vacío y tope), argumentos ausentes y mal tipados → `null`, tiempo (`FakeTimeProvider`), cancelación, registro solo de metadatos | Mutación temporal: quitar el filtro de cancelación y registrar `ex.Message` hacen fallar 3 pruebas; restaurado |
| 3.1 | suite completa | n/a | `DependencyRuleTests` y `RepositoryConventionsTests` en verde | n/a | Sin duplicación con 2a (el motor reutiliza `ToolHarness`, `AssistantJson`, `AssistantLog`) |

### Puerta de unidad (reales)

- `RESTORE` y `BUILD -warnaserror`: 0 advertencias, 0 errores.
- `TEST` (Docker en marcha): Domain 67, Application 195, **Infrastructure.Tests 142 (eran 54: +88)**, Web 180, Architecture 77, Api.IntegrationTests 326: todas en verde.
- `COV-A`: líneas 100,0 %, ramas 99,3 % (mínimo 80). `COV-D`: 100,0 % / 100,0 %.
- `FORMAT`: sin cambios (exit 0). `VULN`: sin paquetes vulnerables.
- `docs/api/openapi.json` quedó modificado solo en finales de línea tras compilar: restaurado con `git checkout`.

### Work Unit Evidence

| Evidencia | Valor |
|---|---|
| Prueba enfocada | `dotnet test tests/TimeTracker.Infrastructure.Tests` → 142/142 |
| Harness de ejecución | Pipeline real `FunctionInvokingChatClient` sobre `FakeChatClient`; `EngineLifetimeTests` con un contenedor real (`ValidateScopes` y `ValidateOnBuild`). Sin API ni proveedor reales (PR 3) |
| Frontera de reversión | Borrar `INF/Assistant/{AiOptions,AiOptionsValidator,AssistantChatClient,AssistantChatPipeline,AssistantPrompt,ChatAssistantEngine,InMemoryAssistantQuota}.cs`, revertir las dos entradas añadidas a `AssistantLog.cs` y borrar las pruebas nuevas de `IFT/Assistant` y `IFT/Fakes/HangingChatClient.cs` |

### Ciclo de vida de `AssistantToolset` (comprobación pedida)

Compatible, con una condición para PR 3. `AssistantToolset` es **scoped** porque depende de los casos de uso y de `ICurrentUser`, que en la API son scoped (`HttpContextCurrentUser`, `AddScoped<...UseCase>`). `ChatAssistantEngine` lo recibe por constructor, así que **debe registrarse scoped**; `AssistantChatClient` y `InMemoryAssistantQuota` son singleton. `EngineLifetimeTests` lo fija con un contenedor real: motor scoped resuelve y responde; motor singleton falla la validación del contenedor (captura un servicio scoped). La tarea 2.2 de PR 3 debe registrar `IAssistantEngine` como scoped (el diseño no lo precisaba).

### Desviaciones y decisiones

1. **Tamaño**: el diff real es de 1.199 líneas (345 `src`, 854 pruebas) frente a las ≈ 225 previstas; supera el umbral de 450 como PR 1 y PR 2a. Lo grande son las pruebas exigidas por las specs (validador con 20 bordes, motor, pipeline).
2. `AiOptions.Provider` se interpreta sin distinguir mayúsculas (`anthropic` vale); expone `ResolvedProvider` (`null` si no se reconoce), `IsEnabled` y el enum público `AiProvider`. El validador devuelve todos los fallos a la vez, salvo proveedor desconocido, que se informa solo.
3. `InMemoryAssistantQuota` recibe `IOptions<AiOptions>` y `TimeProvider`; el tope se lee en cada llamada. El contador no guarda estado de rechazos (no crece al rechazar).
4. `AssistantChatClient(IChatClient? inner, AiOptions options)` monta el pipeline en el constructor; `Pipeline` es `internal`. La fábrica de PR 3 solo debe pasar el cliente del proveedor (o `null`).
5. El registro del motor son dos mensajes `LoggerMessage` nuevos en `AssistantLog`: `QueryCompleted` (resultados `ok` y `empty`) y `QueryFailed` (`error` y `timeout`, solo el nombre del tipo de la excepción). La cancelación del cliente no registra nada.
6. El texto fijo de respuesta vacía se aplica a texto en blanco (y al tope de iteraciones, que devuelve `Text` vacío).
7. Argumentos de herramienta ausentes o mal tipados: el SDK relanza la excepción tras superar los errores consecutivos tolerados y el motor devuelve `null` (503); solo se registra el tipo.

### Para PR 3

- Registrar `IAssistantEngine` scoped; `AssistantChatClient` singleton (desechable, lo libera el contenedor); `IAssistantQuota` singleton.
- `docs/api/openapi.json` sigue regenerándose con ruido de saltos de línea: restaurar con `git checkout`.

---

## PR 3 — Endpoint y proveedores (Api y documentación): completado (tareas 1.1-5.1; la 4.5 se difiere al archivo)

Rama `feat/asistente-ia-pr3-endpoint` (desde `origin/main` con PR 1, 2a y 2b), con Strict TDD. Ninguna prueba llama a un modelo real: `FakeChatClient` y `HangingChatClient` (enlazados desde `Infrastructure.Tests` a `Api.IntegrationTests`). Ninguna clave en disco: `clave-secreta-prueba` es un literal ficticio de las pruebas. La imagen Docker no se construyó (el `Dockerfile` no menciona `Ai__*` ni claves), así que la comprobación con `docker history` queda para la verificación manual.

### Evidencia del ciclo TDD

| Tarea | Archivo de pruebas | RED (mensaje real) | GREEN | REFACTOR / mutación |
|---|---|---|---|---|
| Seguimiento 2: `Ai:Provider` numérico | `AiOptionsValidatorTests` | `Assert.Null() Failure: Value of type 'Nullable<AiProvider>' has a value` para `"0"`, `"1"`, `"2"`, `" 1 "` | 53/53 | `ResolvedProvider` compara nombres, sin `Enum.TryParse` |
| Seguimiento 1: corte por errores de herramienta | `ChatAssistantEngineTests`, `AssistantChatPipelineTests` | `CS0246: The type or namespace name 'AssistantToolFailureException' could not be found` | 159/159 | El origen (invocación de la herramienta) decide, no el tipo de excepción: pruebas con `ArgumentException`/`JsonException` lanzadas por el proveedor siguen dando `null` |
| 2.1 `ChatClientFactory` | `ChatClientFactoryTests` | `CS0103: The name 'ChatClientFactory' does not exist in the current context` | 8/8 | Mutación temporal: un literal de modelo en la fábrica hace fallar `Src_DoesNotHardcodeModelNames` |
| 2.2 `AddAssistant` | `AssistantRegistrationTests` | `CS1061: 'OptionsBuilder<AiOptions>' does not contain a definition for 'BindConfiguration'` (falta el paquete `Options.ConfigurationExtensions`) | 175/175 (Infrastructure.Tests) | `ValidateScopes` y `ValidateOnBuild` activos |
| 3.2/3.3 endpoint | `AssistantEndpointTests` | 39 de 41 fallan con `Assert.Equal() Failure: Values differ` (404 sin ruta) | 41/41 | — |
| 1.5/3.5 V-5 (413) | `AssistantBodyLimitTests` | Bajo `TestServer`: `Expected: RequestEntityTooLarge / Actual: BadRequest` (el atributo se ignora) | 3/3 con `UseKestrel()` | Mutación: quitar `RequestSizeLimitAttribute` y `ValidateOnStart` hace fallar 18 pruebas |
| 3.1/3.4 limitador | `AssistantRateLimitTests` (14) | Escritas tras el código; mutación temporal con `UseRateLimiter` antes de la autenticación: fallan `AnonymousRequests_DoNotConsumeAnyUsersQuota` y `Users_AreIndependentEvenFromTheSameAddress` (`LoginRateLimitTests` siguen en verde con ambos órdenes) | 14/14 | Un único `AddRateLimiting()` |
| 3.6 arranque | `AiStartupTests` (33) | Verde a la primera (el validador ya existía desde 2b): la mutación de quitar `ValidateOnStart` hace fallar los casos de clave y proveedor inválidos | 33/33 | — |
| 4.1-4.4 contrato y documentos | `OpenApiContractTests`, `DeliveryArtifactsTests` (+8) | Con el `README`, `.env.example`, compose y sin ADR 0017 originales fallan 6 pruebas (`Adr_Directory_HasSeventeenAdrsWithFourSections`, `Adr0017_…`, `Readme_ExplainsTheAssistant`, `EnvExample_DocumentsTheNineAiKeys…`, `EnvExample_Explains…`, `Compose_LetsTheApiReachAnOllamaOnTheHost`) | 85/85 | — |

### Puerta de unidad (reales)

- `RESTORE` y `BUILD -warnaserror`: 0 advertencias, 0 errores (sin NU1903/NU1605/NU1608).
- `TEST` (Docker en marcha, sin la prueba inestable de arranque en esta ejecución): Domain 67, Application 195, Web 180, **Infrastructure.Tests 175 (+33)**, **Architecture 85 (+8)**, **Api.IntegrationTests 419 (+93)**: todas en verde.
- `COV-A`: líneas 100,0 %, ramas 99,3 % (mínimo 80). `COV-D`: 100,0 % / 100,0 %.
- `FORMAT`: exit 0. `VULN`: sin paquetes vulnerables.
- `docs/api/openapi.json`: **cambio real** (+134 líneas: `/api/asistente` y tres esquemas), conservado y versionado.

### Work Unit Evidence

| Evidencia | Valor |
|---|---|
| Prueba enfocada | `dotnet test tests/TimeTracker.Api.IntegrationTests --filter "FullyQualifiedName~Assistant|FullyQualifiedName~AiStartupTests"`: endpoint 42, 413 3, limitador 14, arranque 33; más `T-IF` 175 y `T-AR` 85 |
| Harness de ejecución | API real (`WebApplicationFactory`) con el pipeline de seguridad completo y el `AssistantChatClient` real montado sobre un `IChatClient` falso; el 413 sobre Kestrel real (`UseKestrel`). Sin proveedor real |
| Frontera de reversión | Revertir los 4 commits de la rama y restaurar `..\TimeTracker.rollback\pr-3\`; borrar `API/Assistant/`, `INF/Assistant/{ChatClientFactory,AssistantServiceCollectionExtensions,AssistantToolFailureException}.cs`, `IT/Assistant/`, `IT/Startup/AiStartupTests.cs`, `IFT/Assistant/{ChatClientFactory,AssistantRegistration}Tests.cs` y el ADR 0017. Al revertir vuelve el limitador antes de la autenticación |

### Resultados de las verificaciones (detalle en `design.md`, sección 11)

- **V-1 (parte 2):** correcto; hizo falta añadir `Microsoft.Extensions.Options.ConfigurationExtensions` 10.0.12 en Infrastructure para `BindConfiguration`.
- **V-2:** `AnthropicClient.ApiKey`, `AsIChatClient(model)` en el espacio de nombres `Microsoft.Extensions.AI` (extensión de `IAnthropicClient`), `MaxRetries` (int) y `Timeout` (TimeSpan).
- **V-4:** `OllamaApiClient(Uri, string)` implementa `IChatClient` directamente.
- **V-5:** el atributo basta con Kestrel (`413`, también sin `Content-Length`); `TestServer` lo ignora, por eso las pruebas del 413 usan `UseKestrel()`.

### Desviaciones y decisiones

1. **Tamaño**: el diff real es de 1.801 inserciones y 24 borrados (src 235/8, pruebas 1.334/10, `openapi.json` generado 134, documentos y props ≈ 98) frente a las ≈ 430 previstas; supera el umbral de 450 como las unidades anteriores. Las pruebas exigidas por las specs (endpoint, límites, arranque) son la parte grande.
2. **Seguimiento 1 (corte por errores de herramienta):** `AssistantChatPipeline` instala `FunctionInvoker` y envuelve el fallo en `AssistantToolFailureException`; el motor devuelve el texto fijo con `200` y registra `tool-error` con el nombre del tipo interno. Sustituye la nota 7 de 2b; `design.md` DD-11 actualizado.
3. **Seguimiento 2:** `Ai:Provider` solo admite nombres (`Anthropic`, `Ollama`, `None`, vacío; sin distinguir mayúsculas).
4. **Paquete extra** `Microsoft.Extensions.Options.ConfigurationExtensions` 10.0.12 (no de IA) en Infrastructure.
5. `AddAssistant()` también registra `AskAssistantUseCase` (scoped). El motor y las herramientas son scoped; cliente y cuota, singleton; arrancar sin `Ai:Provider` deja la API operativa y el endpoint en `503`.
6. El `429` del asistente tiene detalles distintos por minuto y por día; `Retry-After` nunca baja de 1. Con el asistente deshabilitado `Ai:RequestsPerMinute` fuera de rango no rompe el limitador (5 de respaldo).
7. La tarea 4.5 (`openspec/config.yaml`) queda para el archivo, como indica el propio punto.
8. Prueba de arranque inestable conocida (`ObjectDisposedException` en `Api.IntegrationTests/Startup`): no apareció en esta ejecución.
9. Los permisos del entorno impiden leer `.env.example` con las herramientas de lectura: se editó a partir de `git show HEAD:.env.example` y se verificó con `DeliveryArtifactsTests`.

### Para PR 4

- El contrato está en `docs/api/openapi.json`: `POST /api/asistente` con `{ message, history: [{ role, text }] }` → `200 { reply }`; `400`, `429` (con `Retry-After`) y `503` traen `application/problem+json` con `detail` en español (los dos `503` comparten `title` «Asistente no disponible»). El `401` no lleva `detail`. El `413` no está en el contrato.
- `detail` de `503`: «El asistente no está disponible en este entorno.» (deshabilitado) y «El asistente no está disponible en este momento. Inténtelo de nuevo más tarde.» (fallo).

---

## PR 4 — Web: completado salvo 3.2 (tareas 1.1-3.1; la 3.2 queda pendiente: verificación manual en navegador)

Rama `feat/asistente-ia-pr4-web`, con Strict TDD.

### Evidencia del ciclo TDD

| Tarea | RED (mensaje real) | GREEN |
|---|---|---|
| Seguimiento PR 3 (`required` del contrato) | `OpenApiContractTests`: `Assert.Equal() Failure: Collections differ. Expected: ["message"]; Actual: ["message", "history"]` | `AssistantRequest(string? Message, IReadOnlyList<AssistantTurnRequest>? History = null)`: el exportador deja de marcar `history` como obligatorio; `required` = `message` en la petición y `role`, `text` en el turno. El contrato regenerado cambió de verdad (1 línea añadida, 2 quitadas) y se conserva |
| 1.2 | `CS1501: Ninguna sobrecarga para el método 'SendAsync' toma 3 argumentos` | Sobrecarga con `IReadOnlySet<HttpStatusCode>`; la existente delega con `{ Conflict }`. 12 pruebas nuevas en `WT/Http/ApiRequestTests.cs` (el 409 de fichajes y equipo sigue igual) |
| 1.3 | `CS0246: 'AssistantConversation' no se encontró` | `AssistantConversation` (clase pura) + 26 pruebas |
| 1.4 | `CS0246: 'AssistantApiClient' no se encontró` (comprobado retirando el archivo) | `AssistantApiClient`, `AssistantDtos`, registro en `Program.cs` con `CreateAuthorizedClient` (BearerTokenHandler, `ApiBaseUrl`). 4 pruebas con un `HttpMessageHandler` falso |
| 2.1-2.3 | `Web_HasTheAssistantPageForAnyRoleWithPlainTextAndNoBrowserStorage` falla (falta `Assistant.razor`) | Página, enlace de menú y CSS. `Web_DeclaresNoAiPackagesNorUsesTheirNamespaces` pasó a la primera (el veto de paquetes de IA ya existía para `src/`); mutación temporal con `using Anthropic;` en un archivo de la Web: falla con «La Web usa espacios de nombres de IA» |

### Puerta de unidad (reales)

`RESTORE` ok · `BUILD -warnaserror` 0 advertencias, 0 errores · `TEST` Domain 67, Web 223, Application 195, Infrastructure 175, Architecture 87, Api.IntegrationTests 419 (Docker en marcha, 7 min 22 s), todo en verde y sin que apareciera la prueba de arranque inestable · `COV-A` líneas 100,0 % / ramas 99,3 % · `COV-D` 100 % / 100 % · `FORMAT --verify-no-changes` ok (tras corregir el orden de `using` en `AssistantConversation.cs`) · `VULN` sin paquetes vulnerables.

### Desviaciones y decisiones

1. **Retry-After**: `ApiRequestException` no lleva cabeceras. `AssistantApiClient.AskAsync` no lanza: devuelve `AssistantCallResult(Reply, Error, RetryAfterSeconds)`; la página pasa el error y los segundos a `AssistantConversation.Fail`. Solo se admite la forma en segundos.
2. **Hallazgo (CORS)**: la API no declara `WithExposedHeaders("Retry-After")`; con la Web en otro origen el navegador no deja leer `Retry-After`, así que en la práctica el 429 se mostrará sin tiempo de espera. La lógica ya lo cubre; exponer la cabecera es un cambio de la API fuera de este PR (candidato a seguimiento).
3. **Turno largo en el historial**: la API acepta turnos de hasta 2000 caracteres y la respuesta del asistente podría superarlos; al construir la petición el texto de un turno se trunca a 2000 (la pantalla conserva el texto completo). No lo fija la spec; evita un 400 en cadena.
4. El aviso del 503 (`Notice`) permanece hasta una respuesta 200; el 503 no genera `ErrorMessage`.
5. El `401` produce el mensaje genérico en la conversación; el cierre de sesión y la navegación a `/login?returnUrl=` los hace el `BearerTokenHandler`, como en el resto de clientes.
6. Pendiente: tarea 3.2 (verificación manual en navegador, `verify-report.md`).
7. Rollback: copia previa en `..\TimeTracker.rollback\pr-4\` (ApiRequest, Program, NavMenu, app.css, AssistantRequest, openapi.json y las dos pruebas de arquitectura modificadas).

## Mejoras tras verificación 5.2

Rama `feat/asistente-ia-fechas-y-equipo` (desde `main`), con Strict TDD. Origen: `verify-report.md`, donde ni `qwen2.5:3b` ni `7b` resolvían fechas relativas, el flujo nombre → id → fichajes ni la conversión de minutos.

### Evidencia del ciclo TDD

| Cambio | RED (mensaje real) | GREEN |
|---|---|---|
| C: textos precalculados | `CS0103: El nombre 'DurationText' no existe en el contexto actual`; `CS1061: "DaySummary" no contiene una definición para "WorkedText"`; en la herramienta, `SevenDayPeriod_ReturnsTheSummaryByDay` falla por falta de `workedText` | `DurationText.Format` («X h Y min») y propiedades `WorkedText`, `BreakText`, `MealBreakText` en `DaySummary` y `DailySummaryResult`, serializadas como `workedText`, etc. El contrato anterior (`workedMinutes`...) no cambia |
| A: tabla de fechas | `Assert.Contains() Failure: Sub-string not found` (hoy/ayer/semana pasada...) y `Assert.Equal() Failure: Expected: 4 / Actual: 0` en el barrido de 365 días | `AssistantPrompt.Build` con referencias (lunes→hoy, lunes→domingo, 1→hoy, mes pasado) e instrucciones; el barrido comprueba fin ≤ hoy y ≤ 31 días |
| B: descripciones | `ToolDescriptions_KeepTheDateLimitAndTheMemberListReference`: `Assert.Contains() Failure: Sub-string not found` | `memberId`: identificador (GUID) obtenido SOLO de `get_team_members`, nunca el nombre; ambas herramientas de equipo indican llamar antes a `get_team_members` |
| REFACTOR | Un primer GREEN rompió `Assert.EndsWith` de la descripción de la herramienta de equipo (la instrucción iba al final) | La instrucción pasa al principio de la descripción y se conserva el sufijo «entre dos fechas (máximo 31 días).» |

### Puerta de unidad (reales)

`RESTORE` ok · `FORMAT --verify-no-changes` exit 0 · `BUILD -c Release -warnaserror` 0 advertencias, 0 errores · `TEST` Domain 67, Application 201, Web 223, Infrastructure 182, Architecture 87, Api.IntegrationTests 422 (Docker en marcha), todo en verde · `COV-D` 100,0 % / 100,0 % · `COV-A` líneas 100,0 % / ramas 99,3 % · `VULN` sin paquetes vulnerables. `docs/api/openapi.json` solo cambió en finales de línea y se restauró con `git checkout`.

### Decisiones y desviaciones

1. (D) Sin cambios de política: el nombre en `memberId` sigue dando la respuesta uniforme de no encontrado; la identidad nunca es parámetro; solo lectura.
2. Los textos son siempre «X h Y min» (también «0 h 45 min» y «1 h 0 min») para que el formato sea predecible.
3. Las fechas de referencia nunca superan 31 días ni terminan después de hoy por construcción; el aviso de recorte a hoy se indica igualmente al modelo.
4. 5.2 queda marcada como ejecutada; la repetición con el modelo tras estos cambios queda pendiente (M.6): no hay verificación con modelo real de las mejoras.
