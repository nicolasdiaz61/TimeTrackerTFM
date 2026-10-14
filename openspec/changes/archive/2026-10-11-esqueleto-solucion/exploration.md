# Exploración: esqueleto-solucion

Fecha: 2026-10-10 · Fase: sdd-explore · Estado: pendiente de aprobación del usuario.

## 1. Estado actual

- Proyecto nuevo (greenfield). Solo existen `AGENTS.md`, `openspec/config.yaml` y `.atl/skill-registry.md`. No hay solución, proyectos, repositorio Git ni índice CodeGraph.
- `AGENTS.md` fija estas restricciones: Clean Architecture con 4 capas y `Api` como composition root; Blazor WASM independiente; OpenAPI generado desde el código; analizadores con advertencias como errores y `dotnet format`; cobertura por niveles (dominio 100 %, aplicación 80 %, infraestructura sin umbral); secretos solo por variables de entorno con `.env.example` y fallo rápido; Docker, GitHub Actions y ADR en `docs/adr/`; hooks de Git (no aplicables en este cambio).
- `openspec/config.yaml` marca el ejecutor de pruebas como no disponible. Este cambio debe dejar `dotnet test` operativo y actualizar `test_command`, `build_command` y la cobertura.
- Entorno conocido: SDK .NET 10.0.302 y 8.0.425. Node, Docker, Postgres y Ollama no están confirmados y no se asumen.

## 2. Hechos verificados (con fuente)

| Hecho | Fuente |
|---|---|
| .NET 10 es LTS, con fin de soporte el 14-nov-2028. .NET 8 y .NET 9 terminan el 10-nov-2026. | dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core |
| Desde .NET 10, `dotnet new sln` genera `.slnx` por defecto. | learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new-sdk-templates |
| `blazorwasm` es "Blazor WebAssembly Standalone App" (PWA opcional con `--pwa`). | misma fuente |
| El despliegue "hosted" de Blazor WASM no figura en la guía de .NET 10; solo "Standalone deployment". Se trata como no soportado. | learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/webassembly/?view=aspnetcore-10.0 |
| La plantilla `webapi` usa minimal APIs por defecto (`--use-controllers` para controladores). | dotnet-new-sdk-templates |
| OpenAPI integrado: `Microsoft.AspNetCore.OpenApi` (`AddOpenApi()`, `MapOpenApi()`), con exportación en build mediante `Microsoft.Extensions.ApiDescription.Server`. Sin interfaz visual. | learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-10.0 |
| `Microsoft.Extensions.AI` 10.10.0 estable; `IChatClient` está en Abstractions. | learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai |
| Adaptador Ollama: la documentación oficial usa `OllamaSharp` (5.5.0). `Microsoft.Extensions.AI.Ollama` solo tiene versión preliminar. | learn.microsoft.com/en-us/dotnet/ai/ichatclient y NuGet |
| `xunit.v3` 4.0.2 estable; plantillas con `dotnet new install xunit.v3.templates` y `dotnet new xunit3`. La plantilla integrada `xunit` sigue siendo v2. | xunit.net/docs/getting-started/v3/getting-started |
| Con SDK 10, `dotnet test` puede usar Microsoft.Testing.Platform mediante `global.json`; por defecto usa VSTest. | learn.microsoft.com/en-us/dotnet/core/tools/global-json |
| Gestión central de paquetes: `Directory.Packages.props` con `ManagePackageVersionsCentrally`. | learn.microsoft.com/en-us/nuget/consume-packages/central-package-management |
| `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 y `Microsoft.EntityFrameworkCore` 10.0.12 estables; `Testcontainers.PostgreSql` 4.16.0; `NetArchTest.Rules` 1.3.2. | NuGet |

No verificado (no bloquea el esqueleto, salvo test y cobertura, que se comprueban al implementar): funcionamiento de `coverlet.collector` y `coverlet.MTP` con xUnit v3 y MTP; compatibilidad de NetArchTest.Rules con net10.0; licencia actual de FluentAssertions; si `Microsoft.Extensions.AI.Ollama` está declarado obsoleto; versión de Node necesaria para el cliente WASM; versiones exactas de imágenes base de Docker.

## 3. Decisiones a tomar

### D1. Versión de .NET
- A. net10.0 (LTS hasta nov-2028, ya instalado). **Recomendada.**
- B. net8.0: termina soporte el 10-nov-2026, antes de cualquier despliegue real.
- C. Multi-target: complejidad sin beneficio.
Registrar en un ADR.

### D2. Formato de solución y estructura
- A. `TimeTracker.slnx` con carpetas `src/`, `tests/`, `docs/`. **Recomendada** (formato por defecto en .NET 10). Riesgo: IDE o herramientas antiguas pueden no leerlo.
- B. `.sln` clásico (`dotnet new sln --format sln`): máxima compatibilidad. Es el respaldo si el IDE no abre `.slnx`.

```
/
  TimeTracker.slnx  global.json  Directory.Build.props  Directory.Packages.props
  .editorconfig  .gitignore  .gitattributes  .dockerignore  .env.example  Dockerfile  docker-compose.yml
  .github/workflows/ci.yml
  src/    TimeTracker.Domain  TimeTracker.Application  TimeTracker.Infrastructure  TimeTracker.Api  TimeTracker.Web
  tests/  TimeTracker.Domain.Tests  TimeTracker.Application.Tests  TimeTracker.Api.IntegrationTests  TimeTracker.ArchitectureTests
  docs/adr/
