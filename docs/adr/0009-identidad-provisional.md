# ADR 0009: Identidad provisional del empleado

- Estado: Sustituido por 0011
- Fecha: 2026-10-12

## Contexto

`AGENTS.md` exige que el identificador del usuario salga siempre del token y nunca de un parámetro que pueda manipular el cliente. Todavía no existe autenticación, pero los fichajes ya deben pertenecer a un empleado concreto y aislarse entre empleados. Hace falta una identidad que sirva para desarrollar sin dejar una puerta abierta en producción.

## Decisión

- **Puerto:** `Application` define `ICurrentUser` (con `EmployeeId`) y los casos de uso solo conocen esa abstracción.
- **Adaptador provisional:** `DevelopmentCurrentUser` (en `Api`) devuelve el empleado fijo configurado en `Identity:DevelopmentEmployeeId` (variable `Identity__DevelopmentEmployeeId`, un GUID inventado). Solo se registra cuando el entorno es `Development`.
- **Fallo de arranque fuera de Development:** `ProvisionalIdentityOptionsValidator` y `ValidateOnStart` impiden arrancar en cualquier otro entorno y el mensaje nombra `Identity`. En `Development` el GUID debe existir, tener formato válido y no ser vacío. No se imprime el valor.
- **Ninguna identidad del cliente:** la API ignora `?employeeId=` y la cabecera `X-Employee-Id`; el contrato OpenAPI no expone `employeeId` y la Web no lo envía. Hay pruebas de aislamiento con dos empleados sobre la misma base.
- **Generación del contrato:** `dotnet-getdocument` arranca la aplicación al compilar. `DocumentGeneration` fuerza `Development` y aporta valores ficticios (origen CORS, cadena de conexión sin servidor real, GUID inventado y migraciones desactivadas) solo en ese proceso. Amplía lo acordado en el [ADR 0004](0004-minimal-apis-y-openapi.md) (origen ficticio) sin reescribirlo, y no relaja ninguna validación en la ejecución normal.
- **Compose solo para desarrollo:** `docker-compose.yml` fija `ASPNETCORE_ENVIRONMENT=Development` y publica los puertos únicamente en `127.0.0.1`.

## Consecuencias

- Es una identidad **provisional**: no hay usuarios, contraseñas ni roles. Cualquiera que alcance la API en `Development` actúa como ese empleado. No debe desplegarse ni exponerse fuera de la máquina local.
- El cambio `autenticacion-y-roles` sustituirá `DevelopmentCurrentUser` por un adaptador basado en el token sin tocar `Application` ni los endpoints.
- Un despliegue accidental en `Production` falla al arrancar en lugar de funcionar sin autenticación.
- El README avisa de que la identidad es provisional y de que no hay usuario de prueba real.

## Alternativas

- **Cabecera o parámetro con el identificador del empleado:** permite suplantar a cualquiera; contradice `AGENTS.md`.
- **JWT desde ya:** añade emisión de tokens, claves y gestión de usuarios que quedan fuera del alcance de este cambio.
- **Esquema de autenticación «Development» de ASP.NET Core:** más piezas (esquema, handler, política) para un resultado equivalente al de `ICurrentUser`.
- **Registrar siempre una identidad por defecto:** el arranque no fallaría en producción.
