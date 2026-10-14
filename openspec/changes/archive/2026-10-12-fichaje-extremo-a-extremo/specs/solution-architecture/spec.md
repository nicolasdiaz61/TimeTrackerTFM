# Delta para solution-architecture

Cambio: `fichaje-extremo-a-extremo`. Actualiza los requisitos de la spec vigente que dejan de ser ciertos al añadir PostgreSQL, la página `/fichajes` y la cobertura de Application. Los demás requisitos de `solution-architecture` no cambian. Nota: el texto `Purpose` de la spec vigente, que excluye PostgreSQL, EF Core y la pantalla de marcajes, queda obsoleto al archivar este cambio y debe actualizarse entonces.

## RENAMED Requirements

### Requirement: Docker Compose con solo la API → Docker Compose con API y PostgreSQL

(Reason: el compose pasa a definir también el servicio `postgres`; el nombre anterior sería falso)
(Migration: referencias al nombre antiguo en tareas, informes y documentación pasan al nuevo)

## MODIFIED Requirements

### Requirement: Docker Compose con API y PostgreSQL

`docker-compose.yml` DEBE definir exactamente dos servicios, `api` y `postgres`, y leer su configuración de `.env`. `postgres` DEBE usar la imagen `postgres:18-alpine`, tener `healthcheck` con `pg_isready`, un volumen con nombre `pgdata` (montado en `/var/lib/postgresql`) y publicar el puerto `5432` solo en `127.0.0.1`. Usuario, contraseña y base de datos NO DEBEN estar escritos en el archivo: salen de `.env`. `api` DEBE esperar a `postgres` con `depends_on` en condición `service_healthy`, ejecutarse con `ASPNETCORE_ENVIRONMENT=Development`, con `Database__ApplyMigrationsOnStartup=true` y con `ConnectionStrings__Default` construida a partir de las variables de `.env` y apuntando al servicio `postgres`, y publicar su puerto `8080` solo en `127.0.0.1` (la identidad provisional no debe quedar expuesta a la red local). Compose NO DEBE definir el servicio de la Web.
(Previously: solo el servicio `api`, sin `postgres` ni otros servicios)

#### Scenario: Servicios definidos
- DADO `docker-compose.yml`
- CUANDO se ejecuta `docker compose config --services`
- ENTONCES la salida contiene exactamente `api` y `postgres`, en cualquier orden

#### Scenario: Configuración de postgres
- DADO `docker-compose.yml`
- CUANDO se revisa el servicio `postgres`
- ENTONCES usa `postgres:18-alpine`, declara `healthcheck`, el volumen `pgdata` y publica `5432` únicamente con la dirección `127.0.0.1`

#### Scenario: Puerto de la API solo local
- DADO `docker-compose.yml`
- CUANDO se revisa el servicio `api`
- ENTONCES publica `8080` únicamente con la dirección `127.0.0.1` y declara `ASPNETCORE_ENVIRONMENT=Development`

#### Scenario: Sin credenciales en el archivo
- DADO `docker-compose.yml`
- CUANDO se buscan valores de usuario, contraseña o base de datos de PostgreSQL
- ENTONCES solo aparecen como referencias a variables de `.env`, nunca como literales

#### Scenario: Arranque ordenado
- DADO Docker en marcha y `.env` creado a partir de `.env.example` y completado con una contraseña de PostgreSQL, un GUID inventado en `Identity__DevelopmentEmployeeId` y un `Cors__AllowedOrigins__0` válido
- CUANDO se ejecuta `docker compose up -d --wait`
- ENTONCES `postgres` queda `healthy`, `api` arranca después, las migraciones se aplican y `GET /health` en el puerto publicado responde `200`

### Requirement: Regla de dependencias por referencias de proyecto

