# Informe de verificación: asistente-config-responsable

Fecha: 2026-10-20 · Fase: sdd-verify · Modo: openspec · Strict TDD: activo · Rama `main`, HEAD `22297cc` (PR 1 a 8, 2.1, 3a.1, 4.1, 4.2, 6a y 6b fusionadas) · Idioma: español (petición del orquestador).

## Veredicto: PASS WITH WARNINGS

CRITICAL: 0 · WARNING: 6 · SUGGESTION: 7. Ningún bloqueante. La implementación cumple las 4 specs (21 requisitos, 127 escenarios: 125 CUMPLE, 2 PARCIAL con desviación declarada o evidencia solo manual, 0 NO CUMPLE), el diseño (DD-1 a DD-19, V-1 a V-8) y la puerta de calidad completa está en verde.

## Estado final tras la PR de cierre (2026-10-20)

Veredicto final: **PASS WITH WARNINGS** (sin cambios; CRITICAL 0).

Cerrados en la rama `chore/asistente-config-cierre-verify`:

- W-1 `tasks.md`: 0.1, 0.2, 7.6 y 8.5 marcadas y anotadas.
- W-2 `.env.example`: texto de `Ai__AllowedProviders` corregido y fijado por `DeliveryArtifactsTests`.
- W-3 `design.md`: tabla de DD-3 con la URL fija de Anthropic.
- S-1 `AssistantSettingsPoller.PollCompleted`: cada suscriptor se invoca por separado y una excepción solo se registra por su tipo (prueba nueva; el evento sigue público porque lo consume `Api.IntegrationTests`). Las vueltas canceladas siguen disparando el evento (inocuo; no se cambió).
- S-2 ADR 0019 y comentario de la migración: el trigger no frena a quien tenga privilegios de propietario (prueba de documentación nueva).
- S-3 `apply-progress.md`: cabecera, cita de `2ba6b73` y «No cerrado» actualizados.
- S-7 README: ejemplo de `Ai__AllowedProviders` en el paso de Railway.
- `openspec/config.yaml`: línea de «Estado» actualizada (aprobado por el usuario).

Siguen abiertos: S-4 (prueba de integración del truncado de `updatedAt`), S-5 (`PrintMembers` duplicado), S-6 (booleano en `CommitSaveAsync`) y las desviaciones declaradas y aceptadas W-4 (Ollama 400 = `unreachable`), W-5 (DTO numéricos `int?`) y W-6 (`DELETE` sin token del cliente). Pendiente fuera del código: alta en Railway por el usuario y custodia de la clave maestra.

## 1. Completitud de tareas

Líneas de tarea: 91 · marcadas `[x]`: 88 · sin marcar: 3.

| Tarea | Estado en `tasks.md` | Estado real | Severidad |
|---|---|---|---|
| 0.1 archivar `asistente-ia` | `[ ]` (tasks.md:39) | Cumplida: existe `openspec/changes/archive/2026-10-18-asistente-ia` y existen `openspec/specs/assistant-configuration/` y `startup-configuration/` | WARNING (marca sin actualizar) |
| 0.2 plan B si no se pudiera archivar | `[ ]` (tasks.md:40) | No aplica (0.1 se cumplió) | WARNING (misma causa) |
| 7.6 verificación manual en Chrome | `[ ]` (tasks.md:199) | Hecha por el orquestador el 2026-10-20 (sección 7) | WARNING (marca sin actualizar) |

Además, 8.5 (tasks.md:211) sigue diciendo «PARADA para `sdd-verify` PENDIENTE»: queda resuelta por este informe. Todas las tareas de PR 1 a 8 y las de PR 2.1, 3a.1, 4.1 y 4.2 están `[x]` y con evidencia TDD (RED/GREEN/TRIANGULAR/REFACTOR) en `apply-progress.md` (una sección por PR).

## 2. Ejecución real (puerta)

