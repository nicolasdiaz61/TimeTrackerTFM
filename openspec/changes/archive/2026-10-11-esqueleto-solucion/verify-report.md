```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:fc4d22fee101c50c491cacb4489c947593ac16857039eaa7664899657a7ee40d
verdict: pass_with_warnings
blockers: 0
critical_findings: 0
requirements: 27/27
scenarios: 53/53 con evidencia (52 CUMPLE, 1 PARCIAL [SA-18])
test_command: dotnet test TimeTracker.sln -c Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults --logger trx
test_exit_code: 0
test_output_hash: sha256:fc4d22fee101c50c491cacb4489c947593ac16857039eaa7664899657a7ee40d
build_command: dotnet build TimeTracker.sln -c Release --no-restore -warnaserror
build_exit_code: 0
build_output_hash: sha256:7bc4d102e58a47752dc64da186a29fd7400df82fdb79af1b78f1bcec8c8cd58a
```

# Informe de verificación: esqueleto-solucion

**Cambio**: esqueleto-solucion · **Modo**: openspec, Strict TDD · **Fecha**: 2026-10-11 · **Idioma**: español
**Alcance**: verificación independiente (los comandos se ejecutaron de nuevo; no se reutilizó la evidencia de `tasks.md`). No se modificó código ni especificaciones. No se ejecutó ningún comando Git.
**Persistencia**: solo este archivo (modo openspec). No se usó Engram.

## Veredicto

**PASS WITH WARNINGS. Se puede archivar.** 0 CRITICAL, 8 WARNING, 7 SUGGESTION.

Todas las puertas de calidad terminan en 0, Docker se comporta como exige la especificación y no hay vulnerabilidades conocidas. Los avisos son límites conocidos y ya admitidos, como la CI sin ejecutar, y una desviación de diseño sin registrar.

## 1. Completitud de tareas

| Métrica | Valor |
|---|---|
| Tareas totales (checklist de `tasks.md`) | 52 |
| Completas | 52 |
| Incompletas | 0 |
| Escenarios en la tabla de trazabilidad | 53 de 53 |
| Requisitos / escenarios contados en las especificaciones | 27 / 53 (SA 11 req. y 20 esc.; AH 4 y 8; SC 6 y 12; WH 6 y 13) |

## 2. Ejecución real de comandos

| Comando | Salida | Resultado |
|---|---|---|
| `dotnet --version` | 0 | 10.0.302 |
| `dotnet restore TimeTracker.sln` | 0 | Proyectos al día |
| `dotnet format TimeTracker.sln --verify-no-changes --no-restore` | 0 | Sin cambios |
| `dotnet build TimeTracker.sln -c Release --no-restore -warnaserror` | 0 | 0 advertencias, 0 errores |
| `dotnet test TimeTracker.sln -c Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults` (más `--logger trx`) | 0 | Domain.Tests 10/10, ArchitectureTests 25/25, Api.IntegrationTests 14/14 (49 pruebas, 0 fallos). Application.Tests: "No hay ninguna prueba disponible", salida 0 (V3). |
| `pwsh scripts/check-coverage.ps1 -ResultsDirectory TestResults -Package TimeTracker.Domain -MinimumPercent 100` | 0 | "líneas 100,0 %, ramas 100,0 %" |
| `dotnet list TimeTracker.sln package --vulnerable --include-transitive` | 0 | "has no vulnerable packages" en los 9 proyectos |
| `docker build --no-cache -t timetracker-api:verify .` | 0 | Imagen de 54,6 MB |
| `docker compose config --services` (con `.env` temporal) | 0 | `api` (el `.env` se borró después) |

Cobertura: hay 4 informes `coverage.cobertura.xml`, uno por proyecto de pruebas. El de `Domain.Tests` tiene `line-rate="1" branch-rate="1"` para `TimeTracker.Domain`; en los otros tres, `line-rate="0"`. El script toma el mejor valor, como prevé D-12. Aparecieron 3 informes más, que son copias del registrador `trx` que añadí yo (`TestResults/*/In/...`).

