# Delta para startup-configuration

Cambio: `asistente-ia`. Añade al arranque de la API la validación de las claves `Ai:*` (reglas por clave en `assistant-configuration`) y actualiza los requisitos vigentes de secretos versionados y de `.env.example`. La política CORS, la cadena de conexión, `Jwt:SigningKey`, la siembra y las cabeceras de proxy no cambian.

> Aviso de delta que cambia un escenario vigente: el escenario «Sin claves de IA activas» de `.env.example` se sustituye por «Claves de IA documentadas» (las nueve claves `Ai__*` pasan a documentarse: `Ai__Provider` y `Ai__ApiKey` activas y vacías, el resto comentadas, según el diseño). El `Purpose` y los demás requisitos no cambian.

Notas de verificación:
- Los fallos de arranque se evalúan sobre la excepción de validación y sus internas (puede ser un `AggregateException`).
- La ausencia de `Ai:Provider` NO es un error de arranque: es el estado «deshabilitado» (decisión 5 de la propuesta).

## ADDED Requirements

### Requirement: Arranque con la configuración del asistente

La API DEBE validar las claves `Ai:*` con `ValidateOnStart()` y mismo patrón que `Jwt:SigningKey`. Sin `Ai:Provider` (ausente, `None` o en blanco) DEBE arrancar sin exigir ninguna otra clave `Ai:*`. Con `Ai:Provider` = `Anthropic` u `Ollama` y una clave `Ai:*` incorrecta o ausente según `assistant-configuration`, el proceso NO DEBE arrancar ni atender peticiones. El mensaje DEBE estar en español, nombrar la clave y la razón, y NO DEBE imprimir sus valores. Si varias validaciones fallan a la vez, la excepción PUEDE ser un `AggregateException`.

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

#### Scenario: Proveedor desconocido
- DADO `Ai__Provider=Gemini`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Ai:Provider`

#### Scenario: El valor no se imprime
- DADO `Ai__Provider=Anthropic`, `Ai__ApiKey` = `clave-secreta-prueba` y un `Ai:MaxOutputTokens` inválido
- CUANDO la API falla al arrancar
- ENTONCES la excepción (y sus internas) contiene `Ai:MaxOutputTokens` y no contiene `clave-secreta-prueba`

#### Scenario: Las demás validaciones no cambian
- DADO `Ai__Provider` sin definir y `Jwt__SigningKey` ausente
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Jwt:SigningKey`

## MODIFIED Requirements

### Requirement: Configuración por variables de entorno

La API DEBE leer su configuración de variables de entorno en todos los entornos. NO DEBE haber valores de configuración sensibles ni específicos de entorno en el código, en el repositorio ni en la imagen Docker.
(Previously: el escenario de secretos nombraba `Cors:AllowedOrigins`, `ConnectionStrings`, `Jwt` y `Seed`; ahora también `Ai`)

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

### Requirement: Documentación de claves en `.env.example`

`.env.example` DEBE listar cada clave de entorno que la API y `docker-compose.yml` consumen, incluida la del puerto, con valor vacío o un marcador claramente ficticio: `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`, `ConnectionStrings__Default`, `Database__ApplyMigrationsOnStartup`, `Jwt__SigningKey`, `Seed__DemoUsers` y `Seed__DemoPassword`, además de las existentes. `Jwt__SigningKey` DEBE figurar con valor vacío (con un comentario que indique cómo generarla). `Seed__DemoUsers` DEBE valer `false`. `Seed__DemoPassword` DEBE figurar con valor vacío (activa, no comentada) y un comentario de que solo se usa en local, que debe tener al menos 12 caracteres y que su valor de demostración se documenta únicamente en el README. Las nueve claves del asistente `Ai__Provider`, `Ai__Model`, `Ai__Endpoint`, `Ai__ApiKey`, `Ai__MaxOutputTokens`, `Ai__MaxToolIterations`, `Ai__TimeoutSeconds`, `Ai__RequestsPerMinute` y `Ai__DailyRequestsPerUser` DEBEN documentarse en `.env.example`: `Ai__Provider` y `Ai__ApiKey` como líneas activas con valor vacío (asistente deshabilitado por defecto) y las demás como líneas comentadas (`#`), con `Ai__Endpoint` de ejemplo `http://host.docker.internal:11434/` y los límites con sus valores por defecto (800, 5, 25, 5 y 50). DEBE haber un comentario que explique que sin `Ai__Provider` el asistente queda deshabilitado, los valores admitidos de `Ai__Provider` y que `Ai__ApiKey` es un secreto que nunca se escribe en el archivo. NO DEBE contener `Identity__DevelopmentEmployeeId`, ni valores reales (contraseñas, cadenas de conexión, claves de firma, claves de API). La clave del puerto PUEDE figurar comentada o sin definir, porque un valor vacío anularía el puerto que ya fija la imagen.
(Previously: las claves `Ai:*` «PUEDEN» figurar solo como comentario y el escenario exigía que no existiera ninguna línea `Ai__*` sin `#`; ahora DEBEN documentarse las nueve, con `Ai__Provider` y `Ai__ApiKey` activas y vacías)

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
- CUANDO se buscan las nueve claves `Ai__*`
- ENTONCES las nueve aparecen; `Ai__Provider=` y `Ai__ApiKey=` están activas y con valor vacío, las otras siete llevan `#` inicial y ninguna lleva un valor real

#### Scenario: Entrega sin clave de API
- DADO `.env.example`
- CUANDO se lee `Ai__ApiKey`
- ENTONCES su valor está vacío (comprobado por `DeliveryArtifactsTests`)

#### Scenario: Nota del asistente deshabilitado
- DADO `.env.example`
- CUANDO se leen los comentarios de las claves `Ai__*` (`Ai__Provider` y `Ai__ApiKey` activas pero vacías)
- ENTONCES indican que sin `Ai__Provider` el asistente queda deshabilitado, los valores `Anthropic` y `Ollama`, y que la clave de API es un secreto que no se escribe en el archivo

#### Scenario: Copiar el archivo no activa el asistente
- DADO un `.env` creado copiando `.env.example` sin editar las claves `Ai__*`
- CUANDO la API arranca
- ENTONCES arranca con el asistente deshabilitado
