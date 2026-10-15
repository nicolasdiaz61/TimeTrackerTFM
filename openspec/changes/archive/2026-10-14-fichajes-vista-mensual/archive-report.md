# Informe de archivo: fichajes-vista-mensual

Fecha: 2026-10-14 · Veredicto de verificación: **PASS**. Destino: `openspec/changes/archive/2026-10-14-fichajes-vista-mensual/`.

| Dominio | Acción | Detalle |
|---|---|---|
| `time-tracking` | Actualizada | 1 ADDED (Ventana de consulta del listado) y 2 MODIFIED (Caso de uso GetTimeEntries y total; Contrato REST de fichajes); propósito y decisiones ajustados |
| `team-access` | Actualizada | 1 MODIFIED (Fichajes de un miembro del equipo: ventana `from`/`to`) |
| `time-entries-ui` | Actualizada | 4 ADDED (Reproductor de fichaje en el Inicio; Página /fichajes de consulta mensual; Barra de franjas por día; Presentación en hora local), 2 MODIFIED (Mensajes de error en español; Cliente tipado y URL base configurable) y 4 REMOVED (Página /fichajes con listado y total; Botones de fichaje; Presentación de tiempos en UTC y «en curso»; Totales de trabajo y de pausas); propósito y fuera de alcance reescritos |
| `web-session` | Actualizada | 3 MODIFIED (Página Equipo; Página de inicio; Página de inicio de sesión: destino por defecto `/`) |

Los REMOVED son destructivos para la capacidad `time-entries-ui` (retiran la tabla plana en UTC, los botones de `/fichajes` y `WorkedTimeFormatter`); cada uno tiene su sustituto indicado en el delta y en el ADR 0016. Sin RENAMED. Las 17 tareas de `tasks.md` están marcadas `[x]`.

Decisiones registradas en el ADR 0016 (nuevo); `openapi.json` regenerado; README actualizado.