Puerto: con `ASPNETCORE_HTTP_PORTS=8081` y `dotnet run --no-launch-profile`, la API escucha en `http://[::]:8081` y `GET /health` responde 200 con `{"status":"Healthy"}`. El proceso se detuvo después.

## 3. Docker (comprobado en esta ejecución)

| Comprobación | Resultado |
|---|---|
| `docker build` sin caché | rc 0 |
| `.Config.User` | `1654` (`uid=1654(app)`), no root |
| `Healthcheck` | Presente: `wget` a `127.0.0.1:${ASPNETCORE_HTTP_PORTS:-8080}/health`, 30 s / 3 s / 15 s / 3 reintentos |
| `GET /health` en contenedor (8080, publicado como 18080) | 200, `application/json; charset=utf-8`, `{"status":"Healthy"}`, sin cabecera `Server` |
| `POST /health` | 405 |
| CORS | El `Origin` permitido recibe `Access-Control-Allow-Origin`; el otro origen, no |
| `Health.Status` | `healthy` con el puerto 8080 y con `ASPNETCORE_HTTP_PORTS=8081` (el `HEALTHCHECK` expande la variable) |
| Sin `Cors__AllowedOrigins__0` | Sale con código **139** (distinto de 0); el log contiene `OptionsValidationException: Cors:AllowedOrigins: debe definirse al menos un origen permitido.` |
| Secretos en `docker history --no-trunc` e `docker inspect` | Sin `Cors__`, `example.test`, `Ai__` ni `.env`. La única coincidencia de "password" es `adduser --disabled-password` de la imagen base. Solo aparece `ASPNETCORE_HTTP_PORTS=8080`. |
| `/app` | Sin archivos `.env` |
| `docker-compose.yml` | Servicio único `api`; no hay `postgres` |
| Limpieza | Contenedores `tt-verify-1..3` e imagen `timetracker-api:verify` eliminados; sin `.env`, `TestResults/` ni `artifacts/` en la raíz. No se tocaron los contenedores ni las imágenes ajenos al proyecto. |

## 4. Matriz de cumplimiento por requisito (27)

