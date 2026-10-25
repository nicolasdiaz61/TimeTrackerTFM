# TimeTracker

## Descripción general

TimeTracker es una aplicación de control de tiempos para empresas: cada persona ficha su jornada (inicio, pausa, comida y fin), consulta su historial por mes, y un responsable ve el de su equipo. Un asistente de IA de solo lectura responde en lenguaje natural a preguntas sobre esos fichajes («¿cuántas horas trabajé la semana pasada?»). Está pensada para equipos de trabajo y sus responsables, y una misma instancia sirve a varias compañías aisladas entre sí. Es el proyecto de fin de máster de su autor; todos los datos de ejemplo son inventados.

Demostración en línea: <https://web-production-a744a.up.railway.app> (véase «Enlaces» para la API y el resto de materiales).

> **Estado actual.** La aplicación es **multicompañía**: cada compañía queda aislada por `company_id` en una base de datos compartida ([ADR 0020](docs/adr/0020-aislamiento-multiempresa.md)). Una compañía nueva se da de alta con un **código de invitación** (`/registro`; cerrado por defecto en `Production` y abierto en `Development`) y su primer responsable da de alta al resto de personas con una contraseña temporal. La autenticación es real, con roles `Employee` y `Manager` (JWT propio de 30 minutos, sin renovación). Cualquier persona ficha con el **reproductor del Inicio** y consulta su historial **por mes** en `/fichajes`; un responsable consulta el de sus reportes directos en `/equipo` y define las **jornadas** de trabajo en `/jornadas`. El **asistente de IA** (`POST /api/asistente`, de solo lectura y deshabilitado por defecto) funciona con los proveedores Anthropic, OpenAI, Gemini, Groq u Ollama, y el responsable de cada compañía configura el suyo en `/asistente/configuracion`. Las **ausencias** todavía no existen.
>
> **Aviso: siembra de demostración.** La siembra de usuarios de prueba está **desactivada por defecto** (solo la activa `src/TimeTracker.Api/appsettings.Development.json`, para el entorno local). No la active en un despliegue real. Ver los [ADR 0011](docs/adr/0011-autenticacion-jwt-roles-y-equipo.md) y [0012](docs/adr/0012-sesion-web-y-riesgo-xss.md).

## Stack tecnológico

- .NET 10 (SDK 10.0.302 o superior de la banda 10.0, fijado en `global.json`), C#.
- API: ASP.NET Core con Minimal APIs y OpenAPI.
- Datos: PostgreSQL 18 con EF Core 10 (Npgsql) y migraciones versionadas ([ADR 0008](docs/adr/0008-persistencia-postgresql-ef-core.md)).
- Frontend: Blazor WebAssembly (proyecto independiente).
- Autenticación: JWT HS256 propio validado con `JwtBearer` y contraseñas con el hash PBKDF2 de `PasswordHasher` (sin ASP.NET Core Identity; [ADR 0011](docs/adr/0011-autenticacion-jwt-roles-y-equipo.md)).
- Pruebas: xUnit v2, `WebApplicationFactory`, Testcontainers (PostgreSQL real), Coverlet (cobertura). El motivo de xUnit v2 está en el [ADR 0006](docs/adr/0006-pila-de-pruebas.md).
- Contenedores: Docker y Docker Compose. Integración continua: GitHub Actions.
- Asistente de IA: `IChatClient` (Microsoft.Extensions.AI) con Anthropic, OpenAI, Gemini o Groq (nube) u Ollama (local) elegidos por configuración ([ADR 0017](docs/adr/0017-asistente-ia-con-herramientas-de-solo-lectura.md); Groq en el [ADR 0023](docs/adr/0023-proveedor-groq.md)).
- Previsto en cambios futuros: ausencias.

## Instalación y ejecución

### Requisitos previos

