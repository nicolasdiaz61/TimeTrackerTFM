# ADR 0032: Configuración de desarrollo en appsettings.Development.json

- Estado: Aceptado
- Fecha: 2026-10-24
- Sustituye parcialmente a: ADR 0007, solo en la regla «appsettings*.json no contiene orígenes ni secretos» para Development

## Contexto

El [ADR 0007](0007-configuracion-y-validacion-al-arrancar.md) exige que toda la configuración llegue por variables de entorno y que `appsettings*.json` no contenga orígenes ni secretos. En la práctica, `dotnet run --launch-profile http` no arrancaba sin definir antes siete variables en la sesión, lo que frena el primer arranque de quien clona el repositorio. Los valores que hacen falta en local (origen de la Web, base de datos en `localhost`, usuarios de demostración) son de un entorno local desechable, no secretos reales.

## Decisión

- `src/TimeTracker.Api/appsettings.Development.json` lleva la configuración de desarrollo: `Cors:AllowedOrigins` con `http://localhost:5172`, `ConnectionStrings:Default` hacia `localhost:5432`, `Database:ApplyMigrationsOnStartup`, `Seed:DemoUsers`, `Seed:DemoData`, `Seed:DemoPassword` y `Jwt:SigningKey`. Solo se carga con `ASPNETCORE_ENVIRONMENT=Development`; Railway usa `Production` y nunca lo lee.
- `appsettings.json` (base) sigue sin ningún valor de entorno ni secreto, y `.env.example`, el `Dockerfile`, `railway.json` y el runbook de Railway no contienen la clave de desarrollo. Una prueba de arquitectura fija esa frontera.
- La base de datos local usa `timetracker`/`timetracker` con una contraseña de desarrollo; los valores por defecto de `POSTGRES_DB`, `POSTGRES_USER` y `POSTGRES_PASSWORD` en `docker-compose.yml` coinciden con ellos.
- El registro de compañías sigue cerrado (`Registration:Enabled` no se define). Las variables de entorno tienen prioridad sobre el JSON, así que se abre con `Registration__Enabled=true` y `Seed__DemoUsers=false` desde la sesión.
- Salvaguarda: `JwtOptionsValidator` hace fallar el arranque si el entorno no es Development y `Jwt:SigningKey` es exactamente `DevelopmentSigningKey.Value`. El mensaje nombra la clave y no imprime su valor.

## Consecuencias

- Con la base de datos levantada (`docker compose up -d postgres`), la API arranca en Development sin variables de entorno.
- Docker Compose usa la misma configuración: la API del contenedor corre con `ASPNETCORE_ENVIRONMENT=Development`, y la imagen incluye `appsettings.Development.json` (el `Dockerfile` copia el proyecto entero y `.dockerignore` no lo excluye). `docker compose up --build` basta, sin crear ni rellenar un `.env`.
- Por eso `docker-compose.yml` ya no define la clave de firma ni la siembra, y no lleva la clave de desarrollo ni la contraseña de demostración. Sus únicos literales son los valores por defecto de `POSTGRES_DB`, `POSTGRES_USER` y `POSTGRES_PASSWORD` (`${VAR:-valor}`), credenciales de la base local que una prueba obliga a coincidir con la cadena de conexión del JSON. La cadena de la API en el contenedor usa `Host=postgres`.
- El `.env` pasa a ser opcional (`env_file` con `required: false`, Compose 2.24 o superior) y, si existe, sobrescribe el JSON. Como `environment` prevalece sobre `env_file`, Compose no fija en `environment` nada que deba poder sobrescribirse (`Jwt__*`, `Seed__*`, `Registration__*`); `.env.example` lleva esas claves comentadas, porque una clave activa y vacía en el `.env` anularía el valor del JSON.
- Los valores versionados son públicos: sirven solo para un entorno local desechable. Quien exponga una instancia de Development a una red no confiable asume que su clave de firma y la contraseña de los usuarios de demostración son conocidas.
- Si alguien copia la clave de desarrollo a Production, la API no arranca.
- Cambiar los valores de desarrollo exige editar el JSON y, si cambian la base de datos o la clave, la constante y los valores por defecto de `docker-compose.yml`.
- El ADR 0007 no se edita (ADR 0001); la sustitución parcial se declara aquí y en el índice del README.

## Alternativas

- **Mantener solo variables de entorno (ADR 0007):** obliga a definir siete variables en cada sesión; descartada para Development.
- **User secrets (`dotnet user-secrets`):** no se versionan, pero añaden un paso manual por máquina y no funcionan con Compose.
- **Un archivo `.env` leído por la API:** no es una convención de .NET y duplicaría la fuente de configuración.
- **Generar la clave al primer arranque:** firmaría con una clave distinta en cada reinicio e invalidaría las sesiones.
