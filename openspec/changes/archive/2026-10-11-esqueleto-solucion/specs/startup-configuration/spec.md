# Especificación: startup-configuration

## Purpose

Define cómo `TimeTracker.Api` obtiene y valida su configuración: variables de entorno, opciones tipadas con validación al arrancar (fail fast) y CORS restrictivo. Capacidad NUEVA; todos los requisitos son `ADDED`.

Convención (a confirmar en diseño): la clave `Cors:AllowedOrigins` se suministra por entorno como `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, etc.

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
- ENTONCES `.env` figura en ambos ignore y `appsettings*.json` no contiene valores de `Cors:AllowedOrigins` ni secretos

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

`.env.example` DEBE listar cada clave de entorno que la API consume, incluida la del puerto, con valor vacío o un marcador claramente ficticio. La clave del puerto PUEDE figurar comentada o sin definir, porque un valor vacío anularía el puerto que ya fija la imagen. NO DEBE contener valores reales. Las claves `Ai:*` PUEDEN figurar solo como comentario.

#### Scenario: Claves documentadas
- DADO `.env.example`
- CUANDO se compara con las claves usadas por la API
- ENTONCES cada clave consumida aparece en el archivo (la del puerto, al menos comentada) y ninguna tiene un valor real ni un valor vacío activo que anule el puerto de la imagen

#### Scenario: Sin claves de IA activas
- DADO `.env.example`
- CUANDO se buscan líneas `Ai__*` sin `#` inicial
- ENTONCES no existe ninguna

### Requirement: Puerto configurable por entorno

El puerto de escucha de la API DEBE poder fijarse con una variable de entorno estándar de ASP.NET Core (a confirmar en diseño, p. ej. `ASPNETCORE_HTTP_PORTS`), sin recompilar ni cambiar archivos.

#### Scenario: Puerto distinto
- DADO la variable de puerto definida con `8081` y la API arrancada fuera de `WebApplicationFactory` (con `dotnet run --no-launch-profile`, que evita la prioridad de `launchSettings.json`, o en un contenedor con `docker run -e`)
- CUANDO la API arranca
- ENTONCES `GET http://localhost:8081/health` responde `200`

Nota de verificación: el puerto NO es comprobable con `WebApplicationFactory` (servidor en memoria); se verifica una vez de forma manual y la evidencia se registra en `tasks.md` / `verify-report.md`, sin prueba automática.