| # | Requisito | Estado | Evidencia |
|---|---|---|---|
| SA-R1 | Estructura de proyectos y destino de plataforma | CUMPLE | `dotnet sln list` = 9 proyectos; no existe `.slnx`; `AllProjects_TargetNet10` (9 casos) pasa; `global.json` sin sección `test` |
| SA-R2 | Regla de dependencias por referencias de proyecto | CUMPLE | `dotnet list reference` coincide: Domain ninguna, Application a Domain, Infrastructure a Application, Api a Application e Infrastructure, Web ninguna; `Web_HasNoProjectReferences` pasa |
| SA-R3 | Prueba de arquitectura por reflexión | PARCIAL | Pasa y falla ante una referencia usada (reproducido). No detecta referencias ilegales sin usar (W2). Cumple la letra de la especificación. |
| SA-R4 | Gestión central de paquetes y analizadores | CUMPLE | `PackageReferences_DoNotDeclareVersion` (9 casos); aviso como error reproducido; `TreatWarningsAsErrors` intacto |
| SA-R5 | Puerta de calidad local | CUMPLE | build, test y format con salida 0. Web sin editar salvo CPM. |
| SA-R6 | Informe de cobertura | CUMPLE | 4 informes y Domain al 100 % en líneas y ramas |
| SA-R7 | Archivos raíz | CUMPLE | Los 5 archivos existen; `.env` en `.gitignore` y `.dockerignore`; `bin/`, `obj/`, `TestResults/` y `coverage*.xml` ignorados |
| SA-R8 | Imagen Docker de la API | CUMPLE | Ver sección 3 |
| SA-R9 | Docker Compose con solo la API | CUMPLE | `api` |
| SA-R10 | Flujo de CI | PARCIAL | Pasos presentes y en orden (revisión manual); CI no ejecutada y sin parser YAML (W1) |
| SA-R11 | ADR y README | CUMPLE | 7 ADR con las 4 secciones; README completo; `config.yaml` con comandos reales; flujo del README reproducido |
| AH-R1 | Respuesta de salud | CUMPLE | `Get_ReturnsOkWithHealthyJson`, `Post_ReturnsMethodNotAllowed`; en contenedor 200 y 405 |
| AH-R2 | Sin información sensible | CUMPLE | `Get_BodyHasOnlyStatusProperty`; cabeceras sin `Server` en el contenedor real (la prueba automática de cabeceras es débil, W3) |
| AH-R3 | Presencia en el contrato OpenAPI | CUMPLE | `ExportedDocument_ContainsHealthGet200`; JSON válido; regeneración reproducida en una copia |
| AH-R4 | Prueba de integración | CUMPLE | `WebApplicationFactory`, sin red; regresión demostrada en `tasks.md` (5.4) |
| SC-R1 | Configuración por variables de entorno | CUMPLE | `Startup_WithValidOrigin_BindsTypedOptions`; `appsettings*.json` sin CORS ni secretos |
| SC-R2 | Opciones tipadas validadas al arrancar | CUMPLE | `ValidateOnStart` con `IValidateOptions`; el contenedor no abre el puerto y termina con código distinto de 0 |
| SC-R3 | CORS restrictivo sin comodines | CUMPLE | Theory de 7 casos y `OnlyConfiguredOrigin_ReceivesAllowOriginHeader` |
| SC-R4 | Mensajes de error sin secretos | CUMPLE | Las pruebas comprueban que el mensaje nombra `Cors:AllowedOrigins` y que no repite el valor |
| SC-R5 | Documentación de claves en `.env.example` | CUMPLE | `Cors__AllowedOrigins__0=` vacío; puerto y `Ai__*` solo comentados |
| SC-R6 | Puerto configurable por entorno | CUMPLE | Manual reproducido: `dotnet run --no-launch-profile` con 8081 devuelve 200; contenedor con 8081 devuelve 200 y `healthy` |
| WH-R1 | Construcción desde minutos enteros | CUMPLE | `FromMinutes_WithPositiveValue_ExposesMinutes`, `FromMinutes_WithZero_IsValid` |
| WH-R2 | Rechazo de valores negativos | CUMPLE | `FromMinutes_WithNegativeValue_Throws` (-1 y `int.MinValue`; `ParamName == "minutes"`) |
| WH-R3 | Suma de dos valores | CUMPLE | `Add_TwoValues_ReturnsExactSumAndKeepsOperands`, `Add_WithZero_IsNeutral` |
| WH-R4 | Desbordamiento explícito | CUMPLE | `Add_BeyondIntMaxValue_ThrowsOverflow`, `Add_UpToIntMaxValue_ReturnsIntMaxValue` |
| WH-R5 | Igualdad por valor | CUMPLE | `Equals_SameMinutes_AreEqualWithSameHash`, `Equals_DifferentMinutes_AreNotEqual` |
| WH-R6 | Dominio sin dependencias y cobertura total | CUMPLE | `Domain_ReferencesOnlyBaseFramework`; cobertura 100 %; ciclo TDD documentado |

**Resumen**: 25 CUMPLE, 2 PARCIAL (SA-R3 y SA-R10), 0 INCUMPLE, 0 SIN EVIDENCIA.

## 5. Matriz de escenarios (53)

"Auto" = prueba automática que pasó en esta ejecución. "Manual R" = evidencia manual que reproduje yo. "Manual D" = evidencia manual solo registrada en `tasks.md`.

