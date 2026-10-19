# Informe de archivo: asistente-ia

Fecha: 2026-10-18 · Destino: `openspec/changes/archive/2026-10-18-asistente-ia/` · Archivo **intencional con avisos** (hay tareas sin marcar, ver abajo).

Entregado en los PR #10 a #14 (resumen diario, herramientas, motor, endpoint, Web), #15 y #16 (correcciones de pruebas y CORS) y #17 (mejoras tras la verificación 5.2: fechas de referencia, textos «X h Y min» y descripción de `memberId`). Decisión registrada en el ADR 0017.

## Especificaciones fusionadas

| Dominio | Acción | Detalle |
|---|---|---|
| `assistant-configuration` | Creada | Copia íntegra del delta (5 requisitos) |
| `assistant-endpoint` | Creada | Copia íntegra del delta (11 requisitos) |
| `assistant-tools` | Creada | Copia íntegra del delta (9 requisitos) |
| `assistant-web` | Creada | Copia íntegra del delta (8 requisitos) |
| `authentication` | Actualizada | 2 ADDED (orden del pipeline de seguridad; rol en el contexto de aplicación) y 2 MODIFIED (límite de intentos de login; validación estricta del token) |
| `startup-configuration` | Actualizada | 1 ADDED (arranque con la configuración del asistente) y 2 MODIFIED (configuración por variables de entorno; documentación de claves en `.env.example`) |
| `solution-architecture` | Actualizada | 2 ADDED (bajo el encabezado nuevo «Tanda asistente-ia») y 4 MODIFIED (estructura de proyectos; regla de dependencias; exportación OpenAPI; ADR y README) |

Sin REMOVED ni RENAMED. Se eliminaron de los requisitos fusionados las anotaciones `(Previously: ...)` del delta; el histórico queda en el delta archivado.

### Decisiones de fusión (avisos)

- `authentication` / «Validación estricta del token»: el delta solo redefine algunos escenarios y declara que el resto se conserva. Se mantuvieron los seis escenarios vigentes no redefinidos (algoritmo distinto, `sub` no válido, emisor o audiencia ajenos, `nbf` futuro, caducidad según el reloj, sin `error_description`) y se añadió el escenario nuevo del endpoint del asistente. Se sustituyó la nota del delta por esos escenarios.
- `solution-architecture` / «ADR y README»: el delta reescribe el requisito y omite cinco escenarios vigentes (ADR 0009 sustituido, contenido de los ADR nuevos, README ejecutable, README cubre la novedad, README sin identidad provisional). No se declaró su retirada, así que se **conservaron** para no hacer una fusión destructiva. Conviene revisar si alguno debe retirarse formalmente (el recuento «trece ADR» del escenario antiguo ya queda sustituido por «diecisiete»).
- `startup-configuration` / «Documentación de claves en `.env.example`»: el escenario «Sin claves de IA activas» se sustituye por el nuevo (las nueve claves `Ai__*` documentadas, con `Ai__Provider` y `Ai__ApiKey` activas y vacías); cambio explícito en el delta.

### Inconsistencia señalada sin corregir: `solution-architecture`

El `Purpose` de `openspec/specs/solution-architecture/spec.md` sigue desfasado: habla de «13 ADR», de IA como fuera de alcance y no menciona el asistente ni `TimeTracker.Infrastructure.Tests`. La tarea 5.4 pedía actualizarlo al fusionar, pero se decidió **no corregirlo en este archivo** y solo informar. Queda como seguimiento. Además, el cuerpo conserva requisitos que el delta no tocó y pueden citar cifras anteriores (p. ej. 13 ADR en otros escenarios).

## Estado de las tareas (honesto)

`tasks.md` archivado conserva estas casillas sin marcar; **no se dan por hechas**:

| Tarea | Estado real |
|---|---|
| 4.5 (PR 3) Actualizar `openspec/config.yaml` solo al archivar | Cubierta por este archivo: `config.yaml` actualizado en el mismo commit. La casilla sigue sin marcar porque el archivo de tareas es registro histórico y no se modificó. |
| 3.2 (PR 4) Verificación manual en navegador de `/asistente` (sin sesión, enlace por rol, aviso con asistente deshabilitado, HTML literal, 429, error de red) | **Pendiente**: no hay constancia en `verify-report.md`; este registra la verificación por API (5.2 y M.6), no la del navegador. |
| 5.1 Suite completa extremo a extremo (`RESTORE`→`BUILD`→`TEST`→`COV-A`→`COV-D`→`FORMAT`→`VULN`, 17 ADR) | Sin constancia explícita en los informes de la ejecución final tras el PR 4 y el PR #17; las puertas de unidad por PR sí constan como marcadas. **Pendiente de confirmar.** |
| 5.3 Railway: el usuario configura `Ai__Provider`, `Ai__Model` y el secreto `Ai__ApiKey` | **Pendiente (acción del usuario).** Desactivar = quitar `Ai__Provider` (503, resto intacto). |
| 5.4 Preparar para verify/archivo: actualizar `config.yaml` y `Purpose` de `solution-architecture` | `config.yaml` actualizado en este archivo; el `Purpose` **no** se actualizó (ver arriba). |
| M.6 Repetir la verificación manual con el modelo tras las mejoras | Casilla obsoleta: `verify-report.md` contiene la sección «Repetición tras mejoras (M.6)» y declara la tarea ejecutada. Sin cambiar `tasks.md` archivado. |

## Hallazgos de verificación relevantes (de `verify-report.md`)

- Seguridad, 503 sin proveedor y límites conformes con la spec con modelo real; sin fugas de datos ajenos.
- `qwen2.5:3b` con `Ai__TimeoutSeconds=55` sirve para consultas propias del empleado tras el PR #17 (9/9); el flujo de responsable (consultar a un reporte por nombre) **no funciona con ningún modelo local probado en CPU**. Para demostrarlo hace falta `Anthropic` o un modelo local de 7B+ con GPU.
- Seguimiento sugerido: que el servidor acepte el nombre mostrado o resuelva nombre a id en `get_team_member_time_entries`. (El cambio `asistente-config-responsable`, aún sin archivar, es un trabajo posterior en esa línea.)
- Sin incidencias CRITICAL en el informe de verificación.

## Contenido archivado

`exploration.md`, `proposal.md`, `design.md`, `specs/` (7 capacidades), `tasks.md`, `apply-progress.md`, `verify-report.md` y este `archive-report.md`. La carpeta ya no existe en `openspec/changes/`.

## Estado de Engram

Modo `openspec`: no se consultaron observaciones de Engram, así que no se registran identificadores.
