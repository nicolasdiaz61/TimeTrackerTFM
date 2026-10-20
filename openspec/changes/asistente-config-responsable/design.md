# Diseño: asistente-config-responsable

Fecha: 2026-10-18 · Fase: sdd-design · Modo: openspec · Base: `proposal.md`, `exploration.md`, las dos specs de este cambio, `AGENTS.md`, el diseño de `asistente-ia` (estilo y numeración) y el código vigente de `src/` y `tests/`.

Convención: **DD-n** = decisión de diseño; **[V-n]** = comprobación que apply hace antes de dar un paso por bueno (sección 12); **E-n** = edición propuesta a las specs (sección 11). Los DD de este documento son independientes de los DD de `asistente-ia`.

Verificación de paquetes hecha en esta fase (caché NuGet local, sin red): `Anthropic` 12.58.0 y `OllamaSharp` 5.5.0 están restaurados y se inspeccionaron sus XML y metadatos; `Microsoft.Extensions.AI.OpenAI` 10.10.0 y `OpenAI` **no están en la caché** y esta fase no dispone de acceso web, así que el comportamiento del adaptador OpenAI queda sin confirmar y se resuelve con un plan de contingencia y una tarea de spike (DD-12).

## 1. Enfoque técnico

Enfoque 3 de la exploración: la configuración del asistente pasa a ser un registro único en PostgreSQL (`assistant_settings`) que se combina con el entorno (`Ai__*`) para obtener la configuración efectiva. Application gana el modelo de configuración, sus reglas de validación, la resolución fila-sobre-entorno y los casos de uso, todo detrás de puertos y sin paquetes de IA. Infrastructure aporta la persistencia (EF Core, `xmin`), el cifrado AES-256-GCM, el catálogo de proveedores con URL fijas, el adaptador compatible con OpenAI, el cambio en caliente de la instantánea del cliente, el sondeo de versión y la prueba de conexión. Api expone los cuatro endpoints bajo la política Manager. Web añade `/asistente/configuracion`. El chat (`POST /api/asistente`) no cambia de contrato.

```
Web /asistente/configuracion ──> Api /api/asistente/configuracion  [Autenticación -> Autorización Manager -> (RateLimiter prueba)]
                                   │
                                   ├ GET  ─> GetAssistantSettingsUseCase ──┐
                                   ├ PUT  ─> SaveAssistantSettingsUseCase ─┤ (Application)
                                   ├ DELETE ─> ResetAssistantSettingsUseCase ┤  reglas + resolver (puro)
                                   └ POST prueba ─> TestAssistantConnectionUseCase ┘
                                              │ puertos
        IAssistantSettingsStore ── EF/xmin ──> assistant_settings + assistant_settings_audit   (Infrastructure)
        IAssistantCredentialProtector ───────> AES-256-GCM con clave maestra del entorno
        IAssistantEnvironment ───────────────> IOptions<AiOptions> (Ai__*)
        IAssistantRuntimeReloader ───────────> AssistantSettingsLoader ──> AssistantChatClient.Swap(snapshot)
        IAssistantConnectionTester ──────────> cliente temporal, categorías fijas
        AssistantSettingsPoller (BackgroundService, ~30 s) ──> xmin distinto? ──> AssistantSettingsLoader
Chat: ChatAssistantEngine ── lee UNA vez AssistantChatClient.Current (pipeline + opciones + versión) por petición
```

Secuencia de guardado y recarga:

```
Manager     Endpoint PUT     SaveUseCase          Store(EF)        Protector     Reloader/Loader     AssistantChatClient
  |--PUT------>|--ExecuteAsync->|                     |                |                |                    |
  |            |                |--valida (reglas)--->|                |                |                    |
  |            |                |--Protect(clave)---------------------->|                |                    |
  |            |                |--SaveAsync(fila, tokenEsperado, auditoría)->|          |                    |
  |            |                |        UPDATE ... WHERE xmin=@token (+INSERT auditoría, misma transacción)     |
  |            |                |<--Saved(nuevoToken) | Conflict (409)  |                |                    |
  |            |                |--ReloadAsync------------------------------------------>|--Swap(instantánea)->|
  |<--200 (estado nuevo)--------|                                                          (anterior: liberación diferida)
```

## 2. Decisiones técnicas

### DD-1. Capas, puertos y dónde vive cada pieza

| Capa | Contenido nuevo | Notas |
|---|---|---|
| Domain | Sin cambios | No hay invariantes del negocio de fichajes; evita arrastrar la exigencia del 100 % de cobertura a configuración operativa (precedente: `asistente-ia` no tocó Domain). |
| Application (`Assistant/Configuration`) | `AssistantProvider` (enum: `None`, `Anthropic`, `OpenAI`, `Gemini`, `Ollama`), `AssistantEffort` (`Low`, `Medium`, `High`), `AssistantProviderTraits` (requiere clave, admite esfuerzo, requiere endpoint de entorno), `AssistantSettings` (registro guardado), `EffectiveAssistantSettings` + `SettingSource`, `AssistantSettingsRules`, `AssistantSettingsResolver`, `AssistantCredential` (envoltorio con `ToString()` redactado), `EncryptedCredential`, comandos/resultados y cuatro casos de uso | Puro, sin paquetes. Cobertura objetivo >= 80 %. |
| Puertos (Application) | `IAssistantSettingsStore`, `IAssistantCredentialProtector`, `IAssistantEnvironment`, `IAssistantRuntimeReloader`, `IAssistantConnectionTester` | `ICurrentUser` y `TimeProvider` existentes se reutilizan para quién y cuándo. |
| Infrastructure | `AesGcmCredentialProtector`, `AssistantSettingsStore` + entidades/configuraciones EF, `ProviderEndpoints` (URL fijas), `IChatClientFactory` (+ `ChatClientFactory.Create` estático conservado), `AssistantSnapshot`, `AssistantChatClient` (soporte de `Swap`), `AssistantSettingsLoader`, `AssistantSettingsPoller`, `AssistantConnectionTester`, `ConnectionFailureClassifier`, `ConfigurationAssistantEnvironment` | Los paquetes de IA (`Microsoft.Extensions.AI.OpenAI`, `OpenAI`) solo aquí (`DependencyRuleTests`). |
| Api | `AssistantConfigurationEndpoints`, DTO de petición/respuesta, política `assistant-config-test`, registro del poller | Composition root. |
| Web | Página, cliente HTTP, modelo del formulario | Sin reglas de negocio; el servidor valida. |

`AiProvider` (Infrastructure, usado por `AiOptions`) se conserva por compatibilidad de código fuente (79 usos en tests) y gana `OpenAI` y `Gemini` sin renumerar (`None`=0, `Anthropic`=1, `Ollama`=2, `OpenAI`=3, `Gemini`=4). Application usa `AssistantProvider` con los mismos nombres; un test fija la correspondencia por nombre. Alternativa descartada: mover el enum a Application (rompería los `using` de los tests sin aportar nada).

### DD-2. Agregado de configuración y validación

`AssistantSettings` es un registro inmutable: `Provider`, `Model?`, `TimeoutSeconds?`, `MaxOutputTokens?`, `Effort?`, `EncryptedCredential?`, `UpdatedBy`, `UpdatedAt`, `Version` (token). Los campos `Model`, `TimeoutSeconds` y `MaxOutputTokens` son **anulables**: `null` significa «usar el valor del entorno». Así el origen por valor (`env`/`stored`) es real. El `PUT` es un reemplazo completo (los campos omitidos vuelven a `null`); la clave es la única excepción (omitida = conservar).

`AssistantSettingsRules.Validate(candidate, environment)` es una función pura que devuelve `IReadOnlyList<FieldError(Field, Message)>` con mensajes en español que nombran el campo y no incluyen valores:

| Campo | Regla |
|---|---|
| `provider` | Nombre del catálogo (sin distinguir mayúsculas) y permitido (DD-4), o `None`. Ollama exige además `Ai__Endpoint` en el entorno. |
| `model` | Si se envía: 1-100 caracteres y `^[A-Za-z0-9._:/@-]+$` (ASCII). Vacío y no nulo = error. Con proveedor distinto de `None` debe resolverse a un valor (propio o de entorno). |
| `timeoutSeconds` | 5-55 si se envía. |
| `maxOutputTokens` | 64-4096 si se envía. |
| `effort` | Nulo o `low`/`medium`/`high` (sin distinguir mayúsculas). Solo si el proveedor lo admite (DD-12); con `None`, 400. |
| `credential` | Opcional. Si el proveedor requiere clave, tras el guardado debe existir una (nueva, guardada del **mismo** proveedor, o de entorno si el proveedor es el del entorno). Cambiar de proveedor descarta la clave guardada anterior. Con proveedor `None` una clave nueva se rechaza con error en `credential`. Rechazada si no hay clave maestra (DD-7). |
| `concurrencyToken` | Obligatorio; cadena numérica (`"0"` = sin fila). |

