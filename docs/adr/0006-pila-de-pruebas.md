# ADR 0006: Pila de pruebas

- Estado: Aceptado
- Fecha: 2026-10-11

## Contexto

`AGENTS.md` exige TDD, xUnit, pruebas de integración de la API y cobertura por niveles: 100 % de líneas y ramas en el dominio, 80 % o más en la aplicación y sin umbral en infraestructura. Hace falta una pila que funcione igual en local (Windows) y en la integración continua (Linux) con el SDK 10.

## Decisión

- **Ejecutor:** VSTest, el predeterminado del SDK 10. No se configura Microsoft.Testing.Platform (MTP) en `global.json`.
- **Framework:** xUnit **v2** (`xunit` 2.9.3 con `xunit.runner.visualstudio`). Se evaluó xUnit v3 (`xunit.v3` 4.0.2) y se descartó: arrastra `Microsoft.Testing.Platform.MSBuild` y `dotnet test` falla en el SDK 10 con "Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later". Es una incompatibilidad de v3 con VSTest, no un fallo de configuración. Los proyectos de prueba no usan `OutputType=Exe`.
- **Cobertura:** `coverlet.collector` con `--collect:"XPlat Code Coverage"`, que genera un `coverage.cobertura.xml` por proyecto de pruebas.
- **Umbrales:** `scripts/check-coverage.ps1` toma el mejor `line-rate` y `branch-rate` del paquete entre los informes y exige el mínimo indicado (100 % para `TimeTracker.Domain`); un paquete sin ramas cuenta como 100 %. Funciona igual con `pwsh` en Windows y en GitHub Actions.
- **Integración:** `WebApplicationFactory<Program>` (`Microsoft.AspNetCore.Mvc.Testing`).
- **Arquitectura:** reflexión y lectura de `.csproj` (ADR 0003), sin biblioteca adicional.
- Todas las versiones de paquetes están centralizadas en `Directory.Packages.props`.

## Consecuencias

- No hay ejecutor nuevo que aprender: los comandos son `dotnet test` con VSTest.
- Las pruebas no pueden usar las APIs exclusivas de xUnit v3 (por ejemplo, `TestContext.Current.CancellationToken`).
- Un proyecto de pruebas sin pruebas (`Application.Tests`) termina con código 0 y un mensaje informativo.
- El umbral de cobertura lo aplica un script propio (pocas líneas) y no el recolector.
- Migrar a xUnit v3 queda pendiente de que sea compatible con VSTest en el SDK 10 o de adoptar MTP de forma completa; sería otro ADR que sustituya a este.

## Alternativas

- **xUnit v3 con MTP:** exige cambiar ejecutor, comandos y cobertura (`coverlet.MTP`, no verificado) y trata "0 pruebas" como fallo.
- **`coverlet.msbuild` con `/p:Threshold`:** no se mezcla con `coverlet.collector` y complica los informes por proyecto.
- **ReportGenerator:** otra herramienta para un umbral que resuelve un script de 40 líneas.
- **NetArchTest:** dependencia innecesaria (ADR 0003).