- [SDK de .NET](https://dotnet.microsoft.com/download) 10.0.302 o superior (`dotnet --version`).
- [PowerShell 7](https://learn.microsoft.com/powershell/) (`pwsh`) para el script de cobertura y para generar una clave de firma propia en un despliegue (el comando de generación no funciona en Windows PowerShell 5.1). Para ejecutar en local no hace falta.
- Docker Desktop o Docker Engine con Compose, en marcha: **las pruebas de integración requieren Docker** (levantan PostgreSQL con Testcontainers y fallan, no se omiten, si Docker no está disponible), y también hace falta para la base de datos local y para ejecutar el contenedor.

Los paquetes se restauran solo desde nuget.org (`nuget.config` del repositorio).

### Compilar

```powershell
dotnet restore TimeTracker.sln
dotnet tool restore
dotnet build TimeTracker.sln -c Release --no-restore -warnaserror
```

Las advertencias se tratan como errores. Al compilar `TimeTracker.Api` se regenera `docs/api/openapi.json`.

### Probar

Con Docker en marcha:

```powershell
dotnet test TimeTracker.sln -c Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults
pwsh scripts/check-coverage.ps1 -ResultsDirectory TestResults -Package TimeTracker.Domain -MinimumPercent 100
pwsh scripts/check-coverage.ps1 -ResultsDirectory TestResults -Package TimeTracker.Application -MinimumPercent 80
```

Los comandos de cobertura exigen 100 % en `TimeTracker.Domain` y al menos 80 % en `TimeTracker.Application`. Para comprobar el formato: `dotnet format TimeTracker.sln --verify-no-changes --no-restore`.

### Configuración

En despliegue (`Production`) la configuración llega por variables de entorno y nunca está en el repositorio. `.env.example` documenta las claves sin valores reales; un `.env` (que no se versiona) es opcional y solo sirve para sobrescribir la configuración de desarrollo con Compose. `.env.example` nunca lleva claves ni contraseñas.

En `Development` la API arranca sin variables de entorno: `src/TimeTracker.Api/appsettings.Development.json` trae los valores de un entorno local desechable (origen de la Web en `http://localhost:5172`, PostgreSQL en `localhost:5432`, migraciones y siembra de demostración activadas, clave de firma de desarrollo, registro de compañías abierto con un código de invitación de desarrollo que convive con la demostración, [ADR 0033](docs/adr/0033-registro-y-demostracion-en-desarrollo.md)). Solo se carga con `ASPNETCORE_ENVIRONMENT=Development` (Railway usa `Production`), y `appsettings.json` base sigue sin ningún valor de entorno. **Las variables de entorno tienen prioridad sobre el JSON**, de modo que cualquiera de las claves de la tabla se puede sobrescribir en la sesión. Fuera de `Development`, la API no arranca si la clave de firma es la de desarrollo. Decisión en el [ADR 0032](docs/adr/0032-configuracion-de-desarrollo-en-appsettings.md).

La cadena de conexión de desarrollo apunta a la base de datos `timetracker` con el usuario `timetracker` y la contraseña de desarrollo que figura en el JSON. Compose usa esos mismos valores como defecto de `POSTGRES_DB`, `POSTGRES_USER` y `POSTGRES_PASSWORD` (una prueba comprueba que coinciden), así que **no hace falta un `.env`**: si existe, es opcional y sobrescribe la configuración de desarrollo. Si cambia `POSTGRES_*` en él, sobrescriba también `ConnectionStrings__Default` para `dotnet run`. `.env.example` documenta las claves; casi todas están comentadas, pero `Registration__Enabled=false`, `Ai__Provider=` y `Ai__ApiKey=` figuran activas, así que copiarlo entero a `.env` sobrescribiría esos valores del JSON: deje en su `.env` solo lo que quiera cambiar.

| Variable | Obligatoria | Descripción |
|---|---|---|
| `Cors__AllowedOrigins__0` (y `__1`, ...) | En `Production` (en `Development` salen del JSON) | Orígenes permitidos por CORS: URL absoluta `http`/`https` sin ruta ni barra final; no se admite `*`. Para la Web local: `http://localhost:5172`. |
| `ConnectionStrings__Default` | En `Production` (en `Development` salen del JSON) | Cadena de conexión de PostgreSQL, por ejemplo `Host=localhost;Port=5432;Database=timetracker;Username=timetracker;Password=<su contraseña>`. |
| `Jwt__SigningKey` | En `Production` (en `Development` salen del JSON) | Clave de firma de los tokens, de al menos 32 bytes. Si falta, la API no arranca. Véase «Generar la clave de firma». |
| `ASPNETCORE_ENVIRONMENT` | No | `Development` o `Production`; con `dotnet run --launch-profile http` ya viene fijado a `Development` y Compose lo fija igual. |
| `Seed__DemoUsers` | No (por defecto `false`; `appsettings.Development.json` lo pone en `true`) | Con `true`, la API siembra al arrancar los usuarios de demostración. **No lo active en un despliegue real.** |
| `Seed__DemoPassword` | Solo con `Seed__DemoUsers=true` (en `Development` sale del JSON) | Contraseña de los usuarios de demostración (mínimo 12 caracteres). En local es la de `appsettings.Development.json` (o la que usted sobrescriba), y solo en local. |
| `Seed__DemoData` | No (por defecto `false`; `appsettings.Development.json` lo pone en `true`; exige `Seed__DemoUsers=true`) | Con `true`, la API siembra al arrancar un mes de fichajes inventados para los usuarios de demostración (véase «Datos de demostración»). Con `Seed__DemoUsers` en `false` o sin definir, la API no arranca. |
| `Database__ApplyMigrationsOnStartup` | No (por defecto `false`; `appsettings.Development.json` y Compose lo ponen en `true`) | Con `true`, la API aplica las migraciones al arrancar. |
| `Registration__Enabled` | No (por defecto `false`; `appsettings.Development.json` lo pone en `true`) | Con `true`, la API expone `POST /api/auth/register-company` (véase «Compañías, registro y alta de personas»). |
| `Registration__InviteCodes` | Con `Registration__Enabled=true` (en `Development` sale del JSON) | **Secreto.** Códigos de invitación separados por comas (16 a 128 caracteres cada uno). Fuera de `Development` no puede incluir el de desarrollo. |
| `Registration__GlobalHourlyLimit` | No (por defecto `30`) | Tope global de altas de compañía por hora, de 1 a 1000. |
| `Company__MaxEmployees` | No (por defecto `50`) | Máximo de personas por compañía, de 1 a 1000. |
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | No (Compose trae valores por defecto de desarrollo) | Base de datos, usuario y contraseña (inventada) del contenedor de PostgreSQL. |
| `ASPNETCORE_HTTP_PORTS` | No | Puerto de escucha; la imagen Docker lo fija en `8080`. |
| `PORT` | No | Si la plataforma lo define (Railway), la imagen de la API escucha ahí y prevalece sobre `ASPNETCORE_HTTP_PORTS`. |
| `ForwardedHeaders__Enabled` | No (por defecto `false`) | Con `true`, la API confía en `X-Forwarded-For`/`X-Forwarded-Proto` de un proxy inverso. Solo tras un proxy que la proteja de accesos directos (véase «Despliegue en Railway»). |
| `ForwardedHeaders__Hops` | No (por defecto `2`) | Saltos de proxy de confianza, contados desde la derecha de `X-Forwarded-For` (Railway: «cliente, borde»). |
| `Ai__Provider` | No (vacío = asistente deshabilitado) | Proveedor de la compañía por defecto: `Anthropic`, `OpenAI`, `Gemini`, `Groq` u `Ollama` (sin distinguir mayúsculas; `None` o vacío lo deshabilita). Cualquier otro valor, incluido un número, impide arrancar. Véase «Asistente de IA». |
| `Ai__Model` | Con proveedor | Modelo de la compañía por defecto (máximo 100 caracteres); es configuración, nunca código. |
| `Ai__ApiKey` | Con `Anthropic`, `OpenAI`, `Gemini` o `Groq` | Clave de API de la compañía por defecto. **Es un secreto**: solo en variables de entorno o en el secreto de la plataforma; nunca en archivos del repositorio. |
| `Ai__Endpoint` | Con `Ollama` (de cualquier compañía) | URL `http`/`https` del servidor de Ollama, por ejemplo `http://host.docker.internal:11434/`. Solo se lee del entorno, nunca de lo que guarda el responsable. |
| `Ai__MaxOutputTokens`, `Ai__MaxToolIterations`, `Ai__TimeoutSeconds` | No (800, 5 y 25) | Tokens de salida (64-4096), iteraciones de herramientas (1-10) y tiempo máximo en segundos (5-55). |
| `Ai__RequestsPerMinute`, `Ai__DailyRequestsPerUser` | No (5 y 50) | Peticiones por minuto (1-60) y por día natural de España (1-1000) por usuario. |

`Ai__Provider`, `Ai__Model` y `Ai__ApiKey` son valores **de plataforma**: solo los usa la compañía por defecto (`Demo`). Las compañías que se registran empiezan con el asistente deshabilitado y su responsable lo configura desde la Web (véase «Configuración del asistente por el responsable»); no heredan esas tres claves. `Ai__Endpoint` es la excepción: lo usan todas las compañías que elijan Ollama, y solo se lee del entorno.

Si falta una clave obligatoria, la API termina al arrancar con un mensaje que nombra la clave (sin imprimir valores). `ASPNETCORE_URLS` y `launchSettings.json` tienen prioridad sobre `ASPNETCORE_HTTP_PORTS`.

### Generar la clave de firma

Con PowerShell 7 (`pwsh`; en Windows PowerShell 5.1 falla porque el método estático `GetBytes` no existe):

```powershell
pwsh -NoProfile -Command "[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))"
```

Copie el resultado en `Jwt__SigningKey` (en su `.env` o en una variable de la sesión). En `Development` no hace falta: el JSON trae una clave de desarrollo, que la API rechaza fuera de `Development`. No comparta ni versione la suya.

### Ejecutar en local (API y Web)

1. No hace falta crear un `.env`: la API en `Development` y Compose usan los valores de `src/TimeTracker.Api/appsettings.Development.json` (la base de datos `timetracker`, el usuario `timetracker` y la contraseña de desarrollo del JSON son también los valores por defecto de `POSTGRES_*` en Compose).
2. Levante solo la base de datos: `docker compose up -d postgres` (queda en `127.0.0.1:5432`). Si prefiere levantar también la API en contenedor, vea «Docker y Docker Compose».
3. Arranque la API (perfil `http`, `Development`, `http://localhost:5274`). No hace falta definir ninguna variable: la configuración sale de `appsettings.Development.json`, aplica las migraciones y siembra los usuarios y los datos de demostración.

```powershell
dotnet run --project src/TimeTracker.Api --launch-profile http
```

   Al arrancar en local se usa la compañía por defecto `Demo` y se crean cuatro usuarios de demostración (correos y roles en «Datos de demostración»; la contraseña está en `appsettings.Development.json`, sección `Seed`). Existen y funcionan solo en `Development`: en `Production` no se siembran salvo que el despliegue lo active con `Seed__DemoUsers`.

   En local también se puede **crear una compañía** desde la Web sin definir variables: `appsettings.Development.json` abre el registro con un código de invitación de desarrollo (está en ese archivo; no se repite aquí) y **conserva los usuarios y datos de demostración**. Las variables de entorno de la sesión sobrescriben el JSON.

Comprobación: `Invoke-WebRequest http://localhost:5274/health` responde `200` con `{"status":"Healthy"}`; esa ruta es anónima y no consulta la base de datos. Salvo el inicio de sesión (`POST /api/auth/login`), el registro de compañías (`POST /api/auth/register-company`, si está abierto) y, en `Development`, el contrato OpenAPI, las rutas de la API exigen un token (`401` sin él).

4. En otra terminal, arranque la Web (`http://localhost:5172`):

```powershell
dotnet run --project src/TimeTracker.Web --launch-profile http
```

5. Abra `http://localhost:5172/login`. Puede iniciar sesión con uno de los usuarios de demostración que crea la siembra (definidos en `src/TimeTracker.Api/Seeding/DemoUsers.cs`; su contraseña es `Seed:DemoPassword` de `appsettings.Development.json`) o pulsar «Crear una compañía» para registrar la suya en `/registro` con el código de invitación de desarrollo de `appsettings.Development.json`; desde `/registro`, «Volver a iniciar sesión» regresa al login. Tras entrar se llega al Inicio, que muestra el reproductor: pulse «Iniciar jornada»; el control indica el estado y ofrece solo las acciones válidas («Pausa», «Comida», «Reanudar», «Finalizar jornada») con el tiempo del tramo actual y el total trabajado hoy. `/fichajes` es de consulta: muestra el estado, un enlace al Inicio para fichar y el mes elegido (con «Anterior», «Siguiente» y «Mes actual»; no se pueden ver meses futuros), con una fila por día, la barra de franjas a escala en hora local, la leyenda y los totales del día y del mes. Un responsable ve además `/equipo`, con dos pestañas: «Personas a cargo» (sus reportes directos y su jornada) y «Fichajes» (selector de persona y la misma vista mensual; la persona elegida se conserva al cambiar de mes). La sesión caduca a los 30 minutos: la Web lo indica en `/login` («Su sesión ha caducado. Inicie sesión de nuevo.») y hay que volver a iniciarla. La Web toma la URL de la API de `ApiBaseUrl` en `src/TimeTracker.Web/wwwroot/appsettings.json` (por defecto `http://localhost:5274`); si falta o no es válida, la Web no arranca y lo indica.

Si sobrescribió algo en la sesión, al terminar bórrelo (`Remove-Item Env:<nombre>`) y pare la base de datos con `docker compose down`.

### Migraciones con `dotnet ef`

La herramienta `dotnet-ef` es local al repositorio (`.config/dotnet-tools.json`; `dotnet tool restore`). Para crear una migración tras cambiar el modelo:

```powershell
dotnet tool run dotnet-ef migrations add <Nombre> --project src/TimeTracker.Infrastructure --startup-project src/TimeTracker.Infrastructure --output-dir Persistence/Migrations
```

Las migraciones se aplican al arrancar la API con `Database__ApplyMigrationsOnStartup=true`.

### Docker y Docker Compose

```powershell
docker compose up --build
dotnet run --project src/TimeTracker.Web --launch-profile http
```

Con esos dos comandos basta: **no hace falta crear ni rellenar un `.env`** (requiere Docker Compose 2.24 o superior). Compose levanta PostgreSQL 18 y la API en `Development`, de modo que la API lee `src/TimeTracker.Api/appsettings.Development.json` igual que con `dotnet run` (clave de firma de desarrollo, usuarios y datos de demostración, registro de compañías abierto, orígenes CORS) y aplica las migraciones al arrancar. Las credenciales de la base de datos local son los valores por defecto de `POSTGRES_DB`, `POSTGRES_USER` y `POSTGRES_PASSWORD` en `docker-compose.yml`, iguales a los de la cadena de conexión del JSON; la API, dentro del contenedor, se conecta al servicio `postgres`. La API queda en `http://localhost:5274` (compruebe `http://localhost:5274/health`), y ambos puertos (5274 y 5432) se publican **solo en `127.0.0.1`**. Compose es únicamente para desarrollo: siembra los usuarios de demostración. La Web no forma parte de Compose: la segunda orden la arranca en `http://localhost:5172`, y su `ApiBaseUrl` ya apunta a `http://localhost:5274`, así que funciona contra el contenedor sin cambios (el JSON ya permite el origen `Cors__AllowedOrigins__0=http://localhost:5172`). No ejecute a la vez la API local y la de Compose: ambas usan el puerto 5274.

**`.env` opcional.** Si existe un `.env` en la raíz, Compose lo pasa a la API y sus valores sobrescriben los del JSON (por ejemplo, para usar sus propios códigos de invitación con `Registration__InviteCodes`, que es un secreto: solo en su `.env` local). El registro y la demostración ya conviven en Compose sin necesidad de un `.env`; `.env.example` documenta el resto de las claves (véase «Configuración» sobre las tres que figuran activas).

**Empezar de cero:** `docker compose down -v` borra el volumen de PostgreSQL (todos los datos locales) y la siguiente ejecución parte de una base limpia.

Sin Compose, la imagen de la API necesita en producción las mismas claves de la tabla de «Configuración»:

```powershell
docker build -t timetracker-api .
```

La imagen se ejecuta con un usuario no root, escucha en HTTP (el TLS se termina en el ingreso, [ADR 0007](docs/adr/0007-configuracion-y-validacion-al-arrancar.md)) e incluye un `HEALTHCHECK`. No contiene secretos: las claves (entre ellas `Jwt__SigningKey`) se pasan al ejecutarla.

## Estructuración

```
src/
  TimeTracker.Domain          Entidades y objetos de valor (TimeEntry, WorkedHours, UserAccount). Sin dependencias.
  TimeTracker.Application     Casos de uso (fichar, listar, iniciar sesión, equipo) y puertos. Depende de Domain.
  TimeTracker.Infrastructure  EF Core, migraciones, repositorios PostgreSQL y asistente de IA (herramientas, bucle, proveedores). Depende de Application.
  TimeTracker.Api             Endpoints, autenticación JWT, autorización y composición. Depende de Application e Infrastructure.
  TimeTracker.Web             Blazor WebAssembly (inicio con el reproductor de fichaje, /login, /fichajes, /equipo y /jornadas, con pestañas y un diálogo reutilizable; ADR 0029), independiente.
tests/                        Pruebas de Domain, Application, Infrastructure, Web (formato, sesión, hora local, meses y franjas), Api (integración con PostgreSQL) y de arquitectura.
deploy/web/                   Imagen de la Web para Railway (Dockerfile, plantilla de nginx y script de arranque).
docs/adr/                     Registros de decisiones de arquitectura.
docs/runbook-railway.md       Runbook de despliegue manual, copia de seguridad, reversión y apertura del registro.
docs/presentacion/            Slides de la presentación del TFM (TimeTracker-TFM.pptx).
docs/api/openapi.json         Contrato OpenAPI generado (no se edita a mano).
scripts/check-coverage.ps1    Comprobación del umbral de cobertura.
docker-compose.yml            Entorno local: API y PostgreSQL.
.github/workflows/ci.yml      Integración continua.
openspec/                     Especificaciones y cambios (Spec Driven Development).
```

## Funcionalidades

### Fichajes, tipos de marcaje y jornadas

Cada intervalo tiene un tipo (`Work`, `Break`, `MealBreak`) y el empleado tiene como máximo uno abierto. La API lo expone sin cuerpo y con el empleado del token:

| Endpoint | Efecto |
| --- | --- |
| `POST /api/time-entries/clock-in` | Abre un intervalo de trabajo. |
| `POST /api/time-entries/start-break` / `start-meal-break` | Cierra el trabajo y abre una pausa o una pausa de comida. `409` si no se está trabajando. |
| `POST /api/time-entries/end-break` | Cierra la pausa y reanuda el trabajo. `409` si no hay pausa abierta. |
| `POST /api/time-entries/clock-out` | Cierra el intervalo abierto, sea del tipo que sea. |
| `GET /api/time-entries` | Intervalos con `type` y `durationMinutes`, totales de trabajo, pausas y comidas, y el `status` actual. Con `from` y `to` (instantes UTC, ventana `[from, to)`) devuelve los intervalos que solapan esa ventana; sin ellos, los últimos 300. |
| `GET /api/team/members/{id}/time-entries` | Lo mismo para un reporte directo del responsable (solo lectura; `404` idéntico para ajenos). |

La ventana admite como máximo 32 días y no puede empezar en el futuro (`400` en español en otro caso); el `status` sale siempre del intervalo abierto, esté o no en la ventana. La Web calcula en la hora del navegador las 00:00 del primer día del mes y del mes siguiente y las envía en UTC, de modo que el cambio de hora de marzo y de octubre queda bien resuelto ([ADR 0016](docs/adr/0016-vista-mensual-y-hora-local.md)).

La migración `AddTimeEntryType` deja los fichajes existentes como `Work` (columna `type` con valor por defecto `Work`) y es reversible; la decisión está en el [ADR 0014](docs/adr/0014-tipos-de-marcaje-pausas-y-comida.md).

Una **jornada** (`/jornadas`, solo Manager) define su nombre, hora de entrada, hora de salida (opcional en jornadas antiguas), inicio y duración del almuerzo, pausa esperada y días de la semana; con hora de salida, las horas extra del asistente usan sus minutos de trabajo previstos en lugar de la suposición de 8 h ([ADR 0031](docs/adr/0031-hora-de-salida-en-las-jornadas.md)).

### Asistente de IA

El asistente responde en español, en lenguaje natural, a preguntas sobre los fichajes de quien pregunta («¿cuántas horas trabajé la semana pasada?») y, si es responsable (`Manager`), sobre los de sus reportes directos, uno a uno o comparando al equipo (herramienta `get_team_summary`: quién hace más pausas, quién está por encima de la media y quién hace más horas extra, con las horas de su jornada o, sin hora de salida, una jornada diaria supuesta de 8 h; [ADR 0025](docs/adr/0025-resumen-de-equipo-del-asistente.md) y [ADR 0027](docs/adr/0027-horas-extra-en-el-resumen-de-equipo.md)). Es **de solo lectura**: no ficha ni modifica nada, y cada herramienta aplica la autorización de la persona autenticada (la identidad sale siempre del token). Se consulta con `POST /api/asistente` (`{ "message": "…", "history": [...] }`); la página `/asistente` de la Web lo consulta. Está **deshabilitado por defecto**: sin `Ai__Provider` la API arranca igual y el endpoint responde `503` «El asistente no está disponible en este entorno.». Las decisiones están en el [ADR 0017](docs/adr/0017-asistente-ia-con-herramientas-de-solo-lectura.md). Cuando la respuesta compara varias personas o periodos puede incluir una tabla, que la Web pinta como tabla HTML (siempre con texto escapado) y permite descargar como CSV con «Descargar CSV» ([ADR 0026](docs/adr/0026-tablas-y-csv-en-el-asistente.md)).

**Habilitarlo** (solo configuración, sin cambiar código; el modelo también es configuración):

- **Ollama (local, desarrollo).** Instale [Ollama](https://ollama.com), descargue un modelo con soporte de herramientas (por ejemplo `qwen2.5:7b`) y defina en su `.env` local `Ai__Provider=Ollama`, `Ai__Model=qwen2.5:7b` y `Ai__Endpoint=http://host.docker.internal:11434/` (Compose añade `host.docker.internal` para el contenedor; si ejecuta la API fuera de Docker, use `http://localhost:11434/`). Los modelos pequeños pueden fallar con las herramientas o con el español.
- **Anthropic (nube, producción).** En Railway: `Ai__Provider=Anthropic`, `Ai__Model=<modelo económico elegido en la consola de Anthropic>` y `Ai__ApiKey` como **secreto**, que usted ejecuta en su terminal sin escribirlo en ningún archivo ni en el historial: `railway variables set Ai__ApiKey --stdin -s api` (y pegue la clave). Fije además un **presupuesto de gasto** en la consola de Anthropic: es el techo real del coste.

**Deshabilitarlo:** quite `Ai__Provider` (o déjelo vacío o `None`); el resto de la API no cambia. Con un proveedor activo, `Ai__*` se valida al arrancar y un valor inválido impide arrancar con un mensaje que nombra la clave (sin imprimir su valor).

**Límites por usuario (valores por defecto, configurables con `Ai__*`):** 5 peticiones por minuto (`429` con `Retry-After`), 50 por día natural de España (`Ai__DailyRequestsPerUser`; el contador está en memoria: se reinicia con cada despliegue y no se comparte entre réplicas), cuerpo de 32 KiB como máximo (`413`), mensaje de 500 caracteres, historial de 10 mensajes, 800 tokens de salida, 5 iteraciones de herramientas y 25 s de tiempo máximo.

> **Datos personales.** Con el proveedor en la nube (`Anthropic`), los nombres y las horas de los fichajes consultados se envían a ese proveedor para elaborar la respuesta (RGPD). En una demostración use datos inventados y revise el tratamiento de datos antes de habilitarlo con datos reales; con `Ollama` los datos no salen de su máquina. Los registros del servidor no contienen la pregunta, la respuesta ni la clave.

Las pruebas del asistente **no llaman a ningún modelo real**: usan un `IChatClient` falso. La comprobación con un modelo real es manual y opcional.

### Configuración del asistente por el responsable

Además de las variables de entorno, un responsable (`Manager`) puede elegir el proveedor, el modelo, el tiempo máximo, los tokens de salida y el esfuerzo de razonamiento, aportar su propia clave de API, probar la conexión y deshabilitar el asistente desde la página `/asistente/configuracion` (enlace «Config. asistente» del menú, solo para responsables). Lo guardado se fusiona sobre el entorno, vale para su compañía (cada compañía tiene la suya y ninguna afecta a otra) y se restablece al entorno borrando la configuración. Las decisiones están en el [ADR 0018](docs/adr/0018-configuracion-del-asistente-por-el-responsable.md), matizado para varias compañías por el [ADR 0021](docs/adr/0021-configuracion-del-asistente-por-compania.md): una compañía sin configuración guardada tiene el asistente deshabilitado, y la clave de plataforma (`Ai__ApiKey`) solo sirve a la compañía de demostración.

Claves de entorno nuevas (todas opcionales; se documentan en `.env.example`):

| Clave | Uso |
|---|---|
| `Ai__AllowedProviders` | Proveedores que el responsable puede elegir, separados por comas: `Anthropic`, `OpenAI`, `Gemini`, `Groq`, `Ollama`. Sin ella, solo el proveedor del entorno. |
| `Ai__Effort` | Esfuerzo de razonamiento por defecto: `low`, `medium` o `high`. |
| `Ai__MasterKeys__1` | **Secreto.** Clave maestra de 32 bytes aleatorios en Base64 que cifra la clave de API guardada. Sin ella, el responsable no puede guardar una clave nueva (el asistente sigue funcionando con el entorno). |
| `Ai__ActiveMasterKeyVersion` | Versión de la clave maestra con la que se cifra (por ejemplo `1`). |
| `Ai__SettingsPollSeconds` | Cada cuántos segundos cada instancia revisa la configuración guardada (30 por defecto). |

`OpenAI`, `Gemini` y `Groq` se suman a `Anthropic` y `Ollama`: el proveedor del entorno (`Ai__Provider`) puede ser cualquiera de los cinco, y los tres nuevos exigen `Ai__ApiKey` y `Ai__Model`. `Groq` habla el protocolo de OpenAI en una URL fija del servidor ([ADR 0023](docs/adr/0023-proveedor-groq.md)). La URL de cada proveedor en la nube la fija el servidor; solo `Ollama` usa `Ai__Endpoint`.

**Rotación de la clave maestra.** Añada `Ai__MasterKeys__2`, cambie `Ai__ActiveMasterKeyVersion` a `2` y redespliegue: las claves guardadas con la versión 1 siguen descifrándose mientras exista `Ai__MasterKeys__1`. Un guardado del responsable sin clave nueva recifra la conservada con la versión activa. Retire la versión 1 solo cuando ninguna fila la use (`credential_key_version` en `assistant_settings`). Defina la clave maestra **antes** de que un responsable guarde una clave de API y nunca la escriba en archivos del repositorio.

> **Datos personales y alcance.** Con una clave aportada por el responsable, los nombres y las horas de los fichajes consultados se envían a ese proveedor externo (RGPD), y un cambio afecta a todas las personas de la compañía. Revise el tratamiento de datos antes de habilitar un proveedor en la nube con datos reales.

### Compañías, registro y alta de personas

Una misma instancia sirve a varias compañías, aisladas por `company_id` ([ADR 0020](docs/adr/0020-aislamiento-multiempresa.md)). El token lleva la compañía de la persona (`cid`, y `cname` para mostrar su nombre) y nunca se envía un identificador de compañía en una ruta, consulta, cuerpo ni cabecera.

**Registro de una compañía con código de invitación.** `POST /api/auth/register-company` crea una compañía y a su primer responsable (`Manager`) y abre sesión. Está **cerrado por defecto** (base y `Production`): solo se abre con `Registration__Enabled=true`, y exige un código de invitación de `Registration__InviteCodes` (lista separada por comas; cada código de 16 a 128 caracteres; reutilizables y rotables; **secretos**, solo en el entorno del despliegue). La Web lo ofrece en `/registro`. En `Development` la API arranca con el registro abierto y el código de desarrollo de `appsettings.Development.json`, y el login muestra el botón «Crear una compañía»; en un despliegue ese botón **no aparece** salvo que el servicio Web defina `SHOW_REGISTER_LINK=true` (véase «Despliegue en Railway»), y sin él `/registro` se entrega por URL directa ([ADR 0033](docs/adr/0033-registro-y-demostracion-en-desarrollo.md)). Un código incorrecto da siempre el mismo mensaje genérico, hay un límite de 5 intentos por hora y por IP (las direcciones IPv6 se agrupan por prefijo `/64`), un tope global de altas por hora (`Registration__GlobalHourlyLimit`, 30 por defecto) y un correo ya registrado responde `409` (riesgo aceptado: enumeración de correos, [ADR 0022](docs/adr/0022-autorregistro-con-codigo-de-invitacion-y-contrasenas.md)). Fuera de `Development`, la API se niega a arrancar con el registro abierto si la siembra de usuarios de demostración sigue activa o si `Registration__InviteCodes` incluye el código de desarrollo; en `Production` también si faltan `ForwardedHeaders__Enabled=true` con `ForwardedHeaders__Hops` de 1 o más. Para rotar un código filtrado se **sustituye la lista completa** de `Registration__InviteCodes` (no se registra qué código se aceptó).

**Alta de personas con contraseña temporal.** Un responsable da de alta a una persona en `/equipo/alta` (`POST /api/employees`) hasta el tope `Company__MaxEmployees`. El servidor genera una **contraseña temporal** que se muestra **una sola vez** (no se vuelve a ver ni se guarda en claro); el responsable directo puede regenerarla desde `/equipo` mientras la persona no haya entrado. En su primer inicio de sesión la persona es llevada a `/cambiar-contrasena` y no puede usar nada más hasta cambiarla. La política de contraseñas es de 12 a 128 caracteres, sin reglas de composición. La contraseña temporal **no caduca** y un token emitido antes de regenerarla no se invalida (caduca a los 30 minutos): el [runbook](docs/runbook-railway.md) incluye revisar periódicamente las cuentas con el cambio pendiente.

**Clave maestra.** La clave maestra del asistente (`Ai__MasterKeys__1`) protege las claves de API de todas las compañías. La clave maestra vive solo en Railway: nunca en el repositorio, en `.env.example` ni en este documento.

**Límites del registro y `ForwardedHeaders` (PR de seguimiento 5.1).** El límite por IP agrupa IPv6 por prefijo `/64` (rotar direcciones de un mismo `/64` no da cupo nuevo), el número de particiones del limitador está acotado y el tope global de altas limita el daño de un código de invitación filtrado. El número correcto de saltos de confianza de `ForwardedHeaders__Hops` depende de los proxies reales y no puede comprobarse en código: lo verifica el responsable (V-7 del [runbook](docs/runbook-railway.md)). Hasta entonces el registro del despliegue se mantiene cerrado salvo apertura controlada.

**Operación.** Despliegue manual con `railway up`, copia de seguridad, ensayo de migraciones, reversión y apertura del registro: [docs/runbook-railway.md](docs/runbook-railway.md).

### Datos de demostración

**Compañía y usuarios por defecto (solo en local).** Las migraciones crean siempre la compañía por defecto, de nombre `Demo`. Al arrancar en `Development`, `appsettings.Development.json` activa la siembra y la API crea en esa compañía estos cuatro usuarios de demostración (definidos en `src/TimeTracker.Api/Seeding/DemoUsers.cs`):

| Correo | Nombre | Rol | Responsable |
|---|---|---|---|
| `marta.responsable@example.test` | Marta Ejemplo | `Manager` | (ninguno) |
| `luis.empleado@example.test` | Luis Ejemplo | `Employee` | Marta |
| `ana.empleada@example.test` | Ana Ejemplo | `Employee` | Marta |
| `pablo.empleado@example.test` | Pablo Ejemplo | `Employee` | (ninguno) |

Todos comparten una contraseña de desarrollo que **no se repite aquí**: está en la sección `Seed` (`DemoPassword`) de `src/TimeTracker.Api/appsettings.Development.json`. Estos usuarios y sus datos son solo para local: el `appsettings.json` base no activa la siembra, así que en `Production` no se crean salvo que el despliegue la active de forma expresa con `Seed__DemoUsers` (como hace la demostración pública; véase «Apagar la siembra demo»).

Con `Seed__DemoUsers=true` y `Seed__DemoData=true` (ambas vienen activadas en `appsettings.Development.json`), la API crea al arrancar (tras las migraciones y la siembra de usuarios) unos 30 días de fichajes inventados, distintos para cada usuario de demostración, con trabajo, pausas y pausas de comida. Solo cuenta los días hasta **ayer**: hoy queda libre para que fiche en vivo. Es determinista (sin azar no controlado) e idempotente por día y empleado: si un empleado ya tiene algún intervalo un día, ese día se omite, de modo que reiniciar la API no duplica nada y cada arranque añade solo los días nuevos. Fines de semana libres y uno o dos días sin fichajes por persona. Los horarios son de la hora local de España, convertidos a UTC con la regla de cambio de hora de la UE. En el registro solo aparecen contadores.

| Persona | Perfil |
|---|---|
| Marta (responsable) | Jornada regular 09:00-17:30 con reuniones (pausas de duración y presencia variables) y comida de unos 45 min. |
| Luis | Madrugador 08:00-17:00, comida de 60 min y una pausa de unos 15 min casi todos los días. |
| Ana | 09:30-18:30, comida larga (~75 min) y alguna pausa de tarde; los miércoles teletrabaja (09:00-17:00) y registra la comida como pausa normal, sin comida. |
| Pablo | Media jornada 08:00-14:00 con una sola pausa corta y sin comida. |

Para apagarla basta con `Seed__DemoData=false` (en un despliegue también sirve quitar la variable; en local, donde el JSON la activa, hay que ponerla a `false`): no se borran los intervalos ya creados, que son datos normales. Sin ventana, el listado de fichajes devuelve hasta 300 intervalos ([ADR 0015](docs/adr/0015-datos-de-demostracion-de-un-mes.md)); la Web pide siempre un mes concreto ([ADR 0016](docs/adr/0016-vista-mensual-y-hora-local.md)). `.env.example` no incluye esta clave a propósito; se documenta solo aquí. Con Compose, la API ya la recibe activada del JSON; para apagarla ponga `Seed__DemoData=false` en su `.env` local (Compose lo pasa a la API). Si ejecuta varias instancias de la API a la vez, no active la siembra en todas: podrían duplicar días.

## Despliegue en Railway

La demostración pública se despliega en [Railway](https://railway.com) como un proyecto (`timetracker-tfm`) con tres servicios en el entorno `production`: `Postgres` (plantilla de base de datos de Railway), `api` (imagen del `Dockerfile` de la raíz) y `web` (imagen de `deploy/web/Dockerfile`). La Web es estática: Blazor WebAssembly publicado y servido por nginx sin privilegios (imagen oficial `nginxinc/nginx-unprivileged`), con respaldo a `index.html` para las rutas de la SPA, tipos MIME de WebAssembly, compresión y cabeceras de seguridad (CSP con `'wasm-unsafe-eval'`, `X-Content-Type-Options`, `Referrer-Policy`, `X-Frame-Options`/`frame-ancestors`, HSTS). `railway.json` (en la raíz) fija el constructor Docker y la comprobación de salud `/health`, que sirven tanto la API como la Web; no contiene secretos.

### Pasos reproducibles (CLI de Railway)

Requiere la [CLI de Railway](https://docs.railway.com/guides/cli) con la sesión iniciada (`railway login`). Desde la raíz del repositorio:

```powershell
railway init -n timetracker-tfm            # crea el proyecto y lo enlaza (el enlace queda fuera del repositorio)
railway status                             # compruebe que el proyecto activo es el suyo antes de seguir
railway add -d postgres                    # servicio PostgreSQL
railway add -s api                         # servicio de la API
railway add -s web -v "RAILWAY_DOCKERFILE_PATH=deploy/web/Dockerfile"
railway domain -s api                      # dominio público de la API (railway domain list -s api)
railway domain -s web                      # dominio público de la Web
```

Variables del servicio `api` (`railway variables set -s api --skip-deploys "CLAVE=valor"`; **sin valores en este documento**):

| Variable | Valor |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` (OpenAPI no se sirve; sin páginas de desarrollo). |
| `ConnectionStrings__Default` | Referencia a las variables del servicio de base de datos: `Host=${{Postgres.PGHOST}};Port=${{Postgres.PGPORT}};Database=${{Postgres.PGDATABASE}};Username=${{Postgres.PGUSER}};Password=${{Postgres.PGPASSWORD}}`. Railway las resuelve en cada servicio; no se copia ningún valor. Escríbala entre comillas simples en PowerShell/Bash. |
| `Jwt__SigningKey` | Clave aleatoria de al menos 32 bytes (véase «Regenerar la clave JWT»). |
| `Cors__AllowedOrigins__0` | **Obligatoria.** `https://<dominio de la Web>`, sin barra final; sin ella la API no arranca. |
| `Database__ApplyMigrationsOnStartup` | `true`: la API aplica las migraciones al arrancar. |
| `ForwardedHeaders__Enabled` | `true`: la API está tras el proxy de Railway (véase «Riesgos y decisiones»). |
| `Seed__DemoUsers` y `Seed__DemoPassword` | `true` y una contraseña de demostración de al menos 12 caracteres, definida solo en Railway (solo para la demostración; véase «Apagar la siembra demo»). |
| `Seed__DemoData` | `true` para sembrar el historial de un mes (véase «Datos de demostración»); opcional. |
| `Ai__Provider`, `Ai__Model` y `Ai__ApiKey` | Opcionales: habilitan el asistente (véase «Asistente de IA»). `Ai__ApiKey` es un secreto y se define con `railway variables set Ai__ApiKey --stdin -s api`; nunca en este repositorio. Para deshabilitarlo, quite `Ai__Provider`. |
| `Ai__MasterKeys__1` y `Ai__ActiveMasterKeyVersion` | Opcionales, pero **imprescindibles antes de que un responsable guarde una clave de API** desde la Web (véase «Configuración del asistente por el responsable en Railway»). `Ai__MasterKeys__1` es un secreto y se define por `stdin`; `Ai__ActiveMasterKeyVersion=1`. |
| `Ai__AllowedProviders` | Opcional: proveedores que el responsable puede elegir (`Anthropic`, `OpenAI`, `Gemini`, `Groq`, `Ollama`). Sin ella, solo el del entorno. |
| `Registration__Enabled` | `false` por defecto (registro cerrado). Se abre solo siguiendo el orden de «Apertura del registro» del [runbook](docs/runbook-railway.md). |
| `Registration__InviteCodes` | **Secreto**, solo en el entorno de Railway (lista separada por comas; 16 a 128 caracteres cada código). Se define por `stdin`; nunca en el repositorio. |
| `Company__MaxEmployees` | Opcional: tope de personas por compañía (50 por defecto, de 1 a 1000). |
| `Ai__RequestsPerMinutePerCompany`, `Ai__DailyRequestsPerCompany`, `Ai__MaxLoadedCompanies`, `Ai__RuntimeIdleMinutes` | Opcionales: topes del asistente por compañía (30, 500, 100 y 60 por defecto). Los fija el entorno; un responsable no los edita. |

La demostración se despliega y se revierte siguiendo el [runbook de Railway](docs/runbook-railway.md): copia de seguridad previa, ensayo de las migraciones con datos copiados, orden de reversión y apertura controlada del registro.

Variable del servicio `web`: `API_BASE_URL=https://<dominio de la API>` (sin barra final). Al arrancar, el contenedor genera `appsettings.json` con ella y abre la CSP (`connect-src`) solo a ese origen; si falta o no es una URL `http`/`https`, el contenedor no arranca. La URL de la API no se versiona.

Variable opcional del servicio `web`: `SHOW_REGISTER_LINK=true` muestra en el login el botón «Crear una compañía» (el contenedor la escribe como `Features:ShowRegisterLink` en el `appsettings.json` servido, sin recompilar); si falta o vale otra cosa, el botón queda oculto. **Activar el botón en producción exige dos cosas independientes**: esta variable en la Web y, en la API, `Registration__Enabled=true` con sus códigos de invitación propios. El botón por sí solo no abre el registro: la ruta de la API sigue cerrada ([ADR 0033](docs/adr/0033-registro-y-demostracion-en-desarrollo.md)).

Despliegue (sube el código local; la Web y la API van por separado):

```powershell
railway up --service api --ci
railway up --service web --ci
```

Comprobación: `GET <API>/health` responde `200`; inicie sesión en la Web con un usuario de demostración.

### Configuración del asistente por el responsable en Railway

Para que un responsable (`Manager`) guarde su propia clave de API desde `/asistente/configuracion`, la API necesita una clave maestra con la que cifrarla. Hágalo **antes de que un responsable guarde una clave**; sin ella la Web desactiva el campo de clave y el asistente sigue con las variables de entorno. Sin escribir ningún valor en archivos ni en el historial:

```powershell
pwsh -NoProfile -Command "[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))" | railway variables set Ai__MasterKeys__1 --stdin -s api
railway variables set Ai__ActiveMasterKeyVersion=1 -s api
```

`Cors__AllowedOrigins__0` es obligatoria (sin ella la API no arranca) y debe ser el origen de la Web. Si quiere que el responsable pueda elegir otro proveedor, defina `Ai__AllowedProviders`; por defecto solo puede usar el del entorno. La rotación de la clave maestra está en «Configuración del asistente por el responsable».

```powershell
railway variables set "Ai__AllowedProviders=Anthropic,OpenAI,Ollama" -s api
```

La lista va separada por comas, no distingue mayúsculas y admite espacios tras las comas. `None`, `GitHubModels` o un nombre desconocido impiden el arranque. `Ollama` solo queda entre los proveedores seleccionables si el entorno define `Ai__Endpoint`. No lleva secretos.

Riesgos y decisiones pendientes de este despliegue (detalle en el [ADR 0019](docs/adr/0019-auditoria-inmutable-y-despliegue-de-la-configuracion-del-asistente.md)):

- **Datos personales a un tercero.** Con un proveedor en la nube, los nombres y las horas de los fichajes consultados salen hacia ese tercero bajo la clave que aporta el responsable. Revise el contrato de tratamiento antes de usar datos reales.
- **Riesgo de la API Responses de OpenAI.** La v1 usa solo la API de chat; algunos modelos de OpenAI exigen Responses para usar herramientas. La prueba de conexión no usa herramientas y puede dar `ok` aunque el chat falle con ese modelo.
- **Alcance de la configuración.** Desde el [ADR 0021](docs/adr/0021-configuracion-del-asistente-por-compania.md) hay una configuración por compañía: cualquier responsable (`Manager`) de la compañía puede leerla, cambiarla, restablecerla y probar conexiones, pero no la de otra compañía (riesgo aceptado, acotado a la compañía).
- **Pregunta abierta para el product owner: custodia de la clave maestra.** Quién la genera, dónde se guarda su copia de seguridad y quién puede leerla en Railway sigue sin decidirse (Q-8). Ahora protege las claves de **todas** las compañías. Si se pierde, las claves guardadas quedan ilegibles (cada responsable las vuelve a introducir); el asistente sigue funcionando con el entorno en la compañía de demostración.
- **La clave maestra vive solo en Railway.** Su valor lo define usted en Railway por `stdin` (véase arriba); nunca se escribe en el repositorio, en `.env.example` ni en un documento.

### Regenerar la clave JWT

Genere una clave nueva (véase «Generar la clave de firma») y póngala sin que quede en el historial ni en ningún archivo:

```powershell
pwsh -NoProfile -Command "[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))" | railway variables set Jwt__SigningKey --stdin -s api
```

El cambio redespliega la API e invalida los tokens emitidos (las sesiones abiertas piden iniciar sesión de nuevo).

### Despliegue manual y autodespliegue

**Hoy no hay autodespliegue:** los servicios de Railway no tienen repositorio conectado (`source.repo = null`), así que fusionar una PR no despliega nada; se despliega a mano con `railway up`, y cada despliegue pasa por las puertas previas del [runbook](docs/runbook-railway.md) (copia de seguridad, lectura del valor real de `Database__ApplyMigrationsOnStartup`, `/health`, rollback de código preparado y aprobación del responsable).

**No conecte el repositorio sin rehacer antes el plan de despliegue por fases del runbook.** Con varias compañías, las migraciones A y B van en despliegues separados y observados; un autodespliegue desde `main` las aplicaría seguidas. Si se decide conectarlo (lo hace una persona con acceso al repositorio desde el panel de Railway: en cada servicio, `Settings` → `Source` → `Connect Repo`), `api` usa el Dockerfile de la raíz y `web` conserva `RAILWAY_DOCKERFILE_PATH=deploy/web/Dockerfile`.

### Apagar la siembra demo

Mientras la siembra esté activa existen cuentas de demostración con una contraseña compartida. Para cerrarla:

```powershell
railway variables set Seed__DemoUsers=false -s api
railway variables delete Seed__DemoPassword -s api
```

Desactivar la siembra **no borra** los usuarios ya creados: cámbieles la contraseña o elimínelos en la base de datos, o cree otro proyecto/base limpia. Los datos de la demostración son inventados.

Para dejar de sembrar solo los fichajes (y conservar los usuarios): `railway variables set Seed__DemoData=false -s api`. Tampoco borra los intervalos ya creados.

### Riesgos y decisiones

- **`ForwardedHeaders` (diferido en el ADR 0011, resuelto aquí).** Tras el proxy de Railway, la IP de la conexión es la del proxy y el limitador de login (por IP) vería a todos los clientes como uno. El borde de Railway entrega `X-Forwarded-For: <cliente>, <borde>` (descarta el que envíe el cliente; las direcciones del borde son públicas y cambian) y `X-Forwarded-Proto: https`. Con `ForwardedHeaders__Enabled=true` la API confía en dos saltos (`ForwardedHeaders__Hops`, 2 por defecto): el cliente es la penúltima entrada y cualquier dirección antepuesta a su izquierda no cuenta. Está desactivado por defecto: sin proxy de confianza, esas cabeceras las controla el cliente. Riesgo residual: solo es seguro si la API únicamente es alcanzable a través del borde de Railway (así es en este despliegue: no hay acceso público directo al contenedor); un ataque distribuido no queda cubierto por el límite por IP.
- **Demostración pública con cuentas de prueba:** véase «Apagar la siembra demo». No hay refresh token: la sesión dura 30 minutos.
- **TLS:** lo termina el borde de Railway; la API y nginx escuchan en HTTP dentro de la plataforma. La Web envía HSTS.
- **Base de datos:** la API usa la red privada de Railway (`postgres.railway.internal`); el servicio de PostgreSQL no se expone por TCP público.
- **Imagen de la Web:** la CSP admite el `importmap` en línea que genera la publicación de Blazor por su hash SHA-256 (calculado al construir la imagen), sin `'unsafe-inline'`.


## Decisiones de arquitectura (ADR)

Todos los registros de decisiones están en [docs/adr/](docs/adr/) (33 en total, del 0001 al 0033); los ADR aceptados no se reescriben, se sustituyen por otro nuevo. Las decisiones clave:

1. [Monolito modular y regla de dependencias](docs/adr/0003-monolito-modular-y-regla-de-dependencias.md)
2. [Autenticación con JWT propio, roles y equipo](docs/adr/0011-autenticacion-jwt-roles-y-equipo.md)
3. [Asistente de IA con herramientas de solo lectura](docs/adr/0017-asistente-ia-con-herramientas-de-solo-lectura.md)
4. [Configuración del asistente por el responsable](docs/adr/0018-configuracion-del-asistente-por-el-responsable.md) (sustituido parcialmente por el 0021)
5. [Auditoría inmutable y despliegue de la configuración del asistente](docs/adr/0019-auditoria-inmutable-y-despliegue-de-la-configuracion-del-asistente.md) (sustituido parcialmente por el 0021)
6. [Aislamiento multiempresa en una base de datos compartida](docs/adr/0020-aislamiento-multiempresa.md)
7. [Configuración del asistente por compañía](docs/adr/0021-configuracion-del-asistente-por-compania.md) (sustituye parcialmente al 0018 y al 0019)
8. [Autorregistro con código de invitación y contraseñas](docs/adr/0022-autorregistro-con-codigo-de-invitacion-y-contrasenas.md)
9. [Configuración de desarrollo en appsettings.Development.json](docs/adr/0032-configuracion-de-desarrollo-en-appsettings.md) (sustituye parcialmente al 0007)
10. [Registro de compañías y datos de demostración en Development](docs/adr/0033-registro-y-demostracion-en-desarrollo.md) (sustituye parcialmente al 0022 y al 0032)

## Integración continua

El flujo `.github/workflows/ci.yml` se ejecuta en GitHub Actions en cada `push` a `main` y en cada pull request: restaura, comprueba el formato, compila, prueba con cobertura (Domain al 100 % y Application al 80 %), audita paquetes vulnerables, publica y construye la imagen (sin subirla). Sus pasos pueden reproducirse en local con los comandos de «Compilar» y «Probar».

## Enlaces

- Web (demostración): <https://web-production-a744a.up.railway.app>
- API (demostración): <https://api-production-0f339.up.railway.app> (comprobación: `/health`). Véase «Despliegue en Railway».
- Vídeo de demostración: **pendiente**.
- Presentación (slides): [`docs/presentacion/TimeTracker-TFM.pptx`](docs/presentacion/TimeTracker-TFM.pptx) (PowerPoint, incluido en el repositorio).
- Credenciales de prueba: las credenciales para la evaluación se entregan por el formulario de entrega y no se publican en el repositorio.

## Convenciones

Las reglas de trabajo (SDD, TDD, arquitectura, seguridad) están en [AGENTS.md](AGENTS.md). Documentación y textos de usuario en español; identificadores de código en inglés.