El juego de caracteres del modelo es seguro porque el modelo viaja en el cuerpo JSON de la petición al proveedor, nunca se concatena en una URL ni en una ruta. Los límites numéricos coinciden con los de `AiOptionsValidator` (una única fuente: constantes en Application que el validador de Infrastructure reutiliza).

### DD-3. Catálogo de proveedores con URL fijas

| Proveedor | Adaptador | Base | Clave | Esfuerzo (DD-12) |
|---|---|---|---|---|
| Anthropic | `AnthropicClient{ApiKey, MaxRetries=1, Timeout}.AsIChatClient(model)` (existente) | Valor por defecto del SDK (no se fija nada) | Sí | Sí |
| OpenAI | `new OpenAIClient(new ApiKeyCredential(key), options).GetChatClient(model).AsIChatClient()` | Valor por defecto del SDK (`Endpoint` sin asignar) | Sí | Sí (pendiente de spike) |
| Gemini | Igual que OpenAI con `OpenAIClientOptions.Endpoint` | `https://generativelanguage.googleapis.com/v1beta/openai/` constante en `ProviderEndpoints` | Sí | Sí (pendiente de spike) |
| Ollama | `OllamaApiClient(new Uri(Ai:Endpoint), model)` (existente) | Solo entorno | No | Sí (pendiente de spike) |

GitHub Models (servicio retirado el 2026-07-30) y OpenCode Zen (varias APIs por modelo) quedan fuera; ningún valor de enum, ni cadena de URL, ni documentación los menciona como opción. Reglas SSRF: (a) el único `new Uri(...)` de Infrastructure/Assistant está en `ProviderEndpoints` y `ChatClientFactory` (guarda de arquitectura, sección 9); (b) ninguna entidad guardada contiene un campo de URL; (c) el DTO del `PUT` rechaza propiedades desconocidas (E-7). El cliente OpenAI usa un reintento (`RetryPolicy` de un intento) y `NetworkTimeout` = `TimeoutSeconds`, como el resto [V-2]. Alternativa descartada: URL base configurable por el Manager (exfiltración de la clave y SSRF).

### DD-4. Semántica de `Ai__AllowedProviders`

Lista separada por comas de nombres del catálogo (sin distinguir mayúsculas, con recorte y sin duplicados). Resolución de la lista **seleccionable**:

1. Con la variable definida: sus elementos. Un nombre fuera del catálogo (incluidos `None`, `GitHubModels`, `OpenCodeZen`) **falla el arranque** con un mensaje que contiene `Ai:AllowedProviders` (AGENTS §6, fallar rápido).
2. Sin la variable: solo el proveedor del entorno, si es un proveedor real; si el entorno es `None`/vacío, el conjunto queda vacío.
3. `None` (deshabilitar) siempre está permitido y no figura en la lista devuelta.
4. `Ollama` solo es seleccionable si hay `Ai__Endpoint`; si está en la lista sin endpoint, se excluye y se registra un aviso (no es un fallo de arranque).

Efecto: el entorno por defecto es seguro (el Manager no puede cambiar de proveedor ni enviar datos a un tercero distinto del que el product owner eligió). El product owner abre más opciones editando la variable. Una fila guardada cuyo proveedor deja de estar permitido no se aplica (se trata como fila inválida, DD-11) y `GET` lo refleja con `applied=false`.

### DD-5. Configuración efectiva (fusión)

`AssistantSettingsResolver.Resolve(env, stored?)` es puro y devuelve valores + origen por valor:

| Valor | Regla | Origen |
|---|---|---|
| `provider` | Fila existe: su proveedor (incluido `None`); si no, el del entorno | `stored` / `env` |
| `model`, `timeoutSeconds`, `maxOutputTokens` | Fila no nula > entorno (`Ai__*` o su valor por defecto) | por valor |
| `effort` | Fila > `Ai__Effort` (opcional) > sin valor | `stored` / `env` |
| clave | Fila (si descifra y el proveedor guardado coincide) > `Ai__ApiKey` solo si el proveedor efectivo es el del entorno > ninguna | `stored` / `env` / ninguna |
| endpoint (Ollama) | Solo `Ai__Endpoint`; nunca de la fila | `env` |

`credentialConfigured` es `true` si hay clave efectiva utilizable. La resolución no descifra: el resolver recibe un booleano `storedCredentialUsable` del cargador. El resolver pertenece a Application para que `GET`, `PUT` (validación contra el estado final) y la prueba compartan la misma lógica que el cargador de runtime. Sin fila, o con la tabla ausente, el resultado es exactamente el entorno actual.

### DD-6. Cifrado: AES-256-GCM con clave maestra versionada por entorno

**Elección**: `System.Security.Cryptography.AesGcm` (etiqueta de 16 bytes, nonce aleatorio de 12 bytes por cifrado) con una clave maestra de 32 bytes en Base64 leída del entorno (`Ai__MasterKeys__{version}`, `Ai__ActiveMasterKeyVersion`).

| Opción | Ventajas | Inconvenientes | Decisión |
|---|---|---|---|
| AES-256-GCM + clave maestra de entorno | Sin estado adicional; rotación explícita y verificable; AAD propio; sin paquetes; se prueba con vectores propios; la clave maestra vive fuera de la base de datos | Hay que custodiar la clave maestra en Railway (se hace ya con `Jwt:SigningKey`) | **Elegida** |
| ASP.NET Data Protection | API integrada, rotación automática | Exige persistir el anillo de claves (Railway tiene sistema de archivos efímero: habría que guardarlo en la misma base de datos, donde queda junto al secreto que protege salvo certificado adicional); formato opaco y versión de clave implícita; la rotación no es auditable; difícil de probar con claves conocidas; un anillo perdido deja todas las claves ilegibles | Descartada |

Diseño de la pieza (`AesGcmCredentialProtector`, singleton): decodifica y valida las claves maestras **una vez** al construirse (cada versión: Base64 válido de exactamente 32 bytes; versión entera 1-32767). Una entrada inválida deja solo esa versión como «no disponible» (nunca se imprime su valor). Estado expuesto: `IsAvailable` = la versión activa existe y es válida. Almacenamiento por registro: `credential_ciphertext` (texto cifrado || etiqueta), `credential_nonce` (12 bytes) y `credential_key_version` (smallint). **No** se guardan los últimos 4 caracteres (menos superficie, sin riesgo con claves cortas; la spec solo lo permite como máximo).

**AAD (datos autenticados asociados)**: la cadena UTF-8 `tt-assistant-credential|v1|{provider}|{keyVersion}`, con el proveedor en su forma canónica. Efecto: un texto cifrado no puede reutilizarse con otro proveedor ni con otra versión de clave sin que falle la autenticación (un atacante con escritura en la base de datos no puede redirigir una clave guardada hacia otro proveedor ni degradar la versión). La fila es única, por lo que no se incluye el identificador de fila.

### DD-7. Rotación, fallo cerrado y mensajes

- **Rotación**: se añade `Ai__MasterKeys__2`, se cambia `Ai__ActiveMasterKeyVersion=2` y se redespliega. Los registros con versión 1 siguen descifrándose mientras la 1 exista. Un `PUT` que conserva la clave (sin `credential`) **recifra con la versión activa** (descifra y vuelve a cifrar con nonce nuevo); así un guardado sin cambios sirve de reenvuelto. La versión 1 se retira de la variable solo tras confirmar con `GET`/SQL que ninguna fila la usa (la fila es única, así que se comprueba `credential_key_version`). No hay recifrado masivo (YAGNI: una sola fila).
- **Fallo cerrado**: sin versión activa válida, `Protect` lanza `CredentialProtectionUnavailableException` y el `PUT` con `credential` responde `400` con el campo `credential` y un mensaje en español sin valores; no se escribe nada. Un `PUT` sin clave nueva sigue permitido (no necesita cifrar, salvo el recifrado oportunista, que se omite si no hay clave activa). Un registro cifrado no se descifra si falta su versión o falla la autenticación: el cargador trata la fila como inválida (DD-11). **El arranque no falla** por la clave maestra (E-5): el asistente sigue con el entorno; al arrancar se registra una línea informativa/aviso «protección de claves disponible/no disponible» sin valores. `GET` incluye `credentialStorageAvailable` para que la Web explique por qué el campo está desactivado.
- Los mensajes de error y los registros nunca incluyen la clave maestra, la clave del proveedor ni su forma cifrada; las excepciones de cifrado se registran solo por nombre de tipo.

### DD-8. Esquema de base de datos y concurrencia

Migración **`AddAssistantSettings`** (la marca de tiempo la asigna `dotnet ef`, posterior a `20261014212440_AddTimeEntryType`); `Down` elimina ambas tablas. Solo añade tablas.

`assistant_settings` (singleton):

