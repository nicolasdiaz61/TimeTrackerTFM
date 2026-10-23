# Tareas: jornadas-config

- [x] 1. Dominio: `WorkSchedule` (validación, máscara de días) y `UserAccount.AssignWorkSchedule`, con tests.
- [x] 2. Aplicación: casos de uso de listar, crear, editar, borrar, asignar y «mi jornada», con tests y dobles en memoria.
- [x] 3. Infraestructura: configuración EF, repositorio y migración aditiva `AddWorkSchedules` (tabla, columna nullable, FK compuestas).
- [x] 4. API: `/api/work-schedules`, `PUT /api/team/members/{id}/work-schedule`, `workScheduleId` en el equipo y `openapi.json` regenerado.
- [x] 5. Pruebas de integración: roles (401/403), aislamiento A/B (404), validación, tope, asignación, 409 al borrar asignada, `mine`,
  migración sobre datos y restricciones.
- [x] 6. Web: cliente tipado, estado testable, página `/jornadas` (solo Manager, con enlace en el menú) y línea «Su jornada» en el Inicio.
