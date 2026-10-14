# Especificación: api-health

## Purpose

Define el contrato del endpoint `GET /health` de `TimeTracker.Api`: comprobación de vida mínima usada por el `HEALTHCHECK` del contenedor y por las pruebas. Capacidad NUEVA; todos los requisitos son `ADDED`.

`/health` es una comprobación de vida (liveness) sin dependencias externas ni consulta a la base de datos. Sigue siendo anónimo (`AllowAnonymous`) tras la política de respaldo autenticada de `authentication`; efecto aceptado: `POST /health` sin token recibe `401` y no `405`.

## ADDED Requirements

### Requirement: Respuesta de salud

La API DEBE responder a `GET /health` con código `200`, tipo de contenido `application/json` (la cabecera real es `application/json; charset=utf-8`; solo se exige el tipo de medio) y el cuerpo exacto `{"status":"Healthy"}`. El endpoint NO DEBE requerir autenticación ni cabeceras especiales para `GET`, aunque el resto de endpoints exijan un usuario autenticado. Con un cliente autenticado, un método distinto de `GET` DEBE recibir `405`; sin token DEBE recibir `401`.

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

### Requirement: Sin información sensible

La respuesta de `/health` NO DEBE exponer versión, nombre de máquina, rutas, variables de entorno, valores de configuración, cadenas de conexión ni trazas de pila. NO DEBE contener más propiedades que `status`.

#### Scenario: Solo la propiedad status
- DADO la respuesta de `GET /health`
- CUANDO se analiza el JSON del cuerpo
- ENTONCES contiene exactamente una propiedad, `status`

#### Scenario: Cabeceras sin versión
- DADO la respuesta de `GET /health`
- CUANDO se inspeccionan sus cabeceras
- ENTONCES ninguna contiene el nombre de la máquina ni valores de configuración

### Requirement: Presencia en el contrato OpenAPI

El documento `docs/api/openapi.json` DEBE generarse en el build desde el código e incluir la ruta `/health` con la operación `get` y la respuesta `200`. NO DEBE editarse a mano.

#### Scenario: Documento exportado
- DADO el build de `TimeTracker.Api` ejecutado
- CUANDO se lee `docs/api/openapi.json`
- ENTONCES es JSON válido y `paths["/health"].get.responses["200"]` existe

#### Scenario: Regeneración tras cambiar un endpoint
- DADO un endpoint añadido o modificado en `TimeTracker.Api`
- CUANDO se ejecuta el build
- ENTONCES `docs/api/openapi.json` refleja el cambio sin edición manual

Nota de verificación: la exportación en build depende de `Microsoft.Extensions.ApiDescription.Server`, cuya versión y comportamiento se confirman en diseño; no se ha probado de extremo a extremo.

### Requirement: Prueba de integración

DEBE existir una prueba en `TimeTracker.Api.IntegrationTests` que use `WebApplicationFactory` y compruebe código, tipo de contenido y cuerpo de `GET /health`, sin red ni contenedores.

#### Scenario: La prueba pasa
- DADO la prueba `Health_returns_healthy` (nombre orientativo) en `TimeTracker.Api.IntegrationTests`
- CUANDO se ejecuta `dotnet test`
- ENTONCES pasa con la API en memoria y `Cors:AllowedOrigins` válido

#### Scenario: La prueba detecta una regresión
- DADO el endpoint `/health` eliminado o con otro cuerpo
- CUANDO se ejecuta `dotnet test`
- ENTONCES la prueba falla