| Columna | Tipo | Restricción |
|---|---|---|
| `id` | smallint | PK, `ck_assistant_settings_singleton CHECK (id = 1)` |
| `provider` | varchar(16) | NOT NULL, `CHECK provider IN ('None','Anthropic','OpenAI','Gemini','Ollama')` |
| `model` | varchar(100) | NULL, `CHECK char_length(model) >= 1` |
| `timeout_seconds` | integer | NULL, `CHECK BETWEEN 5 AND 55` |
| `max_output_tokens` | integer | NULL, `CHECK BETWEEN 64 AND 4096` |
| `effort` | varchar(8) | NULL, `CHECK IN ('low','medium','high')` |
| `credential_ciphertext`, `credential_nonce`, `credential_key_version` | bytea, bytea, smallint | `CHECK` de «los tres nulos o los tres presentes» y `octet_length(credential_nonce) = 12` |
| `updated_by` | uuid | NOT NULL (sin FK: la auditoría no depende del ciclo de vida del usuario) |
| `updated_at` | timestamptz | NOT NULL |
| `xmin` | xid | Token de concurrencia, igual que `time_entries` (`HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken()`) |

Token de API: `xmin` en decimal; `"0"` significa «sin fila» (un `xmin` real nunca es 0). Guardado: si el token es `"0"` se inserta con `id = 1` y una violación de unicidad (`23505`) o una fila existente da `409`; si no, se actualiza con el `xmin` esperado como valor original y `DbUpdateConcurrencyException` (o fila inexistente) da `409` sin modificar nada. La fila y la auditoría se guardan en **una** transacción (`SaveChanges` único). `DELETE` es idempotente (`204` con o sin fila) pero no ciego: el caso de uso lee la fila y borra con su `xmin` como valor esperado (`DeleteAsync(expectedToken, audit)` devuelve `Deleted`, `NotFound` o `Conflict`); si otro Manager guardó entre la lectura y el borrado, no se borra ni se audita y responde `409` (sin token en la petición: lo captura el servidor). La auditoría `Reset` solo se inserta si se borró la fila; una fila ya borrada es `NotFound` (mismo estado final, `204`). La lectura captura `PostgresException` con `SqlState = 42P01` (tabla inexistente) y devuelve «sin fila» (escenario «Tabla ausente»); cualquier otra excepción se propaga.

### DD-9. Auditoría de solo inserción

`assistant_settings_audit`: `id` bigint identidad (PK), `occurred_at` timestamptz, `actor_id` uuid, `action` varchar(16) (`Saved`/`Reset`, con CHECK), `credential_changed` boolean, `previous` jsonb NULL, `current` jsonb NULL (índice por `occurred_at` descendente). Los JSON contienen solo valores no secretos: `provider`, `model`, `timeoutSeconds`, `maxOutputTokens`, `effort`, `credentialConfigured`. «Cambio de clave» = `credential_changed = true` (puesta, sustituida o descartada por cambio de proveedor), sin su valor ni su forma cifrada. Garantía de solo inserción: el puerto expone únicamente `Add` (ninguna operación de actualización o borrado), una prueba de arquitectura busca `Remove`/`Update`/`ExecuteDelete`/`ExecuteUpdate` sobre la entidad, y `UpdatedBy` sale de `ICurrentUser` (token). Alternativa descartada por ahora: trigger de PostgreSQL que impida `UPDATE`/`DELETE` (defensa útil pero complica la limpieza de las pruebas de integración y la operación; se puede añadir después sin cambiar el modelo). La v1 no ofrece lectura de auditoría por API (YAGNI); se consulta por SQL.

### DD-10. Instantánea inmutable y recarga en caliente

`AssistantChatClient` conserva su constructor `(IChatClient? inner, AiOptions options)` (compatibilidad de código fuente) y su `IsEnabled`, y pasa a ser el **soporte** de una `AssistantSnapshot` inmutable: `Pipeline` (`IChatClient?`), `Options` (copia defensiva de `AiOptions` con los valores efectivos), `Version` (`"0"` = entorno; si no, el token de la fila).

- `Current`: lectura con `Volatile.Read` de la instantánea vigente. `ChatAssistantEngine` la lee **una vez** al empezar la petición y usa de ahí en adelante su pipeline y sus opciones (modelo, tiempo máximo, tokens, esfuerzo); deja de leer `IOptions<AiOptions>` (único cambio de firma: el constructor del motor pierde ese parámetro; afecta solo a las pruebas del motor). `RequestsPerMinute` y la cuota diaria siguen leyéndose del entorno.
- `Swap(inner, options, version)`: construye la instantánea nueva (el pipeline con `AssistantChatPipeline.Build`), la publica con `Interlocked.Exchange` y devuelve la anterior.
- **Liberación diferida**: la anterior se libera tras un periodo de gracia de 120 s mediante un temporizador de `TimeProvider` (`CreateTimer`, probable con `FakeTimeProvider`). Justificación: toda petición está acotada por `TimeoutSeconds` <= 55 s (el motor ya enlaza un `CancellationTokenSource` con ese plazo), así que 120 s cubre con holgura cualquier petición iniciada antes del cambio sin llevar contadores de uso. Alternativa descartada: recuento de referencias por petición (más piezas y más puntos de fallo sin beneficio, dado el tope temporal). `Dispose()` libera la vigente y las pendientes. Varios cambios seguidos no filtran recursos (cada instantánea retirada programa su propia liberación; la lista de pendientes se protege con un `lock`).
- Escrituras concurrentes (guardado local y sondeo): el cargador serializa con un `SemaphoreSlim(1)` y siempre lee la fila fresca, de modo que no puede publicar una versión más antigua.

### DD-11. Cargador, sondeo y comportamiento ante fallos

`AssistantSettingsLoader.LoadAsync` (singleton con ámbito propio para el `DbContext`): lee la fila, resuelve la configuración efectiva (DD-5), descifra, valida con las mismas reglas que el `PUT` más las del entorno (modelo presente, clave si el proveedor la requiere, endpoint de Ollama, proveedor permitido), construye el cliente con `IChatClientFactory` y hace `Swap`. Si algo falla, **no** hay `Swap`: se conserva la última configuración válida y se registra un aviso con la categoría (`invalid`, `undecryptable`, `not-allowed`) y la versión, sin secretos, **una vez por versión rechazada**.

`AssistantSettingsPoller : BackgroundService` (registrado por Api, no por `AddAssistant()`): cada `Ai__SettingsPollSeconds` (por defecto 30; 1-300, para poder acelerarlo en pruebas) lee solo la versión (`SELECT xmin` o «sin fila»), la compara con `Current.Version` y con la última versión rechazada, y llama al cargador si difiere. Una caída de la base de datos se captura, se registra un aviso **una vez** por racha de fallos (solo el nombre del tipo de la excepción) y se conserva la configuración; al recuperarse se registra una línea informativa.

- **Primera carga tras las migraciones**: las migraciones se aplican en `StartingAsync` del `DatabaseMigrationHostedService` y todos los `StartAsync` van después. El poller sobrescribe `StartAsync` para hacer una primera carga acotada (10 s, errores capturados) antes de arrancar el bucle. Hasta entonces la instantánea inicial es la del entorno (construida por la fábrica de DI como hoy). Ventana residual aceptada: si Kestrel empezara a escuchar antes de que acabe la primera carga, un chat podría usar el entorno unos milisegundos; el orden real de arranque se comprueba con una prueba de integración [V-6].
- **Pruebas**: el poller es silencioso ante errores (sin ruido con `db.invalid`) y reemplazable: `ApiFactory` sin base de datos lo elimina del contenedor (`ImplementationType == typeof(AssistantSettingsPoller)`), de modo que no hay conexión en segundo plano; las pruebas con PostgreSQL real lo conservan con `Ai__SettingsPollSeconds=1`.
- **Escritura local**: tras confirmar la transacción, el caso de uso llama a `IAssistantRuntimeReloader.ReloadAsync`. Si la recarga falla, el guardado ya está confirmado: se responde `200`, se registra el aviso y el sondeo reintenta.
- Consistencia entre instancias: eventual, <= `SettingsPollSeconds`; Railway ejecuta una instancia (ADR 0017).

### DD-12. Esfuerzo de razonamiento: mapeo por adaptador y contingencia

**Qué se confirmó en esta fase (caché local, XML y metadatos)**

| Adaptador | Resultado | Fuente |
|---|---|---|
| Anthropic 12.58.0 | **Confirmado.** `AsIChatClient(model, maxOutputTokens?, AnthropicThinkingMode = Adaptive)` traduce `ChatOptions.Reasoning`: en modo `Adaptive` envía `thinking.type=adaptive` y `ReasoningOptions.Effort` como `output_config.effort`; sin esfuerzo (pero con `Reasoning` definido) activa el pensamiento con el esfuerzo por defecto del modelo; `ReasoningEffort.None` envía `thinking.type=disabled` (los modelos que siempre piensan responden 400). El modo `Extended` (`budget_tokens`) es para modelos anteriores | `Anthropic.xml` (docs de `AsIChatClient` y `AnthropicThinkingMode`) |
| OllamaSharp 5.5.0 | **Parcial.** El ensamblado hace referencia a `ChatOptions.Reasoning` (el adaptador `IChatClient` lo lee) y `ChatRequest.Think` acepta `ThinkValue` booleano o nivel `low`/`medium`/`high`; el XML no documenta la traducción exacta ni qué hace con `None` | `OllamaSharp.xml`, metadatos de `OllamaSharp.dll` |
| `Microsoft.Extensions.AI.OpenAI` 10.10.0 (Chat Completions) | **Sin confirmar.** Paquete y SDK `OpenAI` ausentes de la caché y sin acceso web en esta fase | — |