| ID | Escenario | Evidencia | Resultado |
|---|---|---|---|
| SA-1 | Contenido de la solución | Manual R: `dotnet sln list` = 9; sin `.slnx` | CUMPLE |
| SA-2 | Destino de plataforma | Auto: `AllProjects_TargetNet10` (9 casos) | CUMPLE |
| SA-3 | Referencias esperadas | Manual R: `dotnet list reference` en los 5 proyectos; Auto: `Web_HasNoProjectReferences` | CUMPLE |
| SA-4 | Referencia ilegal no compila | Manual D: `MSB4006` (7.5). No lo reproduje: mi copia de trabajo quedó contaminada por la prueba de SA-8 y el error mostrado fue otro. | CUMPLE (solo evidencia registrada) |
| SA-5 | Estado conforme | Auto: `ProductionAssembly_ReferencesOnlyAllowedAssemblies` (4 casos), `Domain_ReferencesOnlyBaseFramework` | CUMPLE |
| SA-6 | Referencia ilegal detectada | Manual R, en copia: `FrameworkReference` y `typeof(ILogger)` dan ROJO con el mensaje "TimeTracker.Domain referencia ensamblados ajenos al marco base: Microsoft.Extensions.Logging.Abstractions" (1 fallo, 24 ok) | CUMPLE |
| SA-7 | Versiones centralizadas | Auto: `PackageReferences_DoNotDeclareVersion` (9 casos); demostración roja en `tasks.md` 7.7 (D) | CUMPLE |
| SA-8 | Aviso como error | Manual R, en copia: `error CS0219` con `-warnaserror` | CUMPLE |
| SA-9 | Build limpio | Manual R: 0 avisos, 0 errores | CUMPLE |
| SA-10 | Pruebas en verde | Auto: 49/49 (WorkedHours, `/health`, arranque y dependencias) | CUMPLE |
| SA-11 | Formato verificado | Manual R: salida 0 | CUMPLE |
| SA-12 | Informe generado | Manual R: 4 informes; Domain con `line-rate=1` y `branch-rate=1` | CUMPLE |
| SA-13 | Archivos presentes | Manual R: los 5 archivos; `.env` en ambos ignore | CUMPLE |
| SA-14 | Construcción de la imagen | Manual R: salida 0, `User=1654`, `Healthcheck` | CUMPLE |
| SA-15 | Salud dentro del contenedor | Manual R: 200 y `healthy` (puertos 8080 y 8081) | CUMPLE |
| SA-16 | Sin secretos en la imagen | Manual R: `docker history` e `inspect` limpios | CUMPLE |
| SA-17 | Servicios de compose | Manual R: `api` | CUMPLE |
| SA-18 | Pasos de CI presentes | Revisión manual: 10 pasos en orden y sin push; no ejecutada ni analizada como YAML | PARCIAL |
| SA-19 | ADR completos | Manual R: 7 archivos y 4 secciones cada uno | CUMPLE |
| SA-20 | README ejecutable | Manual R: restore, build, test, cobertura y arranque de la API con los comandos del README; los puertos 5172 y 5274 coinciden con `launchSettings.json` | CUMPLE |
| AH-1 | API en marcha | Auto: `Get_ReturnsOkWithHealthyJson` | CUMPLE |
| AH-2 | Método no permitido | Auto: `Post_ReturnsMethodNotAllowed` | CUMPLE |
| AH-3 | Solo la propiedad status | Auto: `Get_BodyHasOnlyStatusProperty` | CUMPLE |
| AH-4 | Cabeceras sin versión | Auto: `Get_HeadersDoNotExposeMachineNameOrConfiguration` (débil, W3); Manual R: contenedor sin `Server` | CUMPLE |
| AH-5 | Documento exportado | Auto: `ExportedDocument_ContainsHealthGet200` | CUMPLE |
| AH-6 | Regeneración tras cambiar un endpoint | Manual R, en copia: `/probe` aparece al añadirlo (1 coincidencia) y desaparece al retirarlo (0) | CUMPLE |
| AH-7 | La prueba pasa | Auto: `Get_ReturnsOkWithHealthyJson` | CUMPLE |
| AH-8 | La prueba detecta una regresión | Manual D: mutación del cuerpo a Unhealthy (5.4) | CUMPLE (solo evidencia registrada) |
| SC-1 | Valor tomado del entorno | Auto: `Startup_WithValidOrigin_BindsTypedOptions`; Manual R: contenedor y `dotnet run` con variable de entorno | CUMPLE |
| SC-2 | Ningún secreto versionado | Manual R: `appsettings*.json` sin CORS ni secretos; `.env` ignorado | CUMPLE |
| SC-3 | Falta la clave | Auto: `Startup_WithoutAllowedOrigins_FailsNamingKey`; Manual R: el contenedor sale con 139 y el log nombra la clave | CUMPLE |
| SC-4 | Lista vacía | Sin prueba dedicada: una clave ausente vincula una lista vacía, que toma la misma rama `Count == 0`; los casos de cadena vacía y de espacio de la Theory también fallan | CUMPLE (por equivalencia, S1) |
| SC-5 | Comodín rechazado | Auto: Theory con el asterisco | CUMPLE |
| SC-6 | Origen con formato inválido | Auto: Theory con `app.example.test/ruta`, `ftp://app.example.test`, `https://a.test/ruta` y `https://a.test/` | CUMPLE |
| SC-7 | Origen permitido y no permitido | Auto: `OnlyConfiguredOrigin_ReceivesAllowOriginHeader`; Manual R: contenedor | CUMPLE |
| SC-8 | El mensaje nombra la clave | Auto: aserciones en las pruebas de arranque | CUMPLE |
| SC-9 | Prueba de fallo al arrancar | Auto: `Startup_WithoutAllowedOrigins_FailsNamingKey` | CUMPLE |
| SC-10 | Claves documentadas | Manual R: `Cors__AllowedOrigins__0` activa y vacía; `ASPNETCORE_HTTP_PORTS` comentada | CUMPLE |
| SC-11 | Sin claves de IA activas | Manual R: `Ai__*` solo comentadas | CUMPLE |
| SC-12 | Puerto distinto | Manual R: `dotnet run --no-launch-profile` y contenedor con 8081 devuelven 200 | CUMPLE |
| WH-1 | Valor positivo válido | Auto: `FromMinutes_WithPositiveValue_ExposesMinutes` | CUMPLE |
| WH-2 | Cero es válido | Auto: `FromMinutes_WithZero_IsValid` | CUMPLE |
| WH-3 | Un minuto negativo | Auto: Theory con -1 | CUMPLE |
| WH-4 | Valor mínimo del entero | Auto: Theory con `int.MinValue` | CUMPLE |
| WH-5 | Suma de dos valores | Auto: `Add_TwoValues_ReturnsExactSumAndKeepsOperands` | CUMPLE |
| WH-6 | Cero como elemento neutro | Auto: `Add_WithZero_IsNeutral` (ambos órdenes) | CUMPLE |
| WH-7 | Suma que desborda | Auto: `Add_BeyondIntMaxValue_ThrowsOverflow` | CUMPLE |
| WH-8 | Suma en el límite exacto | Auto: `Add_UpToIntMaxValue_ReturnsIntMaxValue` | CUMPLE |
| WH-9 | Mismos minutos | Auto: `Equals_SameMinutes_AreEqualWithSameHash` | CUMPLE |
| WH-10 | Minutos distintos | Auto: `Equals_DifferentMinutes_AreNotEqual` | CUMPLE |
| WH-11 | Sin dependencias | Auto: `Domain_ReferencesOnlyBaseFramework`; `Domain.csproj` sin referencias ni paquetes | CUMPLE |
| WH-12 | Cobertura del dominio | Manual R: 100 % en líneas y ramas | CUMPLE |
| WH-13 | Ciclo TDD demostrable | Revisión de `tasks.md` 2.2 a 2.10 y de la tabla de evidencia (sección 8) | CUMPLE |

