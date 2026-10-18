# Exploración: asistente-ia

## Punto de partida (verificado en el código)

- **Capas** (`src/`): Domain, Application, Infrastructure, Api (composition root) y Web (Blazor WASM independiente). Tests: Domain.Tests, Application.Tests, Api.IntegrationTests (Testcontainers, requiere Docker), ArchitectureTests y Web.Tests.
- **Regla de dependencias ejecutable** (`tests/TimeTracker.ArchitectureTests/DependencyRuleTests.cs`): Application solo puede referenciar Domain y ensamblados `System*` o `netstandard`. Infrastructure puede referenciar Application y Domain. Api puede referenciar Application, Infrastructure y Domain.
- **Plataforma**: `global.json` fija SDK 10.0.302 (`rollForward: latestFeature`); todos los proyectos son `net10.0`; `Directory.Packages.props` usa gestión central de paquetes; `Directory.Build.props` activa `TreatWarningsAsErrors`.
- **Identidad**: `ICurrentUser` (Application) solo expone `EmployeeId`. `HttpContextCurrentUser` lee el claim `sub` del JWT. El rol viaja en el claim `role` (`Employee` o `Manager`).
- **Política de respaldo**: exige usuario autenticado, así que cualquier endpoint nuevo nace protegido. `AuthorizationCoverageTests` fija que los anónimos son exactamente tres: health, openapi y login.
- **Equipo**: conjunto de reportes directos (`ManagerId`). `/api/team` exige la política `Manager` en el grupo (403 a un empleado). El ownership real lo aplican los casos de uso:
  - `GetTeamMembersUseCase`: lista acotada a 100.
  - `GetTeamMemberTimeEntriesUseCase`: usa `IUserRepository.FindDirectReportAsync`; ajeno, inexistente o el propio responsable devuelven `null`, que el endpoint traduce a un 404 idéntico (DD-15).
- **Fichajes** (casos de uso candidatos a herramientas):
  - `GetTimeEntriesUseCase.ExecuteAsync(TimeWindow?, ct)`: fichajes del usuario actual; sin ventana, últimos 300; con ventana, hasta 1000.
  - `TimeEntryListResult`: `Items` (Id, Type Work/Break/MealBreak, ClockInAt, ClockOutAt, DurationMinutes), `TotalWorkedMinutes`, `TotalBreakMinutes`, `TotalMealBreakMinutes` y `Status` (Idle, Working, OnBreak, OnMealBreak). Los totales suman solo intervalos cerrados.
  - `TimeWindow.TryCreate(from, to, now, ...)`: ventana semiabierta en UTC; `to > from`, máximo 32 días, `from` no futuro (mensajes en español).
- **Zona horaria**: las ventanas van en UTC y la Web convierte con JS interop. En servidor, `SpainTime` (Application.DemoData) implementa a mano la regla de la UE porque la imagen Alpine no trae tzdata; no asumir `TimeZoneInfo` con IANA en el servidor.
- **Rate limiting existente**: solo la política `login` (ventana fija por IP, 10/min) con 429 problem+json en español. `UseRateLimiter` está antes de `UseAuthentication`.
- **Web**: clientes tipados (`TimeEntriesApiClient`, `TeamApiClient`) y `ApiRequest.SendAsync` (mensaje de negocio solo para el 409). Páginas: Home (con `ClockPlayer`), `/fichajes`, `/equipo`, `/login`. `NavMenu` usa `AuthorizeView Roles="Manager"`. No hay bUnit ni E2E; Web.Tests cubre lógica pura.
- **Despliegue**: Railway con tres servicios (Postgres, api, web nginx). Configuración solo por variables de entorno (ADR 0013). `Jwt__SigningKey` se valida al arrancar.

## Hallazgos que condicionan el diseño

