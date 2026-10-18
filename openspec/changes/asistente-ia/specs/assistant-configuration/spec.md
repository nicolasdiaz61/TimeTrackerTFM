# Especificación: assistant-configuration

Cambio: `asistente-ia`. Capacidad NUEVA; todos los requisitos son `ADDED`. Define las claves `Ai:*`, la selección de proveedor solo por configuración, las reglas de validación y la desactivación sin `Ai:Provider`. El efecto sobre el arranque de la API (fallo rápido, mensajes sin valores) se complementa en `startup-configuration`. Los valores por defecto y los rangos válidos de las claves numéricas son los fijados por el diseño (DD-2, DD-10, DD-11).

Decisiones aceptadas: proveedores `Anthropic` (nube) y `Ollama` (local); el modelo y el endpoint son solo configuración, sin cambiar código; sin `Ai:Provider` la API arranca y el asistente queda deshabilitado; patrón de validación y mensajes igual que `Jwt:SigningKey` y `ConnectionStrings:Default`.

Notas de verificación:
- Las pruebas de validación son unitarias sobre las opciones y no abren conexión con ningún proveedor.
- Los fallos de arranque se evalúan sobre la excepción de validación y sus internas (puede ser un `AggregateException`).
- La elección de proveedor se comprueba por el tipo de cliente construido, sin llamadas de red.

Fuera de alcance: más de un proveedor activo a la vez, cambio de proveedor en caliente, selección de modelo por usuario.

## ADDED Requirements

### Requirement: Claves de configuración del asistente

La API DEBE leer de variables de entorno las nueve claves `Ai:Provider`, `Ai:Model`, `Ai:Endpoint`, `Ai:ApiKey`, `Ai:MaxOutputTokens`, `Ai:MaxToolIterations`, `Ai:TimeoutSeconds`, `Ai:RequestsPerMinute` y `Ai:DailyRequestsPerUser` (variables `Ai__Provider`, etc.). `Ai:Provider` admite `Anthropic`, `Ollama` o ausente/`None`/en blanco (asistente deshabilitado). Las claves numéricas DEBEN tener estos valores por defecto cuando no se definen: `Ai:MaxOutputTokens` 800, `Ai:MaxToolIterations` 5, `Ai:TimeoutSeconds` 25, `Ai:RequestsPerMinute` 5 y `Ai:DailyRequestsPerUser` 50. Ninguna clave `Ai:*` DEBE figurar en `appsettings*.json`, en el código ni en la imagen Docker.

#### Scenario: Valores tomados del entorno
- DADO `Ai__Provider=Ollama`, `Ai__Model=m`, `Ai__Endpoint=http://localhost:11434/` en el entorno
- CUANDO se enlazan las opciones
- ENTONCES las opciones contienen esos valores

#### Scenario: Valores numéricos por defecto
- DADO un entorno con `Ai__Provider=Ollama`, `Ai__Model` y `Ai__Endpoint` válidos y sin claves numéricas
- CUANDO se enlazan las opciones
- ENTONCES `MaxOutputTokens` = 800, `MaxToolIterations` = 5, `TimeoutSeconds` = 25, `RequestsPerMinute` = 5 y `DailyRequestsPerUser` = 50

#### Scenario: Ausente, None o en blanco
- DADO `Ai:Provider` sin definir, con valor `None` y con valor en blanco
- CUANDO se enlazan las opciones
- ENTONCES las tres son el estado «deshabilitado»

#### Scenario: No versionadas
- DADO los archivos `appsettings*.json`
- CUANDO se buscan claves `Ai`
- ENTONCES no existe ninguna

### Requirement: Asistente deshabilitado sin proveedor

Sin `Ai:Provider` (ausente, `None` o en blanco) la API DEBE arrancar, el resto de endpoints DEBE funcionar y NO DEBE exigirse ninguna otra clave `Ai:*` ni validarse (un `Ai:ApiKey` o `Ai:Model` ausentes o inválidos no impiden el arranque). Ningún cliente de modelo DEBE construirse y ninguna conexión con un proveedor DEBE abrirse. El endpoint del asistente responde `503` (ver `assistant-endpoint`).

#### Scenario: Arranque sin proveedor
- DADO el entorno `Production` con `Cors__AllowedOrigins__0`, `ConnectionStrings__Default` y `Jwt__SigningKey` válidos y sin ninguna clave `Ai__*`
- CUANDO la API arranca
- ENTONCES arranca y `GET /health` responde `200`

#### Scenario: Otras claves ignoradas
- DADO `Ai__Provider` sin definir, `Ai__MaxOutputTokens` = `-5` y `Ai__TimeoutSeconds` = `999`
- CUANDO la API arranca
- ENTONCES arranca sin error

#### Scenario: Sin cliente de modelo
- DADO la API sin `Ai:Provider`
- CUANDO se resuelve la composición del asistente
- ENTONCES no se construye ningún cliente de proveedor

### Requirement: Selección de proveedor solo por configuración

El proveedor y el modelo DEBEN elegirse únicamente por configuración: con `Ai:Provider` = `Anthropic` se usa el proveedor en la nube con `Ai:ApiKey` y `Ai:Model`; con `Ollama`, un servidor local en `Ai:Endpoint` con `Ai:Model`. El nombre del modelo NO DEBE estar escrito en el código. Cambiar de uno a otro NO DEBE requerir cambios de código ni recompilar.