**Decisión**

1. El esfuerzo se fija **solo** cuando el valor efectivo no es nulo: `ChatOptions.Reasoning = new ReasoningOptions { Effort = ... }` con `Low`/`Medium`/`High` (los únicos valores aceptados; `None`, `minimal`, `xhigh`, `max` no se ofrecen porque varían por modelo y producen 400). Con esfuerzo nulo, `Reasoning` queda **sin definir** (no se envía nada; evita el 400 de los modelos que siempre piensan). Una única función, `AssistantChatPipeline.CreateChatOptions`, lo aplica al chat **y** a la prueba de conexión, de modo que la prueba detecta lo que rechazaría el chat.
2. Capacidad **por proveedor**, no por modelo (el servidor no conoce las capacidades de un modelo en texto libre): `AssistantProviderTraits.SupportsEffort` = `true` para Anthropic, OpenAI, Gemini y Ollama **sujeto al spike**. Un `effort` con un proveedor que no lo admite → `400` (E-3). Un modelo que no admite el esfuerzo enviado devuelve 400 del proveedor, que la prueba clasifica como `rejected` (E-6) y el chat como `503` genérico.
3. **Contingencia si el spike no confirma OpenAI Chat Completions (o Ollama)**: para OpenAI/Gemini, asignar `ChatOptions.RawRepresentationFactory = _ => new ChatCompletionOptions { ReasoningEffortLevel = ... }` (propiedad del SDK `OpenAI`, nombre a confirmar [V-2]); para Ollama, `RawRepresentationFactory` devolviendo un `ChatRequest { Think = new ThinkValue("low"|"medium"|"high") }`. Si tampoco funciona, `SupportsEffort = false` para ese proveedor (la Web oculta el campo y el servidor lo rechaza con 400); ninguna otra pieza cambia porque la capacidad es un dato del catálogo.
4. **Spike (primera tarea del PR 4, con test en rojo)**: para cada adaptador, construir el cliente real contra un `HttpMessageHandler` de grabación (`OpenAIClientOptions.Transport`, `AnthropicClient.HttpClient`, `OllamaApiClient(HttpClient, model)`) y comprobar que el cuerpo contiene `reasoning_effort` (OpenAI y Gemini, con el endpoint de Gemini), `output_config.effort` (Anthropic) y `think` (Ollama) con los tres niveles, y que **no** contiene ninguno con esfuerzo nulo. No hace llamadas de red. El resultado se anota en este documento (sección 12, V-2) igual que `asistente-ia` anotó V-3.
5. Riesgos conocidos y aceptados: modelos de OpenAI que exigen Responses para llamar a herramientas (la v1 usa solo Chat Completions; la prueba de conexión no usa herramientas y puede dar `ok` aunque el chat falle con ese modelo; se documenta en el ADR y en la ayuda de la página); bloques de pensamiento de Anthropic en el bucle de herramientas (el adaptador los reenvía; se verifica a mano con una clave real, prueba opcional fuera de la suite).

### DD-13. Prueba de conexión

`POST /api/asistente/configuracion/prueba`, cuerpo opcional con los mismos campos que el `PUT` salvo el token (`provider`, `model`, `timeoutSeconds`, `maxOutputTokens`, `effort`, `credential`). Los campos **ausentes se completan con la configuración efectiva**; el cuerpo vacío prueba la efectiva (E-6). Reglas: las mismas de DD-2 (400 con errores por campo si el candidato no es válido o requiere una clave que no hay), sin token de concurrencia. La clave candidata solo se usa para esa prueba, nunca se guarda ni se registra. Si el candidato mantiene el proveedor efectivo y no trae clave, se usa la efectiva descifrada.

`IAssistantConnectionTester` (Infrastructure) construye un cliente **temporal** con `IChatClientFactory` (se desecha al terminar; no toca la instantánea ni la fila ni la auditoría), envía un único mensaje fijo sin datos del usuario («Responde solo: OK.»), sin herramientas, con `MaxOutputTokens = 64` y el esfuerzo del candidato, y un plazo de `min(timeoutSeconds, 15)` s. `ConnectionFailureClassifier` traduce la excepción a una categoría fija mirando el **estado HTTP** de la excepción del SDK correspondiente (o de `HttpRequestException`) y no su mensaje:

| Resultado | Condición | Mensaje fijo (resumen) |
|---|---|---|
| `ok` | Respuesta correcta | «Conexión correcta.» |
| `authentication` | 401/403 | «El proveedor rechazó la clave.» |
| `model-not-found` | 404 | «El proveedor no reconoce el modelo.» |
| `rejected` (nuevo, E-6) | 400/422 | «El proveedor rechazó los parámetros (modelo o esfuerzo no admitido).» |
| `timeout` | Plazo propio agotado | «El proveedor no respondió a tiempo.» |
| `unreachable` | Sin respuesta HTTP, 429, 5xx o cualquier otro fallo | «No se pudo contactar con el proveedor.» |

La respuesta es `200 { "result": "<categoría>", "message": "<texto fijo>" }` (el fallo del proveedor no es un fallo de la petición). Ni mensaje, ni URL, ni cabeceras, ni clave del proveedor salen en la respuesta ni en los registros (solo el nombre del tipo de la excepción y la categoría). Los nombres exactos de las excepciones de cada SDK (`ClientResultException` en `System.ClientModel` para OpenAI, la excepción HTTP de Anthropic, la de OllamaSharp) se confirman en el spike [V-3]. Límite de tasa: DD-15.

### DD-14. Endpoints, política y contrato JSON

Reutiliza `TeamEndpoints.ManagerPolicy`: la constante y el registro de la política se mueven a `Api/Security/AuthorizationPolicies` (`Manager`), y `AddTeam()`/`MapTeamEndpoints` la consumen desde allí; no se duplica la política ni se hace depender Assistant de Team. Todas las rutas cuelgan de `MapGroup("/api/asistente/configuracion").RequireAuthorization(Manager)` con `ProducesProblem(401/403)` y la etiqueta `AssistantConfiguration`. `POST /api/asistente` sigue siendo la única operación de `/api/asistente` (`OpenApiContractTests`).

| Operación | Éxito | Errores |
|---|---|---|
| `GET /api/asistente/configuracion` | `200` configuración | 401, 403 |
| `PUT /api/asistente/configuracion` | `200` mismo cuerpo que `GET` (token nuevo) | 400 (errores por campo), 401, 403, 409 |
| `DELETE /api/asistente/configuracion` | `204` | 401, 403, 409 |
| `POST /api/asistente/configuracion/prueba` | `200 { result, message }` | 400, 401, 403, 429 |

Nombres JSON (camelCase, E-2). `GET`/`PUT` respuesta: `provider`, `model`, `timeoutSeconds`, `maxOutputTokens`, `effort` (valores efectivos; `effort` puede ser nulo), `sources` (objeto con `provider`, `model`, `timeoutSeconds`, `maxOutputTokens`, `effort`, `credential`: `env`/`stored`; `credential` también `none`), `credentialConfigured`, `credentialStorageAvailable`, `allowedProviders` (elementos `{ provider, requiresCredential, supportsEffort }`), `concurrencyToken`, `updatedAt`, `updatedBy` (nombre visible o nulo), `applied` (la configuración guardada es la que usa esta instancia). `PUT` petición: `provider`, `model`, `timeoutSeconds`, `maxOutputTokens`, `effort`, `credential` (solo escritura), `concurrencyToken`. Las peticiones usan DTO con tipos de cadena (patrón de `LoginCommand`) para validar en Application y devolver mensajes en español, y llevan `[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]`: cualquier propiedad desconocida (`url`, `endpoint`, etc.) da `400` (E-7). Los `400` de validación usan `TypedResults.ValidationProblem` (errores por campo en español) y los de formato el `ProblemDetails` general. Cuerpo máximo: 8 KiB (`RequestSizeLimitAttribute`, `413`). La clave nunca aparece en ninguna respuesta; el campo se llama `credential` en la petición y `credentialConfigured` en la respuesta, de modo que la cadena `apikey` no entra en el contrato.

### DD-15. Límite de tasa de la prueba

