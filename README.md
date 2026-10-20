# TimeTracker

Aplicación de control de tiempos y ausencias para empresas: fichajes, ausencias, saldo de horas y horas extra, con un asistente de IA que responderá consultas en lenguaje natural sobre esos datos.

> **Estado actual:** la aplicación tiene **autenticación real y roles**. Se inicia sesión en `/login` con un correo y una contraseña; cualquier persona con sesión (empleado o responsable) ficha con el **reproductor del Inicio** (iniciar jornada, pausa, comida, reanudar y finalizar, con cronómetro en vivo), consulta su historial **por mes** en `/fichajes` (una fila por día con una barra de franjas de trabajo, pausas y comidas, en su hora local) y un **responsable** (rol `Manager`) consulta la misma vista mensual de sus reportes directos en `/equipo`. La API emite un JWT propio de 30 minutos (sin renovación) y guarda los usuarios y los fichajes en PostgreSQL con EF Core. La Web tiene una página de inicio en español con enlaces según la sesión y avisa en `/login` cuando la sesión ha caducado. La API incluye además un **asistente de IA de solo lectura** (`POST /api/asistente`, deshabilitado por defecto; véase «Asistente de IA»). Todavía **no** hay ausencias.
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
- Asistente de IA: `IChatClient` (Microsoft.Extensions.AI) con Anthropic (nube) u Ollama (local) elegidos por configuración ([ADR 0017](docs/adr/0017-asistente-ia-con-herramientas-de-solo-lectura.md)).
- Previsto en cambios futuros: ausencias.

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
| `Seed__DemoData` | No (por defecto `false`; exige `Seed__DemoUsers=true`) | Con `true`, la API siembra al arrancar un mes de fichajes inventados para los usuarios de prueba (véase «Datos de demostración»). Con `Seed__DemoUsers` en `false` o sin definir, la API no arranca. |
| `Database__ApplyMigrationsOnStartup` | No (por defecto `false`) | Con `true`, la API aplica las migraciones al arrancar. |
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | Solo con Compose | Base de datos, usuario y contraseña (inventada) del contenedor de PostgreSQL. |
| `ASPNETCORE_HTTP_PORTS` | No | Puerto de escucha; la imagen Docker lo fija en `8080`. |
| `PORT` | No | Si la plataforma lo define (Railway), la imagen de la API escucha ahí y prevalece sobre `ASPNETCORE_HTTP_PORTS`. |
| `ForwardedHeaders__Enabled` | No (por defecto `false`) | Con `true`, la API confía en `X-Forwarded-For`/`X-Forwarded-Proto` de un proxy inverso. Solo tras un proxy que la proteja de accesos directos (véase «Despliegue en Railway»). |
| `ForwardedHeaders__Hops` | No (por defecto `2`) | Saltos de proxy de confianza, contados desde la derecha de `X-Forwarded-For` (Railway: «cliente, borde»). |
| `Ai__Provider` | No (vacío = asistente deshabilitado) | `Anthropic` u `Ollama` (sin distinguir mayúsculas; `None` o vacío lo deshabilita). Cualquier otro valor, incluido un número, impide arrancar. Véase «Asistente de IA». |
| `Ai__Model` | Con proveedor | Nombre del modelo (máximo 100 caracteres); es configuración, nunca código. |
| `Ai__ApiKey` | Con `Anthropic` | Clave de API del proveedor. **Es un secreto**: solo en variables de entorno o en el secreto de la plataforma; nunca en archivos del repositorio. |
| `Ai__Endpoint` | Con `Ollama` | URL `http`/`https` del servidor de Ollama, por ejemplo `http://host.docker.internal:11434/`. |
| `Ai__MaxOutputTokens`, `Ai__MaxToolIterations`, `Ai__TimeoutSeconds` | No (800, 5 y 25) | Tokens de salida (64-4096), iteraciones de herramientas (1-10) y tiempo máximo en segundos (5-55). |
| `Ai__RequestsPerMinute`, `Ai__DailyRequestsPerUser` | No (5 y 50) | Peticiones por minuto (1-60) y por día natural de España (1-1000) por usuario. |

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