#### Scenario: Anthropic
- DADO `Ai__Provider=Anthropic`, una clave y un modelo válidos
- CUANDO se construye el cliente de chat
- ENTONCES el cliente es el de Anthropic configurado con ese modelo y esa clave

#### Scenario: Ollama
- DADO `Ai__Provider=Ollama`, `Ai__Endpoint` y `Ai__Model` válidos
- CUANDO se construye el cliente de chat
- ENTONCES el cliente es el de Ollama apuntando a ese endpoint y ese modelo

#### Scenario: Solo configuración
- DADO el código fuente de `src/`
- CUANDO se buscan literales de nombres de modelo de proveedores
- ENTONCES no hay ninguno

### Requirement: Validación de las claves Ai con proveedor activo

Con `Ai:Provider` = `Anthropic` o `Ollama`, la API DEBE validar al arrancar: `Ai:Model` obligatorio y de 100 caracteres como máximo; con `Anthropic`, `Ai:ApiKey` obligatoria y no en blanco; con `Ollama`, `Ai:Endpoint` obligatorio y URL absoluta `http` o `https`; y los rangos (ambos extremos incluidos) `Ai:MaxOutputTokens` 64 a 4096, `Ai:MaxToolIterations` 1 a 10, `Ai:TimeoutSeconds` 5 a 55, `Ai:RequestsPerMinute` 1 a 60 y `Ai:DailyRequestsPerUser` 1 a 1000. Un `Ai:Provider` con un valor no reconocido DEBE impedir el arranque. Cada mensaje DEBE estar en español, nombrar la clave afectada y la razón, y NO DEBE imprimir valores (en particular `Ai:ApiKey`).

#### Scenario: Proveedor desconocido
- DADO `Ai__Provider` = `Gemini`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Ai:Provider`

#### Scenario: Anthropic sin clave
- DADO `Ai__Provider=Anthropic`, `Ai__Model` válido y sin `Ai:ApiKey` (y otro caso con la clave en blanco)
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Ai:ApiKey`

#### Scenario: Ollama sin endpoint o con endpoint inválido
- DADO `Ai__Provider=Ollama` sin `Ai:Endpoint`, y otro con `Ai__Endpoint=no-es-una-url`
- CUANDO la API intenta arrancar en cada caso
- ENTONCES cada arranque falla con un mensaje que contiene `Ai:Endpoint`

#### Scenario: Sin modelo
- DADO `Ai__Provider=Ollama`, `Ai__Endpoint` válido y sin `Ai:Model`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Ai:Model`

#### Scenario: Límites numéricos
- DADO `Ai__MaxOutputTokens` = `63`, `Ai__MaxToolIterations` = `0`, `Ai__RequestsPerMinute` = `0`, `Ai__DailyRequestsPerUser` = `0` y `Ai__TimeoutSeconds` = `4`, cada uno por separado con el resto válido
- CUANDO la API intenta arrancar en cada caso
- ENTONCES cada arranque falla con un mensaje que contiene la clave afectada

#### Scenario: Límites superiores
- DADO `Ai__MaxOutputTokens` = `4097`, `Ai__MaxToolIterations` = `11`, `Ai__RequestsPerMinute` = `61`, `Ai__DailyRequestsPerUser` = `1001` y `Ai__TimeoutSeconds` = `56`, cada uno por separado con el resto válido
- CUANDO la API intenta arrancar en cada caso
- ENTONCES cada arranque falla con un mensaje que contiene la clave afectada

#### Scenario: Extremos válidos
- DADO `Ai__MaxOutputTokens` = `64` o `4096`, `Ai__MaxToolIterations` = `1` o `10`, `Ai__RequestsPerMinute` = `1` o `60`, `Ai__DailyRequestsPerUser` = `1` o `1000` y `Ai__TimeoutSeconds` = `5` o `55`, cada uno por separado
- CUANDO la API arranca con cada uno
- ENTONCES arranca

#### Scenario: Modelo demasiado largo
- DADO `Ai__Model` de 101 caracteres con un proveedor activo
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Ai:Model`

#### Scenario: Configuración completa válida
- DADO `Ai__Provider=Ollama`, `Ai__Endpoint=http://localhost:11434/` y `Ai__Model` válidos
- CUANDO la API arranca
- ENTONCES arranca y `GET /health` responde `200` sin conectar con el servidor de Ollama

#### Scenario: El valor no se imprime
- DADO `Ai__Provider=Anthropic`, `Ai__ApiKey` = `clave-secreta-prueba` y `Ai__Model` en blanco
- CUANDO la API falla al arrancar
- ENTONCES la excepción (y sus internas) no contiene `clave-secreta-prueba`

### Requirement: La clave del proveedor nunca se expone

`Ai:ApiKey` NO DEBE aparecer en respuestas HTTP, registros, contrato OpenAPI, mensajes de error, prompt enviado al modelo ni en la Web.

#### Scenario: Fallo con la clave configurada
- DADO `Ai:ApiKey` con un valor reconocible y un proveedor que falla
- CUANDO se envía una petición al asistente
- ENTONCES ni la respuesta ni los registros contienen ese valor

#### Scenario: Contrato sin la clave
- DADO `docs/api/openapi.json`
- CUANDO se busca `ApiKey`
- ENTONCES no aparece
