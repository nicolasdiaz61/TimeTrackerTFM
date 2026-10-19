# Tareas: asistente-config-responsable

Fecha: 2026-10-18 · Fase: sdd-tasks · Modo: openspec · Base: `proposal.md`, `design.md` (DD-1 a DD-19, V-1 a V-8, sección 10), las 4 specs (`assistant-settings`, `assistant-credential-protection`, y los deltas `assistant-configuration` y `startup-configuration`), `openspec/config.yaml`, `AGENTS.md` y el formato de `asistente-ia/tasks.md`.

La previsión de carga de revisión está al final («Review Workload Forecast»).

## Puntos que cierran estas tareas

1. **Cortes**: los 10 PR de la sección 10 del diseño (1, 2, 3a, 3b, 4, 5, 6a, 6b, 7, 8), apilados sobre main (`stacked-to-main`): cada PR apunta al anterior (o a main si el previo ya se fusionó) y **el usuario fusiona**. Cada PR queda en verde por sí solo.
2. **Tarea 0 (requisito previo)**: archivar `asistente-ia` antes de empezar. Las specs `assistant-configuration` y `startup-configuration` de este cambio son deltas `MODIFIED` y necesitan `openspec/specs/` creado por ese archivo.
3. **Trazabilidad**: las specs no llevan identificadores de escenario; se cita **capacidad + requisito/escenario**. Abreviaturas: AS = assistant-settings, CP = assistant-credential-protection, AC = assistant-configuration (delta), SC = startup-configuration (delta).
4. Si una spec fija un valor distinto del diseño, apply sigue la spec y actualiza `design.md`.
5. Pregunta abierta del diseño (§13): quién genera y custodia la clave maestra en Railway. No bloquea el código; sí el despliegue (ver PR 8).

## Convenciones de ejecución

- **Strict TDD**: cada tarea de código = RED (prueba que falla; se anota el mensaje real y **qué prueba** es) → GREEN (código mínimo) → REFACTOR (suite en verde). Si una prueba pasa a la primera, se marca «verde a la primera» con justificación y mutación temporal.
- Las pruebas **nunca llaman a un proveedor real**: solo `FakeChatClient`, `HttpMessageHandler` de grabación y dobles. Valores reconocibles inventados: `clave-prueba-123456`, `https://api.ejemplo.test/v1`. Ninguna clave real en disco, `.env` ni comandos.
- Identificadores de código en inglés; documentación y textos de usuario en español.
- Entorno limpio antes de probar: `Get-ChildItem Env: | Where-Object Name -Match '^(Cors__|ConnectionStrings__|Database__|Jwt__|Seed__|Ai__|ASPNETCORE_|DOTNET_ENVIRONMENT)'` sin resultados. Docker en marcha para integración (PostgreSQL 18, Testcontainers).
- **Copia previa** de los archivos a modificar en `..\TimeTracker.rollback\pr-N\` antes de cada PR.
- Rutas: `APP` = `src/TimeTracker.Application` · `INF` = `src/TimeTracker.Infrastructure` · `API` = `src/TimeTracker.Api` · `WEB` = `src/TimeTracker.Web` · `AT` = `tests/TimeTracker.Application.Tests` · `IFT` = `tests/TimeTracker.Infrastructure.Tests` · `IT` = `tests/TimeTracker.Api.IntegrationTests` · `AR` = `tests/TimeTracker.ArchitectureTests` · `WT` = `tests/TimeTracker.Web.Tests`.

| Alias | Comando |
|---|---|
| `RESTORE` / `BUILD` | `dotnet restore TimeTracker.sln` / `dotnet build TimeTracker.sln -c Release --no-restore -warnaserror` |
| `TEST` | `dotnet test TimeTracker.sln -c Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults` |
| `COV-A` / `COV-D` | `pwsh scripts/check-coverage.ps1 -ResultsDirectory TestResults -Package TimeTracker.Application -MinimumPercent 80` / `... TimeTracker.Domain -MinimumPercent 100` |
| `FORMAT` | `dotnet format TimeTracker.sln --verify-no-changes --no-restore` |
| `VULN` | `$env:DOTNET_CLI_UI_LANGUAGE='en'; dotnet list TimeTracker.sln package --vulnerable --include-transitive` (sin `has the following vulnerable packages`) |
| `T-x` | `dotnet test tests/<proyecto> --filter "FullyQualifiedName~Clase"` |

**Puerta de cada PR**: `RESTORE` → `BUILD` → `TEST` → `COV-A` → `COV-D` → `FORMAT` → `VULN`, todo en verde, y PARADA para revisión con contexto limpio. `DocumentGeneration` exporta el contrato al compilar `API`: el host debe arrancar con el entorno limpio (las claves nuevas de IA no tienen validación obligatoria, no hace falta valor ficticio).

---

## Tarea 0 — Requisito previo: archivar `asistente-ia`

- [ ] 0.1 **Requiere aprobación explícita del usuario antes de ejecutarse.** Ejecutar `sdd-archive` de `asistente-ia`; comprobar que existen `openspec/specs/assistant-configuration/` y `openspec/specs/startup-configuration/`. Cubre: orden de archivo de la propuesta y de AC/SC.
- [ ] 0.2 Si no fuera posible archivar, **no empezar PR 1**: decidir con el usuario fusionar los deltas MODIFIED a mano en el informe de archivo (regla `archive` de `config.yaml`: avisar de deltas que cambian escenarios vigentes, p. ej. `Gemini` → `GitHubModels`).

---

## PR 1 — ADR 0018 y documentación (≈ 150 líneas)

**Objetivo:** decisión registrada y claves documentadas; sin código de producción. **Rollback:** restaurar `AGENTS.md`, `.env.example`, `README.md` y borrar el ADR. **Comando:** `T-AR` (`DeliveryArtifactsTests`).

- [x] 1.1 **Copia previa** de `AGENTS.md`, `.env.example`, `README.md`, `docker-compose.yml`, `AR/DeliveryArtifactsTests.cs`.
- [x] 1.2 RED `DeliveryArtifactsTests`: 18 ADR; `0018-configuracion-del-asistente-por-el-responsable.md` con las cuatro secciones (Contexto, Decisión, Consecuencias, Alternativas) y mención de que modifica AGENTS §1/§7 y matiza el ADR 0017. Falla: «esperado 18 ADR, hay 17». Cubre DD-19.
- [x] 1.3 RED `DeliveryArtifactsTests`: `.env.example` contiene `Ai__Effort`, `Ai__AllowedProviders`, `Ai__MasterKeys__1`, `Ai__ActiveMasterKeyVersion`, `Ai__SettingsPollSeconds` con `#` inicial; ninguna clave maestra con valor; comentario «32 bytes aleatorios en Base64»; valores admitidos `Anthropic`, `OpenAI`, `Gemini`, `Ollama`. Cubre SC «Claves nuevas de IA documentadas», «Nota del asistente deshabilitado», «Claves de IA documentadas» (DD-18).
- [x] 1.4 GREEN: crear `docs/adr/0018-configuracion-del-asistente-por-el-responsable.md` (contexto, decisión, consecuencias, alternativas descartadas de DD-19; GitHub Models y OpenCode Zen fuera; datos personales a un tercero; JWT con rol obsoleto aceptado).
- [x] 1.5 GREEN: `AGENTS.md` §1 («proveedor y modelo por configuración de entorno o guardada por un Manager») y §7 (cambio de proveedor por `Ai:*` o configuración guardada, ADR 0018). Sin editar el ADR 0017.
- [x] 1.6 GREEN: `.env.example` con las claves nuevas comentadas y `Ai__Provider` documentando `OpenAI`/`Gemini`; `README.md` (configuración por el Manager, guía de rotación de clave maestra, aviso de datos personales); `docker-compose.yml` solo si consume `Ai__*` (DD-19, DD-7).
- [x] 1.7 REFACTOR y puerta de PR.