Comandos ejecutados en `main` limpio, con las variables `Ai__*`, `Jwt__*`, etc. sin definir. Docker en marcha; solo Testcontainers creó y retiró sus propios contenedores (no se tocó, detuvo ni borró ningún contenedor, volumen, red o imagen existente; sin prune).

| Paso | Comando | Resultado |
|---|---|---|
| BUILD | `dotnet build TimeTracker.sln -warnaserror` | exit 0 · 0 advertencias · 0 errores (30,8 s) |
| FORMAT | `dotnet format TimeTracker.sln --verify-no-changes --no-restore` | exit 0 · sin cambios |
| TEST | `dotnet test TimeTracker.sln --no-build --collect:"XPlat Code Coverage"` | exit 0 · 1.769 pruebas, 0 fallos, 0 omitidas |
| COV-A | `pwsh scripts/check-coverage.ps1 ... -Package TimeTracker.Application -MinimumPercent 80` | exit 0 · líneas 99,8 % · ramas 96,6 % |
| COV-D | `... -Package TimeTracker.Domain -MinimumPercent 100` | exit 0 · líneas 100,0 % · ramas 100,0 % |
| VULN | `dotnet list TimeTracker.sln package --vulnerable --include-transitive` | exit 0 · «has no vulnerable packages» en todos los proyectos · sin «has the following vulnerable packages» |

Pruebas por suite: Domain 67 · Application 324 · Infrastructure 398 · Web 288 · Architecture 154 · Api.IntegrationTests 538 (PostgreSQL 18 por Testcontainers; 2 min). Coinciden con el apply-progress de la PR 8 salvo Architecture (154 frente a 153).

Notas: build y pruebas en configuración Debug (por defecto), no `-c Release --no-restore` como el alias `BUILD` de `tasks.md`; no cambia el resultado. El build modificó `docs/api/openapi.json`; se restauró con `git checkout` y `git status` queda limpio. Se borró `TestResults/` (ignorado por Git) creado por la cobertura.

Strict TDD: el apply-progress contiene la tabla RED/GREEN/TRIANGULAR/REFACTOR por unidad con el mensaje real del RED, caracterizaciones «verde a la primera» justificadas (PR 8: c, d, 8.1, 8.2) y mutaciones temporales anotadas (8.1, V-7). Sin hallazgos.

## 3. Matriz de cumplimiento (requisito, codigo, prueba)

CUMPLE = hay código y una prueba que pasó en esta ejecución. PARCIAL = cumple con una desviación declarada o con evidencia solo manual para parte del escenario.

### assistant-settings (10 requisitos, 60 escenarios)

