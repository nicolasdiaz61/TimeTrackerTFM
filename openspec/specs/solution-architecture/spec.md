# Especificación: solution-architecture

## Purpose

Define la estructura de la solución, la regla de dependencias de Clean Architecture y su comprobación automática (Domain y Application sin paquetes), la puerta de calidad, la cobertura (Domain 100 %, Application ≥ 80 %), el empaquetado Docker, `docker-compose.yml` con la API y PostgreSQL, la CI y la documentación (13 ADR y README). Incluye la Web con las páginas `/fichajes`, `/login` y `/equipo`, su cliente tipado y el manejador del token, la exportación del contrato OpenAPI en build sin base de datos (con `Jwt:SigningKey` ficticia y siembra desactivada), la acotación de los paquetes de autenticación y la ausencia de restos de la identidad provisional. `docker-compose.yml` publica la API en `127.0.0.1:5274` y exige `Jwt__SigningKey` y `Seed__DemoPassword`. Los requisitos se agrupan en Tanda 1 (núcleo compilable) y Tanda 2 (entrega); los actualizados por los cambios `fichaje-extremo-a-extremo` (PostgreSQL, EF Core, fichajes, `/fichajes`) y `autenticacion-y-roles` (JWT, roles, equipo, `/login`, `/equipo`, ADR 0011 y 0012) sustituyen a los de `esqueleto-solucion`.

Fuera de alcance: ausencias, IA, despliegue en Railway, hooks de Git, CI ejecutada (sin remoto Git) y el servicio de la Web en compose.

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

Las referencias de proyecto DEBEN ser: Domain sin referencias; Application solo Domain; Infrastructure solo Application; Api solo Application e Infrastructure (composition root); Web ninguna referencia a los demás proyectos de `src/`, comunicándose con la API solo por HTTP. `Domain` y `Application` NO DEBEN tener dependencias de paquetes externos (Infrastructure incorpora EF Core y Npgsql; las capas internas no).

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

Los comandos `dotnet build -warnaserror`, `dotnet test` y `dotnet format --verify-no-changes` DEBEN terminar con código de salida `0` en el estado final. `TimeTracker.Web` DEBE compilar y contener las páginas `/fichajes`, `/login` y `/equipo`, su cliente HTTP tipado, el manejador del token y la configuración `ApiBaseUrl`; las versiones de sus `PackageReference` DEBEN estar en `Directory.Packages.props`. `TimeTracker.Web` NO DEBE contener reglas de negocio.

#### Scenario: Build limpio
- DADO el cambio aplicado
- CUANDO se ejecuta `dotnet build -warnaserror` desde la raíz
- ENTONCES termina con `0` avisos y `0` errores

#### Scenario: Pruebas en verde
- DADO el cambio aplicado y Docker en marcha
- CUANDO se ejecuta `dotnet test`
- ENTONCES pasan las pruebas de `WorkedHours`, `TimeEntry`, `UserAccount`, casos de uso (incluidos login y equipo), repositorios, migraciones, API de fichajes, autenticación, equipo, `/health`, fallo de arranque sin configuración (`JwtStartupTests` incluidas) y regla de dependencias (Domain y Application)

#### Scenario: Formato verificado
- DADO el cambio aplicado
- CUANDO se ejecuta `dotnet format --verify-no-changes`
- ENTONCES termina con `0`, sin reformatear las migraciones generadas

#### Scenario: Web con sesión y fichajes
- DADO `src/TimeTracker.Web`
- CUANDO se revisan sus archivos
- ENTONCES existen las páginas `/fichajes`, `/login` y `/equipo`, `TimeEntriesApiClient`, el manejador del token y `ApiBaseUrl` en `wwwroot/appsettings.json`, y ningún `.csproj` de `src/` aparece en sus referencias

### Requirement: Informe de cobertura

`dotnet test` DEBE poder generar un informe en formato Cobertura mediante Coverlet. `TimeTracker.Domain` DEBE mostrar 100 % de cobertura de líneas y ramas, medido en el informe de `TimeTracker.Domain.Tests`. `TimeTracker.Application` DEBE alcanzar al menos 80 % de cobertura de líneas, medido en el informe de `TimeTracker.Application.Tests`. Infrastructure y el código generado (migraciones) no tienen umbral.

#### Scenario: Informe generado
- DADO `dotnet test` con recolección de cobertura habilitada
- CUANDO termina
- ENTONCES existe un `coverage.cobertura.xml` por proyecto de pruebas y el de `TimeTracker.Domain.Tests` muestra `line-rate="1"` y `branch-rate="1"` para Domain

