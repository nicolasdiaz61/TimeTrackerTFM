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

## PR 4.2 — Correcciones de la revisión de la PR 4.1 (rama `feat/asistente-config-pr5-recarga`, primeros commits)

| Tarea | Estado | Commit |
|---|---|---|
| (a) Ollama no sigue redirecciones | Hecha | `fdac75e` |
| (b) Validar el `Endpoint` antes de crear el `HttpClient` y exigir http/https absoluto | Hecha | `e899804` |
| (c) Guarda de `Uri`: `new(...)` inferido y `global::System`, nombre y límites documentados | Hecha | `f84ce95` |
| (d) `ANTHROPIC_BASE_URL` en colección sin paralelismo y (e) plazo de Ollama con tipo de excepción y cota | Hechas | `042d3ac` |

### TDD Cycle Evidence (PR 4.2)

| Tarea | Archivo de prueba | Capa | RED | GREEN | TRIANGULAR | REFACTOR |
|---|---|---|---|---|---|---|
| (a) | `ChatClientFactoryTests` | Infrastructure | `El destino de la redirección recibió una conexión.`: con el manejador por defecto el 307 se seguía hacia `127.0.0.2` (dos escuchas TCP locales, sin red externa) | 27/27 | El origen sirve un solo 307; el destino no recibe conexión y la llamada falla acotada por `WaitAsync` | Comentario del porqué |
| (b) | `ChatClientFactoryTests` | Infrastructure | No compila (`Func<HttpMessageHandler>` inexistente) | 36/36 | 4 `Endpoint` inválidos (texto, ruta relativa, `ftp://`, `file://`) × (`CreateOllamaHttpClient`: 0 manejadores creados; `Create`: mensaje con `Ai:Endpoint` y sin el valor) + uno válido que crea el manejador una vez | La guarda de arquitectura admite ahora la forma `TryCreate` exacta de la fábrica |
| (c) | `RepositoryConventionsTests` | Architecture | 7 casos fallaron (`Uri u = new(...)`, `Uri? u = new(...)`, campo `static readonly`, propiedad `{ get; } = new(...)`, `BaseAddress = new(...)`, `global::System.Uri`/`UriBuilder`/`TryCreate`) | 137/137 | 3 negativos (`List<string> items = new()`, `Uri? uri = null`, `new HttpRequestMessage()`) | Prueba renombrada a `InfrastructureAssistant_BuildsNoUriOutsideTheApprovedSyntacticForms`; el comentario de `UriCreation` enumera lo que cubre y lo que no |
| (d) | (infraestructura de prueba) | Infrastructure | N/A: el aislamiento no tiene un fallo observable determinista | 290/290 | `ProcessEnvironmentCollection` con `DisableParallelization = true`; la prueba restaura la variable en `finally` | Nada |
| (e) | `ChatClientFactoryTests` | Infrastructure | Mutación temporal: con `Timeout` de 30 s la prueba falla a los 5 s (`WaitAsync`) en vez de aceptar cualquier excepción | 290/290 | Exige `TaskCanceledException`/`TimeoutException` y `800 ms <= t <= 4 s` (la `TimeoutException` de `WaitAsync` llegaría a los 5 s) | Nada |

### Decisiones fuera de spec (PR 4.2)

- `CreateOllamaClient` y `CreateOllamaHttpClient` reciben `Func<HttpMessageHandler>?` en vez de la instancia: es la única forma de comprobar que no se crea ningún manejador cuando el `Endpoint` es inválido.
- La fábrica ya no usa `new Uri(options.Endpoint!)` sino `Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint)` con comprobación de esquema; el mensaje es fijo («Ai:Endpoint debe ser una URL absoluta con esquema http o https.»).
- La prueba de redirección escucha en `127.0.0.1` y `127.0.0.2`: en Linux y Windows ambas son de bucle local; no funcionaría en macOS.

## PR 5 — Recarga en caliente (rama `feat/asistente-config-pr5-recarga`, apilada sobre `fix/asistente-config-pr4-1-revision`)

