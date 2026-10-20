# Exploración: asistente-config-responsable

Resumen de los hallazgos de código verificados por el orquestador y de las decisiones finales del usuario (2026-10-18). El catálogo de proveedores se verificó contra documentación oficial el 2026-10-18.

## Estado actual (verificado en el código)

- `AddAssistant()` enlaza `AiOptions` (Infrastructure/Assistant). `AssistantChatClient` es un singleton construido una sola vez con un pipeline inmutable.
- `ChatClientFactory` decide por proveedor: Anthropic -> `AnthropicClient{ApiKey, MaxRetries=1, Timeout}.AsIChatClient(model)`; Ollama -> `OllamaApiClient(Uri, model)`. `None` o vacío deshabilita el asistente (503); un valor desconocido hace fallar el arranque (ADR 0017).
- `ChatAssistantEngine` lee `IOptions<AiOptions>` en cada petición (`TimeoutSeconds`, `Model`, `MaxOutputTokens`).
- Unas 79 apariciones en 17 archivos de test construyen `AssistantChatClient` / `AiOptions` directamente: la superficie pública debe seguir siendo compatible a nivel de código fuente.
- Persistencia: puertos en Application e implementaciones en Infrastructure/Persistence, EF con snake_case, check constraints, concurrencia con `xmin`, 3 migraciones (la última `20261014212440_AddTimeEntryType`); `DatabaseMigrationHostedService` las aplica al arrancar.
- Guardas: `DependencyRuleTests` (paquetes de IA solo en Infrastructure), `RepositoryConventionsTests` (Web: sin almacenamiento, sin `MarkupString`, sin espacios de nombres de IA), `OpenApiContractTests`, `Src_DoesNotHardcodeModelNames` (sin nombres de modelo en `src/`).
- El limitador de tasa se ejecuta después de Authorization; política `assistant` 5/min por `sub`; cuota diaria `InMemoryAssistantQuota` (por instancia). Railway ejecuta una sola instancia (ADR 0017). ADR hasta el 0017.
- `TeamEndpoints.ManagerPolicy` existe (puede requerir moverse a un lugar compartido). El JWT (30 min, sin refresh) lleva el rol validado solo en el login.
- `ApiFactory` usa el host inalcanzable `db.invalid` en tests sin BD: cualquier poller de BD en segundo plano debe ser silencioso o reemplazable en tests.
- No existen specs principales en `openspec/specs/`; los cambios anteriores se archivaron en otro lugar.
- Paquetes fijados en `Directory.Packages.props`: `Microsoft.Extensions.AI` 10.10.0, `Anthropic` 12.58.0, `OllamaSharp` 5.5.0.

## Catálogo de proveedores (verificado contra documentación oficial, 2026-10-18)

| Proveedor | Adaptador | URL base (fija, en servidor) | Autenticación | Listado de modelos | Esfuerzo / razonamiento | Estado | Documentación consultada |
|---|---|---|---|---|---|---|---|
| Anthropic | SDK Anthropic existente | Por defecto del SDK | API key | `/v1/models` (no verificado) | `thinking: {type: "adaptive"}` + `output_config.effort` (`low`, `medium`, `high`, `xhigh`, `max`; los niveles disponibles dependen del modelo). `budget_tokens` está obsoleto en 4.6 y es rechazado (400) desde 4.7. Mapeo desde `ChatOptions.Reasoning` en el SDK 12.58.0: no verificado | Verificado (esfuerzo); mapeo del SDK sin verificar | https://platform.claude.com/docs/en/build-with-claude/effort y https://platform.claude.com/docs/en/build-with-claude/extended-thinking |
| OpenAI | Microsoft.Extensions.AI.OpenAI | `https://api.openai.com/v1` (valor por defecto del SDK, no re-verificado en la doc) | Bearer key | `/v1/models` (no verificado) | Chat Completions: `reasoning_effort`; Responses: `reasoning.effort`. Valores `none`, `minimal`, `low`, `medium`, `high`, `xhigh`, `max`, según el modelo; algunos modelos rechazan `none` (400). Con algunos modelos GPT-6, Chat Completions no admite function calling (hay que usar Responses) | Verificado (parámetro); URL y listado sin verificar | https://developers.openai.com/api/docs/guides/reasoning |
| Google Gemini | Compatible con OpenAI | `https://generativelanguage.googleapis.com/v1beta/openai/` | Bearer con la API key de Gemini | `GET .../openai/models` | `reasoning_effort`: `minimal`, `low`, `medium`, `high` (`none` solo en 2.5); se mapea a `thinking_level` / `thinking_budget` según el modelo. No combinar con `thinking_level` / `thinking_budget`. No se puede desactivar en 2.5 Pro ni en 3 | Verificado | https://ai.google.dev/gemini-api/docs/openai |
| GitHub Models | (compatible con OpenAI) | `https://models.github.ai/inference` | PAT | n/d | n/d | **Retirado: inviable.** El servicio (playground, catálogo, API de inferencia y BYOK) se cerró para todos los clientes el 2026-07-30 | https://docs.github.com/en/rest/models/inference y https://github.blog/changelog/2026-07-30-github-models-is-now-retired/ |
| OpenCode Zen | Compatible con OpenAI (solo parcial) | `https://opencode.ai/inference/` (distinta de la `opencode.ai/zen/v1` supuesta antes) | Bearer con la API key de la consola de OpenCode | `https://opencode.ai/inference/v1/models` | n/d | **Solo parcialmente compatible: descartado en v1.** Cada modelo usa una API distinta: Responses (GPT, Grok), Anthropic Messages (Claude, algunos Qwen), Gemini nativo, Chat Completions (`/openai/v1/chat/completions`: Qwen3.8 Max, DeepSeek, MiniMax, GLM, Kimi, Mistral Large 4 y modelos gratuitos), y una API propia (Jev) | Verificado | https://opencode.ai/docs/zen/ |
| Ollama | OllamaSharp existente | Solo entorno (`Ai__Endpoint`) | Ninguna | `/api/tags` (no verificado) | Parámetro `think`: booleano o nivel en texto (p. ej. `gpt-oss`: low/medium/high; los niveles válidos salen de `thinking.values` en `/api/show`); sin valor, se usa el comportamiento por defecto del modelo. La doc no menciona `reasoning_effort`. Mapeo desde OllamaSharp 5.5.0: no verificado | Verificado (`think`); mapeo sin verificar | https://docs.ollama.com/capabilities/thinking |