---

## PR 2 — Application: modelo, reglas, resolver, casos de uso (≈ 380 líneas)

**Objetivo:** toda la lógica de configuración pura detrás de puertos, sin paquetes de IA. **Rollback:** borrar `APP/Assistant/Configuration/` y sus pruebas. **Comando:** `T-A` + `T-AR` (`DependencyRuleTests`). Cobertura de `APP` ≥ 80 %.

- [ ] 2.1 **Copia previa** de `AR/DependencyRuleTests.cs` (no hay archivos de producción que modificar).
- [ ] 2.2 RED `AT/Assistant/Configuration/AssistantSettingsRulesTests`: extremos `timeoutSeconds` 4/5/55/56 y `maxOutputTokens` 63/64/4096/4097; modelo vacío, 101, `<script>`, `a1.b_c-d:e/f@g` válido, `ñ` inválido; esfuerzo `extreme`, `high` con proveedor sin soporte y `low|medium|high` (sin distinguir mayúsculas); proveedor fuera de catálogo (`GitHubModels`) y no permitido; `None` siempre válido; token obligatorio; mensajes en español que nombran el campo sin valores. Cubre AS «Guardado con validación» (Extremos, Modelo inválido, Modelo con caracteres permitidos, Esfuerzo, Esfuerzo por proveedor), «Catálogo…» (Proveedor no permitido, fuera del catálogo, None) · DD-2.
- [ ] 2.3 GREEN `AssistantProvider`, `AssistantEffort`, `AssistantProviderTraits`, `AssistantSettings`, `AssistantSettingsRules` y constantes de límites (fuente única que el validador de Infrastructure reutilizará en 3a).
- [ ] 2.4 RED `AllowedProvidersTests`: con lista (mayúsculas, espacios, duplicados), sin lista = solo proveedor del entorno, entorno `None` = vacío, `Ollama` sin endpoint excluido, nombre desconocido (incl. `None`, `GitHubModels`) → error que nombra `Ai:AllowedProviders` sin imprimir el valor. Cubre AS «Catálogo…» (Sin lista, Nombre desconocido, None siempre seleccionable, Elementos de la lista) · DD-4. GREEN.
- [ ] 2.5 RED `AssistantSettingsResolverTests`: fila > entorno por valor (`db-m` sobre `env-m`), `None` guardado deshabilita, clave de entorno solo con el proveedor del entorno, cambio de proveedor descarta la clave guardada, endpoint de Ollama solo de entorno, sin fila = entorno exacto. Cubre AS «Deshabilitar, prioridad y restablecer» (Guardado gana, Deshabilitar), «Consulta…» (Solo entorno, Valores guardados), AC «La configuración guardada tiene prioridad» · DD-5. GREEN.
- [ ] 2.6 RED `AssistantCredentialTests`: `ToString()` devuelve texto redactado, nunca el valor. Cubre CP «Sin filtración de secretos» (Registros y errores) · DD-1. GREEN `AssistantCredential`, `EncryptedCredential`.
- [ ] 2.7 GREEN tipos sin prueba propia (se ejercitan abajo): puertos `IAssistantSettingsStore`, `IAssistantCredentialProtector`, `IAssistantEnvironment`, `IAssistantRuntimeReloader`, `IAssistantConnectionTester`; `AuditEntry`, `SaveOutcome`, comandos y resultados (DD-1, sección 3).
- [ ] 2.8 RED `SaveAssistantSettingsUseCaseTests` (dobles en memoria, `FakeTimeProvider`): guardado válido + auditoría; conservar clave si se omite; clave nueva cifrada con la versión activa; cambio de proveedor descarta clave anterior y exige nueva; sin protector disponible + clave nueva → error en `credential` sin escribir; sin protector + sin clave nueva → acepta; 409 por token obsoleto y por primera creación; auditoría con `credentialChanged` sin secretos; recarga tras confirmar y `200` aunque la recarga falle; recifrado oportunista de la clave conservada. Cubre AS «Guardado…», «Concurrencia…», «Auditoría…»; CP «Fallo cerrado» (Guardar sin clave maestra, sin clave nueva) · DD-2, 7, 8, 9, 11. GREEN.
- [ ] 2.9 RED `GetAssistantSettingsUseCaseTests`: campos de AS «Campos de la respuesta», `sources`, `applied` falso con proveedor ya no permitido, `credentialStorageAvailable`, `credentialConfigured` sin exponer clave. GREEN. Cubre AS «Consulta…» (Campos, Configuración aplicada, Almacenamiento no disponible, Sin clave) · DD-5, 14.
- [ ] 2.10 RED `ResetAssistantSettingsUseCaseTests`: con fila → borra, audita `Reset`, recarga; sin fila → `204` lógico sin auditoría. GREEN. Cubre AS «Restablecer», «Restablecer sin fila».
- [ ] 2.11 RED `TestAssistantConnectionUseCaseTests`: campos ausentes se completan con lo efectivo, cuerpo vacío prueba lo efectivo, candidato inválido (`timeoutSeconds` 4) → errores sin llamar al tester, no guarda ni audita, clave candidata no se persiste. GREEN. Cubre AS «Prueba de conexión» (Candidato no guardado, Candidato inválido, No guarda) · DD-13.
- [ ] 2.12 REFACTOR (sin duplicación entre reglas y casos de uso), `DependencyRuleTests` verde (Application sin paquetes nuevos) y puerta de PR.

