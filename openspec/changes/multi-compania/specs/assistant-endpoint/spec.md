# Delta para assistant-endpoint

Cambio: `multi-compania`. Capacidad EXISTENTE (`openspec/specs/assistant-endpoint/spec.md`). Contiene un bloque `MODIFIED` y bloques `ADDED`: asistente deshabilitado por compañía, límites y topes agregados por compañía, y el contrato OpenAPI actualizado. Los requisitos vigentes («Endpoint autenticado sin estado», «Contrato de petición y respuesta», «Historial no confiable», «Instrucciones del sistema», «Asistente deshabilitado», «Fallos del proveedor sin detalle», «Límite de peticiones por usuario», «Tope diario por usuario», etc.) siguen vigentes; los límites por usuario se conservan y se SUMAN los de compañía.

> Aviso de delta que cambia un escenario vigente: «Contrato OpenAPI del asistente», escenario «Cobertura de autorización»: los endpoints anónimos dejan de ser exactamente tres cuando `Registration:Enabled` es `true` (ver `authentication`).

Textos fijos nuevos (español): `429` por minuto de la compañía, `detail` «Su compañía ha superado el límite de consultas por minuto. Inténtelo de nuevo en unos segundos.»; `429` diario de la compañía, `detail` «Su compañía ha alcanzado el tope diario de consultas del asistente.». Ambos son distintos entre sí y de los de usuario. El `503` de compañía sin asistente usa el mismo texto fijo que el de asistente deshabilitado («El asistente no está disponible en este entorno.»).

Notas de verificación adicionales:
- Los límites por compañía se verifican con valores de configuración pequeños (`Ai__RequestsPerMinutePerCompany`, `Ai__DailyRequestsPerCompany`) y dos compañías A y B (ver `company-isolation`).
- Los contadores siguen siendo en memoria de la instancia (ADR 0017): se reinician al desplegar y no se comparten entre réplicas.

## MODIFIED Requirements

### Requirement: Contrato OpenAPI del asistente

El contrato `docs/api/openapi.json` DEBE incluir `post` en `/api/asistente` y ninguna otra operación bajo esa ruta. DEBE declarar las respuestas `200`, `400`, `401`, `429` y `503`, la exigencia de autenticación Bearer, el esquema de petición y `{ reply }` como respuesta. NO DEBE contener parámetros ni propiedades `employeeId`, `memberId` ni `companyId` en esa operación. El `413` por tamaño de cuerpo no se declara en el contrato. Los endpoints anónimos de la API DEBEN ser exactamente `/health`, el contrato OpenAPI, el login y, solo con `Registration:Enabled` = `true`, `POST /api/auth/register-company`.
(Previously: los anónimos eran exactamente `/health`, el contrato OpenAPI y el login; la operación no mencionaba `companyId`)

#### Scenario: Operación y respuestas
- DADO el contrato generado
- CUANDO se lee `/api/asistente`
- ENTONCES existe solo `post` con las respuestas `200`, `400`, `401`, `429` y `503` y requisito de seguridad Bearer

#### Scenario: Sin parámetros de identidad ni de compañía
- DADO la operación `post` de `/api/asistente`
- CUANDO se buscan `employeeId`, `memberId` y `companyId`
- ENTONCES no aparecen

#### Scenario: Cobertura de autorización con el registro cerrado
- DADO `AuthorizationCoverageTests` con `Registration:Enabled` apagada
- CUANDO se ejecutan con el endpoint añadido
- ENTONCES pasan y los anónimos son exactamente tres: health, openapi y login

#### Scenario: Cobertura de autorización con el registro abierto
- DADO `AuthorizationCoverageTests` con `Registration:Enabled` = `true`
- CUANDO se ejecutan
- ENTONCES pasan y los anónimos son exactamente cuatro: health, openapi, login y registro de compañía

#### Scenario: Método no permitido
- DADO un usuario autenticado
- CUANDO envía `GET /api/asistente`
- ENTONCES recibe `405`

## ADDED Requirements

### Requirement: Asistente deshabilitado por compañía

El endpoint DEBE responder `503` `application/problem+json` con el `detail` fijo de asistente deshabilitado («El asistente no está disponible en este entorno.») cuando la configuración efectiva de la compañía del token es «deshabilitado» (ver `assistant-settings`): una compañía no demo sin fila guardada o con proveedor `None`, aunque el entorno defina un proveedor y una clave de plataforma. NO DEBE invocar ningún modelo ni consumir el tope diario de usuario ni el de compañía, y NO DEBE revelar valores de configuración. Habilitar el asistente en una compañía NO DEBE habilitarlo en otra.

#### Scenario: Compañía nueva con entorno configurado
- DADO un entorno con `Ai__Provider` y `Ai__ApiKey` definidos y una compañía no demo sin fila
- CUANDO un usuario de esa compañía envía `POST /api/asistente` con un cuerpo válido
- ENTONCES recibe `503` con el `detail` de asistente deshabilitado y el modelo falso no se invoca

#### Scenario: Habilitar A no habilita B
- DADO la compañía A con proveedor guardado y un `IChatClient` falso, y la compañía B sin fila
- CUANDO un usuario de A y otro de B envían una petición válida
- ENTONCES el de A recibe `200` y el de B recibe `503`

#### Scenario: Sin consumo de topes
- DADO la compañía B deshabilitada con `Ai__DailyRequestsPerCompany` = 1 y `Ai__DailyRequestsPerUser` = 1
- CUANDO un usuario de B envía 3 peticiones válidas en el mismo minuto
- ENTONCES las tres reciben `503` y ninguna recibe `429`