**Resumen**: 52 CUMPLE (3 de ellos solo con evidencia registrada o por equivalencia: SA-4, AH-8 y SC-4), 1 PARCIAL (SA-18), 0 INCUMPLE, 0 SIN EVIDENCIA.

## 6. Coherencia diseño y código

| Decisión o elemento | ¿Se sigue? | Notas |
|---|---|---|
| D-1 VSTest, sin sección `test` en `global.json` | Sí | |
| Pila de pruebas: xUnit v3 | Desviación documentada | Se usa xUnit v2 (incompatibilidad de v3 con VSTest en el SDK 10); es el respaldo del diseño y consta en el ADR 0006. `tests/Directory.Build.props` sin `OutputType=Exe`. |
| D-2 `.sln` clásico | Sí | |
| D-3 `Directory.Build.props` y `tests/Directory.Build.props` | Sí | |
| D-4 `TargetFramework` en cada `.csproj` | Sí | |
| D-5 Prueba por reflexión y XML | Parcial (W2) | El XML solo comprueba Web sin `ProjectReference`; el diseño decía que la lectura XML detectaba las referencias que el compilador elimina. Esta desviación no figura en "Desviaciones". |
| D-6 `readonly record struct` con constructor privado | Sí | `Minutes` sin `init` |
| D-7 `MapGet` de `/health` con `TypedResults.Ok(HealthResponse)` | Sí | |
| D-8 `ValidateOnStart` con `IValidateOptions` | Sí | Mensajes con índice y sin el valor |
| D-9 `ASPNETCORE_HTTP_PORTS` | Sí | |
| D-10 `aspnet:10.0-alpine` y `wget` | Sí | `APP_UID` 1654; la imagen trae `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true` (R6) |
| D-11 OpenAPI exportado en el build | Sí, con ajuste | `Program.cs` incluye una rama para `GetDocument.Insider` (W4, ADR 0004) |
| D-12 `check-coverage.ps1` | Sí | |
| D-13 Application e Infrastructure vacíos | Sí | Solo `.csproj` |
| D-14 `.editorconfig` generado más ajustes | Sí | 2 ajustes justificados |
| Dependencias entre capas (`ProjectReference`) | Sí | Idénticas al diagrama del diseño |
| `Directory.Packages.props` sin `Version` en los `.csproj` | Sí | Comprobado por prueba y por lectura de los 9 `.csproj` |
| `nuget.config` (solo nuget.org) | Desviación documentada | No estaba en el diseño; se justifica en "Desviaciones de la Tanda 1"; el `Dockerfile` lo copia |
| Dockerfile (sección 8 del diseño) | Sí | Con `-p:OpenApiGenerateDocuments=false` |
| Compose (sección 8) | Sí | |
| CI (sección 9) | Sí en contenido | 10 pasos en el orden del diseño; acciones `@v7` y `@v6` |
| Orden y alcance por tandas | Sí | OpenAPI pasó a la Tanda 2 (divergencia anunciada en `tasks.md`) |