---

## PR 3a — Cifrado y opciones de entorno (≈ 220 líneas)

**Objetivo:** `AesGcmCredentialProtector` y `AiOptions`/validador con las claves nuevas. **Rollback:** borrar el protector y revertir `AiOptions*`/`AiProvider`. **Comando:** `T-IF` + pruebas de arranque en `IT`.

- [ ] 3a.1 **Copia previa** de `INF/Assistant/AiOptions.cs`, `AiOptionsValidator.cs`, `AiProvider` y pruebas de opciones.
- [ ] 3a.2 **[V-4]** Verificar `AesGcm` con `tagSizeInBytes` explícito en .NET 10 y en la imagen Alpine; anotar en `design.md` §12.
- [ ] 3a.3 RED `IFT/Assistant/AesGcmCredentialProtectorTests`: ida y vuelta (el persistido no contiene el texto); nonce único (dos cifrados difieren); byte alterado → falla autenticación sin devolver clave; AAD con otro proveedor u otra versión → falla; Base64 inválido y longitud ≠ 32 → versión no disponible sin imprimir el valor; versión activa inexistente o fuera de 1-32767 → `IsAvailable` falso y `Protect` lanza `CredentialProtectionUnavailableException`; rotación v1 → v2 descifra v1; versión retirada → no descifrable. Cubre CP «Cifrado en reposo», «Fallo cerrado» (Clave maestra de longitud inválida, Base64 inválido, Versión activa inexistente, Descifrar sin clave maestra), «Rotación» · DD-6, 7. GREEN.
- [ ] 3a.4 RED `IFT/Assistant/AiOptionsValidatorTests` (o clase existente): `OpenAI`/`Gemini` exigen `Ai:ApiKey` y `Ai:Model`; `Ai:Effort` `extreme` → `Ai:Effort`; `Ai:AllowedProviders=Anthropic,GitHubModels` → `Ai:AllowedProviders` (también sin proveedor); `SettingsPollSeconds` 0/1/300/301; valores por defecto (poll 30); `Ai__MasterKeys__1` inválida **no** falla; `GitHubModels` como `Ai:Provider` falla con `Ai:Provider`; la excepción no contiene `clave-secreta-prueba`. Cubre AC «Claves de configuración…» (Proveedores admitidos, Claves adicionales opcionales, Valores numéricos por defecto), «Validación…» (todos los escenarios), «Asistente deshabilitado…» (Lista permitida inválida sin proveedor, Otras claves ignoradas); SC «Arranque…» (Proveedor desconocido, El valor no se imprime) · DD-18. GREEN: `AiOptions` (`Effort`, `AllowedProviders`, `MasterKeys`, `ActiveMasterKeyVersion`, `SettingsPollSeconds`), `AiOptionsValidator`; `AiProvider` gana `OpenAI`=3 y `Gemini`=4 sin renumerar.
- [ ] 3a.5 RED `AssistantProviderMappingTests`: los nombres de `AiProvider` y `AssistantProvider` coinciden. Cubre DD-1. GREEN.
- [ ] 3a.6 RED `IT` (sin Docker, clase de arranque existente): arranca sin `Ai__*` ni claves maestras, con `Gemini`/`OpenAI` válidos (`/health` 200 sin conectar), con `Ai__MasterKeys__1` inválida; falla con `GitHubModels` y con lista permitida inválida. Cubre SC «Sin clave maestra arranca», «Proveedor en la nube nuevo válido», «Las demás validaciones no cambian»; CP «El arranque no falla». GREEN: registro del protector (singleton) y línea informativa «protección de claves disponible/no disponible» sin valores (DD-7).
- [ ] 3a.7 REFACTOR y puerta de PR.

