# Progreso de apply: asistente-config-responsable

Fecha: 2026-10-19 · Modo: Strict TDD · Rama: `feat/asistente-config-pr1-adr` (base `origin/main` 1f5dc47) · Lote: PR 1

## Estado de tareas del PR 1

| Tarea | Estado | Nota |
|---|---|---|
| 1.1 Copia previa | Hecha | `..\TimeTracker.rollback\pr-1\` (AGENTS, README, docker-compose, DeliveryArtifactsTests); `.env.example` no se pudo copiar (acceso denegado) |
| 1.2 RED 18 ADR | Hecha | 4 pruebas fallaron antes del código (18 ADR esperados, hay 17; ADR 0018, AGENTS y README) |
| 1.3 RED `.env.example` | BLOQUEADA | Permisos del entorno denegaron leer/editar `.env.example` |
| 1.4 GREEN ADR 0018 | Hecha | `docs/adr/0018-...md` |
| 1.5 GREEN AGENTS §1/§7 | Hecha | ADR 0017 sin editar |
| 1.6 GREEN docs | PARCIAL | README hecho; `.env.example` bloqueado; `docker-compose.yml` no consume `Ai__*` nuevas |
| 1.7 Refactor y puerta | Puerta ejecutada | Sin refactor necesario; PR no cerrable hasta 1.3/1.6 |

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

1.3 y la parte `.env.example` de 1.6 (ver tabla). Una vez disponible el acceso: añadir pruebas de `.env.example` (claves `Ai__Effort`, `Ai__AllowedProviders`, `Ai__MasterKeys__1`, `Ai__ActiveMasterKeyVersion`, `Ai__SettingsPollSeconds` comentadas y sin valor; comentario «32 bytes aleatorios en Base64»; proveedores `Anthropic`, `OpenAI`, `Gemini`, `Ollama`). Ojo: la prueba existente `EnvExample_DocumentsTheNineAiKeys...` exige que solo `Ai__ApiKey` y `Ai__Provider` estén activas y cita los proveedores; las claves nuevas deben ir comentadas.