Política `assistant-config-test` en `AddRateLimiting()`: ventana fija de 1 minuto, 3 permisos, partición por claim `sub` (igual que `assistant`), `QueueLimit = 0`; constante de código (los límites no son editables, fuera de alcance). Va tras la autorización (orden de `asistente-ia` DD-1), así que un anónimo recibe 401 sin consumir permisos y un Employee recibe 403 antes de contar. `OnRejected` elige un detalle propio por política: «Ha realizado demasiadas pruebas de conexión. Espere un minuto antes de volver a intentarlo.» con `Retry-After`. Cuentan todas las peticiones que llegan al endpoint, también las que acaban en 400.

### DD-16. Restricciones del contrato OpenAPI y de las guardas

`docs/api/openapi.json` se regenera y las pruebas de arquitectura verifican: (1) `/api/asistente` conserva solo `post`; (2) las rutas nuevas tienen exactamente las operaciones y respuestas de DD-14; (3) el documento no contiene (sin distinguir mayúsculas) `apikey` ni `Ai__`, ni `url`/`endpoint` como propiedad de los esquemas de configuración; (4) los esquemas exponen `credential` solo en la petición y `credentialConfigured` en la respuesta; (5) `Src_DoesNotHardcodeModelNames`: ningún nombre de modelo en `src/` (ni en la UI: los textos de ayuda y marcadores son genéricos; las URL, los textos de ejemplo y los nombres de proveedor `Gemini`, `OpenAI` no coinciden con el patrón `gemini-`, `gpt-`, etc., pero se comprueba al implementar [V-5]). Las descripciones XML o los atributos no deben contener la palabra `apikey` en ningún esquema.

### DD-17. Página Web y cliente

`Pages/AssistantConfiguration.razor` con `@page "/asistente/configuracion"` y `[Authorize(Roles = nameof(UserRole.Manager))]` (el rol del JWT, igual que `/equipo`); enlace en `NavMenu` dentro del bloque visible solo para Manager, junto a «Equipo». Un Employee que navega directamente ve «acceso denegado» (plantilla `NotAuthorized` de la ruta) sin formulario. Elementos: selector de proveedor (los `allowedProviders` más «Deshabilitado»), modelo (texto), timeout, tokens, esfuerzo (visible solo si `supportsEffort`), campo de clave de tipo contraseña (desactivado con aviso si `credentialStorageAvailable` es falso; vaciado tras guardar; muestra «clave configurada»), origen de cada valor, botones Guardar, «Probar conexión» y «Restablecer al entorno» (con confirmación), y avisos fijos: datos personales enviados a un tercero con la clave del Manager, y alcance a toda la empresa. Todo texto se pinta como texto plano (sin `MarkupString`), sin `localStorage`/`sessionStorage`. En `409` muestra el mensaje y ofrece recargar; en `400` muestra los mensajes por campo.

Código: `Web/Assistant/AssistantConfigurationApiClient` (usa `ApiRequest` con una sobrecarga de estados con detalle `{ BadRequest, Conflict, TooManyRequests }`, existente desde `asistente-ia`), `AssistantConfigurationDtos`, y `AssistantConfigurationForm` (clase pura y probable: estado editable, mapeo a la petición, limpieza de la clave, gestión de resultados). Sin reglas de negocio en la Web: los límites los valida el servidor; solo se usan `maxlength` y `min`/`max` HTML como ayuda. La página de chat `/asistente` y su cliente no cambian.

### DD-18. Opciones de entorno nuevas y validación

`AiOptions` gana `Effort` (opcional), `AllowedProviders` (cadena), `MasterKeys` (diccionario versión -> Base64), `ActiveMasterKeyVersion` (entero opcional) y `SettingsPollSeconds` (30). `AiOptionsValidator`: acepta los proveedores `OpenAI` y `Gemini` (con `Ai:ApiKey` y `Ai:Model` obligatorios cuando el proveedor del entorno es uno de ellos), valida `Ai:AllowedProviders` (DD-4) y `Ai:Effort`, y **no** valida las claves maestras (el arranque no falla por ellas). Los mensajes nombran la clave (`Ai:...`), nunca el valor. `.env.example` documenta las claves nuevas comentadas y sin valores reales (sin clave maestra). `DeliveryArtifactsTests` pasa a 18 ADR.

### DD-19. ADR 0018 — `docs/adr/0018-configuracion-del-asistente-por-el-responsable.md`

Cuatro secciones obligatorias. **Contexto**: la configuración solo por entorno obliga a redesplegar; el Manager debe poder aportar su propia clave. **Decisión**: configuración guardada en PostgreSQL con catálogo fijo y URL en servidor; fusión fila-sobre-entorno con `None` para deshabilitar y borrar fila para restablecer; `Ai__AllowedProviders`; AES-256-GCM con clave maestra versionada (y AAD); clave de solo escritura; auditoría de solo inserción; `xmin`; recarga en caliente con instantánea y sondeo; prueba de conexión. **Modifica** AGENTS §1 («proveedor y modelo por configuración») y §7 («cambio de proveedor solo mediante configuración `Ai:*`»: pasa a «configuración de entorno o configuración guardada por un Manager»), y **matiza el ADR 0017** (el proveedor de la nube puede recibir datos personales con una clave del Manager, no solo del product owner; el cupo y los límites siguen por entorno). **Consecuencias**: datos personales a un tercero bajo una clave que el Manager aporta (aviso en la UI, minimización de 0017); coste en la clave del Manager (presupuesto en el proveedor); gestión de la clave maestra; rol obsoleto en el JWT (30 min, aceptado); consistencia eventual; un guardado erróneo afecta a toda la empresa (prueba, restablecer, auditoría). **Alternativas**: solo entorno; URL escrita por el Manager; Data Protection; trigger de inmutabilidad; Responses API; GitHub Models y OpenCode Zen descartados. Los ADR no se reescriben: 0018 modifica/matiza; no se edita 0017.

## 3. Interfaces (firmas relevantes)

```csharp
// Application/Assistant/Configuration
public enum AssistantProvider { None, Anthropic, OpenAI, Gemini, Ollama }
public enum AssistantEffort { Low, Medium, High }
public enum SettingSource { Env, Stored, None }
public sealed record EncryptedCredential(byte[] Ciphertext, byte[] Nonce, short KeyVersion);

public interface IAssistantSettingsStore
{
    Task<AssistantSettings?> GetAsync(CancellationToken ct);                  // 42P01 => null
    Task<string> GetVersionAsync(CancellationToken ct);                       // "0" si no hay fila
    Task<SaveOutcome> SaveAsync(AssistantSettings next, string expectedToken, AuditEntry audit, CancellationToken ct); // Saved(token) | Conflict
    Task<DeleteOutcome> DeleteAsync(string expectedToken, AuditEntry audit, CancellationToken ct); // Deleted | NotFound | Conflict
}
public interface IAssistantCredentialProtector
{
    bool IsAvailable { get; }
    EncryptedCredential Protect(AssistantCredential plaintext, AssistantProvider provider);   // lanza CredentialProtectionUnavailableException
    bool TryUnprotect(EncryptedCredential stored, AssistantProvider provider, out string? plaintext);
}
public interface IAssistantEnvironment { AssistantEnvironmentDefaults Current { get; } }
public interface IAssistantRuntimeReloader { Task ReloadAsync(CancellationToken ct); }
public interface IAssistantConnectionTester { Task<ConnectionTestResult> TestAsync(AssistantConnectionCandidate candidate, CancellationToken ct); }

// Infrastructure/Assistant (internal salvo lo necesario para Api)
internal interface IChatClientFactory { IChatClient? Create(AiOptions options); }
internal sealed record AssistantSnapshot(IChatClient? Pipeline, AiOptions Options, string Version);
```

## 4. Cambios de archivos