---

## PR 3b — Persistencia, migración y auditoría (≈ 300 líneas)

**Objetivo:** `assistant_settings` + `assistant_settings_audit` y `AssistantSettingsStore`. **Rollback:** `dotnet ef migrations remove` / `Down` elimina ambas tablas; revertir entidades y `DbContext`. **Comando:** `T-I --filter "FullyQualifiedName~AssistantSettingsStore"` (Docker). Depende de 2 (puertos).

- [ ] 3b.1 **Copia previa** de `INF/Persistence/TimeTrackerDbContext.cs`, el snapshot del modelo y `AR/` de convenciones de migraciones.
- [ ] 3b.2 RED `IT/Assistant/AssistantSettingsStoreTests` (Testcontainers): crear con token `"0"`; segunda creación → `Conflict`; `xmin` obsoleto → `Conflict` sin cambios; fila y auditoría en una transacción (fallo de auditoría no deja la fila); `CHECK id = 1`, tres columnas de clave todas nulas o todas presentes y nonce de 12 bytes; la columna cifrada no contiene `clave-prueba-123456`. Cubre AS «Concurrencia optimista» (Token obsoleto, Primera creación); CP «Cifrado en reposo» (Ida y vuelta, persistido sin texto claro) · DD-8.
- [ ] 3b.3 RED `AssistantSettingsStoreTests`: `DeleteAsync` devuelve `true`/`false` y solo audita si había fila; `GetAsync` con la tabla ausente (`42P01`) → `null` y otra excepción se propaga; `GetVersionAsync` = `"0"` sin fila. Cubre AS «Restablecer sin fila», «Tabla ausente» · DD-8, 11.
- [ ] 3b.4 GREEN: entidades, `AssistantSettingsConfiguration`, `AssistantSettingsAuditConfiguration` (`xmin` igual que `time_entries`, índice por `occurred_at` desc), `AssistantSettingsStore`, `DbContext`; migración `AddAssistantSettings` (+ `Designer`, snapshot). `Down` borra ambas tablas.
- [ ] 3b.5 RED `AR/AssistantAuditAppendOnlyTests`: ninguna referencia a `Update`/`Remove`/`ExecuteDelete`/`ExecuteUpdate` sobre la entidad de auditoría; el puerto solo expone `Add`. Cubre AS «Auditoría…» (Solo inserción), DD-9. GREEN.
- [ ] 3b.6 RED `IT`: la auditoría guarda `previous`/`current` solo con `provider`, `model`, `timeoutSeconds`, `maxOutputTokens`, `effort`, `credentialConfigured`, y `credential_changed` sin clave en claro ni cifrada. Cubre AS «Registro de un cambio», «Cambio de clave»; CP «Auditoría». GREEN.
- [ ] 3b.7 REFACTOR y puerta de PR (la migración no altera tablas existentes).

---

## PR 4 — Catálogo y adaptador compatible con OpenAI (≈ 350 líneas)

**Objetivo:** OpenAI/Gemini/Ollama/Anthropic construibles desde configuración; esfuerzo de razonamiento. **Rollback:** quitar el paquete `Microsoft.Extensions.AI.OpenAI` y revertir fábrica/`ChatOptions`. **Comando:** `T-IF`. **Empieza por el spike.**

