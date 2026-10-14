# Delta para api-health

Cambio: `autenticacion-y-roles`. La política de respaldo autenticada (ver `authentication`) exige usuario autenticado a todo endpoint que no sea anónimo. `GET /health` sigue siendo anónimo (`AllowAnonymous`). Efecto lateral aceptado (V13 del diseño): una petición sin token a una ruta o método sin endpoint propio (por ejemplo `POST /health`) recibe `401` en lugar de `405`; el `405` solo se observa con un cliente autenticado. Solo cambia el escenario «Método no permitido»; el resto de requisitos de la especificación vigente no cambia.

## MODIFIED Requirements

### Requirement: Respuesta de salud

La API DEBE responder a `GET /health` con código `200`, tipo de contenido `application/json` (la cabecera real es `application/json; charset=utf-8`; solo se exige el tipo de medio) y el cuerpo exacto `{"status":"Healthy"}`. El endpoint NO DEBE requerir autenticación ni cabeceras especiales para `GET`, aunque el resto de endpoints exijan un usuario autenticado. Con un cliente autenticado, un método distinto de `GET` DEBE recibir `405`; sin token DEBE recibir `401`.
(Previously: «Método no permitido» no distinguía entre cliente autenticado y anónimo, y el escenario devolvía `405` siempre)

#### Scenario: API en marcha
- DADO la API arrancada con configuración válida
- CUANDO un cliente envía `GET /health` sin cabecera `Authorization`
- ENTONCES recibe `200`, `application/json` y el cuerpo `{"status":"Healthy"}`

#### Scenario: Método no permitido
- DADO la API arrancada y un cliente autenticado con un token vigente
- CUANDO envía `POST /health`
- ENTONCES recibe `405`

#### Scenario: Método no permitido sin token
- DADO la API arrancada y una petición sin cabecera `Authorization`
- CUANDO envía `POST /health`
- ENTONCES recibe `401` (la política de respaldo autenticada se evalúa antes que el `405`)