Las referencias de proyecto DEBEN ser: Domain sin referencias; Application solo Domain; Infrastructure solo Application; Api solo Application e Infrastructure (composition root); Web ninguna referencia a los demás proyectos de `src/`, comunicándose con la API solo por HTTP. `Domain` y `Application` NO DEBEN tener dependencias de paquetes externos (Infrastructure incorpora EF Core y Npgsql; las capas internas no).
(Previously: solo Domain sin paquetes externos)

#### Scenario: Referencias esperadas
- DADO cada proyecto de `src/`
- CUANDO se ejecuta `dotnet list <proyecto> reference`
- ENTONCES coincide con la lista permitida y Web no lista ningún proyecto de `src/`

#### Scenario: Referencia ilegal no compila
- DADO una `ProjectReference` de Domain hacia Application
- CUANDO se ejecuta `dotnet build`
- ENTONCES el build falla por dependencia circular

#### Scenario: Application sin paquetes
- DADO `TimeTracker.Application.csproj`
- CUANDO se ejecuta `dotnet list TimeTracker.Application package`
- ENTONCES no lista ningún paquete directo

### Requirement: Prueba de arquitectura por reflexión

`TimeTracker.ArchitectureTests` DEBE verificar con `Assembly.GetReferencedAssemblies()` que ningún ensamblado de `src/` (salvo Web) referencia a otro más externo que la regla permite, que Domain no referencia ensamblados fuera del marco base y que Application referencia solo Domain y el marco base. Además DEBE comprobar leyendo el XML de `TimeTracker.Domain.csproj` y `TimeTracker.Application.csproj` que ninguno declara `PackageReference` ni `FrameworkReference`. Las pruebas DEBEN fallar si se introduce una referencia ilegal y NO DEBEN depender de NetArchTest.
(Previously: solo comprobaba Domain, y únicamente por reflexión)

#### Scenario: Estado conforme
- DADO las referencias vigentes
- CUANDO se ejecuta `dotnet test` en `TimeTracker.ArchitectureTests`
- ENTONCES las pruebas pasan, incluida `Application_ReferencesOnlyDomainAndBaseFramework`

#### Scenario: Referencia ilegal detectada (comprobado una vez)
- DADO una referencia temporal de `TimeTracker.Domain` a un ensamblado externo (p. ej. `Microsoft.AspNetCore.App`, usado en código para que el compilador la conserve)
- CUANDO se ejecuta la prueba de arquitectura
- ENTONCES falla indicando la referencia ilegal, y tras retirar la referencia vuelve a pasar

#### Scenario: Paquete en Application detectado (comprobado una vez)
- DADO un `PackageReference` temporal en `TimeTracker.Application.csproj`
- CUANDO se ejecuta la comprobación XML de la prueba de arquitectura
- ENTONCES falla indicando el proyecto y el paquete, y tras retirarlo vuelve a pasar

### Requirement: Puerta de calidad local

Los comandos `dotnet build -warnaserror`, `dotnet test` y `dotnet format --verify-no-changes` DEBEN terminar con código de salida `0` en el estado final. `TimeTracker.Web` DEBE compilar y contener la página `/fichajes`, su cliente HTTP tipado y la configuración `ApiBaseUrl`; las versiones de sus `PackageReference` DEBEN estar en `Directory.Packages.props`. `TimeTracker.Web` NO DEBE contener reglas de negocio.
(Previously: Web debía compilar con la plantilla `blazorwasm` sin modificar, salvo el traslado de versiones de paquetes)

#### Scenario: Build limpio
- DADO el cambio aplicado
- CUANDO se ejecuta `dotnet build -warnaserror` desde la raíz
- ENTONCES termina con `0` avisos y `0` errores

#### Scenario: Pruebas en verde
- DADO el cambio aplicado y Docker en marcha
- CUANDO se ejecuta `dotnet test`
- ENTONCES pasan las pruebas de `WorkedHours`, `TimeEntry`, casos de uso, repositorio, migraciones, API de fichajes, `/health`, fallo de arranque sin configuración y regla de dependencias (Domain y Application)