- [ ] 4.1 **Copia previa** de `Directory.Packages.props`, `INF/TimeTracker.Infrastructure.csproj`, `INF/Assistant/ChatClientFactory.cs`, `AssistantChatPipeline.cs`, `AR/DependencyRuleTests.cs`.
- [ ] 4.2 **[V-1] Primero**: añadir `Microsoft.Extensions.AI.OpenAI` 10.10.0 y comprobar contra metadatos reales de NuGet que `RESTORE`/`BUILD` no dan NU1608/NU1903 y que `VULN` queda limpio; si no, elevar la transitiva con `PackageVersion` comentado. Anotar en `design.md` §12.
- [ ] 4.3 **SPIKE (RED, primera prueba de código)** `IFT/Assistant/ReasoningEffortSpikeTests` con `HttpMessageHandler` de grabación sin red (`OpenAIClientOptions.Transport`, `AnthropicClient.HttpClient`, `OllamaApiClient(HttpClient, model)`): el cuerpo contiene `reasoning_effort` (OpenAI y Gemini, este último contra la URL fija de Gemini), `output_config.effort` (Anthropic) y `think` (Ollama) para `low|medium|high`, y **ninguno** con esfuerzo nulo. Falla: «cuerpo sin `reasoning_effort`» o «`CreateChatOptions` no existe». Cubre DD-12 (4), V-2.
- [ ] 4.4 GREEN `AssistantChatPipeline.CreateChatOptions` (`Reasoning` solo si hay esfuerzo; el mismo método para chat y prueba). Si algún proveedor no lo traduce, aplicar la contingencia de DD-12 (3): `RawRepresentationFactory` (`ReasoningEffortLevel`, `ThinkValue`) o `SupportsEffort=false`. **Anotar el resultado del spike en `design.md` §12 (V-2)** y ajustar `AssistantProviderTraits` (PR 2) si cambia `SupportsEffort`.
- [ ] 4.5 **[V-3]** Verificar tipos de excepción y propiedad de estado HTTP de cada SDK (`ClientResultException`, excepción de Anthropic, OllamaSharp); anotar para PR 6a.
- [ ] 4.6 RED `IFT/Assistant/ChatClientFactoryTests`: tipo por proveedor (Anthropic, OpenAI sin `Endpoint`, Gemini con la URL de `ProviderEndpoints`, Ollama con `Ai:Endpoint`); `None` → `null` sin conexión; `NetworkTimeout` = `TimeoutSeconds` y un reintento. Cubre AC «Selección de proveedor solo por configuración» (Anthropic, OpenAI, Gemini, Ollama) · DD-3. GREEN: `ProviderEndpoints`, `IChatClientFactory` (conserva `ChatClientFactory.Create` estático).
- [ ] 4.7 RED `AR`: `OpenAI`/`Microsoft.Extensions.AI.OpenAI` solo en Infrastructure; `new Uri(` en `INF/Assistant` solo en `ProviderEndpoints` y la fábrica; **[V-5]** `Src_DoesNotHardcodeModelNames` sigue verde con nombres de proveedor y URL. Cubre SA de `asistente-ia` «Paquetes de IA acotados», AC «Solo configuración», AS «Sin URL del Manager» (SSRF) · DD-3. GREEN.
- [ ] 4.8 REFACTOR y puerta de PR.

---

## PR 5 — Recarga en caliente (≈ 380 líneas)

**Objetivo:** instantánea inmutable, motor sobre instantánea, cargador, poller y recargador. **Rollback:** revertir `AssistantChatClient`, motor y registro del poller (sin poller el sistema usa el entorno). **Comando:** `T-IF` + `T-I --filter "FullyQualifiedName~Reload"`. Depende de 3b y 4.

- [ ] 5.1 **Copia previa** de `INF/Assistant/AssistantChatClient.cs`, `ChatAssistantEngine.cs`, `AssistantServiceCollectionExtensions.cs` y pruebas del motor (el constructor pierde `IOptions<AiOptions>`).
- [ ] 5.2 RED `IFT/Assistant/AssistantChatClientSwapTests` (`FakeTimeProvider`): `Swap` publica con `Interlocked`; la anterior se libera tras 120 s y no antes; varios cambios seguidos no filtran recursos; `Dispose` libera vigente y pendientes; el constructor `(IChatClient?, AiOptions)` sigue compilando. Cubre AS «Recarga en caliente» (Petición en curso) · DD-10. GREEN `AssistantSnapshot`, `Current`, `Swap`.
- [ ] 5.3 RED `ChatAssistantEngineSnapshotTests`: lee `Current` una vez por petición; un `Swap` a mitad de petición no la afecta (modelo, timeout, tokens, esfuerzo de la instantánea inicial); `RequestsPerMinute` y cuota diaria siguen del entorno. Cubre AS «Aplicación local inmediata», «Petición en curso» · DD-10. GREEN.
- [ ] 5.4 RED `ConfigurationAssistantEnvironmentTests` y `AssistantSettingsLoaderTests`: fila válida → `Swap`; fila inválida, no descifrable o proveedor ya no permitido → **sin** `Swap` y un aviso por versión rechazada sin secretos; sin protector → clave guardada no se usa; `Version` = `"0"` sin fila; serializa con `SemaphoreSlim`. Cubre AS «Fila inválida», «Proveedor ya no permitido»; CP «Manipulación detectada», «Versión retirada», «Descifrar sin clave maestra» · DD-5, 11. GREEN `AssistantSettingsLoader`, `ConfigurationAssistantEnvironment`, `AssistantRuntimeReloader`.
- [ ] 5.5 RED `AssistantSettingsPollerTests`: carga solo si cambia `xmin`; base de datos caída → conserva la última válida, un aviso por racha (solo nombre del tipo) y una línea al recuperarse; primera carga acotada a 10 s en `StartAsync`; `SettingsPollSeconds` configurable. Cubre AS «Base de datos caída», «Otra instancia» · DD-11. GREEN `AssistantSettingsPoller`.
- [ ] 5.6 **[V-6]** RED `IT` de orden de arranque: la primera carga ocurre tras las migraciones y antes del primer chat; si falla, marcar la instantánea inicial como «no cargada». Registrar el poller en `API/Program.cs`; `ApiFactory` sin base de datos lo elimina (`ImplementationType == typeof(AssistantSettingsPoller)`). GREEN.
- [ ] 5.7 RED `IT` con PostgreSQL: guardado externo (SQL) + `Ai__SettingsPollSeconds=1` → el chat usa el modelo nuevo con `IChatClientFactory` falso; fila `None` → `503`. Cubre AS «Deshabilitar». GREEN.
- [ ] 5.8 REFACTOR y puerta de PR.

