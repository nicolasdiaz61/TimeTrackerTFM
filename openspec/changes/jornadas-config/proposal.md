# Propuesta: configuración de jornadas

## Qué
Un Manager configura las jornadas de su compañía (nombre, hora de entrada, almuerzo y su duración, pausa esperada por día y días de
la semana) y las asigna a las personas de su equipo. Cada persona tiene como máximo una jornada y la ve en el Inicio.

## Por qué
Base para comparar más adelante lo fichado con lo esperado. Esta entrega solo configura y asigna.

## Alcance
- Dentro: tabla `work_schedules` y columna `users.work_schedule_id` (migración aditiva `AddWorkSchedules`), API `/api/work-schedules`,
  asignación `PUT /api/team/members/{id}/work-schedule`, página `/jornadas` y línea «Su jornada» en el Inicio.
- Fuera: cálculo de desviaciones, edición de fichajes, RGPD.

## Decisiones
- Aislamiento por compañía (ADR 0020): `company_id`, filtro global, guarda de escritura y claves foráneas compuestas
  (`work_schedules`→`companies`, `users (company_id, work_schedule_id)`→`work_schedules (company_id, id)`, `NO ACTION`).
- Los días se guardan como máscara de bits de `DayOfWeek` (CHECK 1..127); el contrato los expone como números de `DayOfWeek`.
- Asignación por persona (`PUT` al miembro del equipo) y no por jornada: reutiliza la regla de equipo (reportes directos).
- Tope blando de 20 jornadas por compañía (recuento previo; una carrera puede superarlo en una unidad).
- Borrar una jornada asignada es un `409`, sin cascada.
