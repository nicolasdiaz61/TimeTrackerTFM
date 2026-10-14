# ADR 0004: Minimal APIs y contrato OpenAPI exportado

- Estado: Aceptado
- Fecha: 2026-10-11

## Contexto

`AGENTS.md` define el contrato OpenAPI como fuente de verdad, generado a partir del código, y exige actualizarlo con cada cambio de endpoint. Hace falta un estilo de endpoints ligero y un mecanismo que mantenga el contrato versionado sin edición manual.

## Decisión

- Los endpoints son Minimal APIs agrupadas en clases estáticas `*Endpoints` (por ejemplo, `HealthEndpoints.MapHealthEndpoints()`). Cuando existan módulos de negocio, cada uno usará `MapGroup` (`/api/...`).
- `/health` es un `MapGet` que devuelve `{"status":"Healthy"}` en JSON, no `AddHealthChecks`: este último responde `text/plain` por defecto y no aparece en OpenAPI.
- OpenAPI se registra con `AddOpenApi()` (`Microsoft.AspNetCore.OpenApi`) y solo se sirve en desarrollo (`MapOpenApi()`).
- El build de `TimeTracker.Api` exporta el contrato a `docs/api/openapi.json` con `Microsoft.Extensions.ApiDescription.Server` (`OpenApiDocumentsDirectory` y `--file-name openapi`). El archivo se versiona y no se edita a mano; la prueba `OpenApiContractTests` comprueba que contiene `GET /health` con respuesta 200.
- Durante la generación, la herramienta arranca la aplicación sin la configuración real y la validación de arranque fallaría. `Program.cs` detecta ese proceso (`GetDocument.Insider`) y aporta solo ahí un origen ficticio (`https://openapi.example.test`); la validación no se relaja en ejecución normal.
- El `publish` de Docker usa `-p:OpenApiGenerateDocuments=false`: el contrato lo genera el build local, no la imagen.

## Consecuencias

- El contrato cambia en el mismo cambio que el endpoint y se revisa en la pull request.
- La exportación depende de que el build pueda arrancar la aplicación; el origen ficticio es el precio de mantener la validación estricta.
- `Microsoft.AspNetCore.OpenApi` 10.0.10 arrastra `Microsoft.OpenApi` 2.0.0, con una vulnerabilidad conocida (GHSA-v5pm-xwqc-g5wc) que el restore trata como error (NU1903). Se eleva con una referencia directa a la 2.7.6 (corregida), gestionada en `Directory.Packages.props`. Debe revisarse cuando Microsoft publique una versión de `Microsoft.AspNetCore.OpenApi` que ya depende de una versión corregida.
- Está pendiente, para cuando exista repositorio, un paso de CI que compare el contrato versionado con el regenerado.

## Alternativas

- **Controladores MVC:** más ceremonia para endpoints pequeños y sin beneficio en este tamaño.
- **Swagger UI / Swashbuckle:** una dependencia más; el documento exportado y versionado cubre la necesidad.
- **Generar el contrato dentro de una prueba:** funciona (queda como respaldo), pero el archivo no se regenera al compilar.
- **`AddHealthChecks`:** formato de respuesta por defecto no JSON y fuera del contrato OpenAPI.
