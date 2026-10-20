# Especificación: startup-configuration

## Purpose

Define cómo `TimeTracker.Api` obtiene y valida su configuración: variables de entorno, opciones tipadas con validación al arrancar (fail fast) y CORS restrictivo. Especificación vigente; origen en los cambios archivados de la API y actualizada por `asistente-config-responsable` (archivado el 2026-10-20).

Convención (a confirmar en diseño): la clave `Cors:AllowedOrigins` se suministra por entorno como `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, etc.

Actualizada por `asistente-config-responsable`: 2 requisitos MODIFIED (arranque con la configuración del asistente y claves nuevas en `.env.example`).

## ADDED Requirements

### Requirement: Configuración por variables de entorno

La API DEBE leer su configuración de variables de entorno en todos los entornos. NO DEBE haber valores de configuración sensibles ni específicos de entorno en el código, en el repositorio ni en la imagen Docker.

#### Scenario: Valor tomado del entorno
- DADO `Cors__AllowedOrigins__0=https://app.example.test` definida en el entorno
- CUANDO la API arranca
- ENTONCES las opciones tipadas contienen ese origen

#### Scenario: Ningún secreto versionado
- DADO el árbol de archivos del proyecto
- CUANDO se revisan `.gitignore`, `.dockerignore` y el contenido de `appsettings*.json`
- ENTONCES `.env` figura en ambos ignore y `appsettings*.json` no contiene valores de `Cors:AllowedOrigins` ni claves `ConnectionStrings`, `Jwt`, `Seed` o `Ai`, ni otros secretos

#### Scenario: Clave de IA fuera de la imagen
- DADO la imagen Docker construida
- CUANDO se inspeccionan `docker history` y `docker inspect`
- ENTONCES no aparece ningún valor de `Ai__ApiKey`

### Requirement: Opciones tipadas validadas al arrancar

La API DEBE enlazar `Cors:AllowedOrigins` a una clase de opciones tipada y validarla con `ValidateOnStart()`. Si es inválida, el proceso NO DEBE arrancar ni atender peticiones.

#### Scenario: Falta la clave
- DADO un entorno sin `Cors:AllowedOrigins`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con una excepción de validación y no se abre ningún puerto

#### Scenario: Lista vacía
- DADO `Cors:AllowedOrigins` definida sin ningún elemento
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con una excepción de validación

### Requirement: CORS restrictivo sin comodines

Cada origen de `Cors:AllowedOrigins` DEBE ser una URL absoluta `http` o `https` sin ruta, consulta ni fragmento. NO DEBE aceptarse `*` ni valores en blanco. La API DEBE permitir solo esos orígenes.

#### Scenario: Comodín rechazado
- DADO `Cors__AllowedOrigins__0=*`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla

#### Scenario: Origen con formato inválido
- DADO `Cors__AllowedOrigins__0=app.example.test/ruta`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla

#### Scenario: Origen permitido y no permitido
- DADO `Cors__AllowedOrigins__0=https://app.example.test`
- CUANDO se envía una petición con `Origin: https://app.example.test` y otra con `Origin: https://otro.example.test`
- ENTONCES solo la primera recibe `Access-Control-Allow-Origin`

### Requirement: Mensajes de error sin secretos

Cuando el arranque falle por configuración, el mensaje DEBE nombrar la clave incorrecta (por ejemplo `Cors:AllowedOrigins`) y la razón. NO DEBE imprimir valores de claves marcadas como secretas, ni trazas con valores de configuración.

#### Scenario: El mensaje nombra la clave
- DADO un entorno sin `Cors:AllowedOrigins`
- CUANDO la API falla al arrancar
- ENTONCES la excepción o la salida contiene `Cors:AllowedOrigins`

#### Scenario: Prueba de fallo al arrancar
- DADO una prueba en `TimeTracker.Api.IntegrationTests` que crea la API sin `Cors:AllowedOrigins`
- CUANDO se ejecuta `dotnet test`
- ENTONCES la prueba comprueba el fallo y la presencia del nombre de la clave, y pasa

### Requirement: Documentación de claves en `.env.example`

