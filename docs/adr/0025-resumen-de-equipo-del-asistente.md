# ADR 0025: Resumen de equipo del asistente

- Estado: Aceptado
- Fecha: 2026-10-23

## Contexto

Un responsable pregunta al asistente cosas como «¿qué empleados hacen más pausas que el promedio?». Las herramientas existentes ([ADR 0017](0017-asistente-ia-con-herramientas-de-solo-lectura.md)) solo permiten consultar a un miembro cada vez, con resúmenes por día, y el modelo no debe sumar ni comparar (el prompt le pide copiar los totales ya calculados). Un modelo pequeño no resuelve una comparación con N llamadas y, además, el rechazo del alcance ([ADR 0024](0024-alcance-del-asistente.md)) le llevaba a negarse.

## Decisión

- **Nueva herramienta `get_team_summary(fromDate, toDate)`, solo para el rol `Manager`.** Un empleado no la recibe ni puede nombrarla. No acepta compañía ni identificador de miembro: el equipo sale siempre de `ICurrentUser` (reportes directos), igual que `get_team_members`.
- **Todo el cálculo vive en el caso de uso `GetTeamSummaryUseCase` (Application).** Reutiliza `DailySummaryBuilder` por miembro y devuelve minutos y número de pausas y de comidas, días con fichajes, la media del equipo (un miembro sin fichajes cuenta como cero), la diferencia respecto a la media y si está por encima (estrictamente). Los miembros van ordenados por minutos de pausa descendente. El intervalo abierto se informa aparte y no suma.
- **Límites.** Mismo periodo máximo de 31 días y mismo tope de intervalos por ventana que las demás herramientas. Una consulta de fichajes por miembro (sin N+1 evitable con el repositorio actual) y como máximo 50 miembros; si hay más, `membersTruncated` lo indica. No cambian `MaxToolIterations` ni los límites de uso.
- **Prompt.** Al Manager se le indica que para comparaciones, rankings y medias llame una sola vez a `get_team_summary` y responda con sus cifras sin recalcular, y que comparar o ordenar los fichajes del equipo está dentro del alcance. Se mantienen el rechazo de lo ajeno a los fichajes y las reglas contra la inyección. El prompt del empleado no cambia.
- **Seguridad.** Solo lectura, nombres saneados con `DisplayNameSanitizer` como en `get_team_members`, registro con herramienta, usuario, resultado y duración, y pruebas con dos compañías A y B.

## Consecuencias

- Las preguntas comparativas se resuelven con una llamada y sin aritmética del modelo.
- Un equipo de más de 50 personas solo se resume parcialmente (con aviso); la lista completa sigue en `get_team_members` (hasta 100).
- Cada llamada hace hasta 50 consultas de fichajes: aceptable con la cota y el límite de peticiones por usuario.
- Las frases del prompt y el catálogo de herramientas quedan fijados por pruebas.

## Alternativas

- **Que el modelo llame N veces a `get_team_member_time_entries` y compare:** descartada; coste, latencia y errores de cálculo en modelos pequeños.
- **Una única consulta agregada en SQL:** descartada por ahora; añade lógica al repositorio y duplicaría el cálculo de `DailySummaryBuilder`. Reconsiderar si los equipos crecen.
- **Quitar las reglas de alcance del prompt:** descartada; se concreta el alcance en lugar de eliminarlo ([ADR 0024](0024-alcance-del-asistente.md)).