---

## PR 6a — Prueba de conexión y límite de tasa (≈ 220 líneas)

**Objetivo:** tester, clasificador y política `assistant-config-test`. **Rollback:** borrar tester/clasificador y la política. **Comando:** `T-IF`. Depende de 2 y 4.

- [ ] 6a.1 **Copia previa** de `API/RateLimitingServiceCollectionExtensions.cs` y sus pruebas.
- [ ] 6a.2 RED `IFT/Assistant/ConnectionFailureClassifierTests`: 401/403 → `authentication`; 404 → `model-not-found`; 400/422 → `rejected`; plazo propio → `timeout`; 429, 5xx, red y desconocido → `unreachable`; clasifica por estado HTTP, no por mensaje. Cubre AS «Prueba de conexión» (Categorías de error, Parámetros rechazados, Otros fallos) · DD-13, V-3. GREEN.
- [ ] 6a.3 RED `AssistantConnectionTesterTests` (`FakeChatClient`): mensaje fijo sin datos del usuario, sin herramientas, `MaxOutputTokens` 64, mismo `CreateChatOptions` con el esfuerzo del candidato, plazo `min(timeout, 15)`, cliente temporal desechado, no toca la instantánea; la respuesta no contiene `https://api.ejemplo.test/v1` ni `clave-prueba-123456` ni en el registro (solo tipo de excepción y categoría). Cubre AS «Conexión correcta», «Sin eco del proveedor», «No guarda»; CP «Registros y errores» · DD-13. GREEN `AssistantConnectionTester`.
- [ ] 6a.4 RED prueba de política: 3 permisos por minuto por claim `sub`, `QueueLimit = 0`, la 4.ª rechaza con detalle propio «Ha realizado demasiadas pruebas de conexión…» y `Retry-After`; no consume permisos un anónimo ni un Employee (la política va tras autorizar). Cubre AS «Límite de tasa» · DD-15. GREEN política `assistant-config-test` y `OnRejected` por política sin cambiar la del chat.
- [ ] 6a.5 REFACTOR y puerta de PR.

---

## PR 6b — API: endpoints, política Manager y OpenAPI (≈ 380 líneas)

**Objetivo:** los cuatro endpoints, DTO estrictos y contrato. **Rollback:** revertir endpoints y la política movida (**[V-8]**: restaurar referencias a `TeamEndpoints.ManagerPolicy`) y regenerar `docs/api/openapi.json`. **Comando:** `T-I --filter "FullyQualifiedName~AssistantConfigurationEndpoints"` + `T-AR`. Depende de 2, 3b, 5 y 6a.

- [ ] 6b.1 **Copia previa** de `API/Team/TeamEndpoints.cs`, `API/Program.cs`, `docs/api/openapi.json`, `AR/OpenApiContractTests.cs`.
- [ ] 6b.2 **[V-8]** RED/GREEN: mover la política a `API/Security/AuthorizationPolicies` (`Manager`); `AddTeam()`/`MapTeamEndpoints` la consumen desde allí; las pruebas de `/equipo` siguen verdes. Cubre DD-14.
- [ ] 6b.3 **[V-7]** RED `IT`: propiedad desconocida (`url`, `endpoint`) → `400` y no `500`, nada guardado; si `JsonUnmappedMemberHandling.Disallow` no da 400, validar las propiedades a mano. Cubre AS «Guardado…» (Propiedad desconocida), «Sin URL del Manager» (SSRF) · DD-14. GREEN DTO de cadenas con `Disallow`.
- [ ] 6b.4 RED `IT/AssistantConfigurationEndpointsTests` (Docker): 401 anónimo y 403 Employee en los cuatro endpoints sin leer ni escribir nada; 200 Manager en `GET`; `PUT` válido → `200` con cuerpo equivalente a `GET` y fila de auditoría; 400 por campo (`timeoutSeconds` 4, `model` `<script>`, `effort` `extreme`); 409 con dos lectores y primera creación; `DELETE` → `204` con y sin fila y `GET` con origen `env`; cuerpo máximo 8 KiB → `413`. Cubre AS «Acceso exclusivo del Manager», «Consulta…» (Respuesta del guardado), «Guardado…», «Concurrencia…», «Restablecer» · DD-14. GREEN `AssistantConfigurationEndpoints`; registro de casos de uso en `Program.cs`.
- [ ] 6b.5 RED `IT`: sin clave maestra, `PUT` con `credential` → `400` en el campo `credential` sin fila escrita; `GET` con `credentialStorageAvailable=false`; `GET`/`PUT`/prueba nunca devuelven `clave-prueba-123456` ni su forma cifrada; el indicador pasa a `false` tras `DELETE`. Cubre CP «Fallo cerrado» (Guardar sin clave maestra), «Clave de solo escritura» (Respuestas sin clave, Indicador), «Sin filtración de secretos» · DD-7. GREEN.
- [ ] 6b.6 RED `IT`: prueba → `200 { result, message }` con tester falso (`ok`, `authentication`, `rejected`…), `400` con candidato inválido sin contactar al proveedor, `429` a la 4.ª en un minuto, también con las que acaban en `400`. Cubre AS «Prueba de conexión», «Límite de tasa» · DD-13, 15. GREEN.
- [ ] 6b.7 RED `AR/OpenApiContractTests` ampliada: `/api/asistente` conserva solo `post`; rutas nuevas con exactamente las operaciones y respuestas de DD-14; el documento no contiene `apikey` ni `Ai__`; sin `url`/`endpoint` como propiedad de esquemas de configuración; `credential` solo en peticiones y `credentialConfigured` solo en respuestas. Falla: «ruta /api/asistente/configuracion no definida». Cubre CP «Contrato OpenAPI» · DD-16. GREEN: regenerar `docs/api/openapi.json` con el build.
- [ ] 6b.8 REFACTOR y puerta de PR.

