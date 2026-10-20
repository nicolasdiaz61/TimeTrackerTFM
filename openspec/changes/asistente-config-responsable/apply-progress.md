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


## PR 3b — Persistencia, migración y auditoría (rama `feat/asistente-config-pr3b-persistencia`)

| Tarea | Estado | Commit |
|---|---|---|
| Arrastre a) prueba de versión duplicada | Hecha | `2fc84e3` |
| Arrastre b) Restablecer con token de concurrencia | Hecha | `0bc62ee` |
| 3b.1 Copia previa | Hecha (`..\TimeTracker.rollback\pr-3b\`: DbContext, snapshot, puerto, caso de uso, fakes, pruebas, `DependencyRuleTests`, `RepositoryConventionsTests`, spec, diseño, tareas, progreso) | |
| 3b.2 a 3b.4 Almacén, entidades, configuraciones y migración | Hecha | `36f6607` |
| 3b.5 Auditoría de solo inserción (AR) | Hecha | `59ee567` |
| 3b.6 Contenido de la auditoría | Hecha | `97995f8` |
| 3b.7 REFACTOR y puerta | Hecha | |

### TDD Cycle Evidence (PR 3b)

| Tarea | Archivo de prueba | Capa | RED | GREEN | TRIANGULAR | REFACTOR |
|---|---|---|---|---|---|---|
| a) | `AesGcmCredentialProtectorTests.ADuplicatedVersion_FailsClosedEvenIfBothCopiesHoldTheSameKey` | Infrastructure | Mutación temporal que revierte a «gana la última»: la prueba nueva falla (`Assert.False() Failure`); la anterior no distinguía porque las claves de las copias diferían | 33/33 | Las dos órdenes de aparición (`01` antes y después de `1`); aserción positiva de la versión 2 | Sin cambios de producción |
| b) | `ResetAssistantSettingsUseCaseTests` + fake del almacén | Application | No compila: `DeleteOutcome` inexistente y `DeleteAsync(AuditEntry, ...)` sin token | 323/323 (un fallo intermedio: el fake devolvía `Conflict` con fila ausente; se corrigió el doble a `NotFound`, que es la semántica pedida) | Con fila, sin fila, recarga fallida, otro Manager guarda tras la lectura (`Conflict`, fila nueva intacta, sin auditoría ni recarga), otro Manager restablece antes (`NotFound`) | `Reset` ya no usa `previous?` (invariante tras el `null` temprano) |
| 3b.2 | `AssistantSettingsStoreTests` | Integración (PostgreSQL 18, Testcontainers) | No compila: `AssistantSettingsStore` inexistente | 20/20 | Creación, segunda creación, actualización, token obsoleto, token sin fila, fallo de auditoría en creación y en actualización (se borra la tabla de auditoría: la fila no queda), ida y vuelta completa y con todo nulo, clave cifrada con el protector real, tres `CHECK` rechazadas y una aceptada | Mutación temporal (quitar el `xmin` esperado): falla `Save_WithAStaleToken_...` |
| 3b.3 | `AssistantSettingsStoreTests` | Integración | Mismo RED (compilación) | Incluido en 20/20 | `Deleted`/`NotFound`/`Conflict`, `GetVersion` `"0"`/token, tabla ausente (`42P01`) → `null` y `"0"`, base inaccesible → la excepción se propaga | |
| 3b.4 | `MigrationTests` (ampliada) | Integración | `Migrate_..._AppliesMigrationsInOrder` falla con la migración nueva (esperado 3, hay 4) | 40/40 con las del almacén | Aplica sobre datos sin alterar tablas existentes, `Down` borra solo las dos tablas, índice `occurred_at DESC`, identidad `ALWAYS` | |
| 3b.5 | `AssistantAuditAppendOnlyTests` | Architecture | Mutación temporal (`ExecuteDeleteAsync` sobre la entidad de auditoría en el almacén): falla con el enunciado ofensor | 10/10 | Detección de `Remove`, `ExecuteDelete`, `Update` y SQL `DELETE`/`UPDATE`; ignora `Add`, `Remove(row)` de la fila de configuración y SQL de otra tabla | |
| 3b.6 | `AssistantSettingsAuditTests` | Integración | Verde a la primera (el almacén ya serializaba `AuditSnapshot`); mutación temporal que añade un campo con la clave: fallan las 3 pruebas | 3/3 | Alta con clave, actualización conservándola y restablecimiento | |

### Work Unit Evidence (PR 3b)

| Evidencia | Valor |
|---|---|
| Prueba focal | `dotnet test tests/TimeTracker.Api.IntegrationTests --filter "FullyQualifiedName~AssistantSettingsStore|FullyQualifiedName~AssistantSettingsAudit|FullyQualifiedName~MigrationTests"` = 47/47; `dotnet test tests/TimeTracker.ArchitectureTests --filter FullyQualifiedName~AuditAppendOnly` = 10/10 |
| Arnés de ejecución | PostgreSQL 18 real (Testcontainers), una base nueva y migrada por prueba; migración aplicada y revertida en `MigrationTests` |
| Límite de rollback | Un commit por unidad; `Down` de `AddAssistantSettings` borra solo `assistant_settings` y `assistant_settings_audit` |

### Puerta del PR 3b (ejecutada)

RESTORE ok; BUILD `-warnaserror` (Release) 0 advertencias 0 errores; FORMAT sin cambios; TEST: Domain 67, Application 323, Web 223, Infrastructure 243, Architecture 104, Api.IntegrationTests 467 (Docker disponible; sin tocar contenedores existentes), todo en verde. Entorno limpio de variables `Ai__*`, etc. VULN no ejecutado (no se añadieron paquetes). `docs/api/openapi.json` restaurado y fuera de los commits.

### Decisiones fuera de spec (PR 3b)

- `DeleteAsync(expectedToken, audit)` devuelve `DeleteOutcome` (`Deleted`, `NotFound`, `Conflict`) y `ResetAssistantSettingsUseCase` devuelve el mismo enum; el endpoint de la PR 6b traducirá `Conflict` a `409` y `Deleted`/`NotFound` a `204`. Sin fila (o fila ya borrada por otro) es `NotFound`, no conflicto: el estado final pedido ya se cumple. Spec (escenario «Restablecer con token obsoleto»), diseño (DD-8, puerto, tabla de DD-14) y tareas actualizados.
- Las entidades (`AssistantSettingsRecord`, `AssistantSettingsAuditRecord`) son `internal` y se registran con `ApplyConfigurationsFromAssembly`: `TimeTrackerDbContext` no cambia (sin `DbSet` público; el almacén usa `Set<T>()`). `AssistantSettingsStore` es público y se registra como scoped en `AddPersistence`.
- La migración generada incluía una columna `xmin` real; se eliminó a mano de `Up` (igual que en `time_entries`: es la columna de sistema). El índice `occurred_at DESC` queda como `descending: new bool[0]` (en EF, vacío = todas descendentes) y la prueba lo confirma en `pg_indexes`.
- La fila de auditoría guarda `provider`, `effort` y demás en el JSON con nombres camelCase y `effort` en minúsculas (`low|medium|high`), igual que la columna `effort`.
- `MigrationTests.Migrate_OnEmptyDatabase_AppliesMigrationsInOrder` se actualizó de 3 a 4 migraciones.
- Tamaño: 1.439 líneas añadidas en 20 archivos (≈ 390 son la migración generada `Designer` y el snapshot; ≈ 270 de producción manual y ≈ 600 de pruebas) frente a las ≈ 300 estimadas más los arrastres. Se propone contar el código generado fuera del presupuesto (decisión 2 pendiente del forecast).

## PR 4 — Catálogo y adaptador compatible con OpenAI (rama `feat/asistente-config-pr4-catalogo-openai`)

| Tarea | Estado | Commit |
|---|---|---|
| 4.1 Copia previa | Hecha (`..\TimeTracker.rollback\pr-4\`: `Directory.Packages.props`, csproj de Infrastructure, `ChatClientFactory.cs`, `AssistantChatPipeline.cs`, `DependencyRuleTests.cs`; la carpeta ya existía de un cambio anterior y se sobrescribieron esos nombres) | |
| 4.2 [V-1] Paquete | Hecha | `d31840f` |
| 4.3 + 4.4 Spike [V-2] y `CreateChatOptions` | Hecha | `c152358` |
| 4.5 [V-3] Excepciones por SDK | Hecha (anotado en design §12; sondeo temporal, no se commitea) | |
| 4.6 Fábrica | Hecha | `b10fa58` |
| Limpieza acordada (PR 3a.1) | Hecha: `ProviderAdapterLogger`, su registro, `LacksAdapter` y `ProviderAdapterLogTests` eliminados | `648b32b` |
| 4.7 Guardas AR | Hecha | `ea832d5` |
| 4.8 REFACTOR y puerta | Hecha | |

### TDD Cycle Evidence (PR 4)

| Tarea | Archivo de prueba | Capa | RED | GREEN | TRIANGULAR | REFACTOR |
|---|---|---|---|---|---|---|
| 4.2 | (restore/build/`list package`) | Paquete | N/A: no hay código; la comprobación es el propio restore | 0 NU1608/NU1903, VULN limpio | `dotnet package search` confirma que 10.10.0 existe | Ninguno |
| 4.3/4.4 | `ReasoningEffortSpikeTests` | Infrastructure | 9 de 13 fallan por el motivo previsto: OpenAI, Gemini y Anthropic con `Expected: "low" Actual: null` (cuerpo sin el campo). Ollama no se vio en el primer informe (salida recortada); tras el GREEN falló por una excepción no capturada (`OllamaException`) del manejador 400 y se amplió el filtro de la prueba | 13/13 | 3 niveles × 4 proveedores, URL de Gemini fija y caso sin esfuerzo (ninguno envía campo) | `ToReasoningEffort` privado |
| 4.6 | `ChatClientFactoryTests` | Infrastructure | No compila: `ProviderEndpoints`, `IChatClientFactory`, `DefaultChatClientFactory` y `CreateOpenAIClientOptions` inexistentes | 13/13 | OpenAI sin `Endpoint` (URL del SDK), Gemini con la fija aunque haya `Ai:Endpoint`, `NetworkTimeout`, 2 peticiones ante 500 (un reintento), delegación de la fábrica inyectable | Fábrica registrada con `TryAddSingleton` y usada por `AssistantChatClient` |
| Limpieza | `AssistantRegistrationTests.WithOpenAIOrGemini_...` | Infrastructure | Verde a la primera: el comportamiento lo daba la fábrica; protege que OpenAI/Gemini queden habilitados al borrar el aviso | 263/263 | `OpenAI` y `gemini` | |
| 4.7 | `RepositoryConventionsTests` (2 pruebas nuevas) | Architecture | Mutación temporal: `using OpenAI;` en `Api/Assistant/AssistantRequest.cs` y un `new System.Uri(...)` en `Infrastructure/Assistant/Mut.cs`. Primer intento: la expresión solo buscaba `new Uri(` y no vio `new System.Uri(`; se cambió a la expresión `new\s+(System\.)?Uri\(` y se repitió | 106/106 | Declaración del paquete solo en Infrastructure, versión central 10.10.0, uso del espacio de nombres, `new Uri(` solo en `ProviderEndpoints` y fábrica; `Src_DoesNotHardcodeModelNames` (V-5) sigue verde | |

### Work Unit Evidence (PR 4)

| Evidencia | Valor |
|---|---|
| Prueba focal | `dotnet test tests/TimeTracker.Infrastructure.Tests --filter FullyQualifiedName~ReasoningEffortSpike` = 13/13; `--filter FullyQualifiedName~ChatClientFactory` = 13/13; `dotnet test tests/TimeTracker.ArchitectureTests` = 106/106 |
| Arnés de ejecución | Manejador HTTP de grabación sin red sobre los clientes reales de OpenAI, Anthropic y OllamaSharp; ninguna llamada a un proveedor ni clave real |
| Límite de rollback | Quitar `Microsoft.Extensions.AI.OpenAI` (`Directory.Packages.props` y csproj) y revertir `ChatClientFactory`/`CreateChatOptions`/`IChatClientFactory`/`ProviderEndpoints`; la limpieza de `ProviderAdapterLogger` es un commit aparte |

### Puerta del PR 4 (ejecutada)

RESTORE ok; BUILD Release `-warnaserror` 0 advertencias 0 errores; `dotnet format --verify-no-changes` sin cambios; TEST: Domain 67, Application 323, Web 223, Infrastructure 263, Architecture 106, Api.IntegrationTests 464 (Docker disponible; no se tocaron contenedores, volúmenes, redes ni imágenes; sin prune); VULN (`--vulnerable --include-transitive`) limpio. `docs/api/openapi.json` restaurado con `git checkout` y sin commitear.

### Decisiones fuera de spec (PR 4)

- `AssistantProviderTraits.SupportsEffort` no cambia: el spike confirma los cuatro proveedores (design §12, V-2).
- `IChatClientFactory` queda registrada (`TryAddSingleton`) y `AssistantChatClient` ya la usa en vez de la llamada estática, para que la PR 5 y las pruebas puedan sustituirla; `ChatClientFactory.Create` estático se conserva.
- Ollama con 400 no expone estado HTTP (`OllamaException`/`InvalidOperationException`): la PR 6a decide si lo trata como `rejected` o lo deja en `unreachable` (design §12, V-3).
- Para que la guarda `new Uri(` cuente la constante de Gemini, `ProviderEndpoints` usa `new Uri(...)` explícito en lugar de `new(...)`.
- Tamaño: 333 líneas añadidas y 78 borradas (≈ 215 de pruebas, ≈ 60 de producción, resto documentos) frente a las ≈ 350 previstas.

## PR 4.1 — Correcciones de la revisión de la PR 4 (rama `fix/asistente-config-pr4-1-revision`)

| Tarea | Estado | Commit |
|---|---|---|
| 4.1.1 URL base de Anthropic | Hecha | `cf62c3f` |
| 4.1.2 + 4.1.3 Comentario del reintento y HttpClient de Ollama | Hechas | `590c94a` |
| 4.1.4 Revalidación en `Create` | Hecha | `f5b15b5` |
| 4.1.5 Un tipo por archivo y spike sin duplicación | Hecha | `30ece23` |
| 4.1.6 Guarda de `Uri` | Hecha | `6b590f2` |

### TDD Cycle Evidence (PR 4.1)

| Tarea | Archivo de prueba | Capa | RED | GREEN | TRIANGULAR | REFACTOR |
|---|---|---|---|---|---|---|
| 4.1.1 | `ChatClientFactoryTests` | Infrastructure | `Expected: "api.anthropic.com" Actual: "desvio.invalid"`: con `ANTHROPIC_BASE_URL` definida el SDK enviaba la petición al host del entorno (desvío real demostrado). La prueba del `Timeout` salió verde a la primera: el valor ya se asignaba | 15/15 | Variable restaurada en `finally`; no depende del entorno de la máquina; `Timeout` con 17 s | Ninguno |
| 4.1.3 | `ChatClientFactoryTests` | Infrastructure | No compila: `CreateOllamaClient` y `CreateOllamaHttpClient` inexistentes | 18/18 | `BaseAddress` y `Timeout`; manejador que no responde con `TimeoutSeconds = 1` (sin sockets); liberación del manejador al desechar | `OwnedHttpClientChatClient` en su archivo |
| 4.1.2 | (comentario) | N/A | N/A | N/A | N/A | Valor real citado de `ChatAssistantEngine`: `CancellationTokenSource(TimeoutSeconds)`, 25 s por defecto |
| 4.1.4 | `ChatClientFactoryTests` | Infrastructure | 8 casos: 7 «No exception was thrown» y 1 `ArgumentNullException` en vez de `InvalidOperationException` | 43/43 (con registro) | 2 valores × 4 proveedores; el mensaje nombra `Ai:<valor>` y el proveedor y no contiene la clave | Ninguno |
| 4.1.5 | `ReasoningEffortSpikeTests`, `ChatClientFactoryTests` | Infrastructure | N/A (refactor con pruebas verdes antes y después) | 43/43 | Teorías por tabla `Dictionary` + `MemberData` (12 casos de esfuerzo + 4 sin esfuerzo + URL de Gemini) | `DefaultChatClientFactory.cs` |
| 4.1.6 | `RepositoryConventionsTests` | Architecture | La guarda endurecida detectó un caso real: `AiOptionsValidator` usa `Uri.TryCreate` (solo valida http/https). Mutación temporal con `new UriBuilder(...)` en `Assistant/Mut.cs`: la guarda falla y el archivo se borró | 126/126 | 8 formas detectadas, 3 negativas, formas exactas permitidas en fábrica y validador | `SourceFiles` usa `IsBuildOutput` |

### Work Unit Evidence (PR 4.1)

| Evidencia | Valor |
|---|---|
| Prueba focal | `dotnet test tests/TimeTracker.Infrastructure.Tests --filter "FullyQualifiedName~ChatClientFactory\|FullyQualifiedName~ReasoningEffortSpike"` = 43/43; `dotnet test tests/TimeTracker.ArchitectureTests` = 126/126 |
| Arnés de ejecución | Manejadores HTTP sin red (de grabación, que no responde y contador) sobre los clientes reales; ninguna llamada a un proveedor ni clave real |
| Límite de rollback | Revertir los 5 commits por orden inverso; cada uno es independiente salvo que 4.1.3 y 4.1.4 tocan `ChatClientFactory.cs` |

### Puerta del PR 4.1 (ejecutada)

BUILD Release `-warnaserror` 0 advertencias 0 errores; `dotnet format --verify-no-changes` sin cambios; TEST: Domain 67, Application 323, Web 223, Infrastructure 280, Architecture 126, Api.IntegrationTests 464 (Docker disponible; no se tocaron contenedores, volúmenes, redes ni imágenes; sin prune); VULN (`--vulnerable --include-transitive`) limpio. `docs/api/openapi.json` restaurado con `git checkout` y sin commitear.

### Decisiones fuera de spec (PR 4.1)

- `OwnedHttpClientChatClient` (`DelegatingChatClient`): OllamaSharp no libera un `HttpClient` ajeno ni expone el plazo si crea el suyo; `GetService<OllamaApiClient>()` sigue resolviendo el cliente interno.
- `ChatClientFactory.Create` valida antes de construir (no en cada helper interno): los helpers internos conservan `!` porque solo se invocan tras la validación o desde pruebas con valores completos.
- La guarda de `Uri` admite una forma exacta en `AiOptionsValidator.cs` (`Uri.TryCreate(value, UriKind.Absolute, out var uri)`), descubierta por la propia guarda endurecida: el validador solo comprueba el esquema y no conserva la dirección.
- Límite del escáner: no detecta `new(...)` con tipo inferido; `ProviderEndpoints` usa `new Uri(...)` explícito y la PR 4 ya lo exigía.
- La prueba de la variable de entorno modifica estado del proceso: se restaura en `finally`; las demás pruebas que construyen clientes de Anthropic usan manejadores y no dependen de la URL base.