5. Abra `http://localhost:5172/login` e inicie sesión con un usuario de prueba. Tras entrar se llega al Inicio, que muestra el reproductor: pulse «Iniciar jornada»; el control indica el estado y ofrece solo las acciones válidas («Pausa», «Comida», «Reanudar», «Finalizar jornada») con el tiempo del tramo actual y el total trabajado hoy. `/fichajes` es de consulta: muestra el estado, un enlace al Inicio para fichar y el mes elegido (con «Anterior», «Siguiente» y «Mes actual»; no se pueden ver meses futuros), con una fila por día, la barra de franjas a escala en hora local, la leyenda y los totales del día y del mes. Un responsable ve además `/equipo`, con la lista de sus reportes directos y la misma vista mensual de cada uno (el miembro elegido se conserva al cambiar de mes). La sesión caduca a los 30 minutos: la Web lo indica en `/login` («Su sesión ha caducado. Inicie sesión de nuevo.») y hay que volver a iniciarla. La Web toma la URL de la API de `ApiBaseUrl` en `src/TimeTracker.Web/wwwroot/appsettings.json` (por defecto `http://localhost:5274`); si falta o no es válida, la Web no arranca y lo indica.

Al terminar, borre las variables de la sesión (`Remove-Item Env:ASPNETCORE_ENVIRONMENT, Env:ConnectionStrings__Default, Env:Jwt__SigningKey, Env:Database__ApplyMigrationsOnStartup, Env:Cors__AllowedOrigins__0, Env:Seed__DemoUsers, Env:Seed__DemoPassword`) y pare la base de datos con `docker compose down`.

### Migraciones con `dotnet ef`

La herramienta `dotnet-ef` es local al repositorio (`.config/dotnet-tools.json`; `dotnet tool restore`). Para crear una migración tras cambiar el modelo:

```powershell
dotnet tool run dotnet-ef migrations add <Nombre> --project src/TimeTracker.Infrastructure --startup-project src/TimeTracker.Infrastructure --output-dir Persistence/Migrations
```

Las migraciones se aplican al arrancar la API con `Database__ApplyMigrationsOnStartup=true`.

### Tipos de marcaje: pausas y pausas de comida

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

## Asistente de IA

El asistente responde en español, en lenguaje natural, a preguntas sobre los fichajes de quien pregunta («¿cuántas horas trabajé la semana pasada?») y, si es responsable (`Manager`), sobre los de sus reportes directos, uno a uno. Es **de solo lectura**: no ficha ni modifica nada, y cada herramienta aplica la autorización de la persona autenticada (la identidad sale siempre del token). Se consulta con `POST /api/asistente` (`{ "message": "…", "history": [...] }`); la página `/asistente` de la Web lo consulta. Está **deshabilitado por defecto**: sin `Ai__Provider` la API arranca igual y el endpoint responde `503` «El asistente no está disponible en este entorno.». Las decisiones están en el [ADR 0017](docs/adr/0017-asistente-ia-con-herramientas-de-solo-lectura.md).

**Habilitarlo** (solo configuración, sin cambiar código; el modelo también es configuración):