```

### D3. Proyectos y regla de dependencias
```
Domain          -> (nada)
Application     -> Domain
Infrastructure  -> Application
Api             -> Application, Infrastructure   (composition root)
Web (Blazor)    -> (nada; solo HTTP contra la API)
```
Pruebas: `Domain.Tests -> Domain`; `Application.Tests -> Application`; `Api.IntegrationTests -> Api` (`WebApplicationFactory`); `ArchitectureTests -> los 4 proyectos`.

Cómo hacer cumplir la regla:
- A. Referencias de proyecto: un `ProjectReference` ilegal no compila.
- B. Test de arquitectura (NetArchTest.Rules, o reflexión sobre `Assembly.GetReferencedAssemblies()` para evitar la dependencia): red de seguridad frente a dependencias transitivas.
- C. Analizadores de APIs prohibidas: demasiada configuración ahora (YAGNI).
**Recomendada: A + B.**

### D4. Estilo de la API y OpenAPI
- A. Minimal APIs agrupadas con `MapGroup`, una clase estática `*Endpoints` por módulo. **Recomendada.**
- B. Controladores: más ceremonia, mismo OpenAPI.
OpenAPI: `Microsoft.AspNetCore.OpenApi` y exportación del contrato a `docs/api/openapi.json`. Sin Swagger UI en el esqueleto.

### D5. Alojamiento del frontend y CORS
- A. Web WASM standalone separado, con CORS restrictivo por configuración (`Cors:AllowedOrigins`, sin comodines).
- B. La API sirve además los estáticos publicados del WASM: un solo contenedor y un solo origen en producción.
**Recomendada: A en desarrollo, con la opción B en el Dockerfile.** Los detalles de despliegue real quedan para un cambio posterior.

### D6. Contratos compartidos API–frontend
- A. Sin proyecto compartido; DTO propios o generados desde OpenAPI cuando haga falta. **Recomendada (YAGNI).**
- B. Proyecto `TimeTracker.Contracts` con DTO sin lógica. Decidir al llegar el primer caso de uso real.

### D7. Configuración y secretos
Variables de entorno en todos los entornos; `.env.example` con claves sin valores; `.env` ignorado. Opciones tipadas con `ValidateOnStart()` para fallar rápido. El esqueleto incluye la validación de `Cors:AllowedOrigins` y un test que comprueba el fallo cuando falta. Las claves `Ai:*` solo se documentan como comentario.

### D8. EF Core y PostgreSQL
- A. Diferir a un cambio propio (sin entidades no hay nada que validar; Docker sin confirmar). **Recomendada.**
- B. `DbContext` vacío y PostgreSQL en `docker-compose` desde ya.

### D9. Pila de pruebas
- xUnit v3 (recomendada) con respaldo a v2 si la cobertura no funciona rápido.
- Un único ejecutor (VSTest o MTP) fijado en `global.json`.
- Cobertura con Coverlet en formato Cobertura; umbrales por nivel comprobados en CI.
- Aserciones propias de xUnit (sin dependencia nueva).
- Integración con `WebApplicationFactory` para `/health`; Testcontainers queda para el cambio de datos.

### D10. Analizadores, formato y puerta de calidad
`Directory.Build.props` con `TreatWarningsAsErrors`, `Nullable`, `AnalysisLevel=latest`, `EnforceCodeStyleInBuild`. Gestión central de paquetes con versiones fijadas. `global.json` con SDK 10.0.302 y `rollForward: latestFeature`. `dotnet format --verify-no-changes` y `dotnet list package --vulnerable` en CI. Si alguna regla molesta, se afina en `.editorconfig` en vez de desactivar `TreatWarningsAsErrors`.

### D11. Docker y GitHub Actions
- Dockerfile multi-stage, usuario no root, sin secretos, `HEALTHCHECK` contra `/health`.
- `docker-compose.yml` leyendo `.env`.
- `.github/workflows/ci.yml`: restore, format, build con `-warnaserror`, test con cobertura, auditoría de paquetes, publish y build de imagen sin push. No puede ejecutarse hasta que exista el repositorio remoto.

### D12. Hooks de Git
Diferidos: no hay repositorio. Se documenta la propuesta para un cambio posterior.

### D13. ADR previstos en `docs/adr/`
0001 formato de ADR · 0002 net10.0 · 0003 monolito modular y regla de dependencias · 0004 minimal APIs y OpenAPI · 0005 frontend y CORS · 0006 pila de pruebas · 0007 configuración y validación al arrancar.

## 4. Alcance propuesto

**Dentro** (cambio único, entrega `single-pr`):
- Archivos raíz: `.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.gitignore`, `.gitattributes`, `.dockerignore`, `.env.example`.
- Proyectos Domain, Application, Infrastructure, Api y Web (plantilla `blazorwasm` sin PWA ni autenticación) y 4 proyectos de pruebas.
- Un test de dominio trivial para demostrar el ciclo TDD rojo-verde.
- `GET /health` con su prueba de integración; configuración tipada con validación al arrancar; CORS por configuración.
- Test de arquitectura de la regla de dependencias.
- Exportación de OpenAPI a `docs/api/openapi.json`.
- `Dockerfile`, `docker-compose.yml`, workflow de CI, ADR 0001–0007 y `README.md`.
- Actualización de `openspec/config.yaml` con los comandos reales (en apply/archive).

**Fuera:** fichajes, ausencias, saldo, horas extra y asistente; `DbContext`, migraciones, PostgreSQL y Testcontainers; `IChatClient`, OllamaSharp y herramientas de IA; autenticación y autorización; limitación de tasa y cabeceras de seguridad; hooks de Git y `git init`; despliegue; Dependabot; proyecto `Contracts`; Swagger UI; pruebas E2E; observabilidad.

**Tamaño:** entre 350 y 550 líneas escritas a mano (sin contar archivos generados por plantilla). Puede superar el umbral de ~400 líneas. Opciones: dos tandas dentro del mismo cambio (núcleo compilable con tests; luego contenedor, CI, ADR y README) o dividir el cambio en dos.

## 5. Riesgos y dependencias

- Docker, Node, Ollama y PostgreSQL sin confirmar. Sin Docker, `Dockerfile` y compose se escriben pero quedan sin probar, y debe anotarse.
- xUnit v3 con cobertura sin verificar de extremo a extremo: criterio de aceptación `dotnet test` con cobertura generada y respaldo a xUnit v2.
- Las plantillas de xUnit v3 requieren una descarga (`dotnet new install xunit.v3.templates`), o bien crear los `.csproj` a mano.
- `.slnx` puede no abrirse en IDE antiguos.
- `global.json` fijo puede fallar si cambia el SDK de la máquina; `latestFeature` lo mitiga.
- El workflow de CI no puede ejecutarse hasta existir el remoto; riesgo de fallo en el primer push.
- Calendario: unos 15 días hasta el 25-oct-2026 y poco tiempo diario.
- `TreatWarningsAsErrors` con la plantilla Blazor puede dar avisos: dejar margen para afinar `.editorconfig`.
- Plan de reversión: eliminar los directorios creados (no hay Git).
- Engram no disponible: este documento solo existe como archivo.

## 6. Preguntas abiertas

**Producto:**
- Q1. ¿Cuál es el mínimo imprescindible para el 25-oct: esqueleto más un fichaje de extremo a extremo, o más alcance?
- Q2. ¿Dónde se desplegará: nube concreta, VPS o solo local para la demostración?
- Q3. ¿Cuántos roles hay desde el inicio (empleado, responsable)? Afecta a cuándo introducir autenticación.

**Técnicas:**
- Q4. ¿Docker instalado? ¿PostgreSQL en compose ya en este cambio?
- Q5. ¿Qué test de dominio trivial para probar el ciclo TDD (`IClock`, un objeto de valor de horas trabajadas…)?
- Q6. ¿Un solo PR de ~500 líneas o dividir en dos?
- Q7. ¿Minimal APIs (recomendado) o controladores?
- Q8. ¿Qué IDE usas? (para confirmar que abre `.slnx`)
- Q9. ¿xUnit v3 con respaldo a v2, o directamente v2?

## 7. Recomendación y siguiente fase

Aprobar el esqueleto con net10.0, `.slnx`, 4 capas más Web, regla de dependencias por referencias y test de arquitectura, minimal APIs con OpenAPI exportado, xUnit v3 (respaldo v2) con Coverlet, `/health` y un test de dominio trivial. Diferir PostgreSQL, IA, autenticación y hooks. Resolver Q4–Q6 antes de especificar.

Siguiente fase: `sdd-propose` para `esqueleto-solucion`, con plan de reversión y decisión sobre el tamaño de la entrega.
