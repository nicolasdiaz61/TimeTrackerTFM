# Delta para startup-configuration

Cambio: `autenticacion-y-roles`. Añade las claves `Jwt:SigningKey`, `Seed:DemoUsers` y `Seed:DemoPassword`, retira la identidad provisional (`Identity:DevelopmentEmployeeId`) y actualiza los requisitos vigentes de variables de entorno, `.env.example` y puerto. La política CORS no cambia.

> Aviso de delta destructivo: se ELIMINA el requisito «Identidad provisional solo en Development». Desde este cambio la API arranca en `Production` con configuración completa (ya no falla por falta de identidad real). La validación de `Jwt:SigningKey` aquí definida es la que cita `authentication`.

Notas de verificación:
- El puerto NO es comprobable con `WebApplicationFactory` (servidor en memoria); se verifica una vez de forma manual y la evidencia se registra en `verify-report.md`.
- Los fallos de arranque se evalúan sobre la excepción de validación y sus internas (puede ser un `AggregateException`).
- Los límites (32 bytes de la clave; 12 caracteres de la contraseña de demostración) pueden pasar a la primera; DEBEN registrarse como verdes a la primera con su rojo demostrado por mutación o justificado.

## ADDED Requirements

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

La API DEBE leer `Seed:DemoUsers` (variable `Seed__DemoUsers`) como booleano con valor `false` si no se define; un valor no booleano DEBE impedir el arranque con un mensaje que nombre `Seed:DemoUsers`. Cuando sea `true`, DEBE exigir `Seed:DemoPassword` (variable `Seed__DemoPassword`) con al menos 12 caracteres, validada con `ValidateOnStart()`; si falta o es más corta, el arranque falla con un mensaje en español que nombre `Seed:DemoPassword` sin imprimir su valor. Cuando sea `false`, `Seed:DemoPassword` NO se exige ni se valida. Ninguna de las dos claves DEBE figurar en `appsettings*.json`. El valor de demostración de `Seed:DemoPassword` NO DEBE figurar en `.env.example` (vacío) ni en `docker-compose.yml`; solo se documenta en el README. El efecto de la siembra se especifica en `team-access`.

#### Scenario: Valor por defecto
- DADO un entorno sin `Seed:DemoUsers`
- CUANDO se enlazan las opciones
- ENTONCES el valor es `false` y la API arranca sin `Seed:DemoPassword`

#### Scenario: Valor no booleano
- DADO `Seed__DemoUsers` = `quizas`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Seed:DemoUsers`

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

## REMOVED Requirements

### Requirement: Identidad provisional solo en Development

(Reason: la autenticación real con JWT sustituye a la identidad provisional; la API ya no necesita ni rechaza `Identity:DevelopmentEmployeeId`, y arranca fuera de `Development`.)
(Migration: ver los requisitos «Arranque sin identidad provisional» y «Clave de firma del JWT obligatoria y validada al arrancar» de esta especificación. Retirar `Identity__DevelopmentEmployeeId` de `.env.example`, `docker-compose.yml`, README y pruebas; `IdentityStartupTests` pasa a `JwtStartupTests`.)

## MODIFIED Requirements

### Requirement: Configuración por variables de entorno

La API DEBE leer su configuración de variables de entorno en todos los entornos. NO DEBE haber valores de configuración sensibles ni específicos de entorno en el código, en el repositorio ni en la imagen Docker.
(Previously: el escenario de secretos solo nombraba `Cors:AllowedOrigins`; ahora cubre `Jwt` y `Seed`)

#### Scenario: Valor tomado del entorno
- DADO `Cors__AllowedOrigins__0=https://app.example.test` definida en el entorno
- CUANDO la API arranca
- ENTONCES las opciones tipadas contienen ese origen

#### Scenario: Ningún secreto versionado
- DADO el árbol de archivos del proyecto
- CUANDO se revisan `.gitignore`, `.dockerignore` y el contenido de `appsettings*.json`
- ENTONCES `.env` figura en ambos ignore y `appsettings*.json` no contiene valores de `Cors:AllowedOrigins` ni claves `ConnectionStrings`, `Jwt` o `Seed`, ni otros secretos

### Requirement: Documentación de claves en `.env.example`

`.env.example` DEBE listar cada clave de entorno que la API y `docker-compose.yml` consumen, incluida la del puerto, con valor vacío o un marcador claramente ficticio: `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`, `ConnectionStrings__Default`, `Database__ApplyMigrationsOnStartup`, `Jwt__SigningKey`, `Seed__DemoUsers` y `Seed__DemoPassword`, además de las existentes. `Jwt__SigningKey` DEBE figurar con valor vacío (con un comentario que indique cómo generarla). `Seed__DemoUsers` DEBE valer `false`. `Seed__DemoPassword` DEBE figurar con valor vacío (activa, no comentada) y un comentario de que solo se usa en local, que debe tener al menos 12 caracteres y que su valor de demostración se documenta únicamente en el README; para usar la siembra, quien ejecuta el proyecto la rellena en su `.env` con esa contraseña del README. La contraseña de demostración NO DEBE aparecer en `.env.example`. NO DEBE contener `Identity__DevelopmentEmployeeId`, ni valores reales (contraseñas, cadenas de conexión, claves de firma). La clave del puerto PUEDE figurar comentada o sin definir, porque un valor vacío anularía el puerto que ya fija la imagen. Las claves `Ai:*` PUEDEN figurar solo como comentario.
(Previously: incluía `Identity__DevelopmentEmployeeId` y no listaba `Jwt__SigningKey` ni `Seed__*`)

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

#### Scenario: Sin claves de IA activas
- DADO `.env.example`
- CUANDO se buscan líneas `Ai__*` sin `#` inicial
- ENTONCES no existe ninguna

### Requirement: Puerto configurable por entorno

El puerto de escucha de la API DEBE poder fijarse con una variable de entorno estándar de ASP.NET Core (a confirmar en diseño, p. ej. `ASPNETCORE_HTTP_PORTS`), sin recompilar ni cambiar archivos.
(Previously: el escenario exigía `Identity__DevelopmentEmployeeId`; ahora exige `Jwt__SigningKey`)

#### Scenario: Puerto distinto
- DADO la variable de puerto definida con `8081`, `Cors__AllowedOrigins__0`, `ConnectionStrings__Default` (puede apuntar a un servidor inexistente) y `Jwt__SigningKey` (32 caracteres o más) válidos, y la API arrancada fuera de `WebApplicationFactory` (con `dotnet run --no-launch-profile`, que evita la prioridad de `launchSettings.json`, o en un contenedor con `docker run -e`)
- CUANDO la API arranca
- ENTONCES `GET http://localhost:8081/health` responde `200`

Nota de verificación: el puerto NO es comprobable con `WebApplicationFactory` (servidor en memoria); se verifica una vez de forma manual y la evidencia se registra en `verify-report.md`, sin prueba automática.
