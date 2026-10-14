# Exploración: fichaje-extremo-a-extremo

Fecha: 2026-10-11 · Fase: sdd-explore · Modo: openspec · Estado: pendiente de respuestas del usuario (Q1–Q12).

## 1. Estado actual
- Esqueleto archivado (`openspec/changes/archive/2026-10-11-esqueleto-solucion/`): 9 proyectos net10.0, 4 specs principales, 7 ADR, xUnit v2 sobre VSTest.
- Domain solo tiene `WorkedHours` (`readonly record struct`, minutos enteros ≥ 0, `FromMinutes`, `+` con `checked`). Application e Infrastructure vacíos.
- Api: Kestrel sin cabecera Server, CORS restrictivo con `ValidateOnStart`, OpenAPI exportado en build (rama `GetDocument.Insider`, W4), `/health`.
- Web: plantilla `blazorwasm` sin cambios (Web `http://localhost:5172`, API `http://localhost:5274`).
- Docker funciona (imágenes de mcr.microsoft.com). **No verificado**: descarga de `postgres` y `testcontainers/ryuk` desde Docker Hub en esta red.
- Specs afectadas: `solution-architecture` (compose con solo `api` → delta MODIFIED), `startup-configuration` (clave `ConnectionStrings:Default` y `.env.example`). `api-health` y `worked-hours` sin cambios.

