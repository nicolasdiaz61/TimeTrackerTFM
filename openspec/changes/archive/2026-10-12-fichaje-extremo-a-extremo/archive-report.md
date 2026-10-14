# Informe de archivo: fichaje-extremo-a-extremo

**Cambio**: fichaje-extremo-a-extremo · **Modo**: openspec (sin Git) · **Fecha de archivo**: 2026-10-12 · **Idioma**: español
**Veredicto de verificación de origen**: PASS WITH WARNINGS (0 CRITICAL, 8 WARNING, 5 SUGGESTION), ver `verify-report.md`.
**Destino**: `openspec/changes/archive/2026-10-12-fichaje-extremo-a-extremo/` (el movimiento de la carpeta lo realiza el orquestador; este executor solo editó archivos).

## Resumen

Fichaje de entrada y salida de extremo a extremo: dominio (`TimeEntry`, minutos truncados), casos de uso `ClockIn`, `ClockOut` y `GetTimeEntries`, persistencia en PostgreSQL 18 con EF Core (índice único parcial de entrada abierta, migraciones por bandera, Testcontainers), API REST con identidad provisional (`ICurrentUser`, solo Development), errores 409 como ProblemDetails en español, página Blazor `/fichajes`, compose con API y PostgreSQL, cobertura Domain 100 % y Application 100 % (umbral 80 %), 10 ADR y exportación OpenAPI sin base de datos. Estado final: 151 pruebas en verde, 0 avisos, 0 vulnerabilidades.

## Compuerta de tareas

`tasks.md`: todas las casillas marcadas `[x]`; 0 sin marcar (7.8, 9.7 y 10.11 figuran como «retirada» y no cuentan). Sin reconciliación de casillas obsoletas.

## Specs fusionadas en `openspec/specs/`

| Dominio | Acción | Requisitos | Escenarios | Detalle |
|---|---|---|---|---|
| time-tracking | Creada (copia del delta completo) | 11 | 48 | Spec nueva |
| time-entries-persistence | Creada | 9 | 21 | Spec nueva |
| time-entries-ui | Creada | 5 | 13 | Spec nueva |
| solution-architecture | Actualizada | 12 (11 + 1) | 32 | 1 RENAMED (compose), 8 MODIFIED, 1 ADDED (exportación OpenAPI), 0 REMOVED; `Purpose` actualizado (W2); 3 requisitos sin tocar (estructura, gestión central de paquetes, archivos raíz) |
| startup-configuration | Actualizada | 9 (6 + 3) | 24 | 3 ADDED, 2 MODIFIED (`.env.example`, puerto), 0 REMOVED; 4 requisitos sin tocar |

Nota startup-configuration: la spec inicial tenía 6 requisitos (configuración por entorno, opciones tipadas, CORS, mensajes, `.env.example`, puerto); los dos modificados se sustituyen (no suman) y se añaden 3, de modo que el total es **9 requisitos**. Escenarios: 12 iniciales + 1 (claves nuevas en `.env.example`) + 11 (ADDED: 5 + 4 + 2) = **24**.

Recuento final de requisitos / escenarios por spec principal vigente (cálculo aritmético inicial + deltas; el entorno no dispone de Grep, por lo que no se pudo confirmar con búsqueda de texto y se recomienda repetir `Grep "^### Requirement:"` y `Grep "^#### Scenario:"` si se desea comprobación mecánica):

- solution-architecture: inicial 11 / 20; delta 27 escenarios sustituyen a 15 de los modificados; final **12 / 32** (20 - 15 + 27).
- startup-configuration: inicial 6 / 12; final **9 / 24**.
- Total de las 5 specs tocadas: 46 requisitos y 138 escenarios (el verify contaba 40 / 124 sobre los deltas, que incluyen solo los requisitos modificados o añadidos).
- time-tracking **11 / 48**, time-entries-persistence **9 / 21**, time-entries-ui **5 / 13**.
- api-health y worked-hours: sin cambios.

Coherente con el verify-report: 40 requisitos y 124 escenarios contados en los deltas (el verify cuenta 5 en startup-configuration y 10 en solution-architecture porque cuenta solo los encabezados del delta, y el RENAMED como encabezado propio).

W1 aplicada: el requisito «ADR y README» ya no menciona `xmin` en el ADR 0008 (corregido en el delta y en la spec principal). W2 aplicada: `Purpose` de solution-architecture actualizado (compose con API y PostgreSQL, Web con `/fichajes`, 10 ADR).

## Trazabilidad de artefactos (modo openspec)