**Riesgos señalados en la petición**

- **La prueba de arquitectura solo detecta referencias usadas (tarea 7.2)**: confirmado y reproducido en una copia de trabajo. Un `FrameworkReference Microsoft.AspNetCore.App` en Domain sin uso deja los 25 tests en verde; con un `typeof(ILogger)` queda en rojo un test. `tasks.md` y V15 lo cuentan con honestidad, pero queda sin protección "Domain sin paquetes externos" (SA-R2) frente a una referencia sin uso (W2).
- **`Microsoft.OpenApi` 2.7.6 (GHSA-v5pm-xwqc-g5wc)**: el fijado es una referencia directa en `Api.csproj` y en `Directory.Packages.props`. `dotnet list package` resuelve 2.7.6 y la auditoría actual no informa paquetes vulnerables en ninguno de los 9 proyectos. La generación de OpenAPI sigue funcionando con esa versión. Falta retirar el fijado cuando `Microsoft.AspNetCore.OpenApi` arrastre una versión corregida (S3).
- **`ci.yml` revisado a mano**: YAML coherente (sin tabuladores, indentación uniforme, `on`, `permissions` y un solo job). Sus pasos coinciden con los comandos que ejecuté, con el orden del diseño y con el de la especificación. `actions/checkout` v7.0.1 y `actions/setup-dotnet` v6.0.0 son las últimas versiones publicadas (API de GitHub consultada) y las etiquetas mayores `@v7` y `@v6` son coherentes. No se verificó `pwsh` en `ubuntu-latest` ni el comportamiento real del workflow.
- **README veraz**: los únicos enlaces externos son `dotnet.microsoft.com/download` y `learn.microsoft.com/powershell/`; `localhost` y `example.test` son locales o ficticios. Despliegue, vídeo, presentación y usuario de prueba figuran como pendientes, sin URLs inventadas. Los puertos coinciden con `launchSettings.json`.