#### Scenario: Formato verificado
- DADO el cambio aplicado
- CUANDO se ejecuta `dotnet format --verify-no-changes`
- ENTONCES termina con `0`, sin reformatear las migraciones generadas

#### Scenario: Web con la funcionalidad de fichajes
- DADO `src/TimeTracker.Web`
- CUANDO se revisan sus archivos
- ENTONCES existen la página `/fichajes`, `TimeEntriesApiClient` y `ApiBaseUrl` en `wwwroot/appsettings.json`, y ningún `.csproj` de `src/` aparece en sus referencias

### Requirement: Informe de cobertura

`dotnet test` DEBE poder generar un informe en formato Cobertura mediante Coverlet. `TimeTracker.Domain` DEBE mostrar 100 % de cobertura de líneas y ramas, medido en el informe de `TimeTracker.Domain.Tests`. `TimeTracker.Application` DEBE alcanzar al menos 80 % de cobertura de líneas, medido en el informe de `TimeTracker.Application.Tests`. Infrastructure y el código generado (migraciones) no tienen umbral.
(Previously: Application era un objetivo DEBERÍA «cuando exista código»; ahora es obligatorio)

#### Scenario: Informe generado
- DADO `dotnet test` con recolección de cobertura habilitada
- CUANDO termina
- ENTONCES existe un `coverage.cobertura.xml` por proyecto de pruebas y el de `TimeTracker.Domain.Tests` muestra `line-rate="1"` y `branch-rate="1"` para Domain

#### Scenario: Umbral de Application
- DADO los informes de `dotnet test`
- CUANDO se ejecuta `pwsh scripts/check-coverage.ps1 -ResultsDirectory TestResults -Package TimeTracker.Application -MinimumPercent 80`
- ENTONCES termina con código `0`

Nota de verificación: la cobertura de Application se mide con las pruebas unitarias de `Application.Tests`; las pruebas de integración no cuentan para el umbral.

### Requirement: Imagen Docker de la API

`Dockerfile` DEBE ser multi-stage, ejecutar como usuario no root, NO DEBE incluir secretos, DEBE definir `HEALTHCHECK` contra `/health` y DEBE configurar el puerto por variable de entorno.
(Previously: igual; cambia solo el escenario de salud, que ahora necesita `ConnectionStrings__Default` y entorno `Development`)

#### Scenario: Construcción
- DADO Docker en marcha
- CUANDO se ejecuta `docker build -t timetracker-api .`
- ENTONCES termina con `0` y `docker inspect` muestra un `User` distinto de `root` y de vacío, y un `Healthcheck`

#### Scenario: Salud dentro del contenedor
- DADO el contenedor arrancado con `Cors__AllowedOrigins__0` válida, `ConnectionStrings__Default` (puede apuntar a un servidor inexistente, con `Database__ApplyMigrationsOnStartup` sin definir), `Identity__DevelopmentEmployeeId` válido y `ASPNETCORE_ENVIRONMENT=Development` (sin las tres últimas el contenedor no arranca)
- CUANDO se consulta `GET /health` en el puerto publicado
- ENTONCES responde `200` y el estado de salud pasa a `healthy`

#### Scenario: Sin secretos en la imagen
- DADO la imagen construida
- CUANDO se inspeccionan `docker history` y `docker inspect`
- ENTONCES no aparece ningún valor de `.env` ni de claves de `.env.example`

Nota de verificación: versiones de las imágenes base de Docker NO verificadas; se fijan y comprueban en diseño. Sin Docker, el requisito se anota como no probado.

### Requirement: Flujo de CI

`.github/workflows/ci.yml` DEBE existir y ejecutar, en este orden: restore, `dotnet format --verify-no-changes`, `dotnet build -warnaserror`, `dotnet test` con cobertura (en un ejecutor con Docker, por Testcontainers), **comprobación de cobertura de Application ≥ 80 % con `scripts/check-coverage.ps1`**, auditoría de paquetes vulnerables, publish y build de imagen sin push. La CI NO se ejecuta hasta que exista un remoto; sus pasos DEBEN reproducirse localmente y anotarse.
(Previously: sin el paso de comprobación de cobertura de Application)