## 2. Hechos verificados (NuGet / documentación oficial)
- `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 (requiere EF Core ≥ 10.0.4 y < 11).
- `Microsoft.EntityFrameworkCore`, `.Design` y `dotnet-ef`: 10.0.12 estable.
- `Testcontainers.PostgreSql` 4.16.0; el constructor recibe la imagen (`new PostgreSqlBuilder("postgres:18-alpine")`).
- `Microsoft.Extensions.TimeProvider.Testing` 10.10.0 (`FakeTimeProvider`).
- PostgreSQL 18 con soporte hasta 2030-11-14; desde la 18 el volumen se monta en `/var/lib/postgresql` (fuente de terceros, no confirmado en Docker Hub).
- En Blazor WASM la zona local histórica devuelve UTC; no confirmado para .NET 10.

## 3. Decisiones recomendadas
- **D1 Identidad:** puerto `ICurrentUser { Guid EmployeeId }` en Application; adaptador provisional en Api que lee `Identity:DevelopmentEmployeeId` y solo se registra en Development (fuera de Development la API falla al arrancar con mensaje claro). Ningún endpoint recibe `employeeId`. Descartadas: cabecera/parámetro (viola AGENTS §6), JWT real ahora (duplica el siguiente cambio); alternativa B: esquema de autenticación "Development" con claims (+40 líneas). ADR 0009.
- **D2 Modelo:** solo `TimeEntry` (`Id`, `EmployeeId`, `ClockInAt`, `ClockOutAt?`; `timestamptz`), sin tabla `Employee` (YAGNI). Índice `(EmployeeId, ClockInAt DESC)` e índice único parcial `UNIQUE (EmployeeId) WHERE ClockOutAt IS NULL`. Un único constructor privado con parámetros (para no romper el 100 % de Domain). Mapeo Fluent en Infrastructure.
- **D3 Tiempo:** `TimeProvider` (BCL) como puerto; `FakeTimeProvider` en pruebas. Todo en UTC (`DateTimeOffset` offset 0, validado en dominio); contrato ISO 8601 con `Z`. Sin conversión de zona en servidor (no hace falta `tzdata`). Blazor muestra UTC etiquetado.
- **D4 Reglas:** Domain (100 %): `Start`, `Close` (error si cerrada o `at < ClockInAt`; igual es válido), `IsOpen`, `CalculateWorked()` (método, `null` si abierta). Application (≥ 80 %): casos de uso `ClockIn`, `ClockOut`, `GetTimeEntries` con `(ITimeEntryRepository, ICurrentUser, TimeProvider)`; no dos entradas abiertas; salida requiere abierta. Infrastructure traduce `23505` a `OpenTimeEntryAlreadyExistsException`. Excepciones tipadas → `IExceptionHandler` → ProblemDetails 409.
- **D5 Cálculo:** instantes absolutos UTC (cruzar medianoche es correcto); minutos truncados (`(int)TotalMinutes`); entrada abierta no suma (`workedMinutes: null`, "en curso"); total solo de cerradas con `+`. Sin atribución por día ni tope de entrada olvidada (cambio de saldos).
- **D6 Paquetes:** Npgsql EF 10.0.3 y EF Design 10.0.12 (`PrivateAssets=all`); pruebas: Testcontainers.PostgreSql 4.16.0, TimeProvider.Testing 10.10.0; `dotnet-ef` 10.0.12 como herramienta local (`.config/dotnet-tools.json`). Fijar la familia EF en 10.0.12 (pinning transitivo o referencia directa) para evitar mezcla con 10.0.4.
- **D7 Migraciones:** crear con `IDesignTimeDbContextFactory` en Infrastructure; aplicar con bandera `Database:ApplyMigrationsOnStartup` (apagada por defecto, compose la enciende). Las pruebas usan `MigrateAsync()` (nunca `EnsureCreated`) y una prueba `HasPendingModelChanges() == false`. `.editorconfig` marca `**/Migrations/**` como código generado.
- **D8 Configuración:** `ConnectionStrings:Default` obligatoria con `ValidateOnStart` (mensaje en español que nombra la clave sin el valor); `.env.example` con `POSTGRES_*`, `ConnectionStrings__Default`, `Identity__DevelopmentEmployeeId`, `Database__ApplyMigrationsOnStartup`. `ApiFactory` y la exportación OpenAPI necesitan cadena ficticia (conexión perezosa).
- **D9 Pruebas de integración:** fixture propia (~35 líneas) `PostgresFixture : IAsyncLifetime` con `ICollectionFixture` (un contenedor por ejecución); un `EmployeeId` nuevo por prueba (sin limpieza); dentro de `Api.IntegrationTests`. Pruebas: repositorio (índice único, carrera en paralelo), API de extremo a extremo (entrada, salida, 409, aislamiento entre empleados), migraciones. Docker obligatorio para estas pruebas (documentar).
- **D10 Imagen:** `postgres:18-alpine` (respaldo `17-alpine`), misma en compose y pruebas. Digest para Dependabot más adelante.
- **D11 Contrato:** `POST /api/time-entries/clock-in` (201/409), `POST /api/time-entries/clock-out` (200/409), `GET /api/time-entries` (últimas 50 + `totalWorkedMinutes`). `TimeEntryResponse { id, clockInAt, clockOutAt?, workedMinutes? }`. ProblemDetails RFC 9457 en español. Ampliar la prueba de contrato OpenAPI.
- **D12 Web:** página `/fichajes` con tabla, total y botones "Fichar entrada/salida"; `TimeEntriesApiClient` tipado; `ApiBaseUrl` en `wwwroot/appsettings.json`; DTO duplicados en Web (sin `ProjectReference`); sin bUnit (verificación manual registrada).
- **D13 Docker/compose:** servicio `postgres` con healthcheck (`pg_isready`), volumen `pgdata`, puerto en `127.0.0.1:5432`; `api` con `depends_on: service_healthy`, `ASPNETCORE_ENVIRONMENT=Development` y migraciones en arranque. Delta MODIFIED de la spec de compose.
- **D14 Cobertura/CI:** Domain 100 %; Application ≥ 80 % con `check-coverage.ps1` como paso nuevo de CI; resto sin umbral.
- **D15 Documentación:** ADR 0008 persistencia, 0009 identidad provisional, 0010 tiempo; README y `config.yaml` actualizados.
- **D16 Entrega:** capacidades nuevas `time-tracking`, `time-entries-persistence`, `time-entries-ui`; deltas en `solution-architecture` y `startup-configuration`. 4 tandas: (1) Domain+Application con TDD; (2) Infrastructure + Testcontainers; (3) Api + identidad + compose; (4) Web, ADR y README.

## 4. Enfoques
1. **Backend completo con pruebas primero, Web mínima al final (recomendado).** ≈ 1.460–1.820 líneas.
2. Rebanada vertical delgada: reabre capas varias veces.
3. Sin Web (solo API/curl): -150 líneas, pero falla el "extremo a extremo" del TFM.

## 5. Tamaño estimado (líneas a mano)
Domain 160–190 · Application 280–340 · Infrastructure 170–220 · Api 190–240 · Web 140–180 · Pruebas de integración 320–380 · Compose/props/CI 50–70 · ADR+README 150–200 → **≈ 1.460–1.820** (`size:exception`).

## 6. Riesgos
Calendario (14 días, ≈ 1.500+ líneas) · Docker Hub sin verificar (`postgres`, `ryuk`; respaldo 17-alpine / desactivar Ryuk) · pruebas dependen de Docker · desalineación de la familia EF · migración generada vs `-warnaserror`/`format` · identidad provisional desplegada por error · entrada olvidada abierta bloquea volver a fichar · carrera de doble fichaje (cubierta por índice único) · hora local en WASM sin verificar (UTC) · CI sin ejecutar (W1) con Testcontainers · rama `GetDocument.Insider` más frágil (W4) · el delta de compose no debe romper escenarios archivados.

## 7. Preguntas abiertas (valor por defecto recomendado)
- Q1 Identidad con `ICurrentUser` provisional solo en Development → **sí (A)**.
- Q2 Sin tabla `Employee` (GUID sin FK por ahora) → **omitirla**.
- Q3 Botones "Fichar entrada/salida" en la pantalla → **sí**.
- Q4 Minutos: truncar → **truncar**.
- Q5 Horas mostradas en UTC etiquetado → **UTC**.
- Q6 Migraciones por bandera al arrancar → **bandera**.
- Q7 Imagen PostgreSQL → **18-alpine** (respaldo 17).
- Q8 Orden de recorte si falta tiempo: pulido Web → ADR/README → compose; backend y pruebas intocables → **sí**.
- Q9 Listado: últimas 50, sin paginación → **sí**.
- Q10 Confirmar `docker pull postgres:18-alpine` en la red de la máquina → **se comprueba en la primera tarea de apply**.
- Q11 4 tandas dentro del mismo cambio y `size:exception` → **sí**.
- Q12 Mensajes de error en español → **sí**.

## 8. Listo para propuesta
Sí, tras las respuestas del usuario. La propuesta debe incluir plan de reversión (borrar lo creado y restaurar `docker-compose.yml`; sin Git) y la decisión de `size:exception`.