`exploration.md`, `proposal.md`, `design.md`, `tasks.md`, `specs/` (5 deltas), `verify-report.md` y este `archive-report.md` quedan en la carpeta archivada. No hay observaciones de Engram asociadas a este modo de trabajo.

## Hallazgos abiertos del verify (WARNING no resueltos)

- **W3.** La CI nunca se ha ejecutado (sin remoto); `pwsh` en `ubuntu-latest`, `actions/checkout@v7` y `setup-dotnet@v6` sin verificar; sin analizador YAML, SA-21 queda PARCIAL.
- **W4.** La Web no tiene pruebas automáticas (sin bUnit ni E2E, por decisión de la spec); los 13 escenarios de UI descansan en la verificación manual del 2026-10-12. Sin verificar: aspecto visual, estado intermedio de botones deshabilitados y panel de error de Blazor en producción; UI-9 se infiere.
- **W5.** El fallo claro sin Docker (Testcontainers) solo se probó con un sustituto (imagen inexistente); no se omite en silencio (0 `Skip`).
- **W6.** SA-24 PARCIAL: no se siguió literalmente `docker compose up -d postgres` con la contraseña del `.env` temporal.
- **W7.** Los artefactos SDD (`exploration.md` línea 42, `proposal.md` línea 7, `tasks.md` D11) mencionan «TFM»; el usuario aceptó la misma mención en el cambio archivado anterior. No hay menciones en código, README, ADR, specs ni `config.yaml`. Decisión pendiente: mantener o retirar (el archivo es pista de auditoría).
- **W8.** El `.env` temporal con valores ficticios sigue en la raíz (necesario para `docker compose up`; en `.gitignore` y `.dockerignore`; no se leyó). Borrar a mano si no se necesita. La carpeta `..\TimeTracker.rollback` (tandas 1 a 4) se conserva hasta decidir su borrado.

SUGGESTION abiertas: S1 (mencionar en README el riesgo del doble `clock-out`), S2 (reflejar en `design.md` las desviaciones D1, D4, D5, D7 y el logging del 500), S3 (PE-21 depende del entrelazado de hilos), S4 (cambio de seguimiento para `xmin` y límite de tasa), S5 (presupuesto de revisión superado; dividir en entregas más pequeñas).

## Riesgo residual: `xmin` diferido (DD-9)

Se recortó el control de concurrencia de la salida. Dos `clock-out` simultáneos del mismo fichaje pueden devolver dos `200` y la última escritura fija `clock_out_at`. La `CHECK` y las reglas de Domain siguen cumpliéndose y no hay segunda fila; la entrada sigue protegida por el índice único parcial (10 inserciones y 10 `POST` en paralelo). Severidad baja con un único cliente y sin edición de fichajes; ninguna prueba fija este comportamiento. Riesgo aceptado por el usuario (opción A, 2026-10-12). Documentado en `design.md` (DD-9, §14), `tasks.md`, ADR 0008 (consecuencias y alternativas), `config.yaml` y la nota de `time-entries-persistence`. Antes de cualquier despliegue: abrir el cambio de seguimiento.

## Recortes acordados

- Sin `xmin` (DD-9; PE-8/9/10 y V12 retirados).
- Sin escenarios CORS adicionales (TT-46, SC-15..18).
- Sin pruebas automáticas de Web (sin bUnit ni E2E); verificación manual registrada.
- Sin límite de tasa (diferido y declarado en design §14).
- Sin tabla `Employee`, paginación, edición de fichajes ni hora local.

## Trabajo futuro recomendado

1. `autenticacion-y-roles`: sustituir `ICurrentUser` provisional por identidad real (la API no arranca fuera de Development hasta entonces).
2. Saldos de horas, horas extra y ausencias.
3. Asistente de IA (`IChatClient`, herramientas de solo lectura).
4. Despliegue en Railway.
5. Conectar el repositorio Git cuando el usuario lo decida (hoy no existe `.git`).
6. Ejecutar la CI por primera vez y verificar `pwsh`, `checkout@v7` y `setup-dotnet@v6` (W3).
7. Hooks de Git y Dependabot.
8. Mostrar hora local en la Web (hoy solo UTC etiquetado).
9. Cambio de seguimiento para `xmin` y límite de tasa; mención del doble `clock-out` en el README (S1, S4).

## Cierre

Pendientes para el orquestador: mover `openspec/changes/fichaje-extremo-a-extremo/` a `openspec/changes/archive/2026-10-12-fichaje-extremo-a-extremo/` y confirmar que `openspec/changes/` ya no contiene el cambio. Aviso: la fusión incluyó RENAMED y MODIFIED, según la regla de `config.yaml`.
