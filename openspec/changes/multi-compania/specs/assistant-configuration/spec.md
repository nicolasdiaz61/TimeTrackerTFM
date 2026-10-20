# Delta para assistant-configuration

Cambio: `multi-compania`. Capacidad EXISTENTE (`openspec/specs/assistant-configuration/spec.md`). Contiene bloques `MODIFIED` (con el nombre exacto del requisito vigente) y `ADDED`. Las claves `Ai:*` del entorno pasan a ser la configuración de plataforma: el proveedor, el modelo, el endpoint y `Ai:ApiKey` del entorno se usan únicamente, de forma explícita, para la compañía demo; el resto de compañías solo usa lo guardado por su Manager (ver `assistant-settings`).

> Aviso de delta que cambia escenarios vigentes:
> - «Selección de proveedor solo por configuración»: el entorno ya no es el valor por defecto de toda compañía. Una compañía no demo sin fila guardada tiene el asistente deshabilitado aunque el entorno defina un proveedor y una clave. El escenario «La configuración guardada tiene prioridad» se mantiene para la compañía demo.
> - «Asistente deshabilitado sin proveedor»: la excepción de validación permanente (que ya cubría `Ai:AllowedProviders`) se amplía a las claves de topes por compañía.

## MODIFIED Requirements

### Requirement: Selección de proveedor solo por configuración

El proveedor y el modelo DEBEN elegirse únicamente por configuración: para la compañía demo, la del entorno (`Ai:*`) o, con prioridad sobre ella, la guardada por un Manager (ADR 0018; ver `assistant-settings`); para cualquier otra compañía, solo la guardada por su Manager, nunca por código ni por el entorno. Los valores del entorno `Ai:Provider`, `Ai:Model`, `Ai:Endpoint` y `Ai:ApiKey` NO DEBEN aplicarse a una compañía que no sea la demo. Con el proveedor efectivo `Anthropic`, `OpenAI` o `Gemini` se usa el proveedor en la nube con la clave y el modelo de esa compañía; con `Ollama`, un servidor local en `Ai:Endpoint` (el endpoint solo proviene del entorno) con el modelo de esa compañía. El nombre del modelo NO DEBE estar escrito en el código. Cambiar de uno a otro NO DEBE requerir cambios de código ni recompilar.
(Previously: la configuración del entorno era el valor por defecto de la instancia entera)

#### Scenario: Anthropic
- DADO el proveedor efectivo `Anthropic` de una compañía, una clave y un modelo válidos
- CUANDO se construye el cliente de chat
- ENTONCES el cliente es el de Anthropic configurado con ese modelo y esa clave

#### Scenario: OpenAI
- DADO el proveedor efectivo `OpenAI` de una compañía, una clave y un modelo válidos
- CUANDO se construye el cliente de chat
- ENTONCES el cliente es el compatible con OpenAI con ese modelo y esa clave, sin endpoint personalizado

#### Scenario: Gemini
- DADO el proveedor efectivo `Gemini` de una compañía, una clave y un modelo válidos
- CUANDO se construye el cliente de chat
- ENTONCES el cliente es el compatible con OpenAI apuntando al endpoint fijo de Gemini definido en el servidor, con ese modelo y esa clave

#### Scenario: Ollama
- DADO el proveedor efectivo `Ollama` de una compañía, `Ai__Endpoint` y un modelo válidos
- CUANDO se construye el cliente de chat
- ENTONCES el cliente es el de Ollama apuntando a ese endpoint y ese modelo

#### Scenario: La configuración guardada tiene prioridad en la demo
- DADO `Ai__Provider=Anthropic` y una fila guardada de la compañía demo con proveedor permitido `OpenAI`
- CUANDO se resuelve la configuración efectiva de la demo
- ENTONCES el proveedor efectivo es `OpenAI`, sin cambiar código ni variables de entorno

#### Scenario: El entorno solo sirve a la demo
- DADO `Ai__Provider=Anthropic` y `Ai__ApiKey` definidos y una compañía no demo sin fila guardada
- CUANDO se resuelve la configuración efectiva de esa compañía
- ENTONCES el asistente está deshabilitado y no se construye ningún cliente con la clave del entorno

#### Scenario: Compañía no demo con fila propia
- DADO el mismo entorno y una fila de la compañía A con proveedor `OpenAI` y su propia clave
- CUANDO se construye el cliente de la compañía A
- ENTONCES el cliente usa la clave de la fila y nunca `Ai__ApiKey` del entorno

#### Scenario: Solo configuración
- DADO el código fuente de `src/`
- CUANDO se buscan literales de nombres de modelo de proveedores
- ENTONCES no hay ninguno

### Requirement: Asistente deshabilitado sin proveedor