Implicación: el esfuerzo debe ser opcional y condicionado a la capacidad del proveedor/modelo (valores y soporte varían por modelo); el listado de modelos queda fuera de alcance (modelo en texto libre).

## Paquete .NET y cliente OpenAI (verificado)

- `Microsoft.Extensions.AI.OpenAI` 10.10.0 (publicado 2026-09-09) depende de `Microsoft.Extensions.AI.Abstractions` >= 10.10.0 y del SDK `OpenAI` >= 2.13.0. Es la versión alineada con el `Microsoft.Extensions.AI` 10.10.0 fijado. Existen 10.10.1 y 10.11.0 (esta última exige `OpenAI` >= 2.14.0), pero no coinciden con la versión fijada. Fuente: https://www.nuget.org/packages/Microsoft.Extensions.AI.OpenAI/10.10.0 y https://www.nuget.org/packages/Microsoft.Extensions.AI.OpenAI.
- Endpoint personalizado: el SDK `OpenAI` admite `new OpenAIClientOptions { Endpoint = new Uri("...") }` junto con `ApiKeyCredential`; luego `.GetChatClient(model).AsIChatClient()`. Fuente: https://github.com/openai/openai-dotnet (README) y https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/build-chat-app. Nota: el README del SDK es la fuente primaria; la página de referencia de `OpenAIClientOptions.Endpoint` en Microsoft Learn no estaba disponible (404).
- Esfuerzo vía abstracciones: `ChatOptions.Reasoning` (`ReasoningOptions`, con `Effort` y `Output`) y enum `ReasoningEffort` { `None`, `Low`, `Medium`, `High`, `ExtraHigh` }. Los proveedores lo mapean con "mejor esfuerzo" y pueden ignorarlo. Fuente: https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.chatoptions.reasoning y https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.reasoningeffort. Pendiente de verificar en diseño: que el adaptador de 10.10.0 traduzca `Reasoning.Effort` a `reasoning_effort` en Chat Completions (el README del SDK solo muestra el esfuerzo en la API Responses); `minimal` y `max` no existen en el enum.

## Veredicto de viabilidad por proveedor

| Proveedor | Veredicto v1 | Motivo |
|---|---|---|
| Anthropic | Incluir | Adaptador existente; esfuerzo documentado (`output_config.effort`). |
| OpenAI | Incluir | Mismo adaptador OpenAI; `reasoning_effort` documentado. |
| Google Gemini | Incluir | Endpoint compatible con OpenAI y `reasoning_effort` documentados. |
| Ollama | Incluir (endpoint solo por entorno) | Adaptador existente; `think` documentado. |
| GitHub Models | Descartar | Servicio retirado el 2026-07-30. |
| OpenCode Zen | Descartar en v1 | Sin una única superficie compatible; la mayoría de modelos necesita Responses o Anthropic Messages. Reconsiderar más adelante limitado a sus modelos de Chat Completions. |

Lista final v1: Anthropic, OpenAI, Gemini, Ollama.

## Enfoques considerados

1. **Solo entorno (estado actual)** - descartado: el product owner debe redesplegar para cambiar proveedor o clave; el Manager no puede aportar su propia clave.
2. **URL escrita por el Manager** - descartado: SSRF y exfiltración de la clave.
3. **Configuración guardada en PostgreSQL + catálogo fijo en servidor + un adaptador compatible con OpenAI + instantánea con cambio en caliente** - recomendado.
4. **ASP.NET Data Protection para la clave** - alternativa viable; requiere persistir el key ring. AES-256-GCM con clave maestra versionada por entorno es más simple y explícito (a decidir en diseño).

## Recomendación

Enfoque 3, documentado en el ADR 0018 (modifica AGENTS §1/§7 y el ADR 0017). Riesgos principales: datos personales a un tercero con la clave del Manager, clave en reposo, un guardado erróneo que deshabilite el asistente para toda la empresa, rol obsoleto en el JWT y consistencia eventual entre instancias.

## Ronda de preguntas de la propuesta

Modo automático; el usuario delegó las decisiones. Se registran como valores por defecto aceptados:

1. Probar la conexión no es obligatorio antes de guardar (la acción de prueba es opcional).
2. Todos los Managers pueden ver y editar la configuración (sin propietario único).
3. OpenCode Zen se descarta por ser solo parcialmente compatible (confirmado por la verificación).
4. No se notifica a los usuarios del chat cuando cambia la configuración.