| Archivo | Acción | Descripción |
|---|---|---|
| `docs/adr/0018-configuracion-del-asistente-por-el-responsable.md` | Crear | DD-19 |
| `AGENTS.md`, `.env.example`, `README.md`, `docker-compose.yml` (si aplica) | Modificar | §1/§7 de AGENTS; claves nuevas; guía de rotación y de configuración por el Manager |
| `src/TimeTracker.Application/Assistant/Configuration/*` | Crear | Modelo, reglas, resolver, puertos, casos de uso, resultados |
| `src/TimeTracker.Infrastructure/Assistant/AiOptions.cs`, `AiOptionsValidator.cs`, `AiProvider` | Modificar | DD-18, DD-1 |
| `src/TimeTracker.Infrastructure/Assistant/ChatClientFactory.cs` | Modificar | OpenAI y Gemini; `IChatClientFactory`; esfuerzo |
| `.../Assistant/ProviderEndpoints.cs`, `AssistantSnapshot.cs`, `AssistantSettingsLoader.cs`, `AssistantSettingsPoller.cs`, `AssistantConnectionTester.cs`, `ConnectionFailureClassifier.cs`, `ConfigurationAssistantEnvironment.cs`, `AesGcmCredentialProtector.cs` | Crear | DD-3, 6, 10, 11, 13 |
| `.../Assistant/AssistantChatClient.cs`, `ChatAssistantEngine.cs`, `AssistantChatPipeline.cs`, `AssistantServiceCollectionExtensions.cs` | Modificar | `Swap`/`Current`; motor sobre la instantánea; `Reasoning` en `ChatOptions`; registro |
| `.../Persistence/AssistantSettingsConfiguration.cs`, `AssistantSettingsAuditConfiguration.cs`, `AssistantSettingsStore.cs`, entidades, `TimeTrackerDbContext.cs`, migración `AddAssistantSettings` (+ `Designer`, snapshot) | Crear/Modificar | DD-8, DD-9 |
| `src/TimeTracker.Api/Assistant/AssistantConfigurationEndpoints.cs` y DTO | Crear | DD-14 |
| `src/TimeTracker.Api/Security/AuthorizationPolicies.cs`, `Team/TeamEndpoints.cs`, `RateLimitingServiceCollectionExtensions.cs`, `Program.cs` | Crear/Modificar | Política movida; política de la prueba; registro del poller y de los casos de uso |
| `docs/api/openapi.json` | Modificar | Regenerado |
| `Directory.Packages.props`, `TimeTracker.Infrastructure.csproj` | Modificar | `Microsoft.Extensions.AI.OpenAI` 10.10.0 |
| `src/TimeTracker.Web/Pages/AssistantConfiguration.razor`, `Assistant/AssistantConfiguration*.cs`, `Layout/NavMenu.razor`, `Program.cs`, `wwwroot/css/app.css` | Crear/Modificar | DD-17 |
| `tests/**` | Crear/Modificar | Sección 9 |

## 5. Flujo de datos de la configuración efectiva

```
Ai__* (entorno) ─────────────┐
assistant_settings (fila) ───┼─> Resolver (puro) ─> EffectiveAssistantSettings (valores + origen)
clave maestra ─ descifra ────┘                          │
                                                        ├─ GET/PUT/prueba (sin clave en claro hacia fuera)
                                                        └─ Loader ─> ChatClientFactory ─> Swap(instantánea) ─> Chat
```

## 6. Matriz de amenazas

N/A como rutas de shell, subprocesos o automatización de VCS: no existen. Las amenazas del cambio (SSRF, exfiltración de la clave, inyección por el modelo, abuso de coste, escalada por rol obsoleto) se tratan en DD-3, DD-6/7, DD-9, DD-13, DD-14 y DD-15. El rol obsoleto en el JWT tras una degradación (30 min) queda aceptado y documentado en el ADR 0018.

## 7. Migración y reversión

La migración solo añade tablas; el despliegue sin fila equivale al comportamiento actual. Restablecer (`DELETE`) devuelve al entorno al instante. Reversión de código: revertir los PR en orden inverso (la migración inversa elimina las dos tablas); sin `Ai__AllowedProviders` el comportamiento es el del entorno. El despliegue en Railway exige definir `Ai__MasterKeys__1` y `Ai__ActiveMasterKeyVersion=1` antes de que un Manager guarde una clave.

## 8. Riesgos

Esfuerzo no confirmado en OpenAI/Ollama (DD-12, spike); datos personales al proveedor con la clave del Manager; guardado erróneo que deshabilita el asistente (mitigado con prueba y restablecer); fila guardada no aplicada por proveedor retirado de la lista (`applied=false`); clave maestra perdida (las claves guardadas se vuelven ilegibles: el Manager las vuelve a introducir; el entorno sigue funcionando); modelos de OpenAI que solo admiten herramientas por Responses; `Microsoft.Extensions.AI.OpenAI` arrastra `OpenAI` >= 2.13.0 (posibles NU1608/NU1903) [V-1]; PR 3 y PR 6 al límite de 400 líneas.

## 9. Estrategia de pruebas

| Capa | Qué | Cómo |
|---|---|---|
| Application.Tests | Reglas (cada límite en sus extremos 4/5/55/56, 63/64/4096/4097; modelo vacío/101/`<script>`; esfuerzo `extreme`; proveedor no permitido/fuera de catálogo); resolver (fila > entorno por valor, `None` deshabilita, clave de entorno solo con el proveedor del entorno, cambio de proveedor descarta la clave); lista permitida (con y sin variable, Ollama sin endpoint); casos de uso con dobles (guardado, conservar clave, recifrado, 409, restablecer con y sin fila, auditoría sin secretos, prueba que no guarda); `AssistantCredential.ToString()` redactado | xUnit, dobles en memoria, `FakeTimeProvider`. Cobertura >= 80 % |
| Infrastructure.Tests | `AesGcmCredentialProtector` (ida y vuelta, nonce único, byte alterado, AAD con otro proveedor u otra versión, clave de longitud/Base64 inválidos, versión retirada, rotación 1 -> 2, sin clave activa); fábrica por proveedor (tipo y URL de Gemini fija); **spike de esfuerzo** con manejador de grabación (DD-12); `AssistantChatClient` (`Swap` con `Interlocked`, liberación a los 120 s con `FakeTimeProvider`, petición en curso conserva la instantánea, `Dispose` libera las pendientes); cargador (fila inválida, no descifrable, proveedor no permitido, base de datos caída: conserva la última válida y avisa una vez); poller (solo carga si cambia `xmin`, silencioso ante errores, primera carga acotada); clasificador (401/403/404/400/timeout/red/429/5xx) y que ni URL ni clave inventadas (`clave-prueba-123456`, `https://api.ejemplo.test/v1`) aparecen en la respuesta ni en registros; motor sobre instantánea | `FakeChatClient` guionizado; manejadores HTTP de grabación; logger de captura |
| Api.IntegrationTests (Docker) | 401/403/200 en los cuatro endpoints; `PUT` válido -> 200, fila de auditoría y `GET` con origen `stored`; 400 por campo y por propiedad desconocida (`url`); 409 con dos lectores y en la primera creación; `DELETE` -> 204 y `GET` con origen `env`; sin clave maestra -> 400 sin escritura; la columna cifrada no contiene la clave en claro; `GET`/`PUT`/prueba nunca devuelven la clave; el chat siguiente usa el modelo nuevo (con `IChatClientFactory` falso inyectado); `None` guardado -> 503; base de datos sin la tabla -> entorno; prueba: 4.ª en un minuto -> 429; dos instancias sobre la misma base de datos con `SettingsPollSeconds=1` | `WebApplicationFactory`, Testcontainers, sustitución de `IChatClientFactory` |
| ArchitectureTests | `DependencyRuleTests`: `OpenAI`/`Microsoft.Extensions.AI.OpenAI` solo en Infrastructure; `OpenApiContractTests` ampliada (DD-16); auditoría sin `Update`/`Remove`/`ExecuteDelete`/`ExecuteUpdate`; `new Uri(` en Infrastructure/Assistant solo en `ProviderEndpoints` y la fábrica; `DeliveryArtifactsTests` (18 ADR, `.env.example` sin clave maestra con valor); `Src_DoesNotHardcodeModelNames`; Web sin `localStorage`/`sessionStorage`/`MarkupString` en la página | Igual que hoy |
| Web.Tests | `AssistantConfigurationForm` (limpieza de la clave tras guardar, mapeo, origen, esfuerzo oculto, 409/400); sobrecarga de `ApiRequest` | xUnit sin navegador; página verificada a mano en Chrome (`verify-report.md`) |

Las pruebas con proveedor real quedan fuera (opcionales y separadas, AGENTS §5).

## 10. División en PR (apilados sobre main)

| # | PR | Contenido | ~Líneas |
|---|---|---|---|
| 1 | ADR 0018 + documentación | DD-19, AGENTS, `.env.example`, README | 150 |
| 2 | Application | DD-1, 2, 4, 5: modelo, reglas, resolver, puertos, casos de uso | 380 |
| 3a | Cifrado | `AesGcmCredentialProtector`, opciones de claves maestras, validador | 220 |
| 3b | Persistencia | Entidades, configuraciones, migración, `AssistantSettingsStore`, auditoría | 300 |
| 4 | Catálogo y adaptador | Paquete, `ProviderEndpoints`, OpenAI/Gemini, `IChatClientFactory`, esfuerzo (empieza por el spike) | 350 |
| 5 | Recarga en caliente | `AssistantSnapshot`, `Swap`, motor, cargador, poller, recargador | 380 |
| 6a | Prueba de conexión | Tester, clasificador, política de tasa | 220 |
| 6b | API | Endpoints, política movida, DTO, OpenAPI, integración | 380 |
| 7 | Web | Página, cliente, formulario, NavMenu | 350 |
| 8 | Endurecimiento | Integración entre instancias, guardas, documentación | 200 |

Si la estrategia de entrega no admite excepción, se mantienen los cortes 3a/3b y 6a/6b; el PR 6b depende de los puertos de PR 2 y del tester de 6a.

## 11. Suposiciones de la fase de specs: resolución y ediciones propuestas