Sin `Ai:Provider` (ausente, `None` o en blanco) la API DEBE arrancar, el resto de endpoints DEBE funcionar y NO DEBE exigirse ninguna otra clave `Ai:*` del proveedor ni validarse (un `Ai:ApiKey` o `Ai:Model` ausentes o inválidos no impiden el arranque), con la única excepción de las claves que limitan lo que un Manager o una compañía pueden hacer aunque el entorno no tenga proveedor: `Ai:AllowedProviders` y las claves de topes por compañía (ver «Topes del asistente por compañía»), que se validan siempre. Ningún cliente de modelo DEBE construirse a partir del entorno y ninguna conexión con un proveedor DEBE abrirse; la configuración guardada por un Manager PUEDE activar un proveedor para su compañía (ver `assistant-settings`). El endpoint del asistente responde `503` mientras la configuración efectiva de la compañía del usuario sea «deshabilitado» (ver `assistant-endpoint`).
(Previously: la única excepción de validación permanente era `Ai:AllowedProviders`)

#### Scenario: Arranque sin proveedor
- DADO el entorno `Production` con `Cors__AllowedOrigins__0`, `ConnectionStrings__Default` y `Jwt__SigningKey` válidos y sin ninguna clave `Ai__*`
- CUANDO la API arranca
- ENTONCES arranca y `GET /health` responde `200`

#### Scenario: Otras claves ignoradas
- DADO `Ai__Provider` sin definir, `Ai__MaxOutputTokens` = `-5` y `Ai__TimeoutSeconds` = `999`
- CUANDO la API arranca
- ENTONCES arranca sin error

#### Scenario: Lista permitida inválida sin proveedor
- DADO `Ai__Provider` sin definir y `Ai__AllowedProviders=GitHubModels`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Ai:AllowedProviders`

#### Scenario: Tope por compañía inválido sin proveedor
- DADO `Ai__Provider` sin definir y `Ai__DailyRequestsPerCompany` = `0`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Ai:DailyRequestsPerCompany`

#### Scenario: Sin cliente de modelo
- DADO la API sin `Ai:Provider` y sin configuración guardada
- CUANDO se resuelve la composición del asistente
- ENTONCES no se construye ningún cliente de proveedor

## ADDED Requirements

### Requirement: Topes del asistente por compañía

La API DEBE leer de variables de entorno, con estos valores por defecto, tres claves de tope por compañía que el Manager NO puede editar: `Ai:RequestsPerMinutePerCompany` (30; rango 1 a 600), `Ai:DailyRequestsPerCompany` (500; rango 1 a 100000) y `Ai:MaxLoadedCompanies` (100; rango 1 a 1000; número máximo de instantáneas de compañía cargadas a la vez). Los extremos de los rangos están incluidos. Un valor fuera de rango o no numérico DEBE impedir el arranque con un mensaje en español que nombre la clave y la razón sin imprimir valores, incluso con el asistente deshabilitado a nivel de entorno. Ninguna de las tres DEBE figurar en `appsettings*.json`, en el código ni en la imagen Docker. Los límites por usuario existentes (`Ai:RequestsPerMinute`, `Ai:DailyRequestsPerUser`) se conservan.

#### Scenario: Valores por defecto
- DADO un entorno sin las tres claves
- CUANDO se enlazan las opciones
- ENTONCES `RequestsPerMinutePerCompany` = 30, `DailyRequestsPerCompany` = 500 y `MaxLoadedCompanies` = 100

#### Scenario: Límites inferiores y superiores inválidos
- DADO `Ai__RequestsPerMinutePerCompany` = `0` o `601`, `Ai__DailyRequestsPerCompany` = `0` o `100001` y `Ai__MaxLoadedCompanies` = `0` o `1001`, cada uno por separado
- CUANDO la API intenta arrancar en cada caso
- ENTONCES cada arranque falla con un mensaje que contiene la clave afectada

#### Scenario: Extremos válidos
- DADO `Ai__RequestsPerMinutePerCompany` = `1` o `600`, `Ai__DailyRequestsPerCompany` = `1` o `100000` y `Ai__MaxLoadedCompanies` = `1` o `1000`, cada uno por separado
- CUANDO la API arranca con cada uno
- ENTONCES arranca

#### Scenario: Valor no numérico
- DADO `Ai__MaxLoadedCompanies` = `muchas`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Ai:MaxLoadedCompanies` y no contiene `muchas`

#### Scenario: No versionadas
- DADO los archivos `appsettings*.json`
- CUANDO se buscan las tres claves
- ENTONCES no existe ninguna

#### Scenario: El Manager no las edita
- DADO el contrato regenerado
- CUANDO se buscan propiedades de topes por compañía en `PUT /api/asistente/configuracion`
- ENTONCES no existe ninguna y una propiedad así en el cuerpo responde `400`