#### Scenario: Umbral de Application
- DADO los informes de `dotnet test`
- CUANDO se ejecuta `pwsh scripts/check-coverage.ps1 -ResultsDirectory TestResults -Package TimeTracker.Application -MinimumPercent 80`
- ENTONCES termina con código `0`

Nota de verificación: la cobertura de Application se mide con las pruebas unitarias de `Application.Tests`; las pruebas de integración no cuentan para el umbral.

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
- DADO el contenedor arrancado con `Cors__AllowedOrigins__0` válida, `ConnectionStrings__Default` (puede apuntar a un servidor inexistente, con `Database__ApplyMigrationsOnStartup` sin definir) y `Jwt__SigningKey` de 32 caracteres o más (sin las tres el contenedor no arranca)
- CUANDO se consulta `GET /health` sin token en el puerto publicado
- ENTONCES responde `200` y el estado de salud pasa a `healthy`

#### Scenario: Sin secretos en la imagen
- DADO la imagen construida
- CUANDO se inspeccionan `docker history` y `docker inspect`
- ENTONCES no aparece ningún valor de `.env` ni de claves de `.env.example`, ni `Jwt__SigningKey`

Nota de verificación: versiones de las imágenes base de Docker NO verificadas; se fijan y comprueban en diseño. Sin Docker, el requisito se anota como no probado.

### Requirement: Docker Compose con API y PostgreSQL

`docker-compose.yml` DEBE definir exactamente dos servicios, `api` y `postgres`, y leer su configuración de `.env`. `postgres` DEBE usar la imagen `postgres:18-alpine`, tener `healthcheck` con `pg_isready`, un volumen con nombre `pgdata` (montado en `/var/lib/postgresql`) y publicar el puerto `5432` solo en `127.0.0.1`. Usuario, contraseña y base de datos NO DEBEN estar escritos en el archivo: salen de `.env`. `api` DEBE esperar a `postgres` con `depends_on` en condición `service_healthy`, ejecutarse con `ASPNETCORE_ENVIRONMENT=Development`, con `Database__ApplyMigrationsOnStartup=true` y con `ConnectionStrings__Default` construida a partir de las variables de `.env` y apuntando al servicio `postgres`. `api` DEBE publicar el puerto `8080` del contenedor como `127.0.0.1:5274:8080` (solo en `127.0.0.1`; el puerto de host `5274` coincide con `ApiBaseUrl` de la Web). `api` DEBE leer `Jwt__SigningKey` y `Seed__DemoPassword` de `.env` como obligatorias (con mensaje de error si faltan o están vacías; sin literal en el archivo) y activar la siembra con `Seed__DemoUsers=true`. Compose NO DEBE definir el servicio de la Web ni referencias a la identidad provisional.

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
- ENTONCES publica el puerto `8080` del contenedor como `127.0.0.1:5274:8080`, sin otra publicación, y declara `ASPNETCORE_ENVIRONMENT=Development`

#### Scenario: Claves de autenticación en compose
- DADO `docker-compose.yml`
- CUANDO se revisa el servicio `api`
- ENTONCES `Jwt__SigningKey` y `Seed__DemoPassword` son referencias obligatorias a variables de `.env`, `Seed__DemoUsers` vale `true` y no aparece ningún valor literal de clave ni de contraseña ni `Identity__`

#### Scenario: Sin credenciales en el archivo
- DADO `docker-compose.yml`
- CUANDO se buscan valores de usuario, contraseña o base de datos de PostgreSQL
- ENTONCES solo aparecen como referencias a variables de `.env`, nunca como literales

#### Scenario: Arranque ordenado y login de demostración
- DADO Docker en marcha y `.env` creado a partir de `.env.example` y completado con una contraseña de PostgreSQL, una `Jwt__SigningKey` generada de 32 caracteres o más, `Seed__DemoPassword` con la contraseña de demostración documentada en el README y un `Cors__AllowedOrigins__0` válido
- CUANDO se ejecuta `docker compose up -d --wait` y se envía `POST http://localhost:5274/api/auth/login` con el usuario de demostración `luis.empleado@example.test` y esa contraseña
- ENTONCES `postgres` queda `healthy`, `api` arranca después, las migraciones se aplican, la siembra crea los usuarios y el login responde `200` con un token (verificación manual registrada en `verify-report.md`)

### Requirement: Flujo de CI