| # | Suposición | Resolución |
|---|---|---|
| 1 | Juego de caracteres del modelo | **Confirmada** con precisión: letras ASCII, dígitos y `. _ - : / @` (DD-2). **E-1**: en el requisito «Guardado con validación», sustituir «letras» por «letras ASCII». |
| 2 | Nombres JSON y códigos | **Confirmados** (DD-14). **E-2**: añadir al requisito «Consulta de la configuración efectiva» la lista de campos de respuesta (`provider`, `model`, `timeoutSeconds`, `maxOutputTokens`, `effort`, `sources`, `credentialConfigured`, `credentialStorageAvailable`, `allowedProviders`, `concurrencyToken`, `updatedAt`, `updatedBy`, `applied`), la de petición del `PUT` (`provider`, `model`, `timeoutSeconds`, `maxOutputTokens`, `effort`, `credential`, `concurrencyToken`), que `PUT` responde `200` con el mismo cuerpo que `GET`, que `DELETE` responde `204` incluso sin fila y que la prueba responde `200 { result, message }` y `429` al superar el límite. |
| 3 | Esfuerzo no admitido | **400**, evaluado **por proveedor** (no por modelo). **E-3**: en el requisito de validación sustituir «solo si el proveedor/modelo lo admite» por «solo si el proveedor lo admite; el servidor no conoce las capacidades de cada modelo» y en el escenario «Esfuerzo» sustituir «proveedor/modelo» por «proveedor». El rechazo por modelo lo observa la prueba (`rejected`). |
| 4 | Formato de la clave maestra | **Confirmado**: 32 bytes en Base64, `Ai__MasterKeys__{version}` (entero 1-32767) y `Ai__ActiveMasterKeyVersion`; versión por registro (`credential_key_version`). **E-4**: en «Cifrado en reposo» y «Rotación» nombrar esas variables y el formato; en «Clave maestra de longitud inválida» precisar que se incluyen Base64 inválido y versión activa inexistente. |
| 5 | Sin clave maestra | **Confirmado**: el arranque no falla y el asistente sigue con el entorno; solo se rechaza guardar una clave nueva. **E-5**: en «Fallo cerrado sin clave maestra» añadir «El arranque NO DEBE fallar por la clave maestra ausente o inválida; el rechazo responde `400` con el campo `credential`, y `GET` indica `credentialStorageAvailable=false`». |
| 6 | Cuerpo de la prueba | **Confirmado**: valores candidatos sin guardar; campos ausentes tomados de la configuración efectiva; cuerpo vacío = efectiva. **E-6**: en «Prueba de conexión» (a) precisar esa regla, (b) **añadir la categoría `rejected`** (el proveedor rechazó modelo o esfuerzo, 400/422) a la lista cerrada y su escenario, (c) establecer `unreachable` como categoría de cualquier otro fallo (429, 5xx, red) y (d) indicar que un candidato no válido da `400`. |
| 7 | Campo de URL en el `PUT` | **Rechazar con 400** (cualquier propiedad desconocida, no solo URL). **E-7**: en el escenario «Sin URL del Manager» eliminar «se ignora o» y dejar «se rechaza con `400`». |
| 8 | Colisión del nombre `assistant-configuration` | Ver más abajo. |

> Estado (fase specs): E-1 a E-11 aplicadas. La capacidad de este cambio es `specs/assistant-settings/`; `specs/assistant-configuration/` y `specs/startup-configuration/` son deltas `MODIFIED` sobre las capacidades de `asistente-ia`. Toda mención posterior de `assistant-configuration` como capacidad propia de este cambio se lee como `assistant-settings`.

**Resolución de 8 (colisión de nombre).** `asistente-ia` (aún sin archivar) define su capacidad `assistant-configuration` (claves `Ai:*` del entorno, deshabilitado sin proveedor, selección por configuración). Al archivar ambos cambios, dos deltas «ADDED» del mismo nombre de capacidad chocarían (la segunda fusión duplicaría o mezclaría requisitos de dos orígenes). Propuesta, por orden:
1. **E-8a**: renombrar la capacidad de este cambio a **`assistant-settings`** (carpeta `specs/assistant-settings/`, actualizar `proposal.md`, esta sección y las referencias cruzadas de `assistant-credential-protection`). La capacidad `assistant-configuration` queda como la del **entorno** (`asistente-ia`).
2. **Orden de archivo**: archivar `asistente-ia` primero (ya tiene verificación); así `openspec/specs/assistant-configuration/` existe cuando se archive este cambio.
3. **E-8b**: este cambio necesita entonces deltas **MODIFIED** sobre capacidades que `asistente-ia` crea, porque cambia comportamiento: (i) `assistant-configuration` -> «Selección de proveedor solo por configuración» (añadir `OpenAI`/`Gemini`, y matizar «solo por configuración» con la configuración guardada del Manager) y «Validación de las claves Ai con proveedor activo» (clave y modelo obligatorios también con `OpenAI`/`Gemini`; nuevas claves `Ai:AllowedProviders`, `Ai:Effort`, `Ai:MasterKeys:*`, `Ai:ActiveMasterKeyVersion`, `Ai:SettingsPollSeconds`); (ii) `startup-configuration` -> el escenario `Ai__Provider=Gemini` hoy exige fallo de arranque con `Ai:Provider` y pasa a ser un proveedor válido (usar otro valor desconocido, p. ej. `GitHubModels`), y el escenario de `.env.example` con «las nueve claves `Ai__*`» pasa a incluir las nuevas. La fase de specs debe añadir `specs/assistant-configuration/spec.md` y `specs/startup-configuration/spec.md` de este cambio con solo bloques `MODIFIED`.
4. Si no se pudiera archivar `asistente-ia` antes, fusionar a mano estos deltas en el informe de archivo y avisar (regla `archive` de `config.yaml`: advertir antes de fusionar deltas destructivos).
5. `proposal.md`: la sección «Capacidades modificadas» deja de decir «Ninguna» y pasa a listar `assistant-configuration` y `startup-configuration`.

**Otras ediciones aditivas** (descubiertas al diseñar): **E-9** el listado de proveedores permitidos devuelve elementos con `requiresCredential` y `supportsEffort` (la Web no duplica reglas); **E-10** `GET` expone `applied` (si la configuración guardada es la usada por la instancia) y el requisito de recarga debe indicar que una fila con proveedor ya no permitido se trata como inválida; **E-11** el requisito de lista permitida añade que un nombre desconocido en `Ai__AllowedProviders` falla el arranque y que `None` siempre es seleccionable.

## 12. Validaciones para apply

| V | Comprobar | Si falla |
|---|---|---|
| V-1 | `dotnet restore` y `build -warnaserror` con `Microsoft.Extensions.AI.OpenAI` 10.10.0 junto a `Microsoft.Extensions.AI` 10.10.0 y `Anthropic` 12.58.0; `dotnet list package --vulnerable --include-transitive` (NU1608/NU1903 por `OpenAI` >= 2.13.0) | Elevar la transitiva con `PackageVersion` comentado (como `Microsoft.OpenApi`); 10.10.1 solo con motivo registrado |
| V-2 | Spike de esfuerzo (DD-12): cuerpo real enviado por OpenAI, Gemini, Anthropic y Ollama; nombre de `ReasoningEffortLevel` en `ChatCompletionOptions`; `OpenAIClientOptions.Endpoint`, `NetworkTimeout`, `RetryPolicy`; `AnthropicClient.HttpClient`/`OllamaApiClient(HttpClient, string)` para inyectar el manejador | Contingencia de DD-12 (3) y, en último caso, `SupportsEffort=false` |
| V-3 | Tipos de excepción y propiedad de estado HTTP de cada SDK para el clasificador | Clasificar por `HttpRequestException.StatusCode`/recorrido de `InnerException`; lo no reconocido -> `unreachable` |
| V-4 | `AesGcm` con `tagSizeInBytes` explícito disponible en .NET 10 y en la imagen Alpine | Ajustar el constructor; el diseño no cambia |
| V-5 | `Src_DoesNotHardcodeModelNames` (`gemini-`, `gpt-`, `qwen`, etc.) no coincide con nombres de proveedor, URL ni textos de ayuda; `RepositoryConventionsTests` de la Web permite el texto «OpenAI»/«Anthropic» en una página (solo prohíbe espacios de nombres de IA) | Reformular textos o ajustar la guarda con justificación |
| V-6 | Orden de arranque: el poller hace su primera carga tras las migraciones y antes de aceptar el primer chat | Cerrar la ventana marcando la instantánea inicial como «no cargada» hasta el primer intento |
| V-7 | `JsonUnmappedMemberHandling.Disallow` en Minimal API produce `400` y no `500`, y `ValidationProblem` se documenta bien en OpenAPI | Validar la lista de propiedades a mano en el endpoint |
| V-8 | Referencias a `TeamEndpoints.ManagerPolicy` en `src/` y `tests/` tras moverla a `AuthorizationPolicies` | Cambio mecánico de referencias |

### Resultados de V-n en apply