---

## PR 7 — Web: `/asistente/configuracion` (≈ 350 líneas)

**Objetivo:** página, cliente y formulario para Managers. **Rollback:** borrar la página, el cliente, el enlace de `NavMenu` y los estilos. **Comando:** `T-W`; verificación manual en Chrome. Depende de 6b (contrato). **Nota:** la Web no tiene bUnit/E2E; la página se verifica a mano.

- [ ] 7.1 **Copia previa** de `WEB/Layout/NavMenu.razor`, `WEB/Program.cs`, `WEB/wwwroot/css/app.css`, `WT/RepositoryConventionsTests.cs`.
- [ ] 7.2 RED `WT/Assistant/AssistantConfigurationFormTests`: mapeo a la petición (campos omitidos = `null`, clave solo si se escribió); clave vaciada tras guardar; muestra «clave configurada»; esfuerzo oculto si `supportsEffort` es falso; selector con `allowedProviders` + «Deshabilitado»; `409` → mensaje y opción de recargar; `400` → mensajes por campo; texto `<b>x</b>` conservado literal. Cubre AS «Página Web» (Clave vaciada, Texto plano), CP «Web» · DD-17. GREEN `AssistantConfigurationForm`, `AssistantConfigurationDtos`.
- [ ] 7.3 RED `WT` de `AssistantConfigurationApiClient` (sobrecarga de `ApiRequest` con estados `{ BadRequest, Conflict, TooManyRequests }`): éxito, `400` con errores, `409`, `429`. GREEN.
- [ ] 7.4 RED `WT/RepositoryConventionsTests`: la página no usa `localStorage`/`sessionStorage`/`MarkupString`; no contiene reglas de negocio ni espacios de nombres de IA. Cubre AS «Sin almacenamiento del navegador», «Texto plano» · DD-17, V-5. GREEN `Pages/AssistantConfiguration.razor` (`[Authorize(Roles = nameof(UserRole.Manager))]`, avisos de datos personales y alcance a toda la empresa, botones Guardar, «Probar conexión» y «Restablecer al entorno» con confirmación, origen de cada valor, campo de clave tipo contraseña desactivado si `credentialStorageAvailable` es falso).
- [ ] 7.5 GREEN `NavMenu` (enlace solo en el bloque Manager junto a «Equipo»), registro en `Program.cs`, estilos; la página `/asistente` no cambia. Cubre AS «Acceso de Employee», «Chat sin cambios».
- [ ] 7.6 Verificación manual en Chrome (registrar en `verify-report.md`): Manager ve avisos y guarda; Employee sin enlace y «acceso denegado» por URL directa; clave vaciada; DOM sin clave. Puerta de PR.

---

## PR 8 — Endurecimiento y documentación (≈ 200 líneas)

**Objetivo:** pruebas de integración entre instancias, guardas finales y documentación. **Rollback:** revertir pruebas y documentación. **Comando:** `T-I` + `T-AR`.

- [ ] 8.1 RED/GREEN `IT`: dos instancias sobre la misma base de datos con `Ai__SettingsPollSeconds=1`; guardar en la primera → la segunda usa el modelo nuevo; la tabla ausente usa el entorno; fila no descifrable conserva la última válida y deja un aviso sin secretos. Cubre AS «Otra instancia», «Tabla ausente», «Fila inválida»; CP «Manipulación detectada».
- [ ] 8.2 RED/GREEN `AR`: confirmar todas las guardas (`DependencyRuleTests`, `Src_DoesNotHardcodeModelNames`, `OpenApiContractTests`, `DeliveryArtifactsTests`, auditoría de solo inserción, `new Uri(`); cerrar huecos de los PR anteriores.
- [ ] 8.3 Cerrar V-1 a V-8 en `design.md` («Resultados de V-n en apply») y actualizar la lista de ADR vigentes/estado en `openspec/config.yaml` solo si el usuario lo aprueba.
- [ ] 8.4 `README.md` y ADR 0018: despliegue en Railway (definir `Ai__MasterKeys__1` y `Ai__ActiveMasterKeyVersion=1` **antes** de que un Manager guarde una clave; riesgo de Responses API; datos personales). Resolver con el product owner la pregunta abierta de custodia de la clave maestra (§13).
- [ ] 8.5 Puerta final completa; PARADA para `sdd-verify`.