| Requisito (escenarios) | Código | Prueba que pasó | Estado |
|---|---|---|---|
| Acceso exclusivo del Manager (3) | `Api/Assistant/AssistantConfigurationEndpoints.cs:32` (política en el grupo), `Api/Security/AuthorizationPolicies.cs:8-13` | IT `AssistantConfigurationEndpointsTests`: `Anonymous_Returns401...`, `Employee_Returns403...` (4 rutas), `Get_AsManagerWithoutRow_Returns200...` | CUMPLE 3/3 |
| Catálogo y lista permitida (8) | `Application/.../AllowedProviders.cs`, `AssistantSettingsRules.cs:43-45`, `AiOptionsValidator.cs`, `AssistantConfigurationRequest.cs:123` | `AllowedProvidersTests`, `AssistantSettingsRulesTests` (`Provider_*`), `AiOptionsValidatorTests`, IT `AiStartupTests.InvalidAllowedProviders...`, `AssistantConfigurationStrictBodyTests`, `GetAssistantSettingsUseCaseTests.AllowedProviders_AreObjectsWithTheirCapabilities` | CUMPLE 8/8 |
| Consulta de la configuración efectiva (7) | `AssistantConfigurationResponse.cs:172-210`, `GetAssistantSettingsUseCase`, `AssistantSettingsResolver` | `GetAssistantSettingsUseCaseTests` (9), `AssistantSettingsResolverTests` (8), IT `Get_AsManagerWithoutRow...`, `Put_Valid_Returns200WithTheSameBodyAsGet...`, `TheKeyIsWriteOnly...` | CUMPLE 7/7 |
| Guardado con validación (9) | `AssistantSettingsRules.cs:6-128`, `SaveAssistantSettingsUseCase.cs:172-196`, `AssistantConfigurationRequest.cs:123` | `AssistantSettingsRulesTests` (extremos 4/5/55/56 y 63/64/4096/4097, modelo, esfuerzo), `SaveAssistantSettingsUseCaseTests` (22), IT `Put_WithInvalidFields...`, `Put_WithAnUnknownProperty...`, `Put_WithProviderNoneAndAKey...` | CUMPLE 9/9 (valores no enteros: 400 general, W-5) |
| Concurrencia optimista (2) | `Persistence/AssistantSettingsStore.cs:55-135` (`xmin` como valor original; 23505 en creación) | IT `AssistantSettingsStoreTests` (`Save_WithAStaleToken...`, `Save_WithTheNoRowTokenWhenARowAlreadyExists...`), `Put_ByTwoReaders_SecondGets409...` | CUMPLE 2/2 |
| Deshabilitar, prioridad y restablecer (6) | `AssistantSettingsResolver`, `ResetAssistantSettingsUseCase.cs:249-271`, `AssistantSettingsStore.cs:23-37,67-94` | `AssistantSettingsResolverTests`, `ResetAssistantSettingsUseCaseTests` (5), IT `Delete_*` (3), `AssistantSettingsStoreTests.Delete_*` y `WhenTheTableDoesNotExist...`, `AssistantSettingsExternalSaveTests.AnExternalNoneRow_DisablesTheChatWith503...` | CUMPLE 6/6 |
| Prueba de conexión (10) | `AssistantConnectionTester.cs:339-377`, `ConnectionFailureClassifier.cs:278-307`, `TestAssistantConnectionUseCase` | `ConnectionFailureClassifierTests` (7), `AssistantConnectionTesterTests` (12), `TestAssistantConnectionUseCaseTests` (16), IT `AssistantConfigurationConnectionTestEndpointTests` (7), `AssistantConfigTestRateLimitTests` (5) | CUMPLE 9/10; PARCIAL «Parámetros rechazados» solo para Ollama (400 = `unreachable`, declarado; W-4) |
| Auditoría de solo inserción (3) | `AssistantSettingsStore.cs:167-192`, migración `20261020111927_AddAssistantAuditImmutability.cs`, puerto sin lectura | IT `AssistantSettingsAuditTests` (3), `AssistantSettingsAuditImmutabilityTests` (6), AR `AssistantAuditAppendOnlyTests` | CUMPLE 3/3 |
| Recarga en caliente (6) | `AssistantChatClient` (`Swap`/`Current`), `AssistantSettingsLoader.cs:73-145`, `AssistantSettingsPoller.cs:176-248` | `AssistantChatClientSwapTests` (10), `ChatAssistantEngineSnapshotTests` (5), `AssistantSettingsLoaderTests` (17), `AssistantSettingsPollerTests` (12), IT `AssistantSettingsMultiInstanceTests` (4), `AssistantSettingsExternalSaveTests` (3), `AssistantSettingsStartupOrderTests` (4) | CUMPLE 6/6 |
| Página Web (6) | `Web/Pages/AssistantConfiguration.razor:2,14,17,112-126`, `Layout/NavMenu.razor:41`, `Assistant/AssistantConfigurationForm.cs` | WT `AssistantConfigurationFormTests` (40), `AssistantConfigurationApiClientTests`, AR `RepositoryConventionsTests.Web_HasTheAssistantConfigurationPage...`, `Web_LinksTheAssistantConfigurationOnlyInTheManagerBlock...`; manual Chrome 7.6 | CUMPLE 5/6; PARCIAL «Clave vaciada» en la parte de `sessionStorage` y campo de contraseña por script (no comprobable por navegador; cubierta por prueba del formulario y guarda estática) |