- **V-4 (PR 3a, verificado)**: `new AesGcm(key, 16)` con `tagSizeInBytes` explícito, cifrado/descifrado con AAD y rechazo de una etiqueta alterada (`CryptographicException`) funcionan igual en .NET 10.0.10 sobre Windows y en la imagen de ejecución del Dockerfile `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` (.NET 10.0.13, Alpine 3.24, `AesGcm.IsSupported=True`, tamaños de etiqueta admitidos 12-16). Se ejecutó un único contenedor efímero `tt-pr3a-alpine-check` (`--rm`) con una aplicación de consola publicada fuera del repositorio. El diseño no cambia.
- **V-1 (PR 4, verificado)**: `Microsoft.Extensions.AI.OpenAI` 10.10.0 (con `Microsoft.Extensions.AI` 10.10.0 y `Anthropic` 12.58.0) restaura y compila con `-warnaserror` sin NU1608 ni NU1903; arrastra `OpenAI` 2.13.0 y `System.ClientModel` 1.14.0. `dotnet list package --vulnerable --include-transitive` limpio en los once proyectos: no hizo falta elevar ninguna transitiva ni usar 10.10.1. NuGet fue alcanzable (`dotnet package search` devuelve 10.10.1 y 10.11.0, no usadas).
- **V-2 (PR 4, spike con manejador de grabación, sin red)**: los cuatro adaptadores traducen `ChatOptions.Reasoning` con los tres niveles y ninguno envía nada con esfuerzo nulo. No hizo falta la contingencia de DD-12 (3) ni cambiar `AssistantProviderTraits.SupportsEffort` (sigue `true` en los cuatro).

  | Proveedor | Campo del cuerpo | Con esfuerzo nulo |
  |---|---|---|
  | OpenAI (Chat Completions) | `reasoning_effort` = `low`/`medium`/`high` | ausente |
  | Gemini (mismo cliente, `OpenAIClientOptions.Endpoint` = `ProviderEndpoints.GeminiOpenAi`; la petición va a esa URL) | `reasoning_effort` | ausente |
  | Anthropic (`AnthropicThinkingMode.Adaptive` por defecto) | `output_config.effort` | ni `output_config.effort` ni `thinking` |
  | Ollama (`OllamaApiClient(HttpClient, model)`) | `think` = `"low"`/`"medium"`/`"high"` | ausente |

  Nombres confirmados: `OpenAIClientOptions.Transport` (`HttpClientPipelineTransport`), `.Endpoint`, `.NetworkTimeout`, `.RetryPolicy` (`new ClientRetryPolicy(1)`: una petición fallida con 5xx se repite una vez, 2 peticiones en total); `AnthropicClient.HttpClient` con `MaxRetries`; `OllamaApiClient(HttpClient, string)` exige `BaseAddress`. `ReasoningEffortLevel` de `ChatCompletionOptions` no se necesita.
- **V-3 (PR 4, para la PR 6a)**: con respuestas simuladas 400/401/403/404/422/429/500 y reintentos desactivados:

  | SDK | Excepción | Estado HTTP |
  |---|---|---|
  | OpenAI | `System.ClientModel.ClientResultException` (siempre, sin excepción interna) | propiedad `Status` (`int`) |
  | Anthropic | `Anthropic.Exceptions.AnthropicApiException` y derivadas: `AnthropicBadRequestException`, `AnthropicUnauthorizedException`, `AnthropicForbiddenException`, `AnthropicNotFoundException`, `AnthropicUnprocessableEntityException`, `AnthropicRateLimitException` (todas bajo `Anthropic4xxException`) y `Anthropic5xxException` | propiedad `StatusCode` (`HttpStatusCode`) |
  | Ollama | `System.Net.Http.HttpRequestException` para 401/403/404/422/429/5xx | `StatusCode` (`HttpStatusCode?`) |
  | Ollama, caso 400 | `OllamaSharp.Models.Exceptions.OllamaException` (cuerpo `{}`) o `InvalidOperationException` (cuerpo con `error`), **sin** propiedad de estado | ninguna |

  Consecuencia para el clasificador: Ollama con 400 no se puede clasificar por estado HTTP; la PR 6a debe tratar `OllamaException`/`InvalidOperationException` de Ollama como `rejected` solo si lo decide explícitamente, o dejarlo en `unreachable` (la contingencia de V-3). Un cuerpo de error no modifica el tipo para OpenAI ni Anthropic.
- **V-5 (PR 4, verificado)**: `Src_DoesNotHardcodeModelNames` sigue verde con `ProviderEndpoints` (URL de Gemini `.../v1beta/openai/`) y los nombres `Gemini`/`OpenAI`; el patrón `gemini-` exige guion. Nada que reformular.
- **V-6 (PR 5, verificado)**: el orden de arranque se cumple sin marcar la instantánea inicial como «no cargada». `DatabaseMigrationHostedService` migra en `StartingAsync`; `AssistantSettingsPoller` hace la primera carga en `StartAsync` (acotada a 10 s) y Kestrel es el último servicio alojado. Pruebas de integración con PostgreSQL: la tabla existe en la primera lectura (base vacía con migraciones al arrancar) y el primer chat usa la fila ya cargada. Dos hallazgos: (1) `BackgroundService.StartAsync` no ejecuta `ExecuteAsync` de forma síncrona en este entorno, así que el `PeriodicTimer` se crea en `StartAsync` y no en `ExecuteAsync`; (2) el sondeo no se registra al exportar OpenAPI (`DocumentGeneration.IsRunningUnderTool`), que no debe tocar la base de datos.

## 13. Preguntas abiertas

- [ ] Confirmar con el product owner la política de provisión de la clave maestra en Railway (quién la genera y cómo se custodia la copia de seguridad).
- [x] Resultado del spike (V-2): anotado en «Resultados de V-n en apply» (PR 4).
- [ ] Si se quiere lectura de la auditoría por API/UI en una versión posterior (no incluida en esta).

## 14. Desviaciones y riesgos aceptados (apply)

### Desviación aceptada de DD-14: campos numéricos del PUT

DD-14 pedía tipos de cadena en el DTO para devolver `400` por campo en español ante un valor no numérico. El DTO conserva `int?` en `timeoutSeconds` y `maxOutputTokens`, de modo que un valor que no es un entero JSON (`"abc"`, `1.5`, `true`, un número fuera de rango) recibe el `400` general de formato (`problem+json`, sin mapa `errors` y sin repetir el valor). Motivos:

- Pasar a cadenas cambia el contrato publicado (`integer` → `string` en `openapi.json`) y obliga a que el cliente envíe `"20"` en lugar de `20`; la Web ya envía enteros y sus pruebas de nombres JSON exactos dependen de ello.
- `AssistantSettingsInput` (Application) también es `int?` y lo usan todas las pruebas de reglas, de casos de uso y de la prueba de conexión; migrarlo a texto exige reescribirlas sin que ninguna regla de negocio cambie.
- Los límites 5-55 y 64-4096 sí se validan por campo en español; el caso afectado solo lo produce un cliente que no es la Web (el campo del formulario es numérico), y el `400` no revela ningún valor ni toca la fila.

`AssistantConfigurationStrictBodyTests.Put_WithANonIntegerNumericField_...` fija el comportamiento. Si más adelante se publica la API a terceros, el cambio correcto es pasar ambos DTO y `AssistantSettingsInput` a cadenas, con cambio de contrato explícito.

### Riesgos aceptados

- **Configuración única para toda la instancia, con acceso de cualquier Manager.** Hay una sola fila y la API no distingue equipos: cualquier Manager de cualquier equipo puede leerla, cambiarla, restablecerla y lanzar pruebas de conexión (con el límite de 3 por minuto y persona). Es coherente con el alcance del cambio (una empresa, un proveedor) y se mitiga con el aviso de alcance en la página, la auditoría de solo inserción (quién y cuándo) y el restablecimiento al entorno. Si la instancia pasara a servir a varias empresas o equipos aislados, habría que partir la fila por ámbito y añadir una política por equipo: no está en esta versión.
- **`DELETE` sin token del cliente.** El caso de uso lee la fila y borra con su `xmin` (`Deleted`/`NotFound`/`Conflict`), así que no hay pérdida entre la lectura y el borrado en el servidor, pero el cliente no envía el token que vio. Un Manager con una vista obsoleta puede restablecer sin querer una configuración que otro guardó después de que él abriera la página. La Web lo advierte en el panel de confirmación («aunque otro responsable la haya cambiado»). Añadir un token del cliente es un cambio de contrato (parámetro nuevo y caso de uso) y se deja fuera.
- **Un 400 de Ollama queda como `unreachable`.** Ollama no expone el estado HTTP del 400 en una propiedad (V-3: `OllamaException` o `InvalidOperationException` sin estado), así que la prueba de conexión no puede distinguir «parámetros rechazados» de otros fallos y lo clasifica como `unreachable`. Es el comportamiento de la contingencia de V-3: no se interpreta el texto del mensaje (podría contener datos del proveedor) para decidir la categoría.
