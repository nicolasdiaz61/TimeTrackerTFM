# ADR 0005: Frontend Blazor WebAssembly independiente y CORS restrictivo

- Estado: Aceptado
- Fecha: 2026-10-11

## Contexto

`AGENTS.md` establece que el frontend es Blazor WebAssembly como proyecto independiente, que no contiene reglas de negocio y que solo llama a la API. Si la API y el frontend se sirven desde orígenes distintos, el navegador aplica CORS. La plantilla "hosted" de Blazor WebAssembly ya no tiene soporte en .NET 10.

## Decisión

- `TimeTracker.Web` es una aplicación WebAssembly independiente (plantilla `blazorwasm`, sin PWA ni autenticación), sin `ProjectReference` a ningún otro proyecto. Se comunica con la API únicamente por HTTP.
- La API aplica una política CORS por defecto restrictiva: solo los orígenes de `Cors:AllowedOrigins` (variable `Cors__AllowedOrigins__N`), con cualquier cabecera y método, sin credenciales ni comodines. La configuración se valida al arrancar (ADR 0007).
- Dónde se sirve el WebAssembly en producción (por ejemplo, desde la propia API) se decide en el cambio de despliegue, no aquí.
- La URL base de la API en el frontend queda fuera de este cambio y se documenta en el README.

## Consecuencias

- En desarrollo hay dos orígenes (API y Web): hay que añadir el origen de Web a `Cors__AllowedOrigins__0` antes de arrancar la API.
- Un origen mal configurado se detecta al arrancar, no cuando una persona usuaria ve un error de CORS.
- Si se sirve el WebAssembly desde la API, CORS deja de ser necesario para ese origen y este ADR se sustituirá.

## Alternativas

- **Plantilla hosted:** sin soporte en .NET 10.
- **Servir el WebAssembly desde la API desde el principio:** acopla el despliegue antes de tener decisiones de hosting; se aplaza.
- **CORS abierto (`*`):** contrario a `AGENTS.md` (CORS restrictivo) y rechazado por el validador de arranque.
