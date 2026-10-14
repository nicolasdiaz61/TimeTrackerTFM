# TimeTracker

Aplicación de control de tiempos y ausencias para empresas: fichajes, ausencias, saldo de horas y horas extra, con un asistente de IA que responderá consultas en lenguaje natural sobre esos datos.

> **Estado actual:** la aplicación tiene **autenticación real y roles**. Se inicia sesión en `/login` con un correo y una contraseña; un **empleado** ficha su entrada y salida en `/fichajes` y un **responsable** (rol `Manager`) además consulta los fichajes de sus reportes directos en `/equipo`. La API emite un JWT propio de 30 minutos (sin renovación) y guarda los usuarios y los fichajes en PostgreSQL con EF Core. La Web tiene una página de inicio en español con enlaces según la sesión y avisa en `/login` cuando la sesión ha caducado. Todavía **no** hay ausencias ni asistente de IA.
>
> **Aviso: usuarios de demostración.** La siembra de cuatro usuarios de prueba (contraseña pública, ver «Usuarios de prueba») está **desactivada por defecto**. No la active en un despliegue real. Ver los [ADR 0011](docs/adr/0011-autenticacion-jwt-roles-y-equipo.md) y [0012](docs/adr/0012-sesion-web-y-riesgo-xss.md). Todos los datos de ejemplo son inventados.

## Pila tecnológica

- .NET 10 (SDK 10.0.302 o superior de la banda 10.0, fijado en `global.json`), C#.
- API: ASP.NET Core con Minimal APIs y OpenAPI.
- Datos: PostgreSQL 18 con EF Core 10 (Npgsql) y migraciones versionadas ([ADR 0008](docs/adr/0008-persistencia-postgresql-ef-core.md)).
- Frontend: Blazor WebAssembly (proyecto independiente).
- Autenticación: JWT HS256 propio validado con `JwtBearer` y contraseñas con el hash PBKDF2 de `PasswordHasher` (sin ASP.NET Core Identity; [ADR 0011](docs/adr/0011-autenticacion-jwt-roles-y-equipo.md)).
- Pruebas: xUnit v2, `WebApplicationFactory`, Testcontainers (PostgreSQL real), Coverlet (cobertura). El motivo de xUnit v2 está en el [ADR 0006](docs/adr/0006-pila-de-pruebas.md).
- Contenedores: Docker y Docker Compose. Integración continua: GitHub Actions.
- Previsto en cambios futuros: ausencias y asistente de IA mediante `IChatClient` (Microsoft.Extensions.AI).

## Requisitos previos

