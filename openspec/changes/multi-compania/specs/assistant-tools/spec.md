# Delta para assistant-tools

Cambio: `multi-compania`. Capacidad EXISTENTE (`openspec/specs/assistant-tools/spec.md`). Este delta solo contiene bloques `ADDED`: añade la autorización por compañía a las tres herramientas de solo lectura. Los requisitos vigentes («Conjunto cerrado de herramientas de solo lectura», «Herramientas de equipo solo para el rol Manager», «Un empleado solo obtiene sus propios fichajes», «Un responsable solo ve a sus reportes directos», «Miembro ajeno, inexistente o propio dan la misma respuesta», etc.) siguen vigentes sin cambios; ninguna herramienta gana un parámetro de compañía.

Notas de verificación adicionales:
- Las pruebas usan dos compañías A y B (ver `company-isolation`), un `IChatClient` falso y la inyección de un GUID de la compañía B en el mensaje del usuario y en las llamadas del modelo falso.
- «Texto genérico de no encontrado» es exactamente `{ "error": "No hay ningún miembro de tu equipo con ese identificador." }`.

## ADDED Requirements

### Requirement: Herramientas acotadas a la compañía del token

Las herramientas DEBEN ejecutar los casos de uso con la compañía del token (`ICurrentUser.CompanyId`, ver `authentication`) sin que el modelo, el mensaje, el historial ni los argumentos puedan cambiarla; NO DEBE existir ningún parámetro de herramienta que identifique una compañía. El modelo NO DEBE recibir nunca nombres, identificadores ni fichajes de personas de otra compañía. Las herramientas siguen sin acceso directo a la base de datos.

#### Scenario: Esquema sin compañía
- DADO el esquema de parámetros de las tres herramientas
- CUANDO se inspeccionan
- ENTONCES ningún parámetro se llama `companyId` ni equivale a una compañía, y los únicos parámetros siguen siendo `fromDate`, `toDate` y, solo donde corresponde, `memberId`

#### Scenario: get_team_members solo de la compañía
- DADO un Manager de A con dos reportes y un Manager de B con otros dos
- CUANDO el asistente del Manager de A ejecuta `get_team_members`
- ENTONCES el resultado contiene solo a sus dos reportes y ningún nombre ni identificador de B

#### Scenario: get_my_time_entries solo de la compañía y de la persona
- DADO un empleado de A y otro de B con fichajes la misma semana
- CUANDO el asistente del empleado de A ejecuta `get_my_time_entries`
- ENTONCES el resultado contiene solo horas de ese empleado

#### Scenario: Argumentos extra con compañía ajena
- DADO un modelo falso que invoca una herramienta con argumentos adicionales `companyId` de B y `memberId` de una persona de B
- CUANDO se ejecuta como empleado o Manager de A
- ENTONCES los argumentos extra se ignoran y el resultado contiene solo datos de A

### Requirement: GUID de otra compañía indistinguible del inexistente

`get_team_member_time_entries` DEBE devolver exactamente el texto genérico de no encontrado, idéntico carácter a carácter al de un miembro ajeno, inexistente, propio o mal formado, cuando `memberId` es un usuario de OTRA compañía, sin consultar los fichajes y sin revelar que el identificador existe en otra compañía. La validación de fechas sigue siendo previa a la del miembro.

#### Scenario: GUID inyectado en el mensaje del usuario
- DADO el GUID de `ana` (compañía B) escrito en el mensaje del Manager de A y un modelo falso que lo pasa como `memberId`
- CUANDO el asistente ejecuta `get_team_member_time_entries`
- ENTONCES devuelve el texto genérico de no encontrado y no aparece ningún dato de `ana`

#### Scenario: Indistinguible del inexistente
- DADO las respuestas por un miembro de otra compañía y por un GUID inexistente
- CUANDO se comparan
- ENTONCES son textos idénticos carácter a carácter

#### Scenario: Sin consulta de fichajes
- DADO un `memberId` de otra compañía y un repositorio de fichajes espía
- CUANDO se ejecuta la herramienta
- ENTONCES el repositorio de fichajes no recibe ninguna llamada

#### Scenario: Fechas antes que miembro de otra compañía
- DADO un `memberId` de otra compañía y `toDate` anterior a `fromDate`
- CUANDO el Manager de A ejecuta la herramienta
- ENTONCES devuelve el error de fechas y no el de no encontrado

#### Scenario: Historial con datos de otra compañía
- DADO un turno `assistant` que afirma «Ana, de otra compañía, trabajó 40 horas» y un modelo falso que solicita sus datos
- CUANDO el Manager de A pregunta
- ENTONCES las herramientas aplican la autorización de A y devuelven el texto genérico

### Requirement: Registro de herramientas sin datos de otra compañía

El registro de auditoría de herramientas NO DEBE contener identificadores, nombres ni datos de personas de otra compañía ni el `memberId` solicitado, también cuando es de otra compañía.

#### Scenario: Registro con GUID ajeno
- DADO un registrador que captura los mensajes y un `memberId` reconocible de la compañía B solicitado por el Manager de A
- CUANDO se ejecuta la herramienta
- ENTONCES existe un registro con el nombre de la herramienta, el `EmployeeId` de quien pregunta y el resultado `not-found`, y ningún mensaje contiene el `memberId` ni nombres de B