## 7. Cumplimiento de AGENTS.md

| Regla | Resultado |
|---|---|
| Sin mención del curso en los entregables (README, ADR, `src`, `tests`, `docs`, scripts, `Dockerfile`, CI) | Cumple |
| Sin mención del curso en artefactos SDD | **W6**: `proposal.md` (líneas 7, 13 y 125) cita el TFM y su fecha |
| Sin datos de empresas reales | Cumple: solo `example.test` y `localhost` |
| No existe `.git` | Cumple: la raíz no contiene `.git`. Existen `.gitignore`, `.gitattributes` y `.github/`, que son archivos de configuración. |
| Idioma | Documentación en español; identificadores en inglés |
| Secretos fuera del repositorio | Cumple: `.env` ignorado y sin `.env` presente |
| Validación al arrancar y fallo rápido | Cumple |
| Cobertura por criticidad | Dominio 100 %; Application sin código aún |
| Contrato OpenAPI generado, no editado a mano | Cumple |

## 8. Strict TDD

| Comprobación | Resultado |
|---|---|
| Existe evidencia TDD | Sí: la tabla "Evidencia" de `tasks.md` (en modo openspec no hay un `apply-progress` aparte) |
| Todas las tareas de código tienen archivo de prueba | Sí: los archivos de prueba referenciados existen |
| Las pruebas pasan ahora (verde) | 49/49 |
| RED plausible | Sí. Los mensajes de error son los que produciría el compilador o xUnit en cada paso (por ejemplo `CS0103`, `CS0019` y "No exception was thrown"). Las pruebas que pasaron a la primera llevan justificación o mutación. |
| Triangulación | Adecuada: Theory para negativos (-1 e `int.MinValue`), bordes de suma (`int.MaxValue` más y menos 1) y 7 orígenes inválidos |
| Mutaciones temporales | Documentadas para 2.4, 2.9, 5.3 y 5.4. Reproduje de forma independiente SA-6, SA-8 y AH-6. |
| Calidad de las aserciones | Sin tautologías ni bucles fantasma. Débil: `Get_HeadersDoNotExposeMachineNameOrConfiguration` (W3). |

Distribución por capa: unitarias 10 (Domain.Tests, xUnit v2); integración 14 (Api.IntegrationTests, `WebApplicationFactory`); arquitectura 25 (ArchitectureTests). Total 49. No hay E2E (fuera de alcance).

## 9. Hallazgos

### CRITICAL

Ninguno.

### WARNING