### assistant-credential-protection (5 requisitos, 18 escenarios)

| Requisito (escenarios) | Código | Prueba | Estado |
|---|---|---|---|
| Cifrado en reposo (3) | `Infrastructure/Assistant/AesGcmCredentialProtector.cs:265-315` (AES-256-GCM, nonce 12 B, etiqueta 16 B, AAD con proveedor y versión) | `AesGcmCredentialProtectorTests` (ida y vuelta, nonce único, byte alterado), IT `AssistantSettingsStoreTests.TheStoredCredential_DoesNotContain...`, `AssistantSettingsMultiInstanceTests.ATamperedRow...` | CUMPLE 3/3 |
| Fallo cerrado sin clave maestra (7) | `AesGcmCredentialProtector.cs:221-259`, `SaveAssistantSettingsUseCase.cs:179-182`, `Api/Assistant/ProtectionStatusLogger.cs` | `AesGcmCredentialProtectorTests` (longitud, Base64, versión activa, sin claves), IT `AssistantConfigurationCredentialTests`, `AiStartupTests.InvalidMasterKeys_DoNotStopTheStartup`, `AssistantSettingsLoaderTests.LoadAsync_WithoutAMasterKey...` | CUMPLE 7/7 |
| Rotación (2) | mismo protector (versión por registro) | `Rotation_DecryptsOldVersionsAndEncryptsWithTheActiveOne`, `RetiredVersion_IsNotDecryptable`, `LoadAsync_WithARetiredKeyVersion...` | CUMPLE 2/2 |
| Clave de solo escritura (2) | `AssistantConfigurationResponse.cs:172-185` (sin campo de clave) | IT `TheKeyIsWriteOnly_...`, `AnInvalidResponse400_NeverEchoesTheCandidateKey` | CUMPLE 2/2 |
| Sin filtración de secretos (4) | `AssistantSettingsLog.cs`, `AssistantConnectionTestLog.cs` (solo versión, categoría y tipo de excepción), `AssistantCredential.ToString()`, `PrintMembers` redactado (`AssistantConfigurationRequest.cs:134,152`), `AssistantSettingsStore.cs:179-192` | `AssistantCredentialTests`, `AssistantSettingsInputTests`, `AssistantConnectionTesterTests.TheProviderFailureMessage...`, IT `AssistantSettingsAuditTests`, AR `OpenApiContractTests.ExportedDocument_NeverMentionsTheProviderApiKey` y `..._TheKeyIsInRequestsAndOnlyItsIndicatorInResponses`; manual Chrome | CUMPLE 3/4; PARCIAL «Web» solo en la parte de almacenamiento del navegador por script (ver arriba) |

### assistant-configuration, delta (4 requisitos, 32 escenarios) y startup-configuration, delta (2 requisitos, 17 escenarios)

| Requisito (escenarios) | Código | Prueba | Estado |
|---|---|---|---|
| AC Claves de configuración (6) | `AiOptions.cs`, `AiProvider` (OpenAI=3, Gemini=4) | `AiOptionsValidatorTests`, `AssistantProviderMappingTests`, `AiStartupTests.AppSettingsFiles_DoNotDefineAiKeys`, AR guarda de `appsettings*.json` | CUMPLE 6/6 |
| AC Asistente deshabilitado sin proveedor (4) | `AiOptionsValidator.cs`, `AssistantServiceCollectionExtensions.cs` | `Validate_WithoutProvider_IsValidEvenWithInvalidLimits`, `AiStartupTests.WithoutProvider_StartsEvenWithInvalidLimits`, `AssistantRegistrationTests` | CUMPLE 4/4 |
| AC Selección de proveedor solo por configuración (6) | `ChatClientFactory.cs:82`, `DefaultChatClientFactory.cs`, `ProviderEndpoints.cs:10-12` | `ChatClientFactoryTests` (19), `AssistantSettingsResolverTests`, AR `Src_DoesNotHardcodeModelNames` | CUMPLE 6/6 |
| AC Validación de claves con proveedor activo (16) | `AiOptionsValidator.cs` | `AiOptionsValidatorTests` (22), IT `AiStartupTests` (límites, extremos, `GitHubModels`, no imprime la clave) | CUMPLE 16/16 |
| SC Arranque con la configuración del asistente (8) | `AiOptionsValidator` + `ValidateOnStart` | `AiStartupTests`, `StartupConfigurationTests` | CUMPLE 8/8 |
| SC Documentación de claves en `.env.example` (9) | `.env.example` | AR `DeliveryArtifactsTests` (`EnvExample_*`, 11 pruebas) | CUMPLE 9/9 (texto engañoso en `.env.example:54`, W-2) |

