# Progreso de apply: asistente-config-responsable

Fecha: 2026-10-19 · Modo: Strict TDD · Lotes: PR 1 (`feat/asistente-config-pr1-adr`, fusionado) y PR 2 (`feat/asistente-config-pr2-application`, base a82fd4b)

## Estado de tareas del PR 1

| Tarea | Estado | Nota |
|---|---|---|
| 1.1 Copia previa | Hecha | `..\TimeTracker.rollback\pr-1\` (AGENTS, README, docker-compose, DeliveryArtifactsTests); `.env.example` no se pudo copiar (acceso denegado) |
| 1.2 RED 18 ADR | Hecha | 4 pruebas fallaron antes del código (18 ADR esperados, hay 17; ADR 0018, AGENTS y README) |
| 1.3 RED `.env.example` | Hecha | 4 pruebas nuevas; 2 fallaron antes (comentario de 32 bytes y proveedor OpenAI), 30/30 en verde tras GREEN |
| 1.4 GREEN ADR 0018 | Hecha | `docs/adr/0018-...md` |
| 1.5 GREEN AGENTS §1/§7 | Hecha | ADR 0017 sin editar |
| 1.6 GREEN docs | Hecha | README y `.env.example` hechos; `docker-compose.yml` no consume `Ai__*` nuevas |
| 1.7 Refactor y puerta | Hecha | Sin refactor necesario; revisión acotada aprobada |

## TDD Cycle Evidence

| Tarea | Archivo de prueba | Capa | Red de seguridad | RED | GREEN | TRIANGULAR | REFACTOR |
|---|---|---|---|---|---|---|---|
| 1.2 / 1.4 | `tests/TimeTracker.ArchitectureTests/DeliveryArtifactsTests.cs` (`Adr_Directory_HasEighteenAdrsWithFourSections`, `Adr0018_...`) | Arquitectura | 22/22 previas | Escrita; falló «Expected: 18 Actual: 17» | 26/26 | 2 pruebas (recuento y contenido) | Nada necesario |
| 1.5 | `AgentsMd_AllowsStoredConfigurationForTheAssistantProviderAndPointsToAdr0018` | Arquitectura | N/A | Escrita; falló | Pasó | Single | Nada |
| 1.6 (README) | `Readme_ExplainsTheManagerAssistantConfigurationAndKeyRotation` | Arquitectura | N/A | Escrita; falló | Pasó | Single | Nada |

Work Unit Evidence: prueba focal `dotnet test tests/TimeTracker.ArchitectureTests --filter FullyQualifiedName~DeliveryArtifacts` = 26/26. Arnés de ejecución: N/A (solo documentación). Rollback: restaurar `AGENTS.md` y `README.md`, borrar el ADR 0018 y revertir `DeliveryArtifactsTests.cs`.

## Puerta del PR (ejecutada)

BUILD -warnaserror 0 advertencias 0 errores; TEST: Domain 67, Web 223, Application 201, Infrastructure 182, Architecture 90, Integration 422 (Docker en marcha), todo en verde; COV-A 100,0 % líneas; COV-D 100 %; FORMAT sin cambios; VULN sin paquetes vulnerables. `docs/api/openapi.json` restaurado con git checkout tras el build.

## Pendiente

Nada del PR 1. Antes de abrir la PR, ejecutar `Api.IntegrationTests` con Docker disponible y las pruebas E2E previas a Railway.

---

# PR 2 — Application: modelo, reglas, resolver, casos de uso

Rama: `feat/asistente-config-pr2-application` (base `main` a82fd4b) · Modo: Strict TDD · Tareas 2.1 a 2.12 hechas.

## Estado de tareas del PR 2

| Tarea | Estado | Nota |
|---|---|---|
| 2.1 Copia previa | Hecha | `..\TimeTracker.rollback\pr-2\DependencyRuleTests.cs` |
| 2.2 RED reglas | Hecha | 39 pruebas; RED = error de compilación (tipos inexistentes) |
| 2.3 GREEN modelo y reglas | Hecha | 2 fallos de datos de prueba corregidos (clave ausente en el caso «permitido») |
| 2.4 AllowedProviders | Hecha | RED de comportamiento sobre un `AllowedProviders` mínimo (devolvía lista; faltaban error y exclusión) |
| 2.5 Resolver | Hecha | 8 pruebas |
| 2.6 AssistantCredential | Hecha | RED real: 2 fallos «Sub-string found» (el `ToString()` de un record imprime el valor) |
| 2.7 Puertos y tipos | Hecha | Sin prueba propia; ejercitados por 2.8-2.11 |
| 2.8 Guardar | Hecha | 16 pruebas |
| 2.9 Consultar | Hecha | 9 pruebas |
| 2.10 Restablecer | Hecha | 3 pruebas |
| 2.11 Probar conexión | Hecha | 9 pruebas |
| 2.12 Refactor y puerta | Hecha | Recarga tras confirmar extraída a `ReloadAfterCommitAsync`; `AllowedProviders` usa `RequiresEnvironmentEndpoint` en lugar de nombrar Ollama; la regla de endpoint duplicada salió de las reglas |

## TDD Cycle Evidence (PR 2)

| Tarea | Archivo de prueba | Capa | RED | GREEN | TRIANGULAR | REFACTOR |
|---|---|---|---|---|---|---|
| 2.2/2.3 | `AssistantSettingsRulesTests` | Application | No compila: «AssistantProvider no existe» | 39/39 | Extremos 4/5/55/56 y 63/64/4096/4097, modelo (vacío, 101, `<script>`, `ñ`, válido), esfuerzo, proveedor, clave | Sin cambios |
| 2.4 | `AllowedProvidersTests` | Application | No compila: `Providers`/`Error` inexistentes | 47/47 (acumulado) | Mayúsculas/espacios/duplicados, sin lista, `None`, Ollama con/sin endpoint, 4 nombres desconocidos | Se quitó la comprobación de endpoint duplicada en las reglas |
| 2.5 | `AssistantSettingsResolverTests` | Application | No compila: `AssistantSettingsResolver` inexistente | 55/55 | Origen por valor, `None`, clave de entorno solo del proveedor del entorno, clave guardada inutilizable | `SourceOf` único |
| 2.6 | `AssistantCredentialTests` | Application | 2 fallos: «Sub-string found» | 58/58 | `ToString`, interpolación y record contenedor | Nada |
| 2.8 | `SaveAssistantSettingsUseCaseTests` | Application | No compila: `SaveAssistantSettingsResult` inexistente | 74/74 | Válido, conservar, recifrar, cambio de proveedor (con y sin clave nueva), sin protector (con y sin clave), 409 (token obsoleto y primera creación), auditoría sin secretos, recarga fallida, token/campos inválidos | Mutación temporal (no recifrar) → falla `KeepingTheKey_ReEncryptsItWithTheActiveVersion` |
| 2.9 | `GetAssistantSettingsUseCaseTests` | Application | No compila: `GetAssistantSettingsUseCase` inexistente | 83/83 | Sin fila, con fila, `applied` falso, `None`, almacenamiento, clave sin exponer, clave no descifrable, lista permitida | Mutación temporal (`applied` ignora la lista) → 1 fallo |
| 2.10 | `ResetAssistantSettingsUseCaseTests` | Application | No compila: `ResetAssistantSettingsUseCase` inexistente | 86/86 | Con fila, sin fila, recarga fallida | Recarga tras confirmar compartida con Guardar |
| 2.11 | `TestAssistantConnectionUseCaseTests` | Application | No compila: `TestAssistantConnectionResult` inexistente | 95/95 | Cuerpo vacío, solo modelo, esfuerzo efectivo, candidato inválido (sin llamar), clave candidata no persistida, clave guardada, `None`, resultado tal cual | Nada |

## Work Unit Evidence (PR 2)

| Evidencia | Valor |
|---|---|
| Prueba focal | `dotnet test tests/TimeTracker.Application.Tests --filter FullyQualifiedName~Assistant.Configuration` = 95/95 |
| Arnés de ejecución | N/A: lógica pura detrás de puertos con dobles en memoria y `FakeTimeProvider`; no hay frontera de ejecución |
| Límite de rollback | Borrar `src/TimeTracker.Application/Assistant/Configuration/` y `tests/TimeTracker.Application.Tests/Assistant/Configuration/` |

## Puerta del PR 2 (ejecutada)

RESTORE ok; BUILD `-warnaserror` 0 advertencias 0 errores; FORMAT sin cambios; TEST: Domain 67, Application 296, Web 223, Infrastructure 182, Architecture 94 (incluye `DependencyRuleTests`: Application sin paquetes nuevos), Api.IntegrationTests 422 (Docker disponible), todo en verde; COV-A 99,7 % líneas / 95,8 % ramas; COV-D 100 %. `docs/api/openapi.json` restaurado con `git checkout` tras el build y fuera de los commits. VULN no ejecutado (no se añadieron paquetes).

## Desviaciones y avisos

- **Tamaño**: ≈ 1.717 líneas añadidas (≈ 700 de producción y ≈ 1.020 de pruebas) frente a las ≈ 380 estimadas. El diseño ya advertía de que el PR 2 era el más dudoso; el corte natural que propone el reparto es «reglas+resolver (2.2-2.6)» / «puertos y casos de uso (2.7-2.11)».
- `AssistantSettingsRules.Validate` recibe además la fila guardada (`stored`) para saber si ya hay clave del mismo proveedor; `ValidateToken` es una función aparte.
- `AllowedProviders.Resolve` devuelve `AllowedProvidersResult(Providers, Error)` (el arranque de PR 3a usará `Error`).
- `AssistantEnvironmentDefaults` incluye la clave del entorno (`AssistantCredential?`, redactada) y `HasEndpoint`; el endpoint de Ollama no entra en Application.
- La recarga fallida tras confirmar se traga sin registrar (Application no tiene registro); el cargador de PR 5 deja el aviso.
- La vista (`AssistantSettingsView`) devuelve `UpdatedBy` como `Guid?`; la API (PR 6b) resolverá el nombre visible.

## Decisión posterior: clave con proveedor None (PR 2)

Decidido por el usuario tras la revisión: guardar una clave nueva con el proveedor `None` ya no se descarta en silencio, sino que falla la validación con un error en `credential` (sin valores) y no escribe ni audita. La prueba de conexión sin proveedor efectivo `None` sigue rechazada con error en `provider` sin llamar al probador, y con clave candidata también se rechaza.

| Prueba | RED | GREEN |
|---|---|---|
| `AssistantSettingsRulesTests.Credential_WithProviderNone_...` | Falla: la regla no devolvía error | Rama `else if` en `AssistantSettingsRules.Validate` |
| `SaveAssistantSettingsUseCaseTests.ANewKeyWithProviderNone_...` | Falla: se guardaba y se auditaba | Misma regla |
| `BlankCredential_WithProviderNone_...`, `ExplicitlyTestingProviderNone_...`, `ACandidateKeyWithProviderNone_...` | Ya en verde (comportamiento ya cubierto por el código); fijan el contrato | Sin cambios |

## PR 2.1 — Correcciones de la revisión de la PR 2 (rama `fix/asistente-config-pr2-1-revision`)

| # | Commit | RED (motivo) | GREEN |
|---|---|---|---|
| 1 | c7963d1 | `ToString()` de `AssistantSettingsInput` y del comando contenía `Credential = clave-prueba-123456` | `PrintMembers` privado que imprime `[clave redactada]` |
| 2 | 99d897d | Save y Test aceptaban una clave guardada no descifrable (Validate solo comprobaba que existiera) | `UsableCredential.FromStored/FromEnvironment` usado por Validate (ahora recibe el protector), vista/resolver y `FindCredential` |
| 3 | a20bdb1 | `CredentialProtectionUnavailableException` escapaba de `Save` (fake `FailProtectAfterAvailabilityCheck`) | `ReEncryptOrKeep` captura la excepción y conserva `existing` |
| 4 | 5425d59 | Proveedor `None` con esfuerzo heredado daba error en `effort` (y `effort`+`credential` con clave); entorno con timeout/tokens fuera de rango fallaba la prueba | `Complete` solo completa proveedor y modelo; esfuerzo heredado se aplica tras validar solo si el proveedor es el mismo y lo admite |
| 5 | 555c98a | Esfuerzo `"  low "` rechazado; aserción de ciphertext sobre fake que guardaba la clave legible | `ParseByName<T>` único con recorte; `FakeProtector` con XOR; aserciones corregidas; prueba renombrada a `TheTest_NeverWritesOrAuditsAnythingEvenWithAStoredRow` |

Puerta: build `-warnaserror` 0/0; `dotnet format --verify-no-changes` limpio; Application 314, Architecture 94, Domain 67, Infrastructure 182, Web 223, Api.IntegrationTests 422 (Docker disponible, sin tocar contenedores). `docs/api/openapi.json` restaurado y fuera de los commits.

Fuera de alcance (siguen pendientes): token de concurrencia en Reset (almacén real/PR 3b) y log de recarga (PR 5).

## PR 3a — Cifrado y opciones de entorno (rama `feat/asistente-config-pr3a-cifrado`)

Modo: Strict TDD. Copia previa en `..\TimeTracker.rollback\pr-3a\`.

| Tarea | Estado | Evidencia |
|---|---|---|
| 3a.1 Copia previa | Hecha | `AiOptions.cs`, `AiOptionsValidator.cs`, `AssistantServiceCollectionExtensions.cs`, `TestAssistantConnectionUseCase(+Tests)`, `AiOptionsValidatorTests`, `AiStartupTests`, `design.md`, `tasks.md`, `apply-progress.md` (`AiProvider` vive en `AiOptions.cs`) |
| 3a.2 V-4 | Hecha | `AesGcm(key, 16)` verificado en .NET 10.0.10 (Windows) y en `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` (.NET 10.0.13, Alpine 3.24): ida y vuelta, AAD y etiqueta alterada rechazada. Un solo contenedor `tt-pr3a-alpine-check` con `--rm`. Anotado en `design.md` |
| 3a.3 Protector | Hecha | 18 pruebas `AesGcmCredentialProtectorTests`; 100 % de líneas del protector |
| 3a.4 Opciones y validador | Hecha | `AiOptionsValidatorTests` ampliada (de 62 a 80 con el mapeo) |
| 3a.5 Mapeo de proveedores | Hecha | `AssistantProviderMappingTests` (nombres iguales y números sin renumerar) |
| 3a.6 Arranque | Hecha | `AiStartupTests` (+9 casos) y `ProtectionStatusLogTests` (3), sin Docker |
| 3a.7 Refactor y puerta | Hecha | ver «Puerta del PR 3a» |
| Extra PR 2.1 | Hecho | commit `d24ae9b`, ver abajo |

### TDD Cycle Evidence (PR 3a)

| Tarea | Archivo de prueba | Capa | RED | GREEN | TRIANGULAR | REFACTOR |
|---|---|---|---|---|---|---|
| Extra | `TestAssistantConnectionUseCaseTests.AnInheritedModelOutsideTheRules_IsNotValidatedAsManagerInput` | Application | `Assert.IsType() Failure`: devolvía `Invalid` (error en `model`) con el modelo de entorno de 101 caracteres | 147/147 (`Assistant`) | Modelo de entorno largo con cuerpo vacío | `Complete` solo completa el proveedor; el modelo efectivo entra por `env with { Model = effective.Model }` |
| Extra | `ACandidateKeyWithProviderNone_...` (aserción reforzada: campo `credential`) | Application | Verde a la primera: el comportamiento ya existía; solo fija el campo | 147/147 | Dos casos (`None` explícito y entorno deshabilitado) | Sin cambios |
| 3a.4/3a.5 | `AiOptionsValidatorTests`, `AssistantProviderMappingTests` | Infrastructure | No compila: `AiProvider` no contiene `OpenAI`/`Gemini`, `AiOptions` no tiene `Effort`, `AllowedProviders`, `MasterKeys`, `SettingsPollSeconds` | 80/80 (filtro) y 209/209 (proyecto) | OpenAI/Gemini con y sin clave/modelo, `GitHubModels` sin imprimir el valor, esfuerzo `low/Medium/ HIGH /extreme/1`, lista permitida válida/inválida con y sin proveedor, poll 0/1/300/301, claves maestras inválidas no fallan | Validador reutiliza `AssistantSettingsLimits`, `AssistantProviderTraits` y `AllowedProviders.Resolve`; sin literales duplicados |
| 3a.3 | `AesGcmCredentialProtectorTests` | Infrastructure | No compila: `AesGcmCredentialProtector` inexistente | 18/18 | Ida y vuelta, nonce único, byte alterado (2 posiciones), registro truncado, otro proveedor, otra versión con la misma clave, Base64/longitud inválidos, versión activa nula/ausente/0/32768/texto, sin claves, rotación, versión retirada | Mutación temporal (AAD sin versión) → falla `AnotherKeyVersionWithTheSameKeyMaterial_FailsAuthentication` |
| 3a.6 | `ProtectionStatusLogTests` | Api.IntegrationTests | 3 fallos: no había ninguna línea «protección de claves» | 47/47 (`AiStartupTests`+`ProtectionStatusLog`) | Con clave válida, sin claves, con clave inválida (sin imprimirla) | Nada |
| 3a.6 | `AiStartupTests` (OpenAI/Gemini válidos, sin clave o modelo, claves maestras inválidas, lista permitida inválida) | Api.IntegrationTests | Verde a la primera: lo cubre el validador de 3a.4; fijan el contrato de arranque (la prueba existente `Gemini` desconocido pasó a `GitHubModels`) | 47/47 | 2+2+3+2 casos | Nada |
| 3a.6 | `AssistantRegistrationTests.TheProtector_IsASingleton...` | Infrastructure | No compila hasta registrar el protector (`GetRequiredService` sin registro) | 228/228 | Con y sin claves maestras | Nada |

### Work Unit Evidence (PR 3a)

| Evidencia | Valor |
|---|---|
| Prueba focal | `dotnet test tests/TimeTracker.Infrastructure.Tests --filter "FullyQualifiedName~AesGcm|FullyQualifiedName~AiOptions|FullyQualifiedName~AssistantProviderMapping"` y `dotnet test tests/TimeTracker.Api.IntegrationTests --filter "FullyQualifiedName~AiStartupTests|FullyQualifiedName~ProtectionStatusLog"` = 47/47 |
| Arnés de ejecución | Arranque real de la API con `WebApplicationFactory` (sin Docker) y `AesGcm` en la imagen Alpine del Dockerfile |
| Límite de rollback | Borrar `AesGcmCredentialProtector.cs`, `AiProviderMapping.cs`, `Api/Assistant/ProtectionStatusLogger.cs`; revertir `AiOptions*`, el registro en `AssistantServiceCollectionExtensions` y `Program.cs`; el extra de Application es un commit aparte (`d24ae9b`) |

### Puerta del PR 3a (ejecutada)

RESTORE ok; BUILD `-warnaserror` 0 advertencias 0 errores; FORMAT sin cambios; TEST: Domain 67, Application 315, Web 223, Infrastructure 228, Architecture 94, Api.IntegrationTests 436 (Docker disponible, sin tocar contenedores existentes), todo en verde; COV-A 99,7 % líneas / 96,5 % ramas; COV-D 100 %. VULN no ejecutado (no se añadieron paquetes). `docs/api/openapi.json` restaurado y fuera de los commits.

### Decisiones fuera de spec (PR 3a)

- `AiOptions.ActiveMasterKeyVersion` es `string?` y no `int?` (DD-18): un valor no numérico con `int?` haría fallar el enlace de opciones al arrancar, contra «el arranque no falla por la clave maestra».
- La línea de estado de la protección la emite `Api/Assistant/ProtectionStatusLogger` (servicio hospedado, `Information` en ambos estados): Infrastructure no referencia `Microsoft.Extensions.Hosting.Abstractions` y no se añadió un paquete. El protector se registra en `AddAssistant`.
- Orden de commits: 3a.4/3a.5 (opciones) antes que 3a.3 (protector), porque el protector lee `AiOptions.MasterKeys`.
- `AiOptions.IsEnabled` pasa a «proveedor real» (incluye OpenAI y Gemini); hasta la PR 4 `ChatClientFactory.Create` devuelve `null` para ellos (asistente sin cliente) sin fallar.
- `AiProviderMapping` (interno) convierte `AiProvider` en `AssistantProvider` por nombre para reutilizar `AllowedProviders.Resolve` y `AssistantProviderTraits` en el validador.
- Tamaño: ≈ 640 líneas añadidas (≈ 210 de producción y ≈ 430 de pruebas) frente a las ≈ 220 estimadas.

## PR 3a.1 — Correcciones de la revisión de la PR 3a

| Tarea | Estado | Commit |
|---|---|---|
| 3a.1.1 Versión de clave duplicada | Hecha | `c1982d0` |
| 3a.1.2 Motivo de «no disponible» | Hecha | `b20bac5` |
| 3a.1.3 Log de proveedor sin adaptador | Hecha | `eaab59a` |
| 3a.1.4 Constantes y `AllowedProviders.Validate` | Hecha | `eccd4f4` |
| 3a.1.5 Pequeñas | Hecha | `7a98e47` |

### TDD Cycle Evidence (PR 3a.1)

| Tarea | Archivo de prueba | Capa | RED | GREEN | TRIANGULAR | REFACTOR |
|---|---|---|---|---|---|---|
| 3a.1.1 | `AesGcmCredentialProtectorTests.VersionsThatNormalizeToTheSameNumber_...` | Infrastructure | 2 fallos: `IsAvailable` verdadero, la última versión sobrescribía a la primera | 21/21 | `1`/`01` y `01`/`1`; otras versiones no se ven afectadas | `seen` + descarte de la versión duplicada |
| 3a.1.2 | `AesGcmCredentialProtectorTests` (motivo) y `ProtectionStatusLogTests` | Infrastructure, Api.IntegrationTests | No compila (`UnavailableReason` inexistente); 4 fallos de log sin «motivo:» | 28/28 y 5/5 | Sin versión activa (nula/vacía), versión sin clave (`2`/`abc`), clave inválida, duplicada; la línea no contiene clave ni versión | Enum en Application, texto fijo por motivo en el logger |
| 3a.1.3 | `ProviderAdapterLogTests` | Api.IntegrationTests | 2 fallos: ninguna línea «adaptador» | 47/47 (con `AiStartupTests`) | OpenAI, Gemini y Anthropic (sin línea); sin clave ni modelo en el texto | `ChatClientFactory.LacksAdapter` |
| 3a.1.4 | `AllowedProvidersTests.Validate_*` | Application | No compila (`Validate` inexistente) | 321/321 y 242/242 | Vacía/espacios/válida y nombres desconocidos sin imprimir el valor | Constantes `Min/Max*` públicas usadas por las pruebas |
| 3a.1.5 | `MasterKeyOfAnotherLength_...`, `TheHighestVersion_...` | Infrastructure | Verdes a la primera: fijan comportamiento ya existente | 32/32 | 31/33/64 bytes y 32767/32768 | `ZeroMemory`, comentario del margen de 3 bytes, rama `ModelRequired` fuera |

### Work Unit Evidence (PR 3a.1)

| Evidencia | Valor |
|---|---|
| Prueba focal | `dotnet test tests/TimeTracker.Infrastructure.Tests --filter FullyQualifiedName~AesGcm` = 32/32; `dotnet test tests/TimeTracker.Api.IntegrationTests --filter "FullyQualifiedName~ProtectionStatusLog|FullyQualifiedName~ProviderAdapterLog"` = 8/8 |
| Arnés de ejecución | Arranque real de la API con `WebApplicationFactory` capturando el registro |
| Límite de rollback | Un commit por corrección (`c1982d0`, `b20bac5`, `eaab59a`, `eccd4f4`, `7a98e47`) |

### Puerta del PR 3a.1 (ejecutada)

BUILD `-warnaserror` 0 advertencias 0 errores; FORMAT sin cambios; TEST: Domain 67, Application 321, Web 223, Infrastructure 242, Architecture 94, Api.IntegrationTests 441 (Docker disponible, sin tocar contenedores), todo en verde. `docs/api/openapi.json` restaurado y fuera de los commits.

### Decisiones fuera de spec (PR 3a.1)

- `IAssistantCredentialProtector` gana `UnavailableReason` (enum `ProtectionUnavailableReason`): `NoActiveVersionConfigured`, `ActiveVersionWithoutKey` (inexistente o versión activa no numérica) e `InvalidOrDuplicateMasterKey`.
- El aviso de proveedor sin adaptador es un servicio hospedado propio (`ProviderAdapterLogger`) y `ChatClientFactory.LacksAdapter` desaparece con la PR 4.
- La rama `ModelRequired` se sustituyó por `?? throw InvalidOperationException` (invariante): el compilador necesita un modelo no nulo y las reglas ya lo garantizan; sin `!`.
- Las constantes de límites del validador son `public` para que las pruebas las usen.
