# Delta para startup-configuration

Cambio: `asistente-config-responsable`. Capacidad EXISTENTE definida por el cambio `asistente-ia`; este delta solo contiene bloques `MODIFIED`. Orden de archivo: archivar `asistente-ia` primero; si no fuera posible, fusionar estos deltas a mano en el informe de archivo y avisar antes de fusionar deltas que cambian escenarios vigentes.

> Aviso de delta que cambia escenarios vigentes: (1) el escenario «Proveedor desconocido» de «Arranque con la configuración del asistente» usaba `Gemini`, que pasa a ser un proveedor válido; el valor desconocido pasa a ser `GitHubModels`. (2) El escenario «Claves de IA documentadas» de `.env.example` exigía exactamente las nueve claves `Ai__*`; ahora incluye también las claves nuevas, todas comentadas y sin valores.

Las claves maestras (`Ai__MasterKeys__{versión}`, `Ai__ActiveMasterKeyVersion`) no se validan al arrancar: su ausencia no impide el arranque (ver `assistant-credential-protection`).

## MODIFIED Requirements

### Requirement: Arranque con la configuración del asistente

La API DEBE validar las claves `Ai:*` con `ValidateOnStart()` y mismo patrón que `Jwt:SigningKey`. Sin `Ai:Provider` (ausente, `None` o en blanco) DEBE arrancar sin exigir ninguna otra clave `Ai:*`, salvo que `Ai:AllowedProviders` esté definida con un nombre desconocido, que sigue siendo un error de arranque (ver `assistant-configuration`). Con `Ai:Provider` = `Anthropic`, `OpenAI`, `Gemini` u `Ollama` y una clave `Ai:*` incorrecta o ausente según `assistant-configuration`, el proceso NO DEBE arrancar ni atender peticiones. La ausencia o invalidez de las claves maestras NO DEBE impedir el arranque. El mensaje DEBE estar en español, nombrar la clave y la razón, y NO DEBE imprimir sus valores. Si varias validaciones fallan a la vez, la excepción PUEDE ser un `AggregateException`.
(Previously: proveedores `Anthropic` u `Ollama`; sin mención de las claves nuevas ni de las claves maestras)

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

### Requirement: Documentación de claves en `.env.example`

`.env.example` DEBE listar cada clave de entorno que la API y `docker-compose.yml` consumen, incluida la del puerto, con valor vacío o un marcador claramente ficticio: `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`, `ConnectionStrings__Default`, `Database__ApplyMigrationsOnStartup`, `Jwt__SigningKey`, `Seed__DemoUsers` y `Seed__DemoPassword`, además de las existentes. `Jwt__SigningKey` DEBE figurar con valor vacío (con un comentario que indique cómo generarla). `Seed__DemoUsers` DEBE valer `false`. `Seed__DemoPassword` DEBE figurar con valor vacío (activa, no comentada) y un comentario de que solo se usa en local, que debe tener al menos 12 caracteres y que su valor de demostración se documenta únicamente en el README. Las nueve claves del asistente `Ai__Provider`, `Ai__Model`, `Ai__Endpoint`, `Ai__ApiKey`, `Ai__MaxOutputTokens`, `Ai__MaxToolIterations`, `Ai__TimeoutSeconds`, `Ai__RequestsPerMinute` y `Ai__DailyRequestsPerUser` DEBEN documentarse en `.env.example`: `Ai__Provider` y `Ai__ApiKey` como líneas activas con valor vacío (asistente deshabilitado por defecto) y las demás como líneas comentadas (`#`), con `Ai__Endpoint` de ejemplo `http://host.docker.internal:11434/` y los límites con sus valores por defecto (800, 5, 25, 5 y 50). Las claves nuevas `Ai__Effort`, `Ai__AllowedProviders`, `Ai__MasterKeys__1`, `Ai__ActiveMasterKeyVersion` y `Ai__SettingsPollSeconds` TAMBIÉN DEBEN documentarse como líneas comentadas (`#`) y sin valores reales; en particular, `.env.example` NO DEBE contener ninguna clave maestra con valor, y su comentario DEBE indicar que se genera con 32 bytes aleatorios en Base64. DEBE haber un comentario que explique que sin `Ai__Provider` el asistente queda deshabilitado, los valores admitidos de `Ai__Provider` (`Anthropic`, `OpenAI`, `Gemini`, `Ollama`) y que `Ai__ApiKey` es un secreto que nunca se escribe en el archivo. NO DEBE contener `Identity__DevelopmentEmployeeId`, ni valores reales (contraseñas, cadenas de conexión, claves de firma, claves de API, claves maestras). La clave del puerto PUEDE figurar comentada o sin definir, porque un valor vacío anularía el puerto que ya fija la imagen.
(Previously: se documentaban solo las nueve claves `Ai__*`, con los valores admitidos `Anthropic` y `Ollama`; sin las claves de esfuerzo, lista permitida, claves maestras y sondeo)

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
