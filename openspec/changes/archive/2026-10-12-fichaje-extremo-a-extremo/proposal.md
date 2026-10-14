# Propuesta: fichaje-extremo-a-extremo

Fecha: 2026-10-11 · Fase: sdd-propose · Base: `exploration.md` (D1–D16) y respuestas del usuario a Q1–Q12 (todos los valores por defecto aceptados).

## Intención

TimeTracker tiene un esqueleto que compila, pero ninguna funcionalidad: un empleado no puede fichar ni ver sus fichajes. Es el mínimo imprescindible del TFM (25-oct-2026, 14 días). Este cambio entrega el primer flujo completo: fichar entrada y salida, persistir en PostgreSQL y consultar los fichajes con las horas trabajadas en una pantalla Blazor.

**Éxito:** con `docker compose up` (o API + Web locales), un empleado provisional ficha entrada y salida desde `/fichajes` y ve sus últimas entradas con los minutos trabajados y el total; las reglas están cubiertas por pruebas (Domain 100 %, Application ≥ 80 %) e integración real con PostgreSQL.

## Alcance

### Dentro
- **Domain:** entidad `TimeEntry` (`Start`, `Close`, `IsOpen`, `CalculateWorked()`), UTC validado, minutos truncados, reutiliza `WorkedHours`.
- **Application:** puertos `ITimeEntryRepository` e `ICurrentUser`; casos de uso `ClockIn`, `ClockOut`, `GetTimeEntries` con `TimeProvider`; excepciones tipadas.
- **Infrastructure:** `DbContext`, mapeo Fluent, índices `(EmployeeId, ClockInAt DESC)` y único parcial de entrada abierta, traducción de `23505`, migración inicial, `IDesignTimeDbContextFactory`.
- **Api:** `POST /api/time-entries/clock-in` (201/409), `POST /api/time-entries/clock-out` (200/409), `GET /api/time-entries` (últimas 50 + `totalWorkedMinutes`); ProblemDetails en español; adaptador `ICurrentUser` solo en Development; `ConnectionStrings:Default` validada; bandera `Database:ApplyMigrationsOnStartup`; contrato OpenAPI ampliado.
- **Web:** página `/fichajes` (tabla, total, botones "Fichar entrada/salida", UTC etiquetado), `TimeEntriesApiClient`, `ApiBaseUrl`.
- **Pruebas:** unitarias con `FakeTimeProvider`; integración con Testcontainers (`postgres:18-alpine`): repositorio, carrera de doble fichaje, API de extremo a extremo, aislamiento entre empleados, migraciones sin cambios pendientes.
- **Entrega:** servicio `postgres` en compose (healthcheck, volumen, `127.0.0.1:5432`), `.env.example`, `dotnet-ef` local, paso de cobertura de Application en CI, ADR 0008–0010, README y `config.yaml`.

### Fuera
Autenticación real y roles (JWT) · tabla `Employee` · atribución por día, saldos, horas extra y tope de entrada olvidada · edición o borrado de fichajes · paginación · zona horaria local · ausencias e IA · bUnit/E2E · despliegue, Git, hooks y Dependabot.

## Capacidades

### Nuevas
- `time-tracking`: reglas de fichaje (una entrada abierta por empleado, salida ≥ entrada, cálculo en minutos truncados, total solo de cerradas) y casos de uso.
- `time-entries-persistence`: almacenamiento PostgreSQL, índices, migraciones y traducción de conflictos.
- `time-entries-ui`: pantalla `/fichajes` y cliente HTTP tipado.

### Modificadas
- `solution-architecture`: compose pasa de solo `api` a `api` + `postgres`; además, en la fase de specs evaluar deltas por Web ya no limitada a la plantilla, paso de cobertura de Application en CI y ADR hasta 0010.
- `startup-configuration`: `ConnectionStrings:Default` obligatoria con `ValidateOnStart`, `Identity:DevelopmentEmployeeId` (solo Development), `Database:ApplyMigrationsOnStartup` y sus claves en `.env.example`.

## Enfoque

Backend completo con pruebas primero; Web mínima al final (enfoque 1 de la exploración). Identidad provisional detrás de `ICurrentUser`: ningún endpoint recibe `employeeId` (AGENTS §6); fuera de Development la API no arranca. Tiempo por `TimeProvider`, todo en UTC (ISO 8601 con `Z`). Familia EF fijada en 10.0.12 (Npgsql 10.0.3).

### Tandas (un solo cambio, `size:exception` aceptado)
| Tanda | Contenido | Punto de control |
|---|---|---|
| 1 | Domain + Application con TDD; comprobación de `docker pull postgres:18-alpine` | build, test y cobertura 100 % / ≥ 80 % en verde |
| 2 | Infrastructure, migración, Testcontainers | pruebas de repositorio y migraciones en verde |
| 3 | Api, identidad, configuración, compose | E2E de API en verde; `docker compose up` responde |
| 4 | Web, ADR, README, `config.yaml` | flujo manual en navegador registrado |

Orden de recorte si falta tiempo: pulido Web → ADR/README → compose. Backend y pruebas no se recortan.

### ADR (`docs/adr/`)
| N.º | Decisión |
|---|---|
| 0008 | Persistencia: PostgreSQL + EF Core, migraciones por bandera, Testcontainers |
| 0009 | Identidad provisional `ICurrentUser` solo en Development |
| 0010 | Tiempo: `TimeProvider`, UTC y minutos truncados |