| Tarea | Estado | Commit |
|---|---|---|
| 5.1 Copia previa (`..\TimeTracker.rollback\pr-5\`) | Hecha | fuera del repositorio |
| 5.2 `AssistantSnapshot`, `Current`, `Swap` | Hecha | `e7c2cb2` |
| 5.3 Motor sobre la instantánea | Hecha | `5bd2a3d` |
| 5.4 Cargador, entorno y recargador | Hecha | `a308f7c`, `bc0d60a` |
| 5.5 Sondeo | Hecha | `41d6ec2` |
| 5.6 [V-6] Registro en `Program.cs` y orden de arranque | Hecha | `c7ca640` |
| 5.7 Guardado externo con PostgreSQL | Hecha | `4ef895f` |
| 5.8 REFACTOR y puerta | Hecha | (sin commit propio) |

### TDD Cycle Evidence (PR 5)

| Tarea | Archivo de prueba | Capa | RED | GREEN | TRIANGULAR | REFACTOR |
|---|---|---|---|---|---|---|
| 5.2 | `AssistantChatClientSwapTests` | Infrastructure | No compila: `Current`, `Swap` y el constructor con `TimeProvider` inexistentes | 10/10 | Versión `"0"` del constructor compatible; Swap a `null` y vuelta; copia defensiva sin `ApiKey` ni `MasterKeys`; anterior viva a 119,999 s y liberada a 120 s; 3 cambios seguidos con plazos propios; `Dispose` una sola vez; `Swap` tras `Dispose` lanza y libera el cliente nuevo | `AiOptions.Copy()` |
| 5.3 | `ChatAssistantEngineSnapshotTests` | Infrastructure | No compila: el constructor del motor aún pedía `IOptions<AiOptions>` | 305/305 | Swap a mitad de una petición con bucle de herramientas (2 llamadas al pipeline inicial, 0 al nuevo; modelo, tokens y esfuerzo iniciales); el plazo inicial de 10 s corta aunque el nuevo sea 40 s; la siguiente petición usa lo nuevo; Swap a `null` a mitad no rompe la petición en curso y la siguiente da `null`; la cuota diaria sigue del entorno | Se quitó `Pipeline` de `AssistantChatClient` |
| 5.4 | `ConfigurationAssistantEnvironmentTests`, `AssistantSettingsLoaderTests`, `AssistantRuntimeReloaderTests` | Infrastructure | No compila: `AssistantSettingsLoader`, `IAssistantSettingsLoader`, `SettingsLoadOutcome`, `ConfigurationAssistantEnvironment` y `AssistantRuntimeReloader` inexistentes | 333/333 | Fila válida (versión, valores, clave descifrada, sin avisos); valores omitidos del entorno; `None`; sin fila tras fila; sin fila inicial y versión repetida sin reconstruir; fila inválida con una sola advertencia por versión y otra por versión nueva; texto cifrado alterado, versión retirada y sin clave maestra (`undecryptable`, la fábrica no se llama); proveedor ya no permitido (`not-allowed`); fábrica que falla (solo el tipo en el registro); rechazo limpiado tras un éxito; base de datos caída propaga; cargas serializadas. Mutación: sin el `SemaphoreSlim` la prueba de serialización falla (`MaxConcurrentReads` 2). Recargador: éxito sin registro, fallo con aviso solo del tipo y relanzado, cancelación sin aviso | Dos commits (cargador / recargador y registro) |
| 5.5 | `AssistantSettingsPollerTests` | Infrastructure | No compila: `AssistantSettingsPoller` inexistente. Después, 2 pruebas fallaron de forma determinista: el primer intervalo se perdía porque el host ejecuta `ExecuteAsync` más tarde que `StartAsync` | 344/344 (3 ejecuciones seguidas verdes) | Carga solo si cambia la versión (4 ticks); versión rechazada no se recarga y una nueva sí; sin leer antes del intervalo; caída: un aviso por racha, una línea al recuperar y otro aviso en la racha siguiente; fallo de carga como misma racha; primera carga antes de devolver, con error capturado y acotada a 10 s (9 s no, 10 s sí); cancelación del host se propaga; `StopAsync` detiene; `SettingsPollSeconds` = 7 | El `PeriodicTimer` se crea en `StartAsync` |
| 5.6 | `AssistantSettingsStartupOrderTests` | Integration (PostgreSQL) | 3 de 4 pruebas fallaron: `La primera carga no se hizo durante el arranque.` y `Assert.Single() Failure: The collection was empty` (poller sin registrar); la de «sin fila» pasó por ser el entorno | 7/7 con 5.7; suite completa 471/471 | Orden: la tabla existe en la primera lectura con una base vacía y migraciones al arrancar; el primer chat usa la fila sembrada; con y sin fábrica que conserva el sondeo | `Program.cs` no lo registra al exportar OpenAPI (verificado: el build dejó de contactar `db.invalid`) |
| 5.7 | `AssistantSettingsExternalSaveTests` | Integration (PostgreSQL) | `El sondeo no leyó la versión a tiempo.` (3 pruebas, sin poller) | 7/7 | INSERT y UPDATE externos cambian el modelo; fila `None` → 503 y borrar la fila restaura el entorno; fila inválida (Anthropic sin clave) conserva el entorno y la fábrica no crea su cliente | Nada |

### Work Unit Evidence (PR 5)

| Evidencia | Valor |
|---|---|
| Prueba focal | `dotnet test tests/TimeTracker.Infrastructure.Tests` = 344/344 (Swap, motor sobre instantánea, cargador, recargador, sondeo); `dotnet test tests/TimeTracker.Api.IntegrationTests --filter "FullyQualifiedName~AssistantSettingsStartupOrder\|FullyQualifiedName~AssistantSettingsExternalSave"` = 7/7 |
| Arnés de ejecución | API real (`WebApplicationFactory`) con PostgreSQL de Testcontainers, `IChatClientFactory` falso (`RecordingChatClientFactory`), SQL externo y el reloj ficticio avanzado un segundo por vuelta; sin red ni proveedor real |
| Límite de rollback | Revertir `c7ca640` y `4ef895f` quita el sondeo (el sistema vuelve al entorno); revertir `5bd2a3d` y `e7c2cb2` devuelve el motor y el cliente al estado de la PR 4; la copia previa está en `..\TimeTracker.rollback\pr-5\` |

### Puerta del PR 5 (ejecutada)

BUILD de la solución con `-warnaserror` 0 advertencias 0 errores; `dotnet format --verify-no-changes` sin cambios; TEST: Domain 67, Application 323, Infrastructure 344, Web 223, Architecture 137, Api.IntegrationTests 471 (Docker disponible; no se tocaron contenedores, volúmenes, redes ni imágenes existentes; sin prune); VULN (`--vulnerable --include-transitive`) limpio en los 11 proyectos. `docs/api/openapi.json` restaurado con `git checkout` y sin commitear (el contrato no cambia en esta PR).

### Decisiones fuera de spec (PR 5)

- `IChatClientFactory` pasa a ser **pública** (la implementación por defecto sigue interna): Api la sustituye en las pruebas de integración y el cargador la recibe por constructor. Alternativa descartada: `InternalsVisibleTo` hacia el proyecto de pruebas de Api.
- `IAssistantSettingsLoader` es una interfaz **pública** (`ReadVersionAsync`, `LoadAsync`, `LastRejectedVersion`) porque `AssistantSettingsPoller` es público (Api lo registra y `ApiFactory` lo elimina por tipo) y su constructor no puede exponer tipos internos. La implementación es interna. El cargador abre su propio ámbito para el `DbContext`, también para la lectura de versión.
- La instantánea **no conserva la clave del proveedor ni las claves maestras** (`ApiKey` y `MasterKeys` se anulan en la copia): el motor solo usa modelo, plazo, tokens y esfuerzo. El diseño decía «copia defensiva con los valores efectivos».
- Una fila con clave guardada que no se puede descifrar se rechaza (`undecryptable`) **aunque el entorno tuviera una clave utilizable del mismo proveedor**: sigue AS «Fila inválida» («no se puede descifrar: conserva la última válida») y DD-11, en lugar del respaldo al entorno que haría el resolver.
- Una versión ya aplicada (`Version` de la fila igual a `Current.Version`) devuelve `Unchanged` sin reconstruir el cliente; sin fila y con la instantánea inicial (`"0"`) tampoco se reconstruye.
- El **aviso de la recarga fallida tras guardar** lo deja `AssistantRuntimeReloader` (registra solo el tipo y relanza); `ReloadAfterCommitAsync` sigue tragando la excepción tras el guardado confirmado, como decidió la PR 2.
- El `PeriodicTimer` del sondeo se crea en `StartAsync`, no en `ExecuteAsync` (hallazgo de 5.5). El sondeo no se registra al exportar OpenAPI (`DocumentGeneration.IsRunningUnderTool`).
- En las pruebas de integración el sondeo avanza con el `FakeTimeProvider` de la aplicación (que también emite los tokens): `ReloadApiFactory.PollOnceAsync` avanza un segundo y espera a la lectura. Los dobles `DisposalTrackingChatClient` y `RecordingChatClientFactory` se comparten por enlace de compilación con Api.IntegrationTests, igual que `FakeChatClient`.
- V-6 se cumple tal como está el orden de arranque, así que **no** se implementó la marca de instantánea inicial «no cargada» (era solo la contingencia).
- Tamaño: 11 commits de código, 495 líneas de producción y 1573 de pruebas (≈ 2068 en total con las correcciones 4.2), por encima de la previsión de ≈ 380 líneas del PR 5 (sobre todo pruebas).

## PR 6a — Prueba de conexión y límite de tasa (rama `feat/asistente-config-pr6a-prueba-conexion`, apilada sobre `feat/asistente-config-pr5-recarga`)

| Tarea | Estado | Commit |
|---|---|---|
| 6a.1 Copia previa (`..\TimeTracker.rollback\pr-6a\`: `RateLimitingServiceCollectionExtensions.cs` y `AssistantRateLimitTests.cs`) | Hecha | fuera del repositorio |
| 6a.2 `ConnectionFailureClassifier` y `ConnectionTestResults` | Hecha | `b5e44f9` |
| 6a.3 `AssistantConnectionTester`, registro y registro de eventos | Hecha | `95896ac` |
| 6a.4 Política `assistant-config-test` y `OnRejected` por política | Hecha | `19a9f51` |
| 6a.5 REFACTOR y puerta | Hecha | (el REFACTOR de 6a.4 va en su commit) |

### TDD Cycle Evidence (PR 6a)

| Tarea | Archivo de prueba | Capa | RED | GREEN | TRIANGULAR | REFACTOR |
|---|---|---|---|---|---|---|
| 6a.2 | `ConnectionFailureClassifierTests` | Infrastructure | No compila: `ConnectionFailureClassifier` y `ConnectionTestResults` inexistentes (solo esos errores CS0103) | 36/36 a la primera | Excepciones reales de los tres SDK (OpenAI, Anthropic, Ollama) con un manejador que solo devuelve el estado y un cuerpo que miente (`401… 404… 400…`): 401/403, 404, 422, 429, 500, 502 en cada SDK y 400 en OpenAI/Anthropic; Ollama 400 = `unreachable`; estado en una excepción interna; el mensaje nunca decide; plazo propio y `TimeoutException` interna = `timeout`; textos fijos de las seis categorías | Nada necesario |
| 6a.3 | `AssistantConnectionTesterTests`, `AssistantRegistrationTests` | Infrastructure | No compila: `AssistantConnectionTester` inexistente; después, la prueba de registro falló con `No service for type 'IAssistantConnectionTester' has been registered` | 17/17 y suite 398/398 | Mensaje fijo único sin herramientas, 64 tokens, modelo y esfuerzo del candidato (Low/High/nulo); la fábrica recibe valores del candidato y solo el endpoint del entorno; la clave candidata gana a la del entorno; plazo `min(40, 15)` = 15 s y `min(10, 15)` = 10 s con `FakeTimeProvider` (a 14,999 s sigue pendiente); cliente desechado en éxito, fallo, plazo y cancelación; fallo de creación = `unreachable`; cancelación del llamante se propaga; instantánea y cliente del chat intactos; 3 fallos con URL y clave en el mensaje: ni respuesta ni registro los contienen y el registro lleva solo tipo y categoría. Mutación: con el plazo sin `min` la prueba de 40 s falla | Nada necesario |
| 6a.4 | `AssistantConfigTestRateLimitTests` | Api.IntegrationTests | No compila: `RateLimitingServiceCollectionExtensions.AssistantConfigTestPolicy` inexistente | 5/5 nuevas + 20 del chat/login = 25/25 | 3 permisos y 4.ª = 429 con detalle propio y `Retry-After` 1–60; partición por `sub` (otro `sub` tiene sus 3); 6 anónimos (401) y 6 Employee (403) no consumen permisos del mismo `sub` que luego hace 3 pruebas como Manager; chat intacto (6.ª = 429 con su texto) y contadores independientes | `PerUser(...)` compartido por las políticas del chat y de la prueba; `DetailFor(policy)` en lugar del ternario |

### Work Unit Evidence (PR 6a)

| Evidencia | Valor |
|---|---|
| Prueba focal | `dotnet test tests/TimeTracker.Infrastructure.Tests` = 398/398 (+54: clasificador 36, probador 17, registro 1); `dotnet test tests/TimeTracker.Api.IntegrationTests --filter "FullyQualifiedName~AssistantConfigTestRateLimit\|FullyQualifiedName~AssistantRateLimitTests\|FullyQualifiedName~LoginRateLimit"` = 25/25 |
| Arnés de ejecución | Clasificador: SDK reales con un `HttpMessageHandler` de estado fijo, sin red. Política: servidor en memoria (`TestServer`) con el `AddRateLimiting()` real y el orden de `Program` (autenticación, autorización, limitador) sobre dos rutas mínimas; el endpoint real llega en la PR 6b (6b.6 repite el 429 sobre él) |
| Límite de rollback | Revertir `19a9f51` quita la política; revertir `95896ac` y `b5e44f9` quita probador, clasificador y su registro. Copia previa en `..\TimeTracker.rollback\pr-6a\` |

### Puerta del PR 6a (ejecutada)

BUILD de la solución con `-warnaserror` 0 advertencias 0 errores; `dotnet format --verify-no-changes` sin cambios; TEST: Domain 67, Application 323, Infrastructure 398, Web 223, Architecture 137, Api.IntegrationTests 476 (Docker disponible; no se tocaron contenedores, volúmenes, redes ni imágenes existentes; sin prune); VULN (`--vulnerable --include-transitive`) limpio en los 11 proyectos. `docs/api/openapi.json` restaurado con `git checkout` y sin commitear (el contrato no cambia: aún no hay endpoints).

### Decisiones fuera de spec (PR 6a)

- **Ollama con 400 = `unreachable`** (V-3): `OllamaException` / `InvalidOperationException` no traen estado HTTP y la segunda es genérica; clasificarlas como `rejected` también etiquetaría fallos locales. Consecuencia aceptada: un modelo de Ollama que rechaza `think` aparece como «No se pudo contactar con el proveedor». Una prueba lo fija; reabrir solo si OllamaSharp expone el estado.
- Un `TimeoutException` en la cadena de excepciones (plazo propio de `HttpClient` en Ollama o de red de los SDK) también es `timeout`, además del plazo propio del probador; así una carrera entre ambos plazos no cambia la categoría.
- La excepción se examina recorriendo `InnerException`; el estado se toma de la primera que lo tenga.
- Cualquier respuesta completada es `ok`, aunque el texto venga vacío (la spec dice «respuesta correcta»).
- El probador no usa el pipeline de herramientas (`AssistantChatPipeline.Build`): reutiliza solo `CreateChatOptions(options, [])` con `MaxOutputTokens = 64` en las opciones copiadas. Del entorno solo toma el endpoint de Ollama; la clave es siempre la del candidato.
- Registro: dos eventos nuevos (20 y 21) con proveedor, categoría y tipo de la excepción; ni el modelo ni el mensaje.
- `IAssistantConnectionTester` se registra como singleton (sin estado); el caso de uso `TestAssistantConnectionUseCase` se registra en 6b.4 con el resto.
- La prueba de la política usa un servidor mínimo en lugar del API completo: el endpoint real no existe hasta la 6b.
- Tamaño: 177 líneas de producción y 567 de pruebas (≈ 744) frente a las ≈ 220 previstas; la desviación son las pruebas triangulares (SDK reales, plazo, no-eco).

## PR 6b — API: endpoints, política Manager y OpenAPI (rama `feat/asistente-config-pr6b-api`, apilada sobre `feat/asistente-config-pr6a-prueba-conexion`)

| Tarea | Estado | Commit |
|---|---|---|
| 6b.1 Copia previa (`..\TimeTracker.rollback\pr-6b\`: `TeamEndpoints.cs`, `Program.cs`, `openapi.json`, `OpenApiContractTests.cs`, `AuthorizationCoverageTests.cs`, `IUserRepository.cs`, `UserRepository.cs`, `InMemoryUserRepository.cs`) | Hecha | fuera del repositorio |
| 6b.2 [V-8] Política `Manager` en `Security/AuthorizationPolicies` | Hecha | `29c1f40` |
| 6b.3 [V-7] DTO estrictos (`Disallow`) | Hecha | `eeb8f61` |
| 6b.4 Los cuatro endpoints y registro de casos de uso | Hecha | `eeb8f61` |
| 6b.5 Clave de solo escritura y fallo cerrado | Hecha | `4a4aa27` |
| 6b.6 Prueba de conexión y 429 sobre el endpoint real | Hecha | `a424868` |
| 6b.7 Contrato OpenAPI regenerado y `OpenApiContractTests` ampliada | Hecha | `22c236b` |
| 6b.8 REFACTOR y puerta | Hecha | (el REFACTOR va en cada commit) |

### TDD Cycle Evidence (PR 6b)

| Tarea | Archivo de prueba | Capa | RED | GREEN | TRIANGULAR | REFACTOR |
|---|---|---|---|---|---|---|
| 6b.2 | `AuthorizationCoverageTests` | Api.IntegrationTests | No compila: `CS0103 El nombre 'AuthorizationPolicies' no existe` (solo ese error) | 48/48 (cobertura de autorización, `/equipo` y política de respaldo; línea base 37/37 en Authorization+Team) | Nueva prueba: la política está registrada una sola vez y exige el rol `Manager`; `/equipo` sigue exigiéndola | Nada necesario |
| 6b.3 | `AssistantConfigurationStrictBodyTests` | Api.IntegrationTests | 6/6 fallan con `Expected: BadRequest, Actual: NotFound` (ruta inexistente) | 6/6 con `JsonUnmappedMemberHandling.Disallow` **sin validar a mano**: V-7 se cumple. Mutación: quitando el atributo, `url`/`endpoint` dan `500` en el PUT y `200` en la prueba | `url`, `endpoint`, `baseUrl` en el PUT; `url`, `endpoint` en la prueba; JSON roto; con un almacén que cuenta lecturas y escrituras: 0 y 0, y el probador sin invocar | Peticiones con valores por defecto `null` (el contrato no marca campos obligatorios) |
| 6b.4 | `AssistantConfigurationEndpointsTests` | Api.IntegrationTests (PostgreSQL 18) | 21 de 25 fallan (`NotFound` en todo); los 4 anónimos pasan por la política de respaldo (el 401 es lo que ya daba cualquier ruta inexistente) | 25/25 | 401 anónimo y 403 Employee en los cuatro endpoints con 0 lecturas/escrituras; `GET` con los 13 campos y orígenes; `PUT` válido = mismo texto que el `GET`, token nuevo, auditoría `Saved`, la fábrica de clientes recibe el modelo nuevo; 400 por campo (`effort`, `model`, `timeoutSeconds`) y por `concurrencyToken` ausente; 409 en primera creación y en actualización con token obsoleto (el ganador se conserva, 2 auditorías); `DELETE` 204 con fila (auditoría `Reset`, `GET` con origen `env` y token `0`), 204 sin fila (0 auditorías) y 409 con fila cambiada tras la lectura; 413 con 9 KiB en `PUT` y en la prueba (Kestrel) sin lecturas; `updatedBy` con nombre visible y `null` si el usuario no existe | `updatedBy` por `IUserRepository.FindDisplayNameAsync` (puerto nuevo) |
| 6b.5 | `AssistantConfigurationCredentialTests` | Api.IntegrationTests (PostgreSQL 18) | **Verde a la primera** (5/5 tras corregir un error de mi propia prueba): los casos de uso y los endpoints de 6b.4 ya cumplen CP; no hay código nuevo. Mutación: con `credentialStorageAvailable` fijo a `true` falla la prueba del indicador | 5/5 | Sin clave maestra: `PUT` con clave = 400 en `credential` sin filas ni auditoría; `GET` con y sin clave maestra; `provider=None` con clave = 400 en `credential`; `GET`, `PUT` y prueba nunca devuelven la clave, su Base64/hex cifrado ni la clave maestra, y no tienen propiedad `credential` en la raíz; el probador recibe la clave candidata; el indicador pasa a `false`/`none` tras `DELETE`; el 400 no repite la clave | Nada necesario |
| 6b.6 | `AssistantConfigurationConnectionTestEndpointTests` | Api.IntegrationTests | **Verde a la primera** (10/10): el endpoint y la política ya se registraron en 6b.4. Mutación: quitando `RequireRateLimiting` fallan las dos pruebas del 429 | 10/10 | `ok`, `authentication`, `rejected`, `unreachable` con texto fijo; cuerpo vacío = configuración efectiva; 400 sin invocar al probador; 4.ª = 429 con detalle propio y `Retry-After` 1-60; 400,400,200,429 (cuentan las que acaban en 400); 6 anónimos (401) y 6 Employee (403) del mismo `sub` no consumen permisos y luego el Manager hace 3; otro Manager tiene los suyos y el resto del grupo no se limita | Nada necesario |
| 6b.7 | `OpenApiContractTests` (4 pruebas nuevas) | Architecture | Con el `openapi.json` anterior restaurado: `Falta la ruta /api/asistente/configuracion en el contrato.` (2 pruebas) y `KeyNotFound` en los esquemas (2) | 15/15 tras regenerar con el build | Operaciones y respuestas exactas de DD-14 y Bearer en las cuatro; sin `requestBody` ni parámetros en `DELETE`; sin 413 declarado; campos exactos de peticiones y respuestas; `additionalProperties=false` en las dos peticiones; ningún esquema de configuración con `url`/`endpoint`; `credential` solo en las dos peticiones (y en `sources`, ver decisiones) y `credentialConfigured` en la respuesta; sin `apikey` ni `Ai__` (prueba existente) | `.ProducesValidationProblem()` en lugar de `ProducesProblem(400)` (el 400 documenta `errors`); peticiones sin campos obligatorios |

### Work Unit Evidence (PR 6b)

| Evidencia | Valor |
|---|---|
| Prueba focal | `dotnet test tests/TimeTracker.Api.IntegrationTests --filter "FullyQualifiedName~AssistantConfiguration"` = 40/40; `dotnet test tests/TimeTracker.ArchitectureTests --filter "FullyQualifiedName~OpenApiContractTests"` = 15/15 |
| Arnés de ejecución | `WebApplicationFactory` con el pipeline real de `Program` (autenticación, autorización, limitador), PostgreSQL 18 (Testcontainers) con migraciones, probador y fábrica de clientes falsos (nunca red); Kestrel para el 413 |
| Límite de rollback | Revertir `22c236b` restaura el contrato; `4a4aa27` y `a424868` solo son pruebas; `eeb8f61` quita endpoints, DTO, registro y `FindDisplayNameAsync`; `29c1f40` devuelve la política a `TeamEndpoints` (V-8). Copia previa en `..\TimeTracker.rollback\pr-6b\` |

### Puerta del PR 6b (ejecutada)

BUILD de la solución con `-warnaserror` 0 advertencias 0 errores; `dotnet format --verify-no-changes` sin cambios; TEST: Domain 67, Application 323, Infrastructure 398, Web 223, Architecture 141, Api.IntegrationTests 517 (Docker disponible; no se tocaron contenedores, volúmenes, redes ni imágenes existentes; sin prune); COV-A Application 99,9 % líneas / 96,5 % ramas (mínimo 80 %), COV-D Domain 100 %; VULN (`--vulnerable --include-transitive`) limpio. `docs/api/openapi.json` commiteado con solo adiciones (529 líneas generadas).

### Decisiones fuera de spec (PR 6b)

- **`DELETE` sin token de concurrencia en la petición**: se sigue DD-8/DD-14 (el servidor lee la fila y borra con su `xmin`; `Conflict` = 409, `Deleted`/`NotFound` = 204). El encargo hablaba de un token del cliente también en `DELETE`; el contrato del diseño no lo tiene y el caso de uso ya lo captura. Si se quiere token del cliente, es un cambio de contrato (nuevo parámetro y caso de uso).
- **`sources.credential`** (origen `env`/`stored`/`none`) es un campo exigido por DD-14 y no la clave: la prueba de contrato lo distingue (`credential` aparece solo en las dos peticiones y en `AssistantConfigurationSources`, tipo cadena). DD-16 (4) debe leerse así.
- **`updatedBy` con nombre visible**: DD-14 lo pide y la vista de Application solo trae el `Guid`; se añade `IUserRepository.FindDisplayNameAsync` (3 líneas en puerto, repositorio EF y doble en memoria). Devuelve `null` si el usuario ya no existe.
- **V-7**: `JsonUnmappedMemberHandling.Disallow` en un `record` posicional con valores por defecto da `400` problem+json (no `500`) con `ThrowOnBadRequest` activo; no hizo falta validar a mano.
- Los `400` de validación usan `ValidationProblem` con título en español «Solicitud no válida» y errores por campo; `409` usa `ProblemDetails` «Conflicto» con detalle fijo. El cuerpo ausente en `PUT` es `400` general; en la prueba equivale a «probar la efectiva».
- Pruebas con base de datos: base vacía por prueba (`CreateEmptyDatabaseAsync`) y migraciones al arrancar la fábrica; las pruebas sin base usan un almacén que cuenta operaciones.
- La política `Manager` se registra con `AddAuthorizationPolicies()` (nueva extensión) antes de `AddTeam()`; `AddTeam()` ya no registra políticas.
- Tamaño: ≈ 245 líneas de producción, ≈ 850 de pruebas y 529 de `openapi.json` generado (≈ 1.620 en total, ≈ 1.090 sin el contrato) frente a las ≈ 380 previstas; la desviación es de pruebas (HTTP real con PostgreSQL) y del contrato generado.

## PR 7 — Web: `/asistente/configuracion` (rama `feat/asistente-config-pr7-web`, apilada sobre `feat/asistente-config-pr6b-api`)

| Tarea | Estado | Commit |
|---|---|---|
| 7.1 Copia previa (`..\TimeTracker.rollback\pr-7\`: `NavMenu.razor`, `Program.cs`, `app.css`, `RepositoryConventionsTests.cs`, `ApiRequest.cs`, `AssistantConversation.cs`) | Hecha | fuera del repositorio |
| 7.2 `AssistantConfigurationForm` y DTO | Hecha | `9313002`, `8784354` |
| 7.3 `AssistantConfigurationApiClient` y sobrecarga sin cuerpo de `ApiRequest` | Hecha | `8d2d9fe` |
| 7.4 Página y guardas de convenciones | Hecha | `ee311d2` |
| 7.5 `NavMenu`, `Program.cs`, estilos y chat sin cambios | Hecha | `ee311d2` |
| 7.6 Verificación manual en Chrome | **Pendiente (orquestador)** | — |

### TDD Cycle Evidence (PR 7)

| Tarea | Archivo de prueba | RED | GREEN | TRIANGULAR | REFACTOR |
|---|---|---|---|---|---|
| 7.2 | `AssistantConfigurationFormTests` (Web.Tests) | No compila: `CS0246` por `AssistantConfigurationForm`, `AssistantConfigurationDto`, `AllowedProviderDto`, `AssistantConfigurationError` (solo ese error) | 39/39 tras corregir una prueba mía (`<b>x</b>` con origen `env` se omite por diseño; se usó origen `stored`) | Campos del entorno sin tocar = `null`; guardados se envían; vaciados = `null`; cambio de proveedor envía todo; esfuerzo omitido si no se admite; clave solo si se escribió y con almacenamiento; la clave se vacía tras éxito y tras 400/409/429/red; la prueba conserva la clave; `ToString` redacta la clave; 409 ofrece recargar; 400 por campo y resto agrupado; 429 con espera; `<b>x</b>` literal en modelo, errores, conflicto y prueba; confirmación de restablecer | La frase de espera del 429 se extrae a `Http/WaitHint` y la reutiliza `AssistantConversation` (223 pruebas previas en verde) |
| 7.2b | `UpdatedText` (misma clase) | No compila: `CS1061` y `CS0117` (`UpdatedText`, `NothingStoredMessage`) | 282/282 | Hora local con desfase, autor omitido, sin fila guardada, autor como texto | Nada necesario |
| 7.3 | `AssistantConfigurationApiClientTests` y 3 en `ApiRequestTests` | No compila: `CS0246 AssistantConfigurationApiClient` | 280/280 | GET/PUT/POST/DELETE con ruta y método; nombres JSON exactos con `null`; 400 con errores por campo, solo con `detail`, y cuerpos ilegibles; 409; 429 con `Retry-After`; 204; 403 genérico; red caída; la clave no vuelve en errores | `ApiRequest.SendAsync` comparte `SendCoreAsync` entre la sobrecarga con cuerpo y la de 204 |
| 7.4 | `RepositoryConventionsTests.Web_HasTheAssistantConfigurationPage...` | `Falta src/TimeTracker.Web/Pages/AssistantConfiguration.razor.` | Verde con la página | Sin `MarkupString`, `localStorage`, `sessionStorage`, `IJSRuntime`, `innerHTML`, `type="url"` ni «endpoint»; `[Authorize(Roles = "Manager")]`; campo `password` con `autocomplete="new-password"` y `disabled="@(!form.CredentialEnabled)"`; la clave se enlaza en un único sitio; avisos, botones y texto «clave configurada» | Componente `Shared/FieldErrors` para los mensajes por campo |
| 7.5 | `RepositoryConventionsTests.Web_LinksTheAssistantConfiguration...` | `Assert.Single() Failure: The collection was empty` (enlace ausente) | 143/143 en Architecture | Enlace solo dentro del bloque `Roles="Manager"` junto a «Equipo»; registro en `Program.cs`; `/asistente` sin referencias a la configuración y con `[Authorize]` | Nada necesario |

### Work Unit Evidence (PR 7)

| Evidencia | Valor |
|---|---|
| Prueba focal | `dotnet test tests/TimeTracker.Web.Tests --filter "FullyQualifiedName~AssistantConfiguration"` y `dotnet test tests/TimeTracker.ArchitectureTests --filter "FullyQualifiedName~RepositoryConventionsTests.Web_"` en verde |
| Arnés de ejecución | N/A en automático: la Web no tiene bUnit ni E2E; el recorrido real es la verificación manual 7.6 |
| Límite de rollback | Borrar `Pages/AssistantConfiguration.razor`, `Shared/FieldErrors.razor`, `Assistant/AssistantConfiguration*`, `Http/WaitHint.cs` (y devolver su frase a `AssistantConversation`), el enlace de `NavMenu`, el registro de `Program.cs`, los estilos y las dos guardas; copia previa en `..\TimeTracker.rollback\pr-7\` |

### Puerta del PR 7 (ejecutada)

BUILD de la solución con `-warnaserror` 0 advertencias 0 errores; `dotnet format --verify-no-changes` sin cambios; TEST: Domain 67, Application 323, Infrastructure 398, Web 282, Architecture 143, Api.IntegrationTests 517 (Docker disponible; no se tocaron contenedores, volúmenes, redes ni imágenes existentes; sin prune); VULN (`--vulnerable --include-transitive`) limpio. `docs/api/openapi.json` restaurado con `git checkout` y sin commitear (el contrato no cambia).

### Decisiones fuera de spec (PR 7)

- `[Authorize(Roles = "Manager")]` en lugar de `nameof(UserRole.Manager)`: la Web no referencia Domain (prueba existente); es el mismo literal que usa `/equipo`.
- La confirmación de «Restablecer al entorno» es un panel en la propia página (sin `confirm()` de JavaScript, para no usar `IJSRuntime`). Advierte que borra la configuración vigente aunque otro responsable la haya cambiado.
- Tras el `DELETE` (204 sin cuerpo) la página vuelve a hacer `GET` para mostrar la configuración efectiva.
- Valores del entorno sin tocar se envían como `null` para no congelarlos como guardados; si cambia el proveedor se envía todo explícito.
- Etiquetas de origen: `env` = «Entorno», `stored` = «Guardado por un responsable», `none` = «Sin clave».
- Texto del menú «Config. asistente» (corto para el menú lateral).
- `ApiRequest` gana una sobrecarga sin cuerpo (necesaria para el 204); la frase de espera del 429 se comparte con el chat.
- Tamaño aprox.: ≈ 800 líneas de producción y pruebas frente a las ≈ 350 previstas; la desviación es de pruebas del formulario y del cliente.

### Pendiente de verificación manual (7.6, orquestador)

Chrome contra API y Web locales: Manager ve ambos avisos y guarda; Employee sin enlace y «acceso denegado» por URL directa; clave vaciada tras guardar; el DOM no contiene la clave (inspeccionar); `<b>x</b>` como modelo se ve literal; 409 con dos pestañas muestra «Recargar»; «Probar conexión» muestra resultado y, tras 3 pruebas en un minuto, el mensaje de espera; sin `Ai__MasterKeys` el campo de clave sale desactivado con aviso. Registrar en `verify-report.md`.