1. **Dónde vive la IA.** Las `AIFunction` y el bucle de `IChatClient` no pueden estar en Application sin relajar la regla; AGENTS §3 ya sitúa «el adaptador de IA» en Infrastructure. Los datos y la autorización salen de los casos de uso existentes, sin cambios.
2. **La autorización por objeto ya está hecha** en los casos de uso. La herramienta solo llama. El rol Manager solo lo exige el grupo HTTP `/api/team`: un empleado que llamase a `GetTeamMembersUseCase` obtendría una lista vacía, no un 403. Defensa en profundidad: registrar las herramientas de equipo solo si el principal es Manager (hace falta el rol en el contexto del asistente).
3. **Salida de herramienta compacta.** Un mes puede dar cientos de intervalos; la herramienta devuelve un resumen por día en hora local más totales y estado, calculado en código (testable) y no por el modelo.
4. **Intervalo abierto.** Los totales excluyen el intervalo abierto; la herramienta expone `Status` y `openSince` para que el modelo no presente un total incompleto como definitivo.
5. **Identidad nunca como parámetro.** Los parámetros son solo fechas locales `yyyy-MM-dd` y, en las de equipo, `memberId` (GUID), que el caso de uso autoriza. Ajeno e inexistente dan la misma respuesta genérica.
6. **Fechas relativas.** Inyectar «hoy» (hora local) en el system prompt; el servidor convierte fechas locales a UTC.

## Alternativas estudiadas

| # | Enfoque | Pros | Contras | Esfuerzo |
|---|---|---|---|---|
| A (recomendada) | Consultas de resumen en Application; `AIFunction` + orquestación (`FunctionInvokingChatClient`) en Infrastructure; endpoint en Api | Respeta Clean Architecture y los tests de arquitectura; autorización reutilizada; proveedor por configuración | Proyecto nuevo `TimeTracker.Infrastructure.Tests` (sin Docker); DI algo más extensa | Medio |
| B | Application referencia `Microsoft.Extensions.AI.Abstractions` (ADR que relaja la regla) | Todo testable en Application.Tests | Contradice AGENTS y el test de arquitectura; acopla el núcleo a un SDK | Medio-bajo, con deuda |
| C | Lógica del asistente en Api | Menos proyectos | Lógica en el composition root; solo testeable con Docker | Bajo, mala deuda |
| D | Sin function calling: volcar el mes en el prompt | Funciona con modelos pequeños | Más datos expuestos y más tokens; contradice la decisión de tools | Bajo (descartada) |

## Recomendación: A

### Contrato del endpoint (propuesta)

- `POST /api/asistente`, autenticado por la política de respaldo, sin streaming en v1 y sin estado en servidor.
- Petición: `{ "message": string (1-500), "history": [ { "role": "user"|"assistant", "text": string } ] (máx. 10 turnos y límite total de caracteres) }`.
- Respuesta 200: `{ "reply": string }` en texto plano.
- Errores: 400 (entrada no válida, en español), 429 con `Retry-After`, 503 si el asistente está deshabilitado o el proveedor falla (mensaje genérico, sin detalle del proveedor), 504 opcional por tiempo agotado.
- El historial lo envía el cliente y se trata como **no confiable**: solo roles user/assistant (nunca `system` ni `tool`), con límites estrictos.

### Herramientas (solo lectura)

1. `get_my_time_entries(fromDate, toDate)`: resumen del usuario actual por día (local), totales por tipo, estado y `openSince`. Envuelve `GetTimeEntriesUseCase`; máximo 32 días, sin periodos futuros.
2. `get_team_members()`: id y nombre de los reportes (solo Manager). Envuelve `GetTeamMembersUseCase`.
3. `get_team_member_time_entries(memberId, fromDate, toDate)`: como la 1 para un miembro del equipo (solo Manager). `null` se traduce al mismo mensaje «no encontrado» para ajeno, inexistente o uno mismo.
4. No hace falta herramienta «hoy»: va en el system prompt.

Los errores de validación vuelven al modelo como texto breve en español, sin excepciones con trazas.

### Seguridad y riesgos de datos (OWASP LLM)

