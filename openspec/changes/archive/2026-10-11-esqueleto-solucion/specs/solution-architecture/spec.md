# Especificación: solution-architecture

## Purpose

Define la estructura de la solución, la regla de dependencias de Clean Architecture y su comprobación automática, la puerta de calidad, el empaquetado Docker, la CI y la documentación mínima. Capacidad NUEVA; todos los requisitos son `ADDED`. Se agrupan en Tanda 1 (núcleo compilable) y Tanda 2 (entrega).

Fuera de alcance: fichajes, ausencias, autenticación, IA, PostgreSQL/EF Core, despliegue en Railway, hooks de Git y la pantalla de marcajes (cambio posterior).

## ADDED Requirements

## Tanda 1: núcleo compilable

### Requirement: Estructura de proyectos y destino de plataforma

La solución DEBE ser `TimeTracker.sln` en formato clásico e incluir en `src/` `TimeTracker.Domain`, `TimeTracker.Application`, `TimeTracker.Infrastructure`, `TimeTracker.Api` y `TimeTracker.Web`, y en `tests/` `TimeTracker.Domain.Tests`, `TimeTracker.Application.Tests`, `TimeTracker.Api.IntegrationTests` y `TimeTracker.ArchitectureTests`. Todos los proyectos DEBEN apuntar a `net10.0`. `global.json` DEBE fijar el SDK `10.0.302` con `rollForward: latestFeature`; NO DEBE seleccionar un ejecutor de pruebas distinto del predeterminado del SDK (VSTest).

#### Scenario: Contenido de la solución
- DADO el repositorio con el cambio aplicado
- CUANDO se ejecuta `dotnet sln TimeTracker.sln list`
- ENTONCES aparecen exactamente los 9 proyectos indicados y no existe `TimeTracker.slnx`

#### Scenario: Destino de plataforma
- DADO cada `.csproj`
- CUANDO se evalúa `TargetFramework`
- ENTONCES su valor es `net10.0`

### Requirement: Regla de dependencias por referencias de proyecto

Las referencias de proyecto DEBEN ser: Domain sin referencias; Application solo Domain; Infrastructure solo Application; Api solo Application e Infrastructure (composition root); Web ninguna referencia a los demás proyectos de `src/`, comunicándose con la API solo por HTTP. `Domain` NO DEBE tener dependencias de paquetes externos.

#### Scenario: Referencias esperadas
- DADO cada proyecto de `src/`
- CUANDO se ejecuta `dotnet list <proyecto> reference`
- ENTONCES coincide con la lista permitida y Web no lista ningún proyecto de `src/`

#### Scenario: Referencia ilegal no compila
- DADO una `ProjectReference` de Domain hacia Application
- CUANDO se ejecuta `dotnet build`
- ENTONCES el build falla por dependencia circular

### Requirement: Prueba de arquitectura por reflexión

`TimeTracker.ArchitectureTests` DEBE verificar con `Assembly.GetReferencedAssemblies()` que ningún ensamblado de `src/` (salvo Web) referencia a otro más externo que la regla permite, y que Domain no referencia ensamblados fuera del marco base. La prueba DEBE fallar si se introduce una referencia ilegal y NO DEBE depender de NetArchTest.

#### Scenario: Estado conforme
- DADO las referencias vigentes
- CUANDO se ejecuta `dotnet test` en `TimeTracker.ArchitectureTests`
- ENTONCES la prueba pasa

#### Scenario: Referencia ilegal detectada (comprobado una vez)
- DADO una referencia temporal de `TimeTracker.Domain` a un ensamblado externo (p. ej. `Microsoft.AspNetCore.App`, usado en código para que el compilador la conserve)
- CUANDO se ejecuta la prueba de arquitectura
- ENTONCES falla indicando la referencia ilegal, y tras retirar la referencia vuelve a pasar

### Requirement: Gestión central de paquetes y analizadores

`Directory.Packages.props` DEBE activar `ManagePackageVersionsCentrally` con versiones fijas; ningún `.csproj` DEBE declarar `Version` en un `PackageReference`. `Directory.Build.props` DEBE activar `TreatWarningsAsErrors`, `Nullable`, `AnalysisLevel=latest` y `EnforceCodeStyleInBuild`. Las reglas molestas se ajustan en `.editorconfig`; NO DEBE desactivarse `TreatWarningsAsErrors`.

#### Scenario: Versiones centralizadas
- DADO todos los `.csproj`
- CUANDO se buscan atributos `Version` en `PackageReference`
- ENTONCES no hay ninguno

#### Scenario: Aviso como error
- DADO una variable local sin usar introducida en Domain
- CUANDO se ejecuta `dotnet build -warnaserror`
- ENTONCES el build falla

### Requirement: Puerta de calidad local

Los comandos `dotnet build -warnaserror`, `dotnet test` y `dotnet format --verify-no-changes` DEBEN terminar con código de salida `0` en el estado final. `TimeTracker.Web` DEBE compilar con la plantilla `blazorwasm` sin modificar, salvo trasladar las versiones de sus `PackageReference` a `Directory.Packages.props`, y NO DEBE contener reglas de negocio.