`.github/workflows/ci.yml` DEBE existir y ejecutar, en este orden: restore, `dotnet format --verify-no-changes`, `dotnet build -warnaserror`, `dotnet test` con cobertura (en un ejecutor con Docker, por Testcontainers), **comprobación de cobertura de Application ≥ 80 % con `scripts/check-coverage.ps1`**, auditoría de paquetes vulnerables, publish y build de imagen sin push. La CI NO se ejecuta hasta que exista un remoto; sus pasos DEBEN reproducirse localmente y anotarse.

#### Scenario: Pasos presentes
- DADO `ci.yml`
- CUANDO se analiza como YAML
- ENTONCES es válido y contiene los pasos citados en ese orden, sin `push` de imagen

#### Scenario: Paso de cobertura de Application
- DADO `ci.yml`
- CUANDO se busca el paso de cobertura
- ENTONCES invoca `check-coverage.ps1` con el paquete `TimeTracker.Application` y `-MinimumPercent 80`, después de `dotnet test` y antes de la auditoría

### Requirement: ADR y README

`docs/adr/` DEBE contener los ADR 0001 a 0012: los diez previos (formato, net10.0, monolito modular y `.sln`, minimal APIs y OpenAPI, WASM y CORS, pila de pruebas, configuración y validación, 0008 persistencia, 0009 identidad provisional y 0010 tiempo) y, nuevos, 0011 «Autenticación con JWT propio, roles y equipo» y 0012 «Sesión en la Web y riesgo XSS»; cada uno con contexto, decisión, consecuencias y alternativas. El ADR 0009 DEBE figurar con el estado «Sustituido por 0011» y el 0011 DEBE declarar que lo sustituye. El 0011 DEBE registrar la ausencia de refresh token (desviación consciente de AGENTS §6) y la ausencia de FK de `time_entries.employee_id` a `users`, ambas con su disparador de revisión; el 0012 DEBE registrar el uso de `sessionStorage` y el riesgo XSS aceptado. `README.md` DEBE describir el proyecto, la pila, cómo compilar, probar (con la necesidad de Docker) y ejecutar (incluido `docker compose up`, las rutas `/login`, `/fichajes` y `/equipo` y el puerto `5274`), la estructura, los usuarios de prueba inventados (dominio `example.test`) con la contraseña de demostración (que figura solo en el README, nunca en `.env.example`, y que se copia a `Seed__DemoPassword` en el `.env` local para usar la siembra), el aviso de no activar la siembra en un despliegue real, el comando para generar `Jwt__SigningKey`, el origen de la Web (`Cors__AllowedOrigins__0=http://localhost:5172`) y que `docker compose down -v` borra los fichajes de desarrollo anteriores. `README.md` NO DEBE mencionar `Identity__DevelopmentEmployeeId`. `openspec/config.yaml` DEBE actualizarse con el estado de la autenticación y sin la nota de identidad provisional.

#### Scenario: ADR completos
- DADO `docs/adr/`
- CUANDO se listan los archivos
- ENTONCES existen trece ADR numerados 0001 a 0013, cada uno con las cuatro secciones

#### Scenario: ADR 0009 sustituido
- DADO `docs/adr/0009*` y `docs/adr/0011*`
- CUANDO se lee el estado de cada uno
- ENTONCES el 0009 indica «Sustituido por 0011» y el 0011 indica que sustituye al 0009

#### Scenario: Contenido de los ADR nuevos
- DADO los ADR 0011 y 0012
- CUANDO se revisan sus consecuencias
- ENTONCES el 0011 menciona la ausencia de refresh token y de FK con su disparador, y el 0012 menciona `sessionStorage` y el riesgo XSS

#### Scenario: README ejecutable
- DADO `README.md`
- CUANDO una persona sigue sus comandos en una máquina limpia con el SDK y Docker
- ENTONCES `dotnet test` pasa, `docker compose up` arranca y puede iniciarse sesión con un usuario de prueba en `http://localhost:5274`

#### Scenario: README cubre la novedad
- DADO `README.md`
- CUANDO se revisa
- ENTONCES menciona `docker compose up`, las rutas `/login`, `/fichajes` y `/equipo`, que las pruebas de integración requieren Docker, las variables para ejecutar la API en local (`ASPNETCORE_ENVIRONMENT=Development`, `ConnectionStrings__Default`, `Jwt__SigningKey`, y `Seed__DemoUsers` y `Seed__DemoPassword` para la siembra), los cuatro correos de prueba con la contraseña de demostración, el aviso de la siembra y `docker compose down -v`

#### Scenario: README sin identidad provisional
- DADO `README.md`
- CUANDO se busca `Identity__DevelopmentEmployeeId`
- ENTONCES no aparece

