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