#### Scenario: Build limpio
- DADO el cambio aplicado
- CUANDO se ejecuta `dotnet build -warnaserror` desde la raíz
- ENTONCES termina con `0` avisos y `0` errores

#### Scenario: Pruebas en verde
- DADO el cambio aplicado
- CUANDO se ejecuta `dotnet test`
- ENTONCES pasan las pruebas de `WorkedHours`, `/health`, fallo de arranque sin configuración y regla de dependencias

#### Scenario: Formato verificado
- DADO el cambio aplicado
- CUANDO se ejecuta `dotnet format --verify-no-changes`
- ENTONCES termina con `0`

### Requirement: Informe de cobertura

`dotnet test` DEBE poder generar un informe en formato Cobertura mediante Coverlet, y `TimeTracker.Domain` DEBE mostrar 100 % de cobertura de líneas y ramas. El informe se genera por proyecto de pruebas; el 100 % de Domain se mide en el informe de `TimeTracker.Domain.Tests` (en los informes de otros proyectos Domain aparece con menor cobertura). Para Application el objetivo es 80 % cuando exista código (DEBERÍA); Infrastructure sin umbral.

#### Scenario: Informe generado
- DADO `dotnet test` con recolección de cobertura habilitada
- CUANDO termina
- ENTONCES existe un `coverage.cobertura.xml` por proyecto de pruebas y el de `TimeTracker.Domain.Tests` muestra `line-rate="1"` y `branch-rate="1"` para Domain

Nota de verificación: xUnit v3 + Coverlet NO está verificado de extremo a extremo. Respaldo: xUnit v2 con `coverlet.collector`; si ninguno genera informe, el requisito queda sin cumplir y se reporta.

### Requirement: Archivos raíz de repositorio

La raíz DEBE contener `.editorconfig`, `.gitignore`, `.gitattributes`, `.dockerignore` y `.env.example`. `.gitignore` y `.dockerignore` DEBEN excluir `.env`, `bin/`, `obj/` y los resultados de cobertura.

#### Scenario: Archivos presentes
- DADO la raíz del repositorio
- CUANDO se listan los archivos
- ENTONCES existen los cinco archivos y `.gitignore` contiene `.env`

## Tanda 2: entrega

### Requirement: Imagen Docker de la API

`Dockerfile` DEBE ser multi-stage, ejecutar como usuario no root, NO DEBE incluir secretos, DEBE definir `HEALTHCHECK` contra `/health` y DEBE configurar el puerto por variable de entorno.

#### Scenario: Construcción
- DADO Docker en marcha
- CUANDO se ejecuta `docker build -t timetracker-api .`
- ENTONCES termina con `0` y `docker inspect` muestra un `User` distinto de `root` y de vacío, y un `Healthcheck`

#### Scenario: Salud dentro del contenedor
- DADO el contenedor arrancado con `Cors__AllowedOrigins__0` válida
- CUANDO se consulta `GET /health` en el puerto publicado
- ENTONCES responde `200` y el estado de salud pasa a `healthy`

#### Scenario: Sin secretos en la imagen
- DADO la imagen construida
- CUANDO se inspeccionan `docker history` y `docker inspect`
- ENTONCES no aparece ningún valor de `.env` ni de claves de `.env.example`

Nota de verificación: versiones de las imágenes base de Docker NO verificadas; se fijan y comprueban en diseño. Sin Docker, el requisito se anota como no probado.

### Requirement: Docker Compose con solo la API

`docker-compose.yml` DEBE definir únicamente el servicio `api`, leer su configuración de `.env` y NO DEBE definir `postgres` ni otros servicios.

#### Scenario: Servicios definidos
- DADO `docker-compose.yml`
- CUANDO se ejecuta `docker compose config --services`
- ENTONCES la salida es exactamente `api`

### Requirement: Flujo de CI

`.github/workflows/ci.yml` DEBE existir y ejecutar, en este orden: restore, `dotnet format --verify-no-changes`, `dotnet build -warnaserror`, `dotnet test` con cobertura, auditoría de paquetes vulnerables, publish y build de imagen sin push. La CI NO se ejecuta hasta que exista un remoto; sus pasos DEBEN reproducirse localmente y anotarse.

#### Scenario: Pasos presentes
- DADO `ci.yml`
- CUANDO se analiza como YAML
- ENTONCES es válido y contiene los pasos citados en ese orden, sin `push` de imagen

### Requirement: ADR y README

`docs/adr/` DEBE contener los ADR 0001 a 0007 (formato, net10.0, monolito modular y `.sln`, minimal APIs y OpenAPI, WASM y CORS, pila de pruebas, configuración y validación), cada uno con contexto, decisión, consecuencias y alternativas. `README.md` DEBE describir el proyecto, la pila, cómo compilar, probar y ejecutar, y la estructura. `openspec/config.yaml` DEBE actualizarse con los comandos reales de prueba, build y cobertura.

#### Scenario: ADR completos
- DADO `docs/adr/`
- CUANDO se listan los archivos
- ENTONCES existen siete ADR numerados 0001 a 0007, cada uno con las cuatro secciones

#### Scenario: README ejecutable
- DADO `README.md`
- CUANDO una persona sigue sus comandos en una máquina limpia con el SDK y Docker
- ENTONCES `dotnet test` pasa y la API responde en `/health`