- **W1. SA-18: CI sin ejecutar ni analizada como YAML.** El escenario pide analizar el archivo como YAML y no hay un parser disponible. Hay revisión manual, reproducción local de los pasos y versiones de las acciones verificadas, pero ni el workflow ni `pwsh` en `ubuntu-latest` se han ejecutado. La especificación y la propuesta lo admiten; debe validarse en el primer push.
- **W2. La prueba de arquitectura no detecta referencias ilegales sin uso.** Reproducido: un `FrameworkReference` sin uso en Domain no hace fallar ninguna prueba. La lectura XML que D-5 prometía para ese caso solo se aplica a Web, así que un paquete externo sin uso en Domain violaría SA-R2 sin que ninguna prueba lo detecte. Esta divergencia con D-5 no está en "Desviaciones". Mitigación sugerida para un cambio posterior: comprobar por XML que Domain no contiene `PackageReference`, `FrameworkReference` ni `ProjectReference`.
- **W3. La prueba de cabeceras no tiene dientes.** `Get_HeadersDoNotExposeMachineNameOrConfiguration` pasaría aunque se quitara `AddServerHeader = false`, porque `TestServer` nunca emite `Server`. `tasks.md` lo admite y lo demostró a mano con Kestrel; yo lo confirmé en el contenedor. No hay regresión automática de esa configuración.
- **W4. `Program.cs` contiene una rama para la herramienta de generación de OpenAPI.** Con `Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider"` se inyecta el origen ficticio `https://openapi.example.test`. Está documentada (V5, ADR 0004) y no relaja la validación en ejecución normal, pero es código de producción que depende del nombre interno de una herramienta del SDK. Sustituye al respaldo del diseño (una prueba con `WebApplicationFactory`).
- **W5. Salida 139 cuando falla la configuración en el contenedor.** Es distinta de 0, como exige la especificación, pero 139 (SIGSEGV) indica que el proceso muere por una caída y no por una salida limpia; el diseño preveía 134. Funciona como fail fast y está documentado.
- **W6. Mención del TFM en un artefacto SDD.** `proposal.md` (líneas 7, 13 y 125) cita el TFM y su fecha. Si la restricción "sin mención del curso" alcanza a `openspec/`, hay que neutralizarlo antes de archivar. En los entregables del repositorio no aparece nada.
- **W7. Evidencia no reproducida por mí.** SC-4 se cubre por equivalencia con SC-3 (una clave ausente vincula una lista vacía, que toma la misma rama). No pude reproducir SA-4 (ciclo Domain a Application) en mi copia, y AH-8 depende de la mutación documentada. No hay contradicciones, solo evidencia no repetida de forma independiente.
- **W8. `Application.Tests` no contiene pruebas.** `dotnet test` termina en 0 pero informa "No hay ninguna prueba disponible" (V3). Es lo esperado en este cambio; el umbral del 80 % de Application todavía no se ejercita.

### SUGGESTION

- **S1.** Añadir una prueba de lista vacía para SC-4 que ancle de forma explícita la rama `Count == 0`.
- **S2.** `RepositoryConventionsTests` mantiene la lista de proyectos a mano: un proyecto nuevo no entra solo en la comprobación. Considerar descubrirlos con `Directory.EnumerateFiles`.
- **S3.** Retirar el fijado de `Microsoft.OpenApi` 2.7.6 cuando `Microsoft.AspNetCore.OpenApi` publique una versión que dependa de una versión corregida, y repasar `dotnet list package --vulnerable` con regularidad. Ya está anotado en `tasks.md`.
- **S4.** `appsettings.json` conserva `AllowedHosts: *` de la plantilla. Conviene restringirlo en el cambio de despliegue (AGENTS.md, sección 6).
- **S5.** `openspec/config.yaml.bak` es un artefacto de reversión; decidir en el archivado si se conserva o se elimina.
- **S6.** El README lista "Vídeo de demostración" y "Presentación (slides)" como pendientes. Si no son requisitos del producto, pueden retirarse; AGENTS.md (sección 8) solo pide enlaces de despliegue y usuario de prueba.
- **S7.** Las acciones de la CI se fijan por etiqueta mayor y las imágenes base por etiqueta (R9). Fijarlas por SHA o digest queda para el cambio de Dependabot. Punto abierto del diseño: comprobar en CI que `docs/api/openapi.json` coincide con el regenerado.

## 10. Veredicto final

**PASS WITH WARNINGS.** Se puede archivar (`sdd-archive`) sin correcciones previas. No hay CRITICAL: todo escenario tiene evidencia (automática o manual documentada) y los comandos de calidad terminan en 0.

Recomendaciones antes de archivar o al hacerlo: neutralizar la mención del TFM en `proposal.md` si aplica (W6), registrar en el informe de archivo la desviación de D-5 (W2) y ejecutar la CI en el primer push (W1).

Limpieza: la verificación no dejó restos en el repositorio (`TestResults/`, `.env`, contenedores e imagen de verificación eliminados) y no se ejecutó ningún comando Git.
