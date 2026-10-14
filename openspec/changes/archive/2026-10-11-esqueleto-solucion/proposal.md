# Propuesta: esqueleto-solucion

Fecha: 2026-10-10 · Fase: sdd-propose · Base: `exploration.md` (D1–D13) y respuestas del usuario a Q1–Q9.

## Intención

TimeTracker no tiene código: no hay solución, pruebas, contenedor ni comandos de calidad. Faltan unos 15 días para el TFM (25-oct-2026) y se dispone de poco tiempo al día. Cada cambio funcional posterior necesita una base que compile, ejecute pruebas, haga cumplir Clean Architecture y documente sus decisiones. Este cambio crea esa base y nada más.

**Éxito:** el siguiente cambio (fichaje de extremo a extremo) puede empezar con TDD sin tener que tocar la estructura, las herramientas ni la calidad.

## Hoja de ruta que habilita (cambios posteriores, en orden)

1. `fichaje-extremo-a-extremo`: primer fichaje con PostgreSQL y EF Core, migraciones, Testcontainers y servicio `postgres` en compose. **Mínimo imprescindible del TFM.**
2. `autenticacion-y-roles`: roles empleado y responsable, con identidad tomada del token.
3. `ausencias`: si queda tiempo.
4. `asistente-ia-solo-lectura`: `IChatClient`, herramientas de solo lectura y `IChatClient` falso en las pruebas. Si queda tiempo.
5. Más adelante: despliegue en Railway, hooks de Git y Dependabot, una vez exista el repositorio remoto.

## Alcance

### Dentro
- Raíz: `TimeTracker.sln` (formato clásico), `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.gitignore`, `.gitattributes`, `.dockerignore` y `.env.example`.
- `src/`: `TimeTracker.Domain`, `TimeTracker.Application`, `TimeTracker.Infrastructure`, `TimeTracker.Api` (minimal APIs, composition root) y `TimeTracker.Web` (`blazorwasm` standalone, sin PWA ni autenticación).
- `tests/`: `Domain.Tests`, `Application.Tests`, `Api.IntegrationTests` y `ArchitectureTests`, con xUnit v3 (respaldo: v2) y Coverlet en formato Cobertura.
- Objeto de valor `WorkedHours`: rechaza valores negativos y permite sumar. Es mínimo y se reutilizará en los fichajes.
- `GET /health` con prueba de integración; opciones tipadas con `ValidateOnStart()` (`Cors:AllowedOrigins`) y una prueba de fallo al arrancar.
- Prueba de arquitectura de la regla de dependencias.
- OpenAPI integrado y exportado a `docs/api/openapi.json`.
- `Dockerfile` de la API (multi-stage, usuario no root, `HEALTHCHECK`, puerto por variable de entorno) y `docker-compose.yml` con un único servicio, `api`.
- `.github/workflows/ci.yml` (no ejecutable hasta que exista el remoto), ADR 0001–0007 y `README.md`.
- Actualizar `openspec/config.yaml` con los comandos reales (en apply/archive).

### Fuera
Fichajes, ausencias, saldo, horas extra e IA · `DbContext`, PostgreSQL y Testcontainers · autenticación y roles · servir el WASM desde la API y el despliegue en Railway · rate limiting y cabeceras de seguridad · hooks de Git, `git init` y Dependabot · proyecto `Contracts` · Swagger UI · E2E · observabilidad.

## Capacidades

### Nuevas
- `solution-architecture`: estructura de capas y regla de dependencias comprobada por prueba.
- `api-health`: endpoint `/health` y su contrato OpenAPI.
- `startup-configuration`: configuración por variables de entorno, validación al arrancar y CORS restrictivo.
- `worked-hours`: invariantes del objeto de valor `WorkedHours`.

### Modificadas
Ninguna (no hay especificaciones previas).

## Enfoque

- **net10.0** (LTS hasta nov-2028; SDK 10.0.302 instalado).
- **`.sln` clásico** (`dotnet new sln --format sln`): VS Code no tiene ahora extensiones de C# y no se ha confirmado en la documentación oficial que C# Dev Kit admita `.slnx`. Se podrá migrar a `.slnx` más adelante con un ADR.
- **Regla de dependencias:** `ProjectReference` estrictos más una prueba por reflexión (`Assembly.GetReferencedAssemblies()`). Así se evita depender de NetArchTest, cuya compatibilidad con net10.0 no está verificada.
- **Pruebas:** proyectos xUnit v3 creados a mano con el paquete `xunit.v3`, sin instalar plantillas. Un único ejecutor fijado en `global.json`, que se decide en el diseño.
- **D8 (PostgreSQL): se difiere al cambio de fichaje.** Aunque Docker funciona, sin entidades no hay nada que validar y se gastarían líneas del presupuesto. El compose contiene solo `api`.
- **D5 refinado:** el WASM se ejecuta localmente con `dotnet run` y llama a la API con CORS. Servir el WASM desde la API (un único origen, adecuado para Railway) se decide en el cambio de despliegue.
- **12-factor:** sin secretos en la imagen; puerto y orígenes por variables de entorno; `.env.example` sin valores.

