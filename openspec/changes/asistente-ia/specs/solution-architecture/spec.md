# Delta para solution-architecture

Cambio: `asistente-ia`. Acota los paquetes de IA a `TimeTracker.Infrastructure`, añade el proyecto de pruebas `TimeTracker.Infrastructure.Tests`, mantiene la regla de dependencias de Clean Architecture, incorpora `POST /api/asistente` al contrato exportado y eleva los ADR a diecisiete (nuevo 0017).

> Aviso de delta: el `Purpose` vigente (13 ADR; lista de proyectos de pruebas sin `Infrastructure.Tests`; «IA» como fuera de alcance) queda obsoleto y debe actualizarse al archivar. El ADR 0017 documenta el tratamiento de datos personales con el proveedor de IA en la nube (RGPD) y la ubicación de la IA en Infrastructure.

Notas de verificación:
- Las versiones de `Microsoft.Extensions.AI` (10.10.0), `Anthropic` (12.58.0) y `OllamaSharp` (5.5.0) provienen de la exploración; el diseño puede subir una versión con motivo documentado si aparece NU1903 con `-warnaserror`.
- `DependencyRuleTests` ya comprueba que Application solo referencia Domain y el marco base; sigue siendo la barrera automática.
- El diseño (DD-1) mueve `UseRateLimiter` tras `UseAuthorization`; ese cambio de orden del pipeline y el rol en `ICurrentUser` (DD-3) se especifican en el delta de `authentication`, no aquí.

## ADDED Requirements

### Requirement: Paquetes de IA acotados a Infrastructure

`Microsoft.Extensions.AI`, `Anthropic` y `OllamaSharp` DEBEN declararse solo en `TimeTracker.Infrastructure`, con versión fija en `Directory.Packages.props`. `Microsoft.Extensions.AI.Abstractions` PUEDE declararse además en proyectos de pruebas que construyen un `IChatClient` falso. `Domain`, `Application` y `Web` NO DEBEN declarar ningún paquete de IA ni referenciar sus ensamblados. `TimeTracker.Api` solo compone las dependencias (composition root) y NO DEBE declarar paquetes de IA por sí mismo.

#### Scenario: Paquetes en su proyecto
- DADO los `.csproj` de `src/`
- CUANDO se buscan `PackageReference` de `Microsoft.Extensions.AI`, `Anthropic` y `OllamaSharp`
- ENTONCES solo aparecen en `TimeTracker.Infrastructure` y ninguno lleva atributo `Version`

#### Scenario: Application, Domain y Web sin IA
- DADO `TimeTracker.Domain.csproj`, `TimeTracker.Application.csproj` y `TimeTracker.Web.csproj`
- CUANDO se ejecuta `dotnet list <proyecto> package`
- ENTONCES ninguno lista un paquete de IA y Application sigue sin ningún paquete directo

#### Scenario: Application sin ensamblados de IA
- DADO `DependencyRuleTests`
- CUANDO se ejecutan
- ENTONCES pasan: Application solo referencia Domain y ensamblados `System*` o `netstandard`

#### Scenario: Sin vulnerabilidades
- DADO el cambio aplicado
- CUANDO se ejecuta `dotnet list TimeTracker.sln package --vulnerable --include-transitive`
- ENTONCES informa de que no hay paquetes vulnerables y `dotnet build -warnaserror` no falla por NU1903

### Requirement: Proyecto de pruebas de Infrastructure sin Docker

La solución DEBE incluir `tests/TimeTracker.Infrastructure.Tests` (xUnit, `net10.0`) con una implementación falsa de `IChatClient`. Sus pruebas NO DEBEN requerir Docker, red ni un modelo real, y DEBEN cubrir la seguridad por herramienta (empleado solo ve lo suyo; herramientas de equipo no ofrecidas a un empleado; responsable solo ve reportes directos; ajeno, inexistente y propio dan la misma respuesta), el tope de iteraciones, los límites y los fallos del proveedor sin filtrar detalle. Las pruebas con un modelo real DEBEN estar separadas, ser opcionales y estar desactivadas por defecto.

#### Scenario: Proyecto en la solución
- DADO `TimeTracker.sln`
- CUANDO se ejecuta `dotnet sln TimeTracker.sln list`
- ENTONCES aparece `tests/TimeTracker.Infrastructure.Tests`

