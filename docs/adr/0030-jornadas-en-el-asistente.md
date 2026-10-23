# ADR 0030: Jornadas en el asistente

- Estado: Aceptado
- Fecha: 2026-10-23

## Contexto

El asistente ([ADR 0017](0017-asistente-ia-con-herramientas-de-solo-lectura.md)) no conocía las jornadas: no podía decir qué jornada tiene una persona ni cuál es su configuración (nombre, hora de entrada, inicio y duración del almuerzo, pausa esperada por día y días de la semana), y el modelo tendía a inventarlo. Los datos ya existen (`WorkSchedule`, `TeamMemberResult.WorkScheduleId` y el caso de uso `GetMyWorkScheduleUseCase`), así que basta exponerlos sin cambios de esquema ni de contrato de la API.

## Decisión

- **`get_team_members` (solo Manager)** añade a cada miembro `workSchedule`: `null` si no tiene jornada (o si su identificador no se resuelve) y, si la tiene, `name`, `startTime` y `lunchStart` en `HH:mm`, `lunchMinutes`, `expectedBreakMinutes` y `days` en español de lunes a domingo. Las jornadas de la compañía se leen con una sola consulta por petición (`ListWorkSchedulesUseCase`), nunca por miembro, y solo si alguien tiene una asignada.
- **`get_my_work_schedule` (todos los roles, sin parámetros)** devuelve la jornada del propio usuario con la misma forma, o `{workSchedule: null, message: "No tiene una jornada asignada."}`. La identidad sale de `ICurrentUser` a través de `GetMyWorkScheduleUseCase`.
- **`get_work_schedules` (solo Manager, sin parámetros)** lista las jornadas de la compañía con la misma forma más `assignedCount`, con el tope existente de 20 jornadas (`WorkScheduleLimits.MaxPerCompany`).
- **Nombres saneados.** El nombre de la jornada lo escribe un usuario, así que pasa por `DisplayNameSanitizer` igual que los nombres de personas.
- **Prompt.** El prompt de todos los roles menciona `get_my_work_schedule` y declara las jornadas dentro del alcance; el del Manager añade `get_work_schedules` y que `get_team_members` ya incluye la jornada de cada persona.
- **Seguridad.** Las tres herramientas son de solo lectura; el empleado no recibe las de Manager; la compañía sale del contexto del token y el filtro por compañía de los repositorios. Se prueban con dos compañías, A y B.

## Consecuencias

- Las preguntas «¿qué jornada tengo?», «qué jornada tiene X» o «quién tiene la jornada Oficina» se responden con datos reales, sin llamadas adicionales para el Manager.
- La salida de `get_team_members` crece (hasta 100 miembros); se mantienen los campos y nombres compactos y una jornada repetida se serializa por miembro.
- Cada consulta de `get_team_members` con alguien asignado añade dos consultas (jornadas y recuentos), aceptable por ser constantes; no hay una consulta por miembro.
- Sin migración ni cambios en la API.

## Alternativas

- **Devolver solo el identificador de la jornada y obligar al modelo a llamar a otra herramienta:** descartada; añade rondas y fomenta que el modelo invente.
- **Una herramienta con el identificador de empleado como parámetro:** descartada; la identidad nunca se acepta como argumento (ADR 0017).
- **Exponer la jornada de un miembro mediante una herramienta propia:** descartada; `get_team_members` ya la incluye y evita más herramientas ofrecidas al modelo.