### Tandas (un solo cambio, entrega `single-pr`)
- **Tanda 1, núcleo compilable:** archivos raíz, 5 proyectos, 4 proyectos de pruebas, `WorkedHours` en TDD, `/health`, validación al arrancar, prueba de arquitectura, cobertura y `dotnet format`. Punto de control: build y test en verde.
- **Tanda 2, entrega:** `Dockerfile`, compose, CI, exportación de OpenAPI, ADR 0001–0007, `README.md` y actualización de `config.yaml`.

### ADR (`docs/adr/`)
| N.º | Decisión |
|---|---|
| 0001 | Formato de ADR |
| 0002 | net10.0 |
| 0003 | Monolito modular, regla de dependencias y `.sln` clásico |
| 0004 | Minimal APIs y OpenAPI exportado |
| 0005 | WASM standalone, CORS restrictivo y la forma de servirlo diferida |
| 0006 | Pila de pruebas (xUnit v3/v2, ejecutor, Coverlet, prueba de arquitectura por reflexión) |
| 0007 | Configuración por entorno y validación al arrancar |

## Áreas afectadas

| Área | Impacto | Descripción |
|---|---|---|
| Raíz (`*.sln`, `*.props`, `global.json`, archivos dot, `Dockerfile`, `docker-compose.yml`) | Nuevo | Solución y calidad |
| `src/`, `tests/` | Nuevo | 5 proyectos y 4 proyectos de pruebas |
| `docs/adr/`, `docs/api/`, `README.md` | Nuevo | Documentación |
| `.github/workflows/ci.yml` | Nuevo | CI (sin ejecutar) |
| `openspec/config.yaml` | Modificado | Comandos de prueba, build y cobertura |

## Riesgos

| Riesgo | Prob. | Mitigación |
|---|---|---|
| xUnit v3 + Coverlet sin verificar de extremo a extremo | Media | Criterio: informe generado; si no, respaldo a v2 |
| Avisos de la plantilla Blazor con `-warnaserror` | Media | Afinar `.editorconfig`, nunca desactivar |
| VS Code sin extensión de C# | Alta | Requisito previo del usuario (ver Dependencias) |
| CI sin probar hasta que exista el remoto | Alta | Reproducir sus pasos localmente; anotarlo |
| Tamaño > 400 líneas | Media | Tandas con punto de control; posible `size:exception` |
| Calendario ajustado | Alta | Alcance mínimo; las tandas permiten parar tras la 1 |
| Versiones de las imágenes base sin verificar | Baja | Comprobarlas en el diseño |

## Plan de reversión

No hay Git. Eliminar `src/`, `tests/`, `docs/`, `.github/` y los archivos raíz creados; restaurar `openspec/config.yaml` con la copia previa que se guarda en apply. Eliminar la imagen local (`docker image rm`). `AGENTS.md` y `openspec/` no se ven afectados.

## Dependencias

- Acción del usuario: instalar la extensión de C# (C# Dev Kit o C#) en VS Code.
- Restauración de NuGet con red durante apply (paquetes xUnit v3, Coverlet, OpenAPI).
- Docker Desktop en marcha (confirmado, versión 29.7.2).

## Criterios de éxito

- [ ] `dotnet build -warnaserror` sin errores ni avisos.
- [ ] `dotnet test` en verde: `WorkedHours`, `/health`, fallo al arrancar sin configuración y regla de dependencias.
- [ ] Informe de cobertura Cobertura generado; `Domain` al 100 %.
- [ ] La prueba de arquitectura falla si se introduce una referencia ilegal (comprobado una vez).
- [ ] `dotnet format --verify-no-changes` limpio.
- [ ] `docker build` genera la imagen y `/health` responde dentro del contenedor.
- [ ] `docs/api/openapi.json` generado en el build.
- [ ] ADR 0001–0007 y `README.md` presentes; `config.yaml` con los comandos reales.

## Previsión de carga de revisión

- Estimación: 350–550 líneas escritas a mano (sin contar las generadas por plantilla); tanda 1 ≈ 250–300, tanda 2 ≈ 200–250.
- Decision needed before apply: Yes (aceptar `size:exception` si sdd-tasks confirma que se superan las 400 líneas)
- Chained PRs recommended: No (estrategia `single-pr`, sin repositorio; las tandas sirven de puntos de control)
- 400-line budget risk: Medium

## Ronda de preguntas de propuesta

Ronda 1 resuelta (Q1–Q9). Ronda 2 resuelta (2026-10-10), decisiones del usuario:
- **Pantalla de marcajes:** el TFM mostrará una pantalla en Blazor para ver los marcajes (fichajes). Pertenece al cambio `fichaje-extremo-a-extremo`, no a este; aquí Web solo muestra la plantilla.
- **`WorkedHours` en minutos enteros:** el cálculo se hace con minutos enteros (sin decimales ni redondeo).
- **Presupuesto de líneas:** se acepta superar las 400 líneas (`size:exception`) si `sdd-tasks` lo confirma. Estrategia `single-pr`, sin cambios.
- **Editor:** extensión de C# (`ms-dotnettools.csharp`) instalada en VS Code. C# Dev Kit no se instala: sus condiciones de licencia dependen del uso comercial y quedan a decisión del usuario; por eso se mantiene `.sln` clásico.

Supuestos vigentes:
- El esqueleto no incluye ninguna pantalla funcional; Web solo muestra la plantilla.
- Si en la tanda 1 se consume más de la mitad del tiempo disponible, se prioriza el fichaje y se aplaza la tanda 2.