#### Scenario: Ejecución sin Docker
- DADO una máquina sin Docker
- CUANDO se ejecuta `dotnet test` sobre `TimeTracker.Infrastructure.Tests`
- ENTONCES las pruebas pasan sin red

#### Scenario: Sin modelo real por defecto
- DADO `dotnet test` sobre toda la solución sin variables de proveedor
- CUANDO termina
- ENTONCES ninguna prueba contacta con un proveedor de IA

#### Scenario: Seguridad por herramienta cubierta
- DADO las pruebas del proyecto
- CUANDO se listan
- ENTONCES existen pruebas para cada uno de los cuatro comportamientos de seguridad indicados

## MODIFIED Requirements

### Requirement: Estructura de proyectos y destino de plataforma

La solución DEBE ser `TimeTracker.sln` en formato clásico e incluir en `src/` `TimeTracker.Domain`, `TimeTracker.Application`, `TimeTracker.Infrastructure`, `TimeTracker.Api` y `TimeTracker.Web`, y en `tests/` `TimeTracker.Domain.Tests`, `TimeTracker.Application.Tests`, `TimeTracker.Infrastructure.Tests`, `TimeTracker.Api.IntegrationTests`, `TimeTracker.ArchitectureTests` y las pruebas de la Web que ya existan. Todos los proyectos DEBEN apuntar a `net10.0`. `global.json` DEBE fijar el SDK `10.0.302` con `rollForward: latestFeature`; NO DEBE seleccionar un ejecutor de pruebas distinto del predeterminado del SDK (VSTest).
(Previously: `tests/` no incluía `TimeTracker.Infrastructure.Tests`)

#### Scenario: Contenido de la solución
- DADO el repositorio con el cambio aplicado
- CUANDO se ejecuta `dotnet sln TimeTracker.sln list`
- ENTONCES aparecen los proyectos indicados, incluido `TimeTracker.Infrastructure.Tests`, y no existe `TimeTracker.slnx`

#### Scenario: Destino de plataforma
- DADO cada `.csproj`
- CUANDO se evalúa `TargetFramework`
- ENTONCES su valor es `net10.0`

### Requirement: Regla de dependencias por referencias de proyecto

Las referencias de proyecto DEBEN ser: Domain sin referencias; Application solo Domain; Infrastructure solo Application (y Domain por transitividad); Api solo Application e Infrastructure (composition root); Web ninguna referencia a los demás proyectos de `src/`, comunicándose con la API solo por HTTP; `TimeTracker.Infrastructure.Tests` solo Infrastructure, Application y Domain. `Domain` y `Application` NO DEBEN tener dependencias de paquetes externos (Infrastructure incorpora EF Core, Npgsql y los paquetes de IA; las capas internas no). El asistente DEBE depender de los casos de uso y puertos existentes de Application; NO DEBE acceder a la base de datos directamente ni duplicar la autorización de los casos de uso.
(Previously: Infrastructure «incorpora EF Core y Npgsql»; sin proyecto de pruebas de Infrastructure ni mención del asistente)

#### Scenario: Referencias esperadas
- DADO cada proyecto de `src/` y `TimeTracker.Infrastructure.Tests`
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

#### Scenario: El asistente no usa la base de datos directamente
- DADO el código del asistente en `TimeTracker.Infrastructure`
- CUANDO se revisan sus dependencias
- ENTONCES obtiene datos solo a través de los casos de uso de Application y no referencia el contexto de EF Core

### Requirement: Exportación del contrato OpenAPI sin base de datos ni identidad real

La generación de `docs/api/openapi.json` durante el build (`GetDocument.Insider`) DEBE funcionar sin base de datos accesible y sin variables de entorno del desarrollador: la rama de generación DEBE forzar el entorno `Development` y aportar valores ficticios para `Cors:AllowedOrigins`, `ConnectionStrings:Default` (servidor inexistente, sin contraseña real) y `Jwt:SigningKey` (ficticia, de al menos 32 bytes), y desactivar la siembra (`Seed:DemoUsers` = `false`), de modo que `ValidateOnStart` no falle y no se ejecute ninguna siembra. El contrato DEBE generarse con el asistente deshabilitado (sin `Ai:Provider`) y sin ninguna clave de IA, sin contactar con proveedor alguno. Estos valores NO DEBEN estar en `appsettings*.json` ni afectar a la ejecución normal.
(Previously: el contrato exportado no incluía `/api/asistente` ni se precisaba que se generaba sin configuración de IA)