`.env.example` DEBE listar cada clave de entorno que la API y `docker-compose.yml` consumen, incluida la del puerto, con valor vacío o un marcador claramente ficticio: `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`, `ConnectionStrings__Default`, `Database__ApplyMigrationsOnStartup`, `Jwt__SigningKey`, `Seed__DemoUsers` y `Seed__DemoPassword`, además de las existentes. `Jwt__SigningKey` DEBE figurar con valor vacío (con un comentario que indique cómo generarla). `Seed__DemoUsers` DEBE valer `false`. `Seed__DemoPassword` DEBE figurar con valor vacío (activa, no comentada) y un comentario de que solo se usa en local, que debe tener al menos 12 caracteres y que su valor de demostración se documenta únicamente en el README. Las nueve claves del asistente `Ai__Provider`, `Ai__Model`, `Ai__Endpoint`, `Ai__ApiKey`, `Ai__MaxOutputTokens`, `Ai__MaxToolIterations`, `Ai__TimeoutSeconds`, `Ai__RequestsPerMinute` y `Ai__DailyRequestsPerUser` DEBEN documentarse en `.env.example`: `Ai__Provider` y `Ai__ApiKey` como líneas activas con valor vacío (asistente deshabilitado por defecto) y las demás como líneas comentadas (`#`), con `Ai__Endpoint` de ejemplo `http://host.docker.internal:11434/` y los límites con sus valores por defecto (800, 5, 25, 5 y 50). Las claves nuevas `Ai__Effort`, `Ai__AllowedProviders`, `Ai__MasterKeys__1`, `Ai__ActiveMasterKeyVersion` y `Ai__SettingsPollSeconds` TAMBIÉN DEBEN documentarse como líneas comentadas (`#`) y sin valores reales; en particular, `.env.example` NO DEBE contener ninguna clave maestra con valor, y su comentario DEBE indicar que se genera con 32 bytes aleatorios en Base64. DEBE haber un comentario que explique que sin `Ai__Provider` el asistente queda deshabilitado, los valores admitidos de `Ai__Provider` (`Anthropic`, `OpenAI`, `Gemini`, `Ollama`) y que `Ai__ApiKey` es un secreto que nunca se escribe en el archivo. NO DEBE contener `Identity__DevelopmentEmployeeId`, ni valores reales (contraseñas, cadenas de conexión, claves de firma, claves de API, claves maestras). La clave del puerto PUEDE figurar comentada o sin definir, porque un valor vacío anularía el puerto que ya fija la imagen.

#### Scenario: Claves documentadas
- DADO `.env.example`
- CUANDO se compara con las claves usadas por la API y por `docker-compose.yml`
- ENTONCES cada clave consumida aparece en el archivo (la del puerto, al menos comentada) y ninguna tiene un valor real ni un valor vacío activo que anule el puerto de la imagen

#### Scenario: Claves de autenticación presentes
- DADO `.env.example`
- CUANDO se buscan `Jwt__SigningKey`, `Seed__DemoUsers` y `Seed__DemoPassword`
- ENTONCES las tres aparecen, `Jwt__SigningKey` y `Seed__DemoPassword` están vacías (sin contraseña de demostración escrita), `Seed__DemoUsers` vale `false` y `Seed__DemoPassword` lleva un comentario de uso solo local, mínimo de 12 caracteres y referencia al README

#### Scenario: Sin identidad provisional
- DADO `.env.example`
- CUANDO se busca `Identity__DevelopmentEmployeeId`
- ENTONCES no aparece

#### Scenario: Valores de las claves de base de datos
- DADO `.env.example`
- CUANDO se revisan `POSTGRES_PASSWORD`, `ConnectionStrings__Default` y `Database__ApplyMigrationsOnStartup`
- ENTONCES los dos primeros están vacíos o son marcadores ficticios (por ejemplo `cambiar-esto`) y el tercero vale `false`

#### Scenario: Claves de IA documentadas
- DADO `.env.example`
- CUANDO se buscan las nueve claves `Ai__*` originales
- ENTONCES las nueve aparecen; `Ai__Provider=` y `Ai__ApiKey=` están activas y con valor vacío, las otras siete llevan `#` inicial y ninguna lleva un valor real

#### Scenario: Claves nuevas de IA documentadas
- DADO `.env.example`
- CUANDO se buscan `Ai__Effort`, `Ai__AllowedProviders`, `Ai__MasterKeys__1`, `Ai__ActiveMasterKeyVersion` y `Ai__SettingsPollSeconds`
- ENTONCES las cinco aparecen con `#` inicial, ninguna lleva un valor real y no existe ninguna línea activa con una clave maestra con valor