## 4. Coherencia con el diseño

| Decisión | Resultado |
|---|---|
| DD-1 capas y puertos; Domain sin cambios | Respetada (`DependencyRuleTests`, `ProviderSdks_AreOnlyReferencedByInfrastructure`) |
| DD-2 reglas puras con constantes únicas | Respetada (`AssistantSettingsLimits` reutilizadas por el validador) |
| DD-3 catálogo con URL fijas | Respetada y ampliada: Anthropic también con URL fija (`ProviderEndpoints.Anthropic`, PR 4.1); la tabla de DD-3 aún dice «valor por defecto del SDK» (W-3) |
| DD-4 `Ai__AllowedProviders` | Respetada en código y pruebas; documentación de `.env.example` contradictoria (W-2) |
| DD-5 resolver | Respetada |
| DD-6/DD-7 AES-GCM, AAD, rotación, fallo cerrado | Respetada (más endurecimiento de PR 3a.1: versiones duplicadas `1`/`01`) |
| DD-8 esquema, `xmin`, `42P01`, `DELETE` idempotente no ciego | Respetada |
| DD-9 auditoría | Respetada y reforzada con trigger (`AddAssistantAuditImmutability`; ADR 0019) |
| DD-10/DD-11 instantánea, `Swap`, 120 s, cargador, poller | Respetada |
| DD-12 esfuerzo por proveedor; spike | Respetada; spike sin contingencia (V-2) |
| DD-13 prueba de conexión | Respetada salvo Ollama 400 (desviación declarada) |
| DD-14 endpoints y contrato | Respetada salvo DTO numéricos `int?` (desviación declarada, §14) |
| DD-15 límite 3/min por `sub`, `QueueLimit = 0`, tras autorizar | Respetada (`RateLimitingServiceCollectionExtensions.cs:17,27,57,110`) |
| DD-16 guardas de contrato | Respetada (`OpenApiContractTests`) |
| DD-17 página Web | Respetada; `[Authorize(Roles = "Manager")]` literal por no referenciar Domain (declarado en PR 7) |
| DD-18 opciones y validador | Respetada |
| DD-19 ADR 0018 intacto; complemento en ADR 0019 | Cumplida: `git diff 38f7c3f HEAD -- docs/adr/0018-...md` vacío; ADR 0019 existe; `Adr0018_IsKeptAsMergedBecauseAdrsAreNotRewritten` verde |
| V-1 a V-8 | Todas cerradas en `design.md` §12 con resultados |

Desviaciones declaradas y aceptadas: (1) `timeoutSeconds` y `maxOutputTokens` como `int?` en vez de cadenas (§14, `AssistantConfigurationStrictBodyTests`); (2) `DELETE` sin token del cliente (§14; la Web advierte; lectura y borrado en el servidor con `xmin`); (3) Ollama 400 = `unreachable` (§14, V-3); (4) Anthropic con URL fija (PR 4.1, sin reflejar en el diseño).

## 5. Seguridad (inspección)