#### Scenario: Contrato generado en build
- DADO un entorno sin `ConnectionStrings__Default`, `Jwt__SigningKey`, `Ai__*` ni `ASPNETCORE_ENVIRONMENT=Development`
- CUANDO se ejecuta `dotnet build` de `TimeTracker.Api`
- ENTONCES el build termina con `0` y `docs/api/openapi.json` contiene los tres endpoints de fichajes, `/api/auth/login`, `/api/auth/me`, `/api/team/members`, `/api/team/members/{memberId}/time-entries` y `/api/asistente`

#### Scenario: Sin efecto en la ejecución normal
- DADO la API arrancada fuera de la generación del contrato, en `Production` y sin `Jwt__SigningKey`
- CUANDO intenta arrancar
- ENTONCES falla por la validación de configuración con un mensaje que contiene `Jwt:SigningKey` (los valores ficticios no se aplican)

#### Scenario: Prueba de contrato
- DADO `OpenApiContractTests`
- CUANDO se ejecutan con el endpoint añadido
- ENTONCES pasan y el documento versionado coincide con el generado

### Requirement: ADR y README

`docs/adr/` DEBE contener los ADR 0001 a 0017: los previos vigentes y, nuevo, 0017 sobre el asistente de IA. El ADR 0017 DEBE registrar con contexto, decisión, consecuencias y alternativas: la IA en Infrastructure con Application sin SDK de IA (alternativa A) y las alternativas descartadas; que las herramientas son de solo lectura y delegan la autorización en los casos de uso; el tratamiento de datos personales (nombres y horas de fichajes) enviados al proveedor en la nube (RGPD), la minimización y el uso de datos inventados en la demostración; los límites de consumo (limitador por minuto y por usuario tras la autorización, tope diario por usuario en memoria con su limitación: se reinicia con cada despliegue y no se comparte entre réplicas, y presupuesto en la consola del proveedor) y la ausencia de prompt y respuesta en los registros; el cambio de orden del pipeline respecto a la decisión archivada DD-21; y el comportamiento sin `Ai:Provider`. Los ADR no se reescriben. `README.md` DEBE describir además el asistente: qué hace, que es de solo lectura, cómo habilitarlo y deshabilitarlo con `Ai__Provider` (`Anthropic` en Railway con `Ai__ApiKey` como secreto; `Ollama` local con `Ai__Endpoint`), el modelo como configuración, la ruta `/asistente`, el aviso sobre datos personales y el proveedor en la nube, y que las pruebas del asistente no llaman a un modelo real. `README.md` NO DEBE contener ninguna clave de API real.
(Previously: ADR 0001 a 0012 en el texto vigente (el escenario ya citaba trece), sin mención del asistente; README sin sección de IA)

#### Scenario: ADR completos
- DADO `docs/adr/`
- CUANDO se listan los archivos
- ENTONCES existen diecisiete ADR numerados 0001 a 0017, cada uno con las cuatro secciones

#### Scenario: Contenido del ADR 0017
- DADO el ADR 0017
- CUANDO se revisan sus consecuencias y alternativas
- ENTONCES menciona los datos personales y el proveedor en la nube, la ubicación de la IA en Infrastructure, el modo solo lectura, los límites de consumo (incluida la limitación del tope diario en memoria), el nuevo orden del limitador y la alternativa B descartada

#### Scenario: ADR anteriores intactos
- DADO los ADR 0001 a 0016
- CUANDO se comparan con su versión anterior al cambio
- ENTONCES no han sido reescritos

#### Scenario: README cubre el asistente
- DADO `README.md`
- CUANDO se revisa
- ENTONCES explica cómo habilitar y deshabilitar el asistente con `Ai__Provider`, los dos proveedores, la ruta `/asistente` y el aviso de datos personales

#### Scenario: README sin secretos
- DADO `README.md`, `.env.example` y `docker-compose.yml`
- CUANDO se busca un valor de `Ai__ApiKey`
- ENTONCES no hay ninguno