- **Inyección.** Los fichajes solo contienen marcas de tiempo y tipos; la superficie indirecta es mínima. `DisplayName` es texto libre controlado por otros usuarios y llega al modelo vía `get_team_members`: sanearlo (longitud y caracteres).
- **Separación instrucciones/datos.** El usuario nunca puede inyectar rol `system`. El system prompt no contiene secretos ni reglas de seguridad: la seguridad la imponen las herramientas.
- **Salida no confiable.** La Web renderiza la respuesta como texto plano (sin `MarkupString`).
- **Exposición de datos.** El proveedor cloud recibe nombres y horas de fichajes (tratamiento de datos personales, RGPD): documentarlo en el ADR; la demo usa datos inventados. Enviar solo lo imprescindible.
- **Bucle de herramientas acotado.** Fijar explícitamente `MaximumIterationsPerRequest` (propuesta: 5), `MaximumConsecutiveErrorsPerRequest` e `IncludeDetailedErrors = false`; no fiarse de los valores por defecto.
- **Coste.** `MaxOutputTokens`, entrada e historial acotados, timeout por petición y tope diario por usuario (AGENTS §7).
- **Rate limiting.** Política `assistant` particionada por usuario (claim `sub`) más concurrencia 1 por usuario. Problema: `UseRateLimiter` va antes de `UseAuthentication`. Opciones: (a) moverlo tras autenticación/autorización (cambia DD-21, revisar 429 con CORS), (b) endpoint filter con `PartitionedRateLimiter` propio. Decidir en el diseño.
- **Registro (privacidad).** Solo metadatos: herramienta, `EmployeeId`, instante, resultado, tokens y duración. No registrar prompt, respuesta ni argumentos con datos.
- **Fallos del proveedor.** Capturar excepciones del SDK y devolver 503 genérico.

### Proveedores y paquetes (verificado en NuGet y documentación oficial, 2026-10-18)

| Paquete | Versión estable | Uso | Notas |
|---|---|---|---|
| `Microsoft.Extensions.AI` | 10.10.0 (existe 10.11.0) | `UseFunctionInvocation`, `ChatClientBuilder`, `AIFunctionFactory` | Depende de `Abstractions` 10.10.0 y `Microsoft.Extensions.*` 10.0.12. `config.yaml` dice 10.10.0 |
| `Microsoft.Extensions.AI.Abstractions` | 10.10.0 (transitivo) | `IChatClient`, `ChatMessage`, `AIFunction` | Referencia directa solo en proyectos de tests |
| `Anthropic` | 12.58.0 | Cloud: `new AnthropicClient{ApiKey=...}.AsIChatClient(model)` | SDK **oficial** desde la v10 (no usar `tryAGI.Anthropic`). Pasar la clave explícitamente desde `Ai__ApiKey` |
| `OllamaSharp` | 5.5.0 | Local: `new OllamaApiClient(new Uri(endpoint), model)` ya es `IChatClient` | Depende de `Microsoft.Extensions.AI`; targets incluyen net10.0 |

- Pendiente al restaurar: que `-warnaserror` no falle por NU1903 en dependencias transitivas.
- El modelo cloud concreto es configuración (`Ai:Model`); no hardcodear.

### Configuración (`Ai:*`)

- Claves: `Ai:Provider` (ausente/`None` = deshabilitado, `Anthropic`, `Ollama`), `Ai:Model`, `Ai:Endpoint` (obligatorio en Ollama), `Ai:ApiKey` (`Ai__ApiKey`, obligatoria con Anthropic), `Ai:MaxOutputTokens`, `Ai:MaxToolIterations`, `Ai:TimeoutSeconds`, `Ai:DailyRequestsPerUser`.
- Validación al arrancar con `IValidateOptions<AiOptions>` y `ValidateOnStart()`, mismo patrón que `JwtOptionsValidator` (mensajes en español, sin valores). Sin `Ai:Provider` la API arranca y el endpoint responde 503.
- Un factory único en Infrastructure crea el `IChatClient` según el proveedor.

### UI Web

- Recomendada: página propia `/asistente` con enlace en `NavMenu` y tarjeta en Home, para cualquier usuario autenticado. Sin JS interop; historial en memoria del componente; lógica de conversación en una clase pura testeable en Web.Tests.
- Componentes: `AssistantApiClient`, DTOs, `Pages/Assistant.razor`, texto plano, indicador de carga, mensajes en español para 429/503 (ampliar `ApiRequest` con cuidado: afecta a los otros clientes).
- Alternativa: panel de chat en Inicio (más visible, acopla con `ClockPlayer`).

