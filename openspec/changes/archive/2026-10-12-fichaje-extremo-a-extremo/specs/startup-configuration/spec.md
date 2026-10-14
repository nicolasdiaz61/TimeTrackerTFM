# Delta para startup-configuration

Cambio: `fichaje-extremo-a-extremo`. Añade las claves `ConnectionStrings:Default`, `Identity:DevelopmentEmployeeId` y `Database:ApplyMigrationsOnStartup`, y actualiza los requisitos vigentes de `.env.example` y de puerto (la política CORS no cambia: ya admite `GET` y `POST`).

## ADDED Requirements

### Requirement: Cadena de conexión obligatoria validada al arrancar

La API DEBE exigir `ConnectionStrings:Default` (variable `ConnectionStrings__Default`) y validarla con `ValidateOnStart()`: si falta, está en blanco o tiene un formato que `NpgsqlConnectionStringBuilder` no puede analizar, el proceso NO DEBE arrancar. El mensaje DEBE estar en español, nombrar la clave `ConnectionStrings:Default` y NO DEBE imprimir su valor ni el mensaje original del analizador. La clave NO DEBE figurar en `appsettings*.json`. Si varias validaciones fallan a la vez, la excepción PUEDE ser un `AggregateException`; los escenarios se evalúan sobre sus excepciones internas.

#### Scenario: Falta la cadena
- DADO un entorno con `Cors:AllowedOrigins` válido y sin `ConnectionStrings:Default`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con una excepción de validación cuyo mensaje contiene `ConnectionStrings:Default`

#### Scenario: Cadena en blanco
- DADO `ConnectionStrings__Default` definida solo con espacios
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con la misma excepción de validación

#### Scenario: Formato inválido
- DADO `ConnectionStrings__Default` = `Host=db.invalid;Password=secreto-prueba;;=;Puerto-roto`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con una excepción de validación cuyo mensaje contiene `ConnectionStrings:Default`

#### Scenario: El valor no se imprime
- DADO `ConnectionStrings__Default` con formato inválido que contiene la contraseña ficticia `secreto-prueba`
- CUANDO la API falla al arrancar
- ENTONCES la excepción (y sus internas) no contiene `secreto-prueba`

#### Scenario: No versionada
- DADO los archivos `appsettings*.json`
- CUANDO se buscan claves `ConnectionStrings`
- ENTONCES no existe ninguna

### Requirement: Identidad provisional solo en Development

En el entorno `Development`, la API DEBE exigir `Identity:DevelopmentEmployeeId` (variable `Identity__DevelopmentEmployeeId`) con un GUID válido distinto de `Guid.Empty`, validado con `ValidateOnStart()`, y registrar con él la implementación de `ICurrentUser`. El validador DEBE estar registrado siempre. Fuera de `Development`, la API NO DEBE registrar ninguna implementación provisional: el arranque DEBE fallar mientras no exista una identidad real, con un mensaje en español que nombre `Identity`, aunque `Identity:DevelopmentEmployeeId` esté definida.

#### Scenario: Development con identidad válida
- DADO el entorno `Development` con `Identity__DevelopmentEmployeeId` = un GUID válido y el resto de la configuración correcta
- CUANDO la API arranca
- ENTONCES arranca y `GET /health` responde `200`

#### Scenario: Development sin identidad
- DADO el entorno `Development` sin `Identity:DevelopmentEmployeeId`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Identity:DevelopmentEmployeeId`

#### Scenario: Identidad con formato inválido
- DADO `Identity__DevelopmentEmployeeId` = `no-es-un-guid` o `00000000-0000-0000-0000-000000000000`
- CUANDO la API intenta arrancar en `Development`
- ENTONCES el arranque falla con un mensaje que contiene `Identity:DevelopmentEmployeeId`

#### Scenario: Fuera de Development
- DADO el entorno `Production` con configuración completa, incluida `Identity__DevelopmentEmployeeId`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje en español que contiene `Identity` y no se abre ningún puerto

### Requirement: Bandera de migraciones al arrancar

La API DEBE leer `Database:ApplyMigrationsOnStartup` (variable `Database__ApplyMigrationsOnStartup`) como booleano con valor `false` si no se define. Un valor no booleano DEBE impedir el arranque con un mensaje que nombre la clave `Database:ApplyMigrationsOnStartup`. El efecto sobre el esquema se especifica en `time-entries-persistence`.

#### Scenario: Valor por defecto
- DADO un entorno sin `Database:ApplyMigrationsOnStartup`
- CUANDO se enlazan las opciones
- ENTONCES el valor es `false`

#### Scenario: Valor no booleano
- DADO `Database__ApplyMigrationsOnStartup` = `quizas`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Database:ApplyMigrationsOnStartup`

## MODIFIED Requirements

### Requirement: Documentación de claves en `.env.example`

`.env.example` DEBE listar cada clave de entorno que la API y `docker-compose.yml` consumen, incluida la del puerto, con valor vacío o un marcador claramente ficticio (el GUID de `Identity__DevelopmentEmployeeId` se deja vacío o con un comentario que indica que debe inventarse): `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`, `ConnectionStrings__Default`, `Identity__DevelopmentEmployeeId` y `Database__ApplyMigrationsOnStartup`, además de las existentes. La clave del puerto PUEDE figurar comentada o sin definir, porque un valor vacío anularía el puerto que ya fija la imagen. NO DEBE contener valores reales (contraseñas, cadenas de conexión ni identificadores reales). Las claves `Ai:*` PUEDEN figurar solo como comentario.
(Previously: sin las claves de PostgreSQL, conexión, identidad ni migraciones)

#### Scenario: Claves documentadas
- DADO `.env.example`
- CUANDO se compara con las claves usadas por la API y por `docker-compose.yml`
- ENTONCES cada clave consumida aparece en el archivo (la del puerto, al menos comentada) y ninguna tiene un valor real ni un valor vacío activo que anule el puerto de la imagen

#### Scenario: Claves nuevas presentes
- DADO `.env.example`
- CUANDO se buscan las seis claves nuevas
- ENTONCES las seis aparecen, y los valores de contraseña, cadena de conexión e identidad están vacíos o son marcadores ficticios (por ejemplo `cambiar-esto`), y `Database__ApplyMigrationsOnStartup` vale `false`

#### Scenario: Sin claves de IA activas
- DADO `.env.example`
- CUANDO se buscan líneas `Ai__*` sin `#` inicial
- ENTONCES no existe ninguna

### Requirement: Puerto configurable por entorno

El puerto de escucha de la API DEBE poder fijarse con una variable de entorno estándar de ASP.NET Core (a confirmar en diseño, p. ej. `ASPNETCORE_HTTP_PORTS`), sin recompilar ni cambiar archivos.
(Previously: igual; cambia solo el escenario, que ahora necesita el resto de claves obligatorias)

#### Scenario: Puerto distinto
- DADO la variable de puerto definida con `8081`, `ASPNETCORE_ENVIRONMENT=Development`, `Cors__AllowedOrigins__0`, `ConnectionStrings__Default` (puede apuntar a un servidor inexistente) e `Identity__DevelopmentEmployeeId` válidos, y la API arrancada fuera de `WebApplicationFactory` (con `dotnet run --no-launch-profile`, que evita la prioridad de `launchSettings.json`, o en un contenedor con `docker run -e`)
- CUANDO la API arranca
- ENTONCES `GET http://localhost:8081/health` responde `200`

Nota de verificación: el puerto NO es comprobable con `WebApplicationFactory` (servidor en memoria); se verifica una vez de forma manual y la evidencia se registra en `tasks.md` / `verify-report.md`, sin prueba automática.