| Comprobación | Resultado |
|---|---|
| Ninguna respuesta devuelve la clave (claro ni cifrada) | `AssistantConfigurationResponse` no tiene campo de clave, solo `CredentialConfigured`; `AssistantConnectionTestResponse` solo `Result`/`Message`; IT `TheKeyIsWriteOnly...` y `AnInvalidResponse400_NeverEchoesTheCandidateKey` |
| Registros | Todos los `LoggerMessage` de `AssistantSettingsLog`, `AssistantConnectionTestLog` y `ProtectionStatusLogger` llevan solo versión, categoría, proveedor, tipo de excepción o motivo categórico; búsqueda de `key\|credential\|secret\|apikey\|master` sobre llamadas de log: sin coincidencias |
| Auditoría | `AssistantSettingsStore.cs:179-192` serializa solo `provider`, `model`, `timeoutSeconds`, `maxOutputTokens`, `effort`, `credentialConfigured`; `credential_changed` es booleano; trigger de solo inserción |
| Persistencia | Solo `credential_ciphertext`/`nonce`/`key_version`; la clave en claro no se persiste (IT con `clave-prueba-123456`) |
| DTO sin url/endpoint | `AssistantConfigurationRequest`/`Response` y `AssistantConfigurationDtos` sin campos de URL; `JsonUnmappedMemberHandling.Disallow` (`AssistantConfigurationRequest.cs:123,143`); `openapi.json` con 0 coincidencias de `apikey`/`Ai__` y sin `url`/`endpoint` en esquemas |
| Salida (SSRF) | `new Uri(` solo en `ProviderEndpoints.cs:10-12`; `Uri.TryCreate` solo en `ChatClientFactory.cs:82` y `AiOptionsValidator.cs:82` (validan `Ai:Endpoint` de entorno); ninguna URL del Manager |
| Web: avisos y almacenamiento | `AssistantConfiguration.razor:14` (datos personales a un tercero) y `:17` (alcance a toda la empresa); sin `localStorage`/`sessionStorage`/`MarkupString`/`IJSRuntime` (búsqueda sin resultados); campo `type="password"`, `autocomplete="new-password"`, desactivado sin almacenamiento (`:112-113`) |
| Menú solo Manager | `NavMenu.razor:41` dentro del bloque Manager; guarda `Web_LinksTheAssistantConfigurationOnlyInTheManagerBlock...` |
| Límites | Cuerpo 8 KiB (`AssistantConfigurationEndpoints.cs:14,33`); 3 pruebas por minuto por `sub` |

Sin hallazgos de seguridad.

## 6. Incidencias

### CRITICAL
Ninguna.

### WARNING
- W-1 `tasks.md`: 0.1, 0.2 y 7.6 sin marcar (y 8.5 con «PARADA PENDIENTE») aunque están cumplidas o resueltas. Corregir al archivar.
- W-2 `.env.example:54` dice «Vacío = todos los admitidos» para `Ai__AllowedProviders`; el comportamiento especificado (DD-4, README:170 y 237) es que sin la variable solo está permitido el proveedor del entorno. Documentación engañosa en un archivo de entrega. Corregir el comentario.
- W-3 `design.md` DD-3 (tabla): Anthropic figura con «valor por defecto del SDK (no se fija nada)»; el código fija `ProviderEndpoints.Anthropic` (PR 4.1) y no consta como desviación en §14. Actualizar el diseño.
- W-4 Ollama 400 clasificado `unreachable` (la spec dice 400/422 = `rejected`). Declarado (§14, ADR 0019, V-3). Aceptado; PARCIAL para Ollama.
- W-5 DTO numéricos `int?`: un valor no entero recibe el 400 general sin mapa por campo, mientras la spec pide errores por campo. Declarado (§14). Aceptado hasta publicar la API a terceros.
- W-6 `DELETE` sin token del cliente: cumple la spec literalmente (409 si cambió entre la lectura y el borrado en el servidor), pero un Manager con vista obsoleta puede restablecer sin querer lo guardado por otro; riesgo aceptado y avisado en la Web.