- [SDK de .NET](https://dotnet.microsoft.com/download) 10.0.302 o superior (`dotnet --version`).
- [PowerShell 7](https://learn.microsoft.com/powershell/) (`pwsh`) para el script de cobertura y para generar la clave de firma (el comando de generación no funciona en Windows PowerShell 5.1).
- Docker Desktop o Docker Engine con Compose, en marcha: **las pruebas de integración requieren Docker** (levantan PostgreSQL con Testcontainers y fallan, no se omiten, si Docker no está disponible), y también hace falta para la base de datos local y para ejecutar el contenedor.

Los paquetes se restauran solo desde nuget.org (`nuget.config` del repositorio).

## Compilar

```powershell
dotnet restore TimeTracker.sln
dotnet tool restore
dotnet build TimeTracker.sln -c Release --no-restore -warnaserror
```

Las advertencias se tratan como errores. Al compilar `TimeTracker.Api` se regenera `docs/api/openapi.json`.

## Probar

Con Docker en marcha:

```powershell
dotnet test TimeTracker.sln -c Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults
pwsh scripts/check-coverage.ps1 -ResultsDirectory TestResults -Package TimeTracker.Domain -MinimumPercent 100
pwsh scripts/check-coverage.ps1 -ResultsDirectory TestResults -Package TimeTracker.Application -MinimumPercent 80
```

Los comandos de cobertura exigen 100 % en `TimeTracker.Domain` y al menos 80 % en `TimeTracker.Application`. Para comprobar el formato: `dotnet format TimeTracker.sln --verify-no-changes --no-restore`.

## Configuración

La configuración llega por variables de entorno (nunca en el repositorio). `.env.example` documenta las claves sin valores reales; para Docker Compose se copia a `.env` (que no se versiona). `.env.example` nunca lleva claves ni contraseñas: la clave de firma y la contraseña de demostración se escriben solo en su `.env` local.

| Variable | Obligatoria | Descripción |
|---|---|---|
| `Cors__AllowedOrigins__0` (y `__1`, ...) | Sí | Orígenes permitidos por CORS: URL absoluta `http`/`https` sin ruta ni barra final; no se admite `*`. Para la Web local: `http://localhost:5172`. |
| `ConnectionStrings__Default` | Sí | Cadena de conexión de PostgreSQL, por ejemplo `Host=localhost;Port=5432;Database=timetracker;Username=timetracker;Password=<su contraseña>`. |
| `Jwt__SigningKey` | Sí | Clave de firma de los tokens, de al menos 32 bytes. Si falta, la API no arranca. Véase «Generar la clave de firma». |
| `ASPNETCORE_ENVIRONMENT` | No | `Development` o `Production`; con `dotnet run --launch-profile http` ya viene fijado a `Development` y Compose lo fija igual. |
| `Seed__DemoUsers` | No (por defecto `false`) | Con `true`, la API siembra al arrancar los cuatro usuarios de prueba. **No lo active en un despliegue real.** |
| `Seed__DemoPassword` | Solo con `Seed__DemoUsers=true` | Contraseña de los usuarios de prueba (mínimo 12 caracteres). Use la de «Usuarios de prueba»; solo en local. |
| `Database__ApplyMigrationsOnStartup` | No (por defecto `false`) | Con `true`, la API aplica las migraciones al arrancar. |
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | Solo con Compose | Base de datos, usuario y contraseña (inventada) del contenedor de PostgreSQL. |
| `ASPNETCORE_HTTP_PORTS` | No | Puerto de escucha; la imagen Docker lo fija en `8080`. |

Si falta una clave obligatoria, la API termina al arrancar con un mensaje que nombra la clave (sin imprimir valores). `ASPNETCORE_URLS` y `launchSettings.json` tienen prioridad sobre `ASPNETCORE_HTTP_PORTS`.

### Generar la clave de firma

Con PowerShell 7 (`pwsh`; en Windows PowerShell 5.1 falla porque el método estático `GetBytes` no existe):

```powershell
pwsh -NoProfile -Command "[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))"
```

Copie el resultado en `Jwt__SigningKey` (en su `.env` o en una variable de la sesión). No lo comparta ni lo versione.

## Ejecutar en local (API y Web)

1. Cree `.env` con `Copy-Item .env.example .env` y rellene `POSTGRES_USER`, `POSTGRES_DB` y `POSTGRES_PASSWORD` con valores inventados (sin `;` ni `=`).
2. Levante solo la base de datos: `docker compose up -d postgres` (queda en `127.0.0.1:5432`). Compose exige también `Jwt__SigningKey` y `Seed__DemoPassword` en `.env` aunque solo levante la base de datos; si no quiere rellenarlos todavía, defínalos en la sesión con valores cualquiera y bórrelos después.
3. Arranque la API (perfil `http`, `Development`, `http://localhost:5274`) con las variables en esa sesión de PowerShell. Con la siembra activada se crean los usuarios de prueba (véase «Usuarios de prueba» para la contraseña):

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ConnectionStrings__Default = "Host=localhost;Port=5432;Database=<POSTGRES_DB>;Username=<POSTGRES_USER>;Password=<su contraseña>"
$env:Jwt__SigningKey = "<clave generada con el comando anterior>"
$env:Database__ApplyMigrationsOnStartup = "true"
$env:Cors__AllowedOrigins__0 = "http://localhost:5172"
$env:Seed__DemoUsers = "true"
$env:Seed__DemoPassword = "<la contraseña de demostración de la tabla de usuarios>"
dotnet run --project src/TimeTracker.Api --launch-profile http
```

Comprobación: `Invoke-WebRequest http://localhost:5274/health` responde `200` con `{"status":"Healthy"}`; esa ruta es anónima y no consulta la base de datos. Cualquier otra ruta de la API exige un token (`401` sin él).

4. En otra terminal, arranque la Web (`http://localhost:5172`):

```powershell
dotnet run --project src/TimeTracker.Web --launch-profile http
```

5. Abra `http://localhost:5172/login` e inicie sesión con un usuario de prueba. Un empleado va a `/fichajes`: pulse «Fichar entrada» y «Fichar salida»; la tabla muestra las horas en UTC, «en curso» para el fichaje abierto y el total de minutos. Un responsable ve además `/equipo`, con la lista de sus reportes directos y los fichajes de cada uno. La sesión caduca a los 30 minutos: la Web lo indica en `/login` («Su sesión ha caducado. Inicie sesión de nuevo.») y hay que volver a iniciarla. La Web toma la URL de la API de `ApiBaseUrl` en `src/TimeTracker.Web/wwwroot/appsettings.json` (por defecto `http://localhost:5274`); si falta o no es válida, la Web no arranca y lo indica.

Al terminar, borre las variables de la sesión (`Remove-Item Env:ASPNETCORE_ENVIRONMENT, Env:ConnectionStrings__Default, Env:Jwt__SigningKey, Env:Database__ApplyMigrationsOnStartup, Env:Cors__AllowedOrigins__0, Env:Seed__DemoUsers, Env:Seed__DemoPassword`) y pare la base de datos con `docker compose down`.

### Migraciones con `dotnet ef`

La herramienta `dotnet-ef` es local al repositorio (`.config/dotnet-tools.json`; `dotnet tool restore`). Para crear una migración tras cambiar el modelo:

```powershell
dotnet tool run dotnet-ef migrations add <Nombre> --project src/TimeTracker.Infrastructure --startup-project src/TimeTracker.Infrastructure --output-dir Persistence/Migrations
```

Las migraciones se aplican al arrancar la API con `Database__ApplyMigrationsOnStartup=true`.

## Docker y Docker Compose

```powershell
Copy-Item .env.example .env
# Edite .env y rellene: POSTGRES_USER, POSTGRES_DB y POSTGRES_PASSWORD (inventados),
# Cors__AllowedOrigins__0=http://localhost:5172,
# Jwt__SigningKey (clave generada, ver «Generar la clave de firma») y
# Seed__DemoPassword (la contraseña de demostración de «Usuarios de prueba»).
docker compose up --build
```

Compose levanta PostgreSQL 18 y la API (en `Development`, con migraciones automáticas, la siembra de usuarios de prueba activada y la cadena de conexión construida a partir de `POSTGRES_*`). Si falta `Jwt__SigningKey` o `Seed__DemoPassword`, Compose se niega a arrancar y lo indica. La API queda en `http://localhost:5274` (compruebe `http://localhost:5274/health`), y ambos puertos (5274 y 5432) se publican **solo en `127.0.0.1`**. Compose es únicamente para desarrollo, por la contraseña de demostración pública. La Web no forma parte de Compose: se ejecuta con `dotnet run --project src/TimeTracker.Web --launch-profile http` y su `ApiBaseUrl` ya apunta a `http://localhost:5274`, así que funciona contra el contenedor sin cambios (con `Cors__AllowedOrigins__0=http://localhost:5172`). No ejecute a la vez la API local y la de Compose: ambas usan el puerto 5274.

**Datos antiguos:** los fichajes anteriores a este cambio pertenecían a una identidad provisional que ya no existe y quedan huérfanos (sin usuario). Si ya tenía un volumen de PostgreSQL de antes, `docker compose down -v` borra esos fichajes de desarrollo (y todos los datos del volumen) para empezar limpio.

Sin Compose, la imagen de la API necesita las mismas claves:

```powershell
docker build -t timetracker-api .
```

La imagen se ejecuta con un usuario no root, escucha en HTTP (el TLS se termina en el ingreso, [ADR 0007](docs/adr/0007-configuracion-y-validacion-al-arrancar.md)) e incluye un `HEALTHCHECK`. No contiene secretos: las claves (entre ellas `Jwt__SigningKey`) se pasan al ejecutarla.

## Usuarios de prueba

Cuatro usuarios inventados (dominio `example.test`) que crea la siembra de demostración cuando `Seed__DemoUsers` es `true` (Compose la activa). Todos usan la misma contraseña de demostración, **inventada y pública**, que debe escribir también en `Seed__DemoPassword` de su `.env` local:

| Correo | Nombre | Rol | Contraseña |
|---|---|---|---|
| `marta.responsable@example.test` | Marta Ejemplo | `Manager` (responsable de Ana y Luis) | `Demo-Fichajes-2026` |
| `luis.empleado@example.test` | Luis Ejemplo | `Employee` (equipo de Marta) | `Demo-Fichajes-2026` |
| `ana.empleada@example.test` | Ana Ejemplo | `Employee` (equipo de Marta) | `Demo-Fichajes-2026` |
| `pablo.empleado@example.test` | Pablo Ejemplo | `Employee` (sin responsable) | `Demo-Fichajes-2026` |

**No active la siembra (`Seed__DemoUsers=true`) en un despliegue real:** la contraseña es pública y cualquiera podría iniciar sesión. Por eso está desactivada por defecto y `.env.example` no la contiene.

## Estructura

```
src/
  TimeTracker.Domain          Entidades y objetos de valor (TimeEntry, WorkedHours, UserAccount). Sin dependencias.
  TimeTracker.Application     Casos de uso (fichar, listar, iniciar sesión, equipo) y puertos. Depende de Domain.
  TimeTracker.Infrastructure  EF Core, migraciones y repositorios PostgreSQL. Depende de Application.
  TimeTracker.Api             Endpoints, autenticación JWT, autorización y composición. Depende de Application e Infrastructure.
  TimeTracker.Web             Blazor WebAssembly (inicio, /login, /fichajes y /equipo), independiente.
tests/                        Pruebas de Domain, Application, Web (formato y estado de sesión), Api (integración con PostgreSQL) y de arquitectura.
docs/adr/                     Registros de decisiones de arquitectura.
docs/api/openapi.json         Contrato OpenAPI generado (no se edita a mano).
scripts/check-coverage.ps1    Comprobación del umbral de cobertura.
docker-compose.yml            Entorno local: API y PostgreSQL.
.github/workflows/ci.yml      Integración continua.
openspec/                     Especificaciones y cambios (Spec Driven Development).
```

## Decisiones de arquitectura (ADR)

1. [Formato de ADR](docs/adr/0001-formato-de-adr.md)
2. [Plataforma .NET 10](docs/adr/0002-net10.md)
3. [Monolito modular y regla de dependencias](docs/adr/0003-monolito-modular-y-regla-de-dependencias.md)
4. [Minimal APIs y OpenAPI](docs/adr/0004-minimal-apis-y-openapi.md)
5. [Frontend WebAssembly y CORS](docs/adr/0005-frontend-wasm-y-cors.md)
6. [Pila de pruebas](docs/adr/0006-pila-de-pruebas.md)
7. [Configuración y validación al arrancar](docs/adr/0007-configuracion-y-validacion-al-arrancar.md)
8. [Persistencia con PostgreSQL y EF Core](docs/adr/0008-persistencia-postgresql-ef-core.md)
9. [Identidad provisional](docs/adr/0009-identidad-provisional.md) (sustituido por el 0011)
10. [Tiempo en UTC y minutos truncados](docs/adr/0010-tiempo-utc-y-minutos.md)
11. [Autenticación con JWT propio, roles y equipo](docs/adr/0011-autenticacion-jwt-roles-y-equipo.md)
12. [Sesión en la Web: token en sessionStorage y riesgo XSS](docs/adr/0012-sesion-web-y-riesgo-xss.md)

## Integración continua

El flujo `.github/workflows/ci.yml` se ejecuta en GitHub Actions en cada `push` a `main` y en cada pull request: restaura, comprueba el formato, compila, prueba con cobertura (Domain al 100 % y Application al 80 %), audita paquetes vulnerables, publica y construye la imagen (sin subirla). Sus pasos pueden reproducirse en local con los comandos de «Compilar» y «Probar».

## Enlaces y datos de demostración

- Despliegue: **pendiente** (aún no hay URL publicada).
- Vídeo de demostración: **pendiente**.
- Presentación (slides): **pendiente**.
- Usuarios de prueba: véase «Usuarios de prueba» (solo para uso local, con la siembra activada).

## Convenciones

Las reglas de trabajo (SDD, TDD, arquitectura, seguridad) están en [AGENTS.md](AGENTS.md). Documentación y textos de usuario en español; identificadores de código en inglés.