---

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ≈ 2.930 en 10 PR (150 + 380 + 220 + 300 + 350 + 380 + 220 + 380 + 350 + 200), según el diseño §10 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 → 2 → 3a → 3b → 4 → 5 → 6a → 6b → 7 → 8 |
| Delivery strategy | ask-on-risk |
| Chain strategy | stacked-to-main (elegida por el usuario; cada PR apunta al anterior o a main; el usuario fusiona) |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

Honestidad de las estimaciones: son del diseño y probablemente optimistas. Cinco PR están entre 350 y 380 líneas (2, 4, 5, 6b, 7) con casi nulo margen: cualquier prueba o ajuste extra los cruza de 400. PR 3b incluye la migración generada (`Designer` y snapshot suman cientos de líneas por sí solas): probablemente supere 400 si se cuenta lo generado; proponer excluirlo del cómputo o `size:exception` solo para ese archivo. El PR 2 (380 con ~10 pruebas) es el más dudoso. Si un PR se pasa, dividirlo en la tarea natural (p. ej. 2: reglas+resolver / casos de uso).

Decisiones necesarias antes de apply:
1. **Aprobación del usuario para archivar `asistente-ia` (tarea 0)**.
2. Criterio para el diff generado de la migración del PR 3b (¿cuenta en las 400?).
3. (No bloqueante del código) custodia de la clave maestra en Railway (§13).

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 0 | Archivar `asistente-ia` | previo | N/A | `sdd-archive`; existe `openspec/specs/assistant-configuration/` | Solo documentación |
| 1 | ADR 0018 + docs | PR 1 (base main) | `dotnet test tests/TimeTracker.ArchitectureTests --filter FullyQualifiedName~DeliveryArtifacts` | N/A (solo documentación) | Archivos de docs |
| 2 | Application | PR 2 (base PR 1) | `dotnet test tests/TimeTracker.Application.Tests --filter FullyQualifiedName~Assistant.Configuration` | N/A (lógica pura) | `APP/Assistant/Configuration/` |
| 3a | Cifrado + opciones | PR 3a (base PR 2) | `dotnet test tests/TimeTracker.Infrastructure.Tests --filter FullyQualifiedName~AesGcm` | Arranque de la API con y sin clave maestra | Protector y opciones |
| 3b | Persistencia | PR 3b (base PR 3a) | `dotnet test tests/TimeTracker.Api.IntegrationTests --filter FullyQualifiedName~AssistantSettingsStore` | PostgreSQL 18 (Testcontainers) | Migración `Down` |
| 4 | Catálogo + OpenAI/Gemini | PR 4 (base PR 3b) | `dotnet test tests/TimeTracker.Infrastructure.Tests --filter FullyQualifiedName~ReasoningEffortSpike` | Manejador de grabación (sin red) | Paquete + fábrica |
| 5 | Recarga en caliente | PR 5 (base PR 4) | `dotnet test tests/TimeTracker.Infrastructure.Tests --filter FullyQualifiedName~Swap` | API + PostgreSQL con sondeo de 1 s | Instantánea/poller |
| 6a | Prueba + tasa | PR 6a (base PR 5) | `dotnet test tests/TimeTracker.Infrastructure.Tests --filter FullyQualifiedName~Classifier` | `FakeChatClient` | Tester y política |
| 6b | API | PR 6b (base PR 6a) | `dotnet test tests/TimeTracker.Api.IntegrationTests --filter FullyQualifiedName~AssistantConfigurationEndpoints` | HTTP real contra WebApplicationFactory | Endpoints y `openapi.json` |
| 7 | Web | PR 7 (base PR 6b) | `dotnet test tests/TimeTracker.Web.Tests --filter FullyQualifiedName~AssistantConfiguration` | Chrome manual contra la API local | Página y enlace |
| 8 | Endurecimiento | PR 8 (base PR 7) | `dotnet test tests/TimeTracker.Api.IntegrationTests --filter FullyQualifiedName~MultiInstance` | Dos instancias sobre una base | Pruebas y docs |

Total de tareas: ≈ 85 (0: 2 · PR 1: 7 · PR 2: 12 · PR 3a: 7 · PR 3b: 7 · PR 4: 8 · PR 5: 8 · PR 6a: 5 · PR 6b: 8 · PR 7: 6 · PR 8: 5). Orden estrictamente secuencial por PR; dentro de cada PR, las pruebas y su código verde son consecutivos. Pueden paralelizarse solo dentro del mismo PR (p. ej. 2.4/2.5/2.6), sin escritores concurrentes.