### SUGGESTION (seguimientos menores abiertos)
- S-1 (media) `AssistantSettingsPoller.PollCompleted` (evento público, `AssistantSettingsPoller.cs:29,101`) se invoca en el `finally` de `PollAsync`; un suscriptor que lance escaparía del bucle de `ExecuteAsync` y, con el comportamiento por defecto de `BackgroundService` (`StopHost`), detendría el host. Hoy solo lo suscriben pruebas. Proteger cada suscriptor con `try/catch` o hacerlo `internal` con `InternalsVisibleTo`.
- S-2 (baja) ADR 0019 (línea 18) y el comentario de la migración afirman que el trigger frena al propietario de la tabla: es cierto para `UPDATE`, `DELETE` y `TRUNCATE`, pero el propietario puede hacer `ALTER TABLE ... DISABLE TRIGGER` o `DROP TRIGGER`. La sección «Consecuencias» ya lo reconoce («sin retirar antes el trigger»); matizar la frase de la decisión.
- S-3 (baja) `apply-progress.md`: la cabecera (línea 3) sigue en «PR 1 y PR 2»; 8.4 (línea 560) cita `2ba6b73`, commit que editó el ADR 0018 y quedó superado por `99b159b` y `195138d` (ADR 0019); «No cerrado» (líneas 600-605) conserva 7.6 y la parada de verify como pendientes.
- S-4 (baja) Falta una prueba de integración con PostgreSQL del truncado de `updatedAt` con resto de ticks (hoy solo `SaveAssistantSettingsUseCaseTests.TheSaveInstant_IsTruncatedToMicroseconds...` con reloj falso; el IT `Put_Valid_Returns200WithTheSameBodyAsGet...` solo lo detecta si el reloj real deja resto).
- S-5 (baja) `AssistantConnectionTestRequest` repite el `PrintMembers` de `AssistantConfigurationRequest` (`AssistantConfigurationRequest.cs:134-139` y `152-157`); extraer un ayudante común.
- S-6 (baja) `AssistantSettingsStore.CommitSaveAsync(row, creating, ...)` recibe un booleano (`AssistantSettingsStore.cs:119`); preferir dos métodos o un enum.
- S-7 (baja) README: `Ai__AllowedProviders` se describe pero sin ejemplo de formato (`Anthropic,OpenAI`), ni se dice que no distingue mayúsculas, que admite espacios, ni que `None` o `GitHubModels` impiden el arranque.

## 7. Verificación manual 7.6 (Chrome, hecha por el orquestador el 2026-10-20 contra `tt-e2e`, ya apagado)

Resultado: OK. Registrado a partir del informe del orquestador (no re-ejecutado en esta fase).

- Manager (Marta Ejemplo) ve el enlace «Config. asistente» y los dos avisos (datos personales a un tercero, alcance a toda la empresa); selector Anthropic/OpenAI/Ollama/Deshabilitado; orígenes `env`/guardado visibles.
- Guardar con Ollama + `llama3.2` + clave inventada: «Configuración guardada» con «Última modificación ... por Marta Ejemplo»; campo de clave vacío y «Hay una clave configurada»; la clave no aparece en el HTML, el texto ni `localStorage`.
- Employee (Ana): sin enlace en el menú; por URL directa «No tiene permiso para ver esta página.».
- No comprobado por navegador: valor de `sessionStorage` y campo de contraseña por script (bloqueados por la herramienta) ni los estados de error por la interfaz; cubiertos por E2E de API y por `AssistantConfigurationFormTests`.
- E2E de API de 24 casos OK con todo fusionado: auditoría inmutable por trigger, cifrado en base de datos sin la clave en claro, recarga en caliente, sin la clave en registros.

## 8. Resultado

PASS WITH WARNINGS. Siguiente fase recomendada: `sdd-archive`, tras corregir (o aceptar explícitamente) W-1 a W-3 y marcar 0.1, 0.2, 7.6 y 8.5 en `tasks.md`. `openspec/config.yaml` sigue sin editar (pendiente de aprobación del usuario, tarea 8.3) y la custodia de la clave maestra en Railway sigue como pregunta abierta para el product owner (design §13, ADR 0019).