### Railway y despliegue

- Sin cambio de esquema ni migración (solo lectura, sin estado).
- Variables nuevas en `api`: `Ai__Provider=Anthropic`, `Ai__Model`, `Ai__ApiKey` (secreto, `railway variables set ... --stdin`), opcional `Ai__Endpoint`.
- Desarrollo: `Ai__Provider=Ollama`, `Ai__Endpoint=http://localhost:11434/` y un modelo pequeño con soporte de herramientas (en Compose, `host.docker.internal` o servicio `ollama` en un perfil).
- Documentar en `.env.example`, README y un ADR nuevo (0017). La CSP de nginx no cambia. Regenerar `docs/api/openapi.json` y `OpenApiContractTests`.
- Tiempos del borde de Railway para peticiones largas: no verificado; mantener el timeout por debajo de ~30-60 s.

### Pruebas (TDD estricto)

- Nuevo proyecto `tests/TimeTracker.Infrastructure.Tests` (xUnit, sin Docker) con `FakeChatClient`.
- Seguridad por herramienta (AGENTS §5): empleado solo ve lo suyo; herramientas de equipo no registradas para empleado; responsable solo ve reportes directos; ajeno, inexistente y propio responsable dan la misma respuesta.
- Más: tope de iteraciones, historial con rol `system`/`tool` rechazado, límites de longitud, fallos del proveedor sin filtrar detalle, el log no contiene el prompt.
- Integración (Docker): endpoint con fake `IChatClient`, 401, 429, 503 deshabilitado, contrato OpenAPI y `AuthorizationCoverageTests`.
- Pruebas con modelo real: separadas, opcionales y desactivadas por defecto.

## Estimación de tamaño

Total aproximado: **1.300-1.700 líneas**. División propuesta en PRs:

1. **PR 1: consultas de resumen y seguridad por herramienta** (~350-400): resumen por día en Application, rol en el contexto, proyecto `Infrastructure.Tests`, tests de ownership.
2. **PR 2: orquestación del asistente** (~400): `AiOptions` y validador, `AIFunction`s, bucle con límites, system prompt, `FakeChatClient`; sin endpoint.
3. **PR 3: endpoint y proveedores** (~400): `POST /api/asistente`, factory Anthropic/Ollama, rate limiting por usuario, logging sin datos, OpenAPI, integración, README, `.env.example`, ADR 0017.
4. **PR 4: UI Web `/asistente`** (~300): cliente tipado, página, enlace, clase pura de conversación con tests.

## Riesgos

1. Regla de dependencias: Application tocando `Microsoft.Extensions.AI` rompe `DependencyRuleTests`.
2. Orden del middleware: el rate limiter por usuario exige reordenar o usar filtro propio.
3. `ICurrentUser` no expone el rol.
4. Modelos pequeños de Ollama fallan con function calling.
5. NU1903 con `TreatWarningsAsErrors` al restaurar paquetes nuevos.
6. Tiempos del borde de Railway no verificados.

## Preguntas abiertas (para propose/design)

1. ¿Opción A (adaptador en Infrastructure con `Infrastructure.Tests` nuevo) o B (ADR que relaja la regla)?
2. Zona horaria: ¿Europe/Madrid fija con `SpainTime` en v1 (hoy en `Application.DemoData`, habría que moverlo o reutilizarlo) o el cliente envía su desfase?
3. ¿Mover `UseRateLimiter` tras la autenticación (revisar DD-21) o filtro de endpoint propio?
4. ¿Qué modelo cloud y qué tope diario? ¿Qué modelo Ollama en desarrollo (con soporte de herramientas)?
5. Con `Ai:Provider` ausente: ¿arranca deshabilitado (recomendado) o falla rápido?
6. Rol en `ICurrentUser` (propiedad nueva) frente a pasarlo desde el endpoint.
7. ¿`Microsoft.Extensions.AI` 10.10.0 o 10.11.0?
8. Idioma de respuesta: el system prompt pide español; los modelos locales pequeños pueden fallar.