### Requirement: Exportación del contrato OpenAPI sin base de datos ni identidad real

La generación de `docs/api/openapi.json` durante el build (`GetDocument.Insider`) DEBE funcionar sin base de datos accesible y sin variables de entorno del desarrollador: la rama de generación DEBE forzar el entorno `Development` y aportar valores ficticios para `Cors:AllowedOrigins`, `ConnectionStrings:Default` (servidor inexistente, sin contraseña real) y `Jwt:SigningKey` (ficticia, de al menos 32 bytes), y desactivar la siembra (`Seed:DemoUsers` = `false`), de modo que `ValidateOnStart` no falle y no se ejecute ninguna siembra. Estos valores NO DEBEN estar en `appsettings*.json` ni afectar a la ejecución normal.

#### Scenario: Contrato generado en build
- DADO un entorno sin `ConnectionStrings__Default`, `Jwt__SigningKey` ni `ASPNETCORE_ENVIRONMENT=Development`
- CUANDO se ejecuta `dotnet build` de `TimeTracker.Api`
- ENTONCES el build termina con `0` y `docs/api/openapi.json` contiene los tres endpoints de fichajes, `/api/auth/login`, `/api/auth/me`, `/api/team/members` y `/api/team/members/{memberId}/time-entries`

#### Scenario: Sin efecto en la ejecución normal
- DADO la API arrancada fuera de la generación del contrato, en `Production` y sin `Jwt__SigningKey`
- CUANDO intenta arrancar
- ENTONCES falla por la validación de configuración con un mensaje que contiene `Jwt:SigningKey` (los valores ficticios no se aplican)

### Requirement: Paquetes de autenticación acotados por proyecto

`Microsoft.AspNetCore.Authentication.JwtBearer` (10.0.10; respaldo 10.0.12 si aparece NU1903) DEBE declararse solo en `TimeTracker.Api` y `Microsoft.AspNetCore.Components.Authorization` (10.0.10; respaldo 10.0.12) solo en `TimeTracker.Web`, ambos con versión fija en `Directory.Packages.props`. `Domain`, `Application` e `Infrastructure` NO DEBEN incorporar paquetes nuevos por este cambio. La solución NO DEBE depender de ASP.NET Core Identity (paquetes `Microsoft.AspNetCore.Identity.*`).

#### Scenario: Paquetes en su proyecto
- DADO los `.csproj` de `src/`
- CUANDO se buscan `PackageReference` de `JwtBearer` y `Components.Authorization`
- ENTONCES el primero solo aparece en `TimeTracker.Api`, el segundo solo en `TimeTracker.Web`, y ninguno lleva atributo `Version`

#### Scenario: Application sigue sin paquetes
- DADO `TimeTracker.Application.csproj`
- CUANDO se ejecuta `dotnet list TimeTracker.Application package`
- ENTONCES no lista ningún paquete directo

#### Scenario: Sin ASP.NET Core Identity
- DADO `Directory.Packages.props` y los `.csproj`
- CUANDO se buscan paquetes `Microsoft.AspNetCore.Identity`
- ENTONCES no aparece ninguno

#### Scenario: Sin vulnerabilidades
- DADO el cambio aplicado
- CUANDO se ejecuta `dotnet list TimeTracker.sln package --vulnerable --include-transitive`
- ENTONCES informa de que no hay paquetes vulnerables

### Requirement: Sin restos de la identidad provisional

El código fuente (`src/`) y las pruebas (`tests/`, salvo la prueba que comprueba precisamente esta ausencia) NO DEBEN contener `DevelopmentCurrentUser`, `ProvisionalIdentity` ni `Identity:DevelopmentEmployeeId`. `docker-compose.yml`, `.env.example`, `README.md` y `openspec/config.yaml` NO DEBEN contener `Identity__DevelopmentEmployeeId`. Se exceptúan el ADR 0009, que describe la decisión sustituida, y el ADR 0011, que la cita como antecedente, y el historial archivado de `openspec/`.

#### Scenario: Búsqueda sin coincidencias
- DADO `src/`, los archivos de `tests/` salvo la prueba de esta ausencia, `docker-compose.yml`, `.env.example`, `README.md` y `openspec/config.yaml`
- CUANDO se buscan los textos indicados para cada ubicación
- ENTONCES no hay ninguna coincidencia

#### Scenario: Clases eliminadas
- DADO `src/TimeTracker.Api`
- CUANDO se listan sus tipos
- ENTONCES no existen el adaptador de desarrollo de `ICurrentUser`, sus opciones, su validador ni su extensión, y `HttpContextCurrentUser` sí existe
