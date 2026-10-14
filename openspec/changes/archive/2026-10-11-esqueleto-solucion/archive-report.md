# Informe de archivo: esqueleto-solucion

**Fecha**: 2026-10-11 · **Modo**: openspec · **Archivado en**: `openspec/changes/archive/2026-10-11-esqueleto-solucion/`
**Estado**: archivado de forma intencional, con avisos (PASS WITH WARNINGS, 0 CRITICAL). No se ejecutó ningún comando Git; no existe `.git`.

## Resumen

El cambio crea el esqueleto de la solución TimeTracker: 9 proyectos .NET 10 con Clean Architecture, el objeto de valor `WorkedHours` (TDD, 100 % de cobertura en Domain), el endpoint `GET /health`, configuración validada al arrancar con CORS restrictivo, prueba de arquitectura, contrato OpenAPI exportado, Docker, Compose, CI, 7 ADR y README. Verificación: 27/27 requisitos y 53/53 escenarios con evidencia (52 CUMPLE, 1 PARCIAL: SA-18); 52/52 tareas completas; 49 pruebas en verde.

Decisiones del usuario al archivar: la mención del TFM en `proposal.md` se deja tal cual (W6, sin editar) y la excepción `size:exception` ya estaba aceptada.

## Especificaciones fusionadas

Las 4 capacidades eran nuevas (no existían especificaciones principales), por lo que cada delta se copió íntegro a `openspec/specs/{capacidad}/spec.md`. Todos los requisitos son ADDED; no hubo MODIFIED, REMOVED ni RENAMED.

| Capacidad | Requisitos | Escenarios |
|---|---|---|
| solution-architecture | 11 | 20 |
| api-health | 4 | 8 |
| startup-configuration | 6 | 12 |
| worked-hours | 6 | 13 |
| **Total** | **27** | **53** |

## Avisos abiertos del verify

- **W1**: CI sin ejecutar ni analizada como YAML (SA-18 PARCIAL). Validar en el primer push cuando exista remoto; `pwsh` en `ubuntu-latest` sin verificar.
- **W2**: la prueba de arquitectura solo detecta referencias realmente usadas (el compilador elimina las no usadas). Un `FrameworkReference` o paquete sin uso en Domain no hace fallar ninguna prueba. **Desviación de D-5 no registrada en `tasks.md`**: el diseño prometía lectura XML para ese caso, pero solo se aplica a Web. Se deja constancia aquí.
- **W3**: `Get_HeadersDoNotExposeMachineNameOrConfiguration` es débil: `TestServer` nunca emite `Server`, así que no protege `AddServerHeader = false`. Comprobado a mano en Kestrel y en el contenedor.
- **W4**: `Program.cs` contiene una rama para `GetDocument.Insider` (origen ficticio `https://openapi.example.test`) para poder exportar OpenAPI; depende del nombre interno de una herramienta del SDK (ADR 0004).
- **W5**: el contenedor sin `Cors__AllowedOrigins__0` sale con código 139 (SIGSEGV), no 134. Es distinto de 0, como exige la especificación.
- **W7**: evidencia no reproducida de forma independiente: SC-4 (por equivalencia con SC-3), SA-4 (ciclo Domain a Application) y AH-8 (mutación solo registrada en `tasks.md`).
- Otros: W6 (TFM en `proposal.md`, aceptado por el usuario) y W8 (`Application.Tests` sin pruebas, esperado).

## Trabajo futuro recomendado

1. Ejecutar la CI en el primer push y validar el YAML y `pwsh` en el runner (W1); considerar comprobar en CI que `docs/api/openapi.json` coincide con el regenerado.
2. Añadir a la prueba de arquitectura una comprobación por XML de que Domain no contiene `PackageReference`, `FrameworkReference` ni `ProjectReference` (W2).
3. Reforzar la prueba de cabeceras con Kestrel real, o con una prueba que verifique `AddServerHeader = false` (W3).
4. Sustituir la rama `GetDocument.Insider` por una exportación con `WebApplicationFactory` cuando sea viable (W4).
5. Añadir una prueba de lista vacía para SC-4 (S1) y descubrir los `.csproj` por enumeración en `RepositoryConventionsTests` (S2).
6. Retirar el fijado de `Microsoft.OpenApi` 2.7.6 cuando `Microsoft.AspNetCore.OpenApi` arrastre una versión corregida (S3); restringir `AllowedHosts` en el cambio de despliegue (S4).
7. Fijar acciones de CI por SHA e imágenes base por digest en el cambio de Dependabot (S7).
8. Siguiente cambio previsto: `fichaje-extremo-a-extremo`.

## Limpieza

`openspec/config.yaml` (actualizado con los comandos reales, `status: available`) es la versión vigente; `openspec/config.yaml.bak` era el artefacto de reversión y se elimina.