#### Scenario: Pasos presentes
- DADO `ci.yml`
- CUANDO se analiza como YAML
- ENTONCES es válido y contiene los pasos citados en ese orden, sin `push` de imagen

#### Scenario: Paso de cobertura de Application
- DADO `ci.yml`
- CUANDO se busca el paso de cobertura
- ENTONCES invoca `check-coverage.ps1` con el paquete `TimeTracker.Application` y `-MinimumPercent 80`, después de `dotnet test` y antes de la auditoría

### Requirement: ADR y README

`docs/adr/` DEBE contener los ADR 0001 a 0010: los siete previos (formato, net10.0, monolito modular y `.sln`, minimal APIs y OpenAPI, WASM y CORS, pila de pruebas, configuración y validación) y 0008 persistencia (PostgreSQL, EF Core, índice único parcial, migraciones por bandera, Testcontainers), 0009 identidad provisional (`ICurrentUser` solo en Development y exportación OpenAPI forzada a Development con valores ficticios) y 0010 tiempo (`TimeProvider`, UTC, instantes truncados a segundos, minutos truncados, JSON con `Z`); cada uno con contexto, decisión, consecuencias y alternativas. `README.md` DEBE describir el proyecto, la pila, cómo compilar, probar (con la necesidad de Docker) y ejecutar (incluido `docker compose up` y la página `/fichajes`), y la estructura. `openspec/config.yaml` DEBE actualizarse con los comandos reales de prueba, build y cobertura y con el estado de las pruebas de integración con PostgreSQL.
(Previously: ADR 0001 a 0007; README sin Docker para pruebas ni `/fichajes`)

#### Scenario: ADR completos
- DADO `docs/adr/`
- CUANDO se listan los archivos
- ENTONCES existen diez ADR numerados 0001 a 0010, cada uno con las cuatro secciones

#### Scenario: README ejecutable
- DADO `README.md`
- CUANDO una persona sigue sus comandos en una máquina limpia con el SDK y Docker
- ENTONCES `dotnet test` pasa y la API responde en `/health`

#### Scenario: README cubre la novedad
- DADO `README.md`
- CUANDO se revisa
- ENTONCES menciona `docker compose up`, la ruta `/fichajes`, que las pruebas de integración requieren Docker y las variables de entorno necesarias para ejecutar la API en local (`ASPNETCORE_ENVIRONMENT=Development`, `ConnectionStrings__Default`, `Identity__DevelopmentEmployeeId`)

## ADDED Requirements

### Requirement: Exportación del contrato OpenAPI sin base de datos ni identidad real

La generación de `docs/api/openapi.json` durante el build (`GetDocument.Insider`) DEBE funcionar sin base de datos accesible y sin variables de entorno del desarrollador: la rama de generación DEBE forzar el entorno `Development` y aportar valores ficticios para `Cors:AllowedOrigins`, `ConnectionStrings:Default` (servidor inexistente, sin contraseña real) e `Identity:DevelopmentEmployeeId`, de modo que `ValidateOnStart` no falle. Estos valores NO DEBEN estar en `appsettings*.json` ni afectar a la ejecución normal.

#### Scenario: Contrato generado en build
- DADO un entorno sin `ConnectionStrings__Default`, `Identity__DevelopmentEmployeeId` ni `ASPNETCORE_ENVIRONMENT=Development`
- CUANDO se ejecuta `dotnet build` de `TimeTracker.Api`
- ENTONCES el build termina con `0` y `docs/api/openapi.json` contiene los tres endpoints de fichajes

#### Scenario: Sin efecto en la ejecución normal
- DADO la API arrancada fuera de la generación del contrato, en `Production` y sin `ConnectionStrings__Default`
- CUANDO intenta arrancar
- ENTONCES falla por la validación de configuración (los valores ficticios no se aplican)