#### Scenario: Entrega sin clave de API
- DADO `.env.example`
- CUANDO se lee `Ai__ApiKey`
- ENTONCES su valor está vacío (comprobado por `DeliveryArtifactsTests`)

#### Scenario: Nota del asistente deshabilitado
- DADO `.env.example`
- CUANDO se leen los comentarios de las claves `Ai__*` (`Ai__Provider` y `Ai__ApiKey` activas pero vacías)
- ENTONCES indican que sin `Ai__Provider` el asistente queda deshabilitado, los valores `Anthropic`, `OpenAI`, `Gemini` y `Ollama`, y que la clave de API es un secreto que no se escribe en el archivo

#### Scenario: Copiar el archivo no activa el asistente
- DADO un `.env` creado copiando `.env.example` sin editar las claves `Ai__*`
- CUANDO la API arranca
- ENTONCES arranca con el asistente deshabilitado

### Requirement: Puerto configurable por entorno

El puerto de escucha de la API DEBE poder fijarse con una variable de entorno estándar de ASP.NET Core (a confirmar en diseño, p. ej. `ASPNETCORE_HTTP_PORTS`), sin recompilar ni cambiar archivos.

#### Scenario: Puerto distinto
- DADO la variable de puerto definida con `8081`, `Cors__AllowedOrigins__0`, `ConnectionStrings__Default` (puede apuntar a un servidor inexistente) y `Jwt__SigningKey` (32 caracteres o más) válidos, y la API arrancada fuera de `WebApplicationFactory` (con `dotnet run --no-launch-profile`, que evita la prioridad de `launchSettings.json`, o en un contenedor con `docker run -e`)
- CUANDO la API arranca
- ENTONCES `GET http://localhost:8081/health` responde `200`

Nota de verificación: el puerto NO es comprobable con `WebApplicationFactory` (servidor en memoria); se verifica una vez de forma manual y la evidencia se registra en `verify-report.md`, sin prueba automática.

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

### Requirement: Clave de firma del JWT obligatoria y validada al arrancar

La API DEBE exigir `Jwt:SigningKey` (variable `Jwt__SigningKey`) y validarla con `ValidateOnStart()`: si falta, está en blanco o mide menos de 32 bytes en UTF-8, el proceso NO DEBE arrancar. El mensaje DEBE estar en español, nombrar la clave `Jwt:SigningKey` y la razón, y NO DEBE imprimir su valor. La clave NO DEBE figurar en `appsettings*.json`, en el código ni en la imagen Docker.

#### Scenario: Falta la clave
- DADO un entorno con `Cors:AllowedOrigins` y `ConnectionStrings:Default` válidos y sin `Jwt:SigningKey`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con una excepción de validación cuyo mensaje contiene `Jwt:SigningKey`

#### Scenario: Clave en blanco
- DADO `Jwt__SigningKey` definida solo con espacios
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con la misma excepción de validación

#### Scenario: Clave demasiado corta
- DADO `Jwt__SigningKey` de 31 caracteres ASCII (31 bytes)
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Jwt:SigningKey`

#### Scenario: Clave en el límite
- DADO `Jwt__SigningKey` de 32 caracteres ASCII, y otra de 16 caracteres `ñ` (32 bytes en UTF-8)
- CUANDO la API arranca con cada una
- ENTONCES arranca y `GET /health` responde `200`

#### Scenario: El valor no se imprime
- DADO `Jwt__SigningKey` = `corta-secreta-prueba` (demasiado corta)
- CUANDO la API falla al arrancar
- ENTONCES la excepción (y sus internas) no contiene `corta-secreta-prueba`

#### Scenario: No versionada
- DADO los archivos `appsettings*.json`
- CUANDO se buscan claves `Jwt`
- ENTONCES no existe ninguna

### Requirement: Siembra de demostración configurada y validada

La API DEBE leer `Seed:DemoUsers` (variable `Seed__DemoUsers`) y `Seed:DemoData` (variable `Seed__DemoData`) como booleanos con valor `false` si no se definen; un valor no booleano DEBE impedir el arranque con un mensaje que nombre la clave afectada. `Seed:DemoData` = `true` exige `Seed:DemoUsers` = `true`: con la combinación inversa el arranque falla con un mensaje en español que nombra `Seed:DemoData`. Cuando `Seed:DemoUsers` sea `true`, DEBE exigir `Seed:DemoPassword` (variable `Seed__DemoPassword`) con al menos 12 caracteres, validada con `ValidateOnStart()`; si falta o es más corta, el arranque falla con un mensaje en español que nombre `Seed:DemoPassword` sin imprimir su valor. Cuando `Seed:DemoUsers` sea `false`, `Seed:DemoPassword` NO se exige ni se valida. Ninguna de las tres claves DEBE figurar en `appsettings*.json`. El valor de demostración de `Seed:DemoPassword` NO DEBE figurar en `.env.example` (vacío) ni en `docker-compose.yml`; solo se documenta en el README, y `Seed__DemoData` también solo en el README. El efecto de la siembra se especifica en `team-access` (usuarios) y `demo-data` (fichajes).

#### Scenario: Valor por defecto
- DADO un entorno sin `Seed:DemoUsers` ni `Seed:DemoData`
- CUANDO se enlazan las opciones
- ENTONCES ambos valores son `false` y la API arranca sin `Seed:DemoPassword`

#### Scenario: Valor no booleano
- DADO `Seed__DemoUsers` = `quizas`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Seed:DemoUsers`