## Áreas afectadas

| Área | Impacto | Descripción |
|---|---|---|
| `src/TimeTracker.Domain`, `.Application`, `.Infrastructure` | Modificado | Entidad, casos de uso, `DbContext`, migraciones |
| `src/TimeTracker.Api` | Modificado | Endpoints, identidad, opciones, manejador de errores |
| `src/TimeTracker.Web` | Modificado | Página `/fichajes` y cliente |
| `tests/*` | Modificado | Unitarias e integración con PostgreSQL |
| `Directory.Packages.props`, `.config/dotnet-tools.json`, `.editorconfig` | Modificado/Nuevo | Paquetes EF, herramienta `dotnet-ef`, migraciones como generadas |
| `docker-compose.yml`, `.env.example`, `.github/workflows/ci.yml` | Modificado | Servicio `postgres`, claves, cobertura de Application |
| `docs/adr/0008–0010`, `docs/api/openapi.json`, `README.md`, `openspec/config.yaml` | Nuevo/Modificado | Documentación y contrato |

## Riesgos

| Riesgo | Prob. | Mitigación |
|---|---|---|
| Calendario (14 días, ≈ 1.500+ líneas) | Alta | Tandas con punto de control; orden de recorte acordado |
| Docker Hub inaccesible (`postgres`, `ryuk`) | Media | Comprobación en la primera tarea; respaldo `17-alpine` o Ryuk desactivado; si falla todo, parar y consultar |
| Desalineación de la familia EF (10.0.4 vs 10.0.12) | Media | Fijar versiones en `Directory.Packages.props`; build `-warnaserror` |
| Migración generada vs `-warnaserror`/`format` | Media | `.editorconfig` marca `**/Migrations/**` como generado |
| Identidad provisional desplegada por error | Media | Registro solo en Development; fallo de arranque en otros entornos con prueba; ADR 0009 |
| Entrada olvidada bloquea volver a fichar | Media | Aceptado; tope diferido al cambio de saldos |
| Doble fichaje concurrente | Baja | Índice único parcial + prueba de carrera |
| W1: CI sin ejecutar, ahora con Testcontainers | Alta | Reproducir pasos localmente; documentar que requiere Docker |
| W4: exportación OpenAPI por `GetDocument.Insider` frágil | Media | Cadena de conexión ficticia y conexión perezosa; prueba de contrato |
| El delta de compose rompe escenarios archivados | Baja | Delta MODIFIED explícito del requisito de compose |

## Plan de reversión

Sin Git. Antes de cada tanda, apply guarda copia de los archivos existentes que modifica (`docker-compose.yml`, `.env.example`, `ci.yml`, `Directory.Packages.props`, `.editorconfig`, `README.md`, `config.yaml`, `Program.cs`, proyectos Web/Api). Revertir: restaurar esas copias, borrar los archivos nuevos (entidad, casos de uso, `Infrastructure/Persistence`, `Migrations`, página Web, pruebas, ADR 0008–0010, `.config/dotnet-tools.json`), `docker compose down -v` y `docker image rm postgres:18-alpine`. No hay datos reales que perder.

## Dependencias

- Docker Desktop en marcha y acceso a Docker Hub (se comprueba en la tarea 1).
- Restauración NuGet con red: Npgsql EF 10.0.3, EF Core/Design 10.0.12, Testcontainers.PostgreSql 4.16.0, TimeProvider.Testing 10.10.0, `dotnet-ef` 10.0.12 (herramienta local, no global).

## Criterios de éxito

- [ ] `dotnet build -warnaserror` y `dotnet format --verify-no-changes` limpios.
- [ ] `dotnet test` en verde, incluidas las pruebas de integración con PostgreSQL.
- [ ] Cobertura: Domain 100 %, Application ≥ 80 % (`check-coverage.ps1`).
- [ ] Segunda entrada abierta → 409; salida sin entrada abierta → 409; un empleado no ve fichajes de otro.
- [ ] `HasPendingModelChanges() == false`.
- [ ] Fuera de Development sin identidad real, la API no arranca; sin `ConnectionStrings:Default`, falla nombrando la clave.
- [ ] `docs/api/openapi.json` incluye los tres endpoints.
- [ ] `docker compose up`: `postgres` sano, migraciones aplicadas, API responde.
- [ ] Flujo manual en `/fichajes` (entrada, salida, total) registrado en `verify-report.md`.
- [ ] ADR 0008–0010 y README actualizados.

## Previsión de carga de revisión

- Estimación: ≈ 1.460–1.820 líneas a mano (tandas 1 ≈ 440–530, 2 ≈ 350–430, 3 ≈ 380–480, 4 ≈ 290–380), sin contar migraciones generadas.
- Decision needed before apply: No (`size:exception` ya aceptado por el usuario, Q11)
- Chained PRs recommended: No (sin repositorio; las tandas actúan como puntos de control)
- 400-line budget risk: High

## Ronda de preguntas de propuesta

Ronda 1 resuelta (Q1–Q12, 2026-10-11): el usuario aceptó todos los valores por defecto. Supuestos vigentes:
- Un único empleado provisional por configuración; la identidad real llega en `autenticacion-y-roles`.
- Las horas se muestran en UTC con etiqueta explícita; los minutos se truncan.
- Una entrada olvidada abierta queda abierta hasta fichar salida; no hay corrección manual en este cambio.
