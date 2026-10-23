# Delta: work-schedules

## ADDED Requirements

### Requirement: Solo el Manager configura jornadas
Todas las operaciones de `/api/work-schedules` (salvo `mine`) y la asignación exigen el rol Manager.

#### Scenario: Empleado o anónimo
- GIVEN una persona con rol Employee, o sin sesión
- WHEN llama a cualquier operación de gestión de jornadas
- THEN recibe `403` (Employee) o `401` (anónimo)

### Requirement: Jornada de compañía con validación
Una jornada tiene nombre (1 a 100 caracteres, único en la compañía sin distinguir mayúsculas), hora de entrada, inicio del almuerzo
posterior a la entrada y que termina antes de medianoche, duración del almuerzo y pausa esperada (0 a 480 minutos) y al menos un día.

#### Scenario: Datos inválidos
- WHEN se crea o edita una jornada con datos que incumplen estas reglas
- THEN la respuesta es `400` con los errores por campo en español

#### Scenario: Nombre repetido o tope
- WHEN el nombre ya existe en la compañía, o la compañía ya tiene 20 jornadas
- THEN la respuesta es `409` con un mensaje claro

### Requirement: Aislamiento entre compañías
#### Scenario: Jornada de otra compañía
- GIVEN una jornada de la compañía B
- WHEN un Manager de A la edita, la borra o la asigna
- THEN recibe `404`, igual que para un identificador inexistente, y no la ve en el listado

### Requirement: Asignación a personas del equipo
Un Manager asigna o retira (`workScheduleId: null`) la jornada de una persona de su equipo; cada persona tiene como máximo una.

#### Scenario: Asignar y retirar
- WHEN el Manager asigna una jornada de su compañía a un reporte directo
- THEN el listado muestra `assignedCount` actualizado, `GET /api/team/members` incluye `workScheduleId` y `GET /api/work-schedules/mine`
  de esa persona devuelve la jornada (`204` si no tiene)

#### Scenario: Persona ajena al equipo o de otra compañía
- THEN `404`

### Requirement: Borrado sin cascada
#### Scenario: Jornada asignada
- WHEN se borra una jornada con personas asignadas
- THEN `409` y nada cambia