#### Scenario: Datos demo con valor no booleano
- DADO `Seed__DemoData` = `quizas`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Seed:DemoData`

#### Scenario: Datos demo sin usuarios demo
- DADO `Seed__DemoData` = `true` y `Seed__DemoUsers` = `false` (o sin definir)
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Seed:DemoData` y `Seed:DemoUsers`

#### Scenario: Datos demo con usuarios demo
- DADO `Seed__DemoData` = `true`, `Seed__DemoUsers` = `true` y una contraseña válida
- CUANDO la API arranca con la base migrada
- ENTONCES arranca y `GET /health` responde `200`

#### Scenario: Siembra activa sin contraseña
- DADO `Seed__DemoUsers` = `true` y sin `Seed:DemoPassword`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Seed:DemoPassword`

#### Scenario: Contraseña de demostración demasiado corta
- DADO `Seed__DemoUsers` = `true` y `Seed__DemoPassword` de 11 caracteres
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Seed:DemoPassword`

#### Scenario: Contraseña de demostración en el límite
- DADO `Seed__DemoUsers` = `true` y `Seed__DemoPassword` de 12 caracteres
- CUANDO la API arranca con la base migrada
- ENTONCES arranca y `GET /health` responde `200`

#### Scenario: Siembra desactivada ignora la contraseña
- DADO `Seed__DemoUsers` = `false` y `Seed__DemoPassword` de 3 caracteres
- CUANDO la API arranca
- ENTONCES arranca sin error

#### Scenario: El valor no se imprime
- DADO `Seed__DemoUsers` = `true` y `Seed__DemoPassword` = `corta-demo` (10 caracteres)
- CUANDO la API falla al arrancar
- ENTONCES la excepción (y sus internas) no contiene `corta-demo`

### Requirement: Arranque sin identidad provisional

La API NO DEBE leer, validar ni requerir `Identity:DevelopmentEmployeeId` en ningún entorno. En `Development` y en `Production`, con `Cors:AllowedOrigins`, `ConnectionStrings:Default` y `Jwt:SigningKey` válidos, DEBE arrancar. La identidad del usuario sale solo del token (ver `authentication`).

#### Scenario: Production con configuración completa
- DADO el entorno `Production` con `Cors__AllowedOrigins__0`, `ConnectionStrings__Default` (puede apuntar a un servidor inexistente) y `Jwt__SigningKey` válidos
- CUANDO la API arranca
- ENTONCES arranca y `GET /health` responde `200`

#### Scenario: Development con configuración completa
- DADO el entorno `Development` con las tres claves válidas y sin `Identity__DevelopmentEmployeeId`
- CUANDO la API arranca
- ENTONCES arranca y `GET /health` responde `200`

#### Scenario: Una clave Identity heredada no tiene efecto
- DADO `Identity__DevelopmentEmployeeId` = un GUID válido en el entorno
- CUANDO la API arranca y se envía `GET /api/time-entries` sin token
- ENTONCES arranca y la petición recibe `401` (no se usa ninguna identidad por configuración)

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

### Requirement: Cabeceras de proxy opcionales (despliegue tras un proxy)