#### Scenario: Compañía demo con el entorno
- DADO la compañía demo sin fila y un entorno con proveedor y clave de plataforma, con un `IChatClient` falso
- CUANDO un usuario de la demo envía una petición válida
- ENTONCES recibe `200`

### Requirement: Límite de peticiones por minuto por compañía

Además del límite por usuario, el endpoint DEBE limitar el ritmo agregado de peticiones por compañía (identificada por el claim `cid`) con una ventana fija de un minuto y `Ai:RequestsPerMinutePerCompany` peticiones por ventana (30 por defecto, sin cola de espera). Cuentan todas las peticiones autenticadas de la compañía que superen el límite por usuario, también las rechazadas después con `400` o `503`; las rechazadas por el límite de usuario NO DEBEN consumir la cuota de la compañía. Al superarlo DEBE responder `429` `application/problem+json` con el `detail` fijo por minuto de la compañía, cabecera `Retry-After` (entero positivo) y las cabeceras CORS de un origen permitido. El límite de una compañía NO DEBE afectar a otra. Las peticiones anónimas NO DEBEN consumir la cuota de ninguna compañía. NO DEBE existir ningún modo de que el Manager lo edite.

#### Scenario: Límite de compañía superado
- DADO `Ai__RequestsPerMinutePerCompany` = 3 y tres peticiones de usuarios distintos de A en el mismo minuto
- CUANDO un cuarto usuario de A envía una petición
- ENTONCES recibe `429` con `Retry-After` y el `detail` por minuto de la compañía, y el modelo no se invoca

#### Scenario: Otra compañía no afectada
- DADO la compañía A con el límite agotado
- CUANDO un usuario de B envía una petición válida
- ENTONCES recibe `200`

#### Scenario: Rechazo por usuario no consume la compañía
- DADO `Ai__RequestsPerMinute` = 1, `Ai__RequestsPerMinutePerCompany` = 2 y un usuario de A que ya hizo 1 petición
- CUANDO el mismo usuario envía otras 3 y después un segundo usuario de A envía una
- ENTONCES las 3 reciben `429` por límite de usuario y la del segundo usuario no recibe `429`

#### Scenario: Anónimo no consume cuota de compañía
- DADO muchas peticiones anónimas
- CUANDO después un usuario autenticado de A envía su primera petición
- ENTONCES recibe `200` y las anónimas recibieron `401`

#### Scenario: CORS en el 429 de compañía
- DADO `Origin: http://localhost:5172` permitido y una compañía que supera el límite
- CUANDO se inspecciona la respuesta `429`
- ENTONCES incluye `Access-Control-Allow-Origin: http://localhost:5172` y las cuatro cabeceras de seguridad

### Requirement: Tope diario por compañía

El endpoint DEBE aplicar además un tope diario agregado por compañía configurable con `Ai:DailyRequestsPerCompany` (500 por defecto), contado por día natural de Europe/Madrid. Al alcanzarlo DEBE responder `429` `application/problem+json` con el `detail` diario de la compañía y `Retry-After` igual a los segundos hasta la siguiente medianoche local, sin invocar al modelo. Se consume una unidad solo después de validar la petición y de comprobar que el asistente de su compañía está habilitado; las peticiones rechazadas con `400`, `401`, `413`, `503` o por el límite de usuario NO DEBEN contar para la compañía, y una petición rechazada por el tope de compañía NO DEBE consumir el tope diario del usuario. El tope de una compañía NO DEBE afectar a otra. El contador es en memoria de la instancia (limitación asumida, ADR 0017).

#### Scenario: Tope de compañía alcanzado
- DADO `Ai__DailyRequestsPerCompany` = 3 y tres peticiones correctas de usuarios distintos de A hoy
- CUANDO un cuarto usuario de A envía otra
- ENTONCES recibe `429` con el `detail` diario de la compañía y `Retry-After`, y el modelo no se invoca

#### Scenario: Reinicio a medianoche local
- DADO la compañía A con el tope agotado y un reloj falso a las 23:59 de Europe/Madrid
- CUANDO el reloj avanza pasada la medianoche local y un usuario de A envía otra petición válida
- ENTONCES recibe `200`

#### Scenario: Retry-After hasta medianoche
- DADO la compañía A con el tope agotado y un reloj falso a las 22:00 de Europe/Madrid
- CUANDO un usuario de A envía otra petición
- ENTONCES `Retry-After` es 7200

#### Scenario: Otra compañía no afectada
- DADO la compañía A con el tope diario agotado
- CUANDO un usuario de B envía una petición válida
- ENTONCES recibe `200`

#### Scenario: Entradas inválidas no cuentan
- DADO `Ai__DailyRequestsPerCompany` = 2 y dos peticiones de A con `message` vacío
- CUANDO un usuario de A envía después una petición válida
- ENTONCES recibe `200`

#### Scenario: Rechazo por la compañía no consume el tope de usuario
- DADO la compañía A con el tope diario agotado y un usuario de A con su tope diario sin consumir
- CUANDO ese usuario envía una petición y recibe `429`
- ENTONCES su contador diario de usuario no cambia

#### Scenario: Valor por defecto
- DADO el asistente habilitado sin `Ai:DailyRequestsPerCompany`
- CUANDO se enlazan las opciones
- ENTONCES el tope diario por compañía es 500