- **Ollama (local, desarrollo).** Instale [Ollama](https://ollama.com), descargue un modelo con soporte de herramientas (por ejemplo `qwen2.5:7b`) y defina en su `.env` local `Ai__Provider=Ollama`, `Ai__Model=qwen2.5:7b` y `Ai__Endpoint=http://host.docker.internal:11434/` (Compose añade `host.docker.internal` para el contenedor; si ejecuta la API fuera de Docker, use `http://localhost:11434/`). Los modelos pequeños pueden fallar con las herramientas o con el español.
- **Anthropic (nube, producción).** En Railway: `Ai__Provider=Anthropic`, `Ai__Model=<modelo económico elegido en la consola de Anthropic>` y `Ai__ApiKey` como **secreto**, que usted ejecuta en su terminal sin escribirlo en ningún archivo ni en el historial: `railway variables set Ai__ApiKey --stdin -s api` (y pegue la clave). Fije además un **presupuesto de gasto** en la consola de Anthropic: es el techo real del coste.

**Deshabilitarlo:** quite `Ai__Provider` (o déjelo vacío o `None`); el resto de la API no cambia. Con un proveedor activo, `Ai__*` se valida al arrancar y un valor inválido impide arrancar con un mensaje que nombra la clave (sin imprimir su valor).

**Límites por usuario:** 5 peticiones por minuto (`429` con `Retry-After`), 50 por día natural de España (`Ai__DailyRequestsPerUser`; el contador está en memoria: se reinicia con cada despliegue y no se comparte entre réplicas), cuerpo de 32 KiB como máximo (`413`), mensaje de 500 caracteres, historial de 10 mensajes, 800 tokens de salida, 5 iteraciones de herramientas y 25 s de tiempo máximo.

> **Datos personales.** Con el proveedor en la nube (`Anthropic`), los nombres y las horas de los fichajes consultados se envían a ese proveedor para elaborar la respuesta (RGPD). En una demostración use datos inventados y revise el tratamiento de datos antes de habilitarlo con datos reales; con `Ollama` los datos no salen de su máquina. Los registros del servidor no contienen la pregunta, la respuesta ni la clave.

Las pruebas del asistente **no llaman a ningún modelo real**: usan un `IChatClient` falso. La comprobación con un modelo real es manual y opcional.

### Configuración del asistente por el responsable

Además de las variables de entorno, un responsable (`Manager`) puede elegir el proveedor, el modelo, el tiempo máximo, los tokens de salida y el esfuerzo de razonamiento, aportar su propia clave de API, probar la conexión y deshabilitar el asistente desde la página `/asistente/configuracion` (enlace «Config. asistente» del menú, solo para responsables). Lo guardado se fusiona sobre el entorno, vale para toda la empresa y se restablece al entorno borrando la configuración. Las decisiones están en el [ADR 0018](docs/adr/0018-configuracion-del-asistente-por-el-responsable.md).

Claves de entorno nuevas (todas opcionales; se documentan en `.env.example`):

| Clave | Uso |
|---|---|
| `Ai__AllowedProviders` | Proveedores que el responsable puede elegir, separados por comas: `Anthropic`, `OpenAI`, `Gemini`, `Ollama`. Sin ella, solo el proveedor del entorno. |
| `Ai__Effort` | Esfuerzo de razonamiento por defecto: `low`, `medium` o `high`. |
| `Ai__MasterKeys__1` | **Secreto.** Clave maestra de 32 bytes aleatorios en Base64 que cifra la clave de API guardada. Sin ella, el responsable no puede guardar una clave nueva (el asistente sigue funcionando con el entorno). |
| `Ai__ActiveMasterKeyVersion` | Versión de la clave maestra con la que se cifra (por ejemplo `1`). |
| `Ai__SettingsPollSeconds` | Cada cuántos segundos cada instancia revisa la configuración guardada (30 por defecto). |

`OpenAI` y `Gemini` se suman a `Anthropic` y `Ollama`: el proveedor del entorno (`Ai__Provider`) puede ser cualquiera de los cuatro, y los dos nuevos exigen `Ai__ApiKey` y `Ai__Model`. La URL de cada proveedor en la nube la fija el servidor; solo `Ollama` usa `Ai__Endpoint`.

**Rotación de la clave maestra.** Añada `Ai__MasterKeys__2`, cambie `Ai__ActiveMasterKeyVersion` a `2` y redespliegue: las claves guardadas con la versión 1 siguen descifrándose mientras exista `Ai__MasterKeys__1`. Un guardado del responsable sin clave nueva recifra la conservada con la versión activa. Retire la versión 1 solo cuando ninguna fila la use (`credential_key_version` en `assistant_settings`). Defina la clave maestra **antes** de que un responsable guarde una clave de API y nunca la escriba en archivos del repositorio.

> **Datos personales y alcance.** Con una clave aportada por el responsable, los nombres y las horas de los fichajes consultados se envían a ese proveedor externo (RGPD), y un cambio afecta a toda la empresa. Revise el tratamiento de datos antes de habilitar un proveedor en la nube con datos reales.

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
| `Seed__DemoUsers` y `Seed__DemoPassword` | `true` y la contraseña de «Usuarios de prueba» (solo para la demostración; véase «Apagar la siembra demo»). |
| `Seed__DemoData` | `true` para sembrar el historial de un mes (véase «Datos de demostración»); opcional. |
| `Ai__Provider`, `Ai__Model` y `Ai__ApiKey` | Opcionales: habilitan el asistente (véase «Asistente de IA»). `Ai__ApiKey` es un secreto y se define con `railway variables set Ai__ApiKey --stdin -s api`; nunca en este repositorio. Para deshabilitarlo, quite `Ai__Provider`. |
| `Ai__MasterKeys__1` y `Ai__ActiveMasterKeyVersion` | Opcionales, pero **imprescindibles antes de que un responsable guarde una clave de API** desde la Web (véase «Configuración del asistente por el responsable en Railway»). `Ai__MasterKeys__1` es un secreto y se define por `stdin`; `Ai__ActiveMasterKeyVersion=1`. |
| `Ai__AllowedProviders` | Opcional: proveedores que el responsable puede elegir (`Anthropic`, `OpenAI`, `Gemini`, `Ollama`). Sin ella, solo el del entorno. |

Variable del servicio `web`: `API_BASE_URL=https://<dominio de la API>` (sin barra final). Al arrancar, el contenedor genera `appsettings.json` con ella y abre la CSP (`connect-src`) solo a ese origen; si falta o no es una URL `http`/`https`, el contenedor no arranca. La URL de la API no se versiona.

Despliegue (sube el código local; la Web y la API van por separado):

```powershell
railway up --service api --ci
railway up --service web --ci
```

Comprobación: `GET <API>/health` responde `200`; inicie sesión en la Web con un usuario de «Usuarios de prueba».

### Configuración del asistente por el responsable en Railway

Para que un responsable (`Manager`) guarde su propia clave de API desde `/asistente/configuracion`, la API necesita una clave maestra con la que cifrarla. Hágalo **antes de que un responsable guarde una clave**; sin ella la Web desactiva el campo de clave y el asistente sigue con las variables de entorno. Sin escribir ningún valor en archivos ni en el historial:

```powershell
pwsh -NoProfile -Command "[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))" | railway variables set Ai__MasterKeys__1 --stdin -s api
railway variables set Ai__ActiveMasterKeyVersion=1 -s api
```

`Cors__AllowedOrigins__0` es obligatoria (sin ella la API no arranca) y debe ser el origen de la Web. Si quiere que el responsable pueda elegir otro proveedor, defina `Ai__AllowedProviders`; por defecto solo puede usar el del entorno. La rotación de la clave maestra está en «Configuración del asistente por el responsable».

Riesgos y decisiones pendientes de este despliegue (detalle en el [ADR 0018](docs/adr/0018-configuracion-del-asistente-por-el-responsable.md)):

- **Datos personales a un tercero.** Con un proveedor en la nube, los nombres y las horas de los fichajes consultados salen hacia ese tercero bajo la clave que aporta el responsable. Revise el contrato de tratamiento antes de usar datos reales.
- **Riesgo de la API Responses de OpenAI.** La v1 usa solo la API de chat; algunos modelos de OpenAI exigen Responses para usar herramientas. La prueba de conexión no usa herramientas y puede dar `ok` aunque el chat falle con ese modelo.
- **Configuración única.** Es una sola para toda la empresa y cualquier responsable de cualquier equipo puede leerla, cambiarla, restablecerla y probar conexiones.
- **Pregunta abierta para el product owner: custodia de la clave maestra.** Quién la genera, dónde se guarda su copia de seguridad y quién puede leerla en Railway sigue sin decidirse. Si se pierde, las claves guardadas quedan ilegibles (el responsable las vuelve a introducir); el asistente sigue funcionando con el entorno.

### Regenerar la clave JWT

Genere una clave nueva (véase «Generar la clave de firma») y póngala sin que quede en el historial ni en ningún archivo:

```powershell
pwsh -NoProfile -Command "[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))" | railway variables set Jwt__SigningKey --stdin -s api
```

El cambio redespliega la API e invalida los tokens emitidos (las sesiones abiertas piden iniciar sesión de nuevo).

### Activar el autodespliegue desde GitHub

Lo activa una persona con acceso al repositorio, desde el panel de Railway (la CLI no puede autorizar la aplicación de GitHub): en cada servicio, `Settings` → `Source` → `Connect Repo` y elija el repositorio y la rama `main` (autorizando antes la aplicación de GitHub de Railway). Para que `api` y `web` usen su Dockerfile: `api` usa el de la raíz y `web` conserva la variable `RAILWAY_DOCKERFILE_PATH=deploy/web/Dockerfile`.

### Apagar la siembra demo

La contraseña de los usuarios de prueba es pública (está en este README): mientras la siembra esté activa, cualquiera puede iniciar sesión con ellos. Para cerrarla:

```powershell
railway variables set Seed__DemoUsers=false -s api
railway variables delete Seed__DemoPassword -s api
```

Desactivar la siembra **no borra** los usuarios ya creados: cámbieles la contraseña o elimínelos en la base de datos, o cree otro proyecto/base limpia. Los datos de la demostración son inventados.

Para dejar de sembrar solo los fichajes (y conservar los usuarios): `railway variables set Seed__DemoData=false -s api`. Tampoco borra los intervalos ya creados.

### Riesgos y decisiones

- **`ForwardedHeaders` (diferido en el ADR 0011, resuelto aquí).** Tras el proxy de Railway, la IP de la conexión es la del proxy y el limitador de login (por IP) vería a todos los clientes como uno. El borde de Railway entrega `X-Forwarded-For: <cliente>, <borde>` (descarta el que envíe el cliente; las direcciones del borde son públicas y cambian) y `X-Forwarded-Proto: https`. Con `ForwardedHeaders__Enabled=true` la API confía en dos saltos (`ForwardedHeaders__Hops`, 2 por defecto): el cliente es la penúltima entrada y cualquier dirección antepuesta a su izquierda no cuenta. Está desactivado por defecto: sin proxy de confianza, esas cabeceras las controla el cliente. Riesgo residual: solo es seguro si la API únicamente es alcanzable a través del borde de Railway (así es en este despliegue: no hay acceso público directo al contenedor); un ataque distribuido no queda cubierto por el límite por IP.
- **Demostración pública con credenciales públicas:** véase «Apagar la siembra demo». No hay refresh token: la sesión dura 30 minutos.
- **TLS:** lo termina el borde de Railway; la API y nginx escuchan en HTTP dentro de la plataforma. La Web envía HSTS.
- **Base de datos:** la API usa la red privada de Railway (`postgres.railway.internal`); el servicio de PostgreSQL no se expone por TCP público.
- **Imagen de la Web:** la CSP admite el `importmap` en línea que genera la publicación de Blazor por su hash SHA-256 (calculado al construir la imagen), sin `'unsafe-inline'`.

## Usuarios de prueba

Cuatro usuarios inventados (dominio `example.test`) que crea la siembra de demostración cuando `Seed__DemoUsers` es `true` (Compose la activa). Todos usan la misma contraseña de demostración, **inventada y pública**, que debe escribir también en `Seed__DemoPassword` de su `.env` local:

| Correo | Nombre | Rol | Contraseña |
|---|---|---|---|
| `marta.responsable@example.test` | Marta Ejemplo | `Manager` (responsable de Ana y Luis) | `Demo-Fichajes-2026` |
| `luis.empleado@example.test` | Luis Ejemplo | `Employee` (equipo de Marta) | `Demo-Fichajes-2026` |
| `ana.empleada@example.test` | Ana Ejemplo | `Employee` (equipo de Marta) | `Demo-Fichajes-2026` |
| `pablo.empleado@example.test` | Pablo Ejemplo | `Employee` (sin responsable) | `Demo-Fichajes-2026` |

**No active la siembra (`Seed__DemoUsers=true`) en un despliegue real:** la contraseña es pública y cualquiera podría iniciar sesión. Por eso está desactivada por defecto y `.env.example` no la contiene.

## Datos de demostración

Con `Seed__DemoUsers=true` y `Seed__DemoData=true`, la API crea al arrancar (tras las migraciones y la siembra de usuarios) unos 30 días de fichajes inventados, distintos para cada usuario de prueba, con trabajo, pausas y pausas de comida. Solo cuenta los días hasta **ayer**: hoy queda libre para que fiche en vivo. Es determinista (sin azar no controlado) e idempotente por día y empleado: si un empleado ya tiene algún intervalo un día, ese día se omite, de modo que reiniciar la API no duplica nada y cada arranque añade solo los días nuevos. Fines de semana libres y uno o dos días sin fichajes por persona. Los horarios son de la hora local de España, convertidos a UTC con la regla de cambio de hora de la UE. En el registro solo aparecen contadores.

| Usuario | Perfil |
|---|---|
| `marta.responsable` | Jornada regular 09:00-17:30 con reuniones (pausas de duración y presencia variables) y comida de unos 45 min. |
| `luis.empleado` | Madrugador 08:00-17:00, comida de 60 min y una pausa de unos 15 min casi todos los días. |
| `ana.empleada` | 09:30-18:30, comida larga (~75 min) y alguna pausa de tarde; los miércoles teletrabaja (09:00-17:00) y registra la comida como pausa normal, sin comida. |
| `pablo.empleado` | Media jornada 08:00-14:00 con una sola pausa corta y sin comida. |

Para apagarla basta con `Seed__DemoData=false` (o quitar la variable): no se borran los intervalos ya creados, que son datos normales. Sin ventana, el listado de fichajes devuelve hasta 300 intervalos ([ADR 0015](docs/adr/0015-datos-de-demostracion-de-un-mes.md)); la Web pide siempre un mes concreto ([ADR 0016](docs/adr/0016-vista-mensual-y-hora-local.md)). `.env.example` no incluye esta clave a propósito; se documenta solo aquí. Con Compose, añada `Seed__DemoData=true` a su `.env` local (Compose lo pasa a la API junto con el resto del archivo). Si ejecuta varias instancias de la API a la vez, no active la siembra en todas: podrían duplicar días.

## Estructura

```
src/
  TimeTracker.Domain          Entidades y objetos de valor (TimeEntry, WorkedHours, UserAccount). Sin dependencias.
  TimeTracker.Application     Casos de uso (fichar, listar, iniciar sesión, equipo) y puertos. Depende de Domain.
  TimeTracker.Infrastructure  EF Core, migraciones, repositorios PostgreSQL y asistente de IA (herramientas, bucle, proveedores). Depende de Application.
  TimeTracker.Api             Endpoints, autenticación JWT, autorización y composición. Depende de Application e Infrastructure.
  TimeTracker.Web             Blazor WebAssembly (inicio con el reproductor de fichaje, /login, /fichajes y /equipo), independiente.
tests/                        Pruebas de Domain, Application, Web (formato, sesión, hora local, meses y franjas), Api (integración con PostgreSQL) y de arquitectura.
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
13. [Despliegue de la demostración en Railway](docs/adr/0013-despliegue-en-railway.md)
14. [Tipos de marcaje: pausas y pausas de comida](docs/adr/0014-tipos-de-marcaje-pausas-y-comida.md)
15. [Datos de demostración de un mes](docs/adr/0015-datos-de-demostracion-de-un-mes.md)
16. [Vista mensual de fichajes y hora local en la Web](docs/adr/0016-vista-mensual-y-hora-local.md)
17. [Asistente de IA con herramientas de solo lectura](docs/adr/0017-asistente-ia-con-herramientas-de-solo-lectura.md)
18. [Configuración del asistente por el responsable](docs/adr/0018-configuracion-del-asistente-por-el-responsable.md)

## Integración continua

El flujo `.github/workflows/ci.yml` se ejecuta en GitHub Actions en cada `push` a `main` y en cada pull request: restaura, comprueba el formato, compila, prueba con cobertura (Domain al 100 % y Application al 80 %), audita paquetes vulnerables, publica y construye la imagen (sin subirla). Sus pasos pueden reproducirse en local con los comandos de «Compilar» y «Probar».

## Enlaces y datos de demostración

- Web (demostración): <https://web-production-a744a.up.railway.app>
- API (demostración): <https://api-production-0f339.up.railway.app> (comprobación: `/health`). Véase «Despliegue en Railway».
- Vídeo de demostración: **pendiente**.
- Presentación (slides): **pendiente**.
- Usuarios de prueba: véase «Usuarios de prueba» (con la siembra activada, como en la demostración desplegada).

## Convenciones

Las reglas de trabajo (SDD, TDD, arquitectura, seguridad) están en [AGENTS.md](AGENTS.md). Documentación y textos de usuario en español; identificadores de código en inglés.