La API DEBE leer `ForwardedHeaders:Enabled` (variable `ForwardedHeaders__Enabled`) como booleano con valor `false` si no se define. Con `false`, `X-Forwarded-For` y `X-Forwarded-Proto` NO DEBEN alterar la dirección ni el esquema de la petición. Con `true`, la API DEBE tratarlos como añadidos por proxies de confianza, en el número de saltos de `ForwardedHeaders:Hops` (por defecto `2`; debe ser al menos `1`), de modo que el límite de intentos de login use la dirección del cliente y no la del proxy, y lo que el cliente anteponga a la izquierda NO DEBE contar. Solo debe activarse cuando la API únicamente es alcanzable a través de ese proxy (véase el ADR 0013).

#### Scenario: Desactivado por defecto
- DADO `ForwardedHeaders:Enabled` sin definir
- CUANDO once peticiones de login llegan con `X-Forwarded-For` distintos
- ENTONCES la undécima recibe `429` (todas comparten la dirección de la conexión)

#### Scenario: Clientes distintos tras el proxy
- DADO `ForwardedHeaders:Enabled` = `true`
- CUANDO once peticiones de login llegan con `X-Forwarded-For: <cliente distinto>, <borde>`
- ENTONCES ninguna recibe `429`

#### Scenario: Dirección antepuesta por el cliente
- DADO `ForwardedHeaders:Enabled` = `true`
- CUANDO once peticiones llegan con `X-Forwarded-For: <inventada distinta>, <mismo cliente>, <borde>`
- ENTONCES la undécima recibe `429`

### Requirement: Arranque con la configuración del asistente

La API DEBE validar las claves `Ai:*` con `ValidateOnStart()` y mismo patrón que `Jwt:SigningKey`. Sin `Ai:Provider` (ausente, `None` o en blanco) DEBE arrancar sin exigir ninguna otra clave `Ai:*`, salvo que `Ai:AllowedProviders` esté definida con un nombre desconocido, que sigue siendo un error de arranque (ver `assistant-configuration`). Con `Ai:Provider` = `Anthropic`, `OpenAI`, `Gemini` u `Ollama` y una clave `Ai:*` incorrecta o ausente según `assistant-configuration`, el proceso NO DEBE arrancar ni atender peticiones. La ausencia o invalidez de las claves maestras NO DEBE impedir el arranque. El mensaje DEBE estar en español, nombrar la clave y la razón, y NO DEBE imprimir sus valores. Si varias validaciones fallan a la vez, la excepción PUEDE ser un `AggregateException`.

#### Scenario: Sin proveedor arranca
- DADO el entorno con `Cors:AllowedOrigins`, `ConnectionStrings:Default` y `Jwt:SigningKey` válidos y sin claves `Ai__*`
- CUANDO la API arranca
- ENTONCES arranca y `GET /health` responde `200`

#### Scenario: Proveedor activo con clave inválida
- DADO `Ai__Provider=Anthropic` sin `Ai:ApiKey`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con una excepción de validación cuyo mensaje contiene `Ai:ApiKey` y no se abre ningún puerto

#### Scenario: Proveedor activo válido
- DADO `Ai__Provider=Ollama`, `Ai__Endpoint=http://localhost:11434/` y `Ai__Model` válidos
- CUANDO la API arranca
- ENTONCES arranca y `GET /health` responde `200` aunque no haya servidor de Ollama accesible

#### Scenario: Proveedor en la nube nuevo válido
- DADO `Ai__Provider=Gemini` (y otro caso con `OpenAI`), `Ai__ApiKey` y `Ai__Model` válidos
- CUANDO la API arranca
- ENTONCES arranca y `GET /health` responde `200` sin conectar con el proveedor

#### Scenario: Proveedor desconocido
- DADO `Ai__Provider=GitHubModels`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Ai:Provider`

#### Scenario: Sin clave maestra arranca
- DADO `Ai__Provider=Ollama` válido y sin `Ai__MasterKeys__*` ni `Ai__ActiveMasterKeyVersion`
- CUANDO la API arranca
- ENTONCES arranca y `GET /health` responde `200`

#### Scenario: El valor no se imprime
- DADO `Ai__Provider=Anthropic`, `Ai__ApiKey` = `clave-secreta-prueba` y un `Ai:MaxOutputTokens` inválido
- CUANDO la API falla al arrancar
- ENTONCES la excepción (y sus internas) contiene `Ai:MaxOutputTokens` y no contiene `clave-secreta-prueba`

#### Scenario: Las demás validaciones no cambian
- DADO `Ai__Provider` sin definir y `Jwt__SigningKey` ausente
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Jwt:SigningKey`
