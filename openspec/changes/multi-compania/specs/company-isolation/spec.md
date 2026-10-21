# Especificación: company-isolation

Cambio: `multi-compania`. Capacidad NUEVA; todos los requisitos son `ADDED`. Define QUÉ debe cumplirse para que varias compañías compartan una misma instancia y una misma base de datos sin ver ni modificar datos ajenas: la tabla de compañías, `company_id` en las tablas con datos de negocio, claves foráneas compuestas, contexto de compañía tomado solo del token, filtro global que falla cerrado, lista cerrada de usos de `IgnoreQueryFilters`, respuesta idéntica ante identificadores de otra compañía, pruebas A/B y la migración expandir/contraer con datos existentes. El alta de compañías se especifica en `company-registration`; el de personas, en `employee-management`; la configuración del asistente por compañía, en `assistant-settings`.

Decisiones firmes (de la propuesta): una sola base de datos compartida con `company_id`; una persona pertenece a una sola compañía (correo único global); `CompanyId` explícito en `TimeEntry`; la compañía sale solo del claim `cid`; RLS de PostgreSQL diferida; un rollback de código NO revierte el esquema.

Notas de verificación:
- «Compañía A» y «compañía B» designan dos compañías de prueba, cada una con un Manager y dos Employees (identificadores inventados). Las pruebas con base de datos usan PostgreSQL real (Testcontainers) y tokens fabricados por `TestTokens` con `cid`.
- «Respuesta idéntica» significa mismo estado, tipo de medio (`application/problem+json`, sin parámetros), `type`, `title` y `detail`, salvo `traceId`.
- «Falla cerrado» significa que ante la ausencia de compañía en el contexto no se devuelve ninguna fila ni se acepta ninguna escritura, sin lanzar un error que revele datos.
- Los nombres de restricciones e índices se fijan en el diseño; los escenarios se evalúan por el tipo de restricción (`pg_constraint`) y por el código SQLSTATE.

Fuera de alcance: RLS de PostgreSQL (diferida, ver `solution-architecture`), pertenencia de una persona a varias compañías, rol de administrador de plataforma, baja o suspensión de compañías desde la interfaz, revocación de tokens, supresión y exportación de datos (RGPD, posposición explícita) y jornadas de trabajo.

## ADDED Requirements

### Requirement: Tabla de compañías y compañía por defecto

La base DEBE tener la tabla `companies` con `id` (uuid, clave primaria), `name` (texto, 1 a 100 caracteres), `name_normalized` (texto con índice único), `status` (texto con `CHECK` que solo admite `Active` y `Suspended`) y `created_at` (`timestamp with time zone`) y `uses_platform_assistant` (booleano, valor por defecto `false`), todas obligatorias. `uses_platform_assistant` indica si la compañía puede usar la configuración y la clave de IA de plataforma (`Ai__ApiKey`): solo una compañía con el indicador en `true` PUEDE usarla; cualquier otra compañía, mientras no tenga fila de configuración propia, tiene el asistente deshabilitado (ver `assistant-settings`). Ningún endpoint DEBE poder modificar el indicador; la compañía por defecto nace con `uses_platform_assistant` = `true` y toda compañía registrada, con `false`. `name_normalized` DEBE ser el nombre sin espacios exteriores, con los espacios consecutivos colapsados y en minúsculas (cultura invariante); NO DEBE plegar acentos. La migración DEBE crear una compañía por defecto con un identificador fijo y constante en el código (el mismo valor en todos los entornos), que es también la compañía de demostración (ver `demo-data`) y que recibe los datos previos al cambio. Un usuario de una compañía con `status` distinto de `Active` NO DEBE poder iniciar sesión (misma respuesta `401` genérica que unas credenciales erróneas); los tokens ya emitidos conservan su validez hasta caducar (límite aceptado y documentado).

#### Scenario: Columnas y restricciones de companies
- DADO la base migrada
- CUANDO se consulta `information_schema.columns`, `pg_indexes` y `pg_constraint` para `companies`
- ENTONCES existen las seis columnas indicadas, todas no nulas, `uses_platform_assistant` con valor por defecto `false`, el índice único sobre `name_normalized` y el `CHECK` de `status`

#### Scenario: Nombre duplicado tras normalizar
- DADO una compañía con `name_normalized` = `acme norte`
- CUANDO se inserta otra con el mismo `name_normalized`
- ENTONCES PostgreSQL la rechaza con `23505`

#### Scenario: Estado fuera de catálogo
- DADO una fila con `status` = `Deleted`
- CUANDO se inserta
- ENTONCES PostgreSQL la rechaza con `23514`

#### Scenario: Compañía por defecto de identificador fijo
- DADO dos bases distintas migradas desde cero
- CUANDO se lee la compañía por defecto de cada una
- ENTONCES ambas tienen el mismo `id`, `status` = `Active` y existe exactamente una

#### Scenario: Indicador de asistente de plataforma
- DADO la base migrada y una compañía registrada después
- CUANDO se lee `uses_platform_assistant` de la compañía por defecto y de la registrada
- ENTONCES la primera tiene `true` y la segunda `false`

#### Scenario: Compañía no activa no inicia sesión
- DADO un usuario cuya compañía tiene `status` = `Suspended`
- CUANDO envía `POST /api/auth/login` con credenciales correctas
- ENTONCES recibe el mismo `401` `application/problem+json` que unas credenciales erróneas y no se emite token

### Requirement: company_id en las tablas de negocio y claves foráneas compuestas

Las tablas `users`, `time_entries`, `assistant_settings` y `assistant_settings_audit` DEBEN tener `company_id` (uuid, obligatorio) con clave foránea hacia `companies(id)`. `users` DEBE tener un índice único `(company_id, id)`. La relación de responsable DEBE ser una clave foránea compuesta `(company_id, manager_id)` → `users(company_id, id)` (con `MATCH SIMPLE`: un `manager_id` nulo no exige nada) y la de fichajes una clave foránea compuesta `(company_id, employee_id)` → `users(company_id, id)`. `assistant_settings` DEBE tener `company_id` como clave primaria (una fila por compañía; se elimina el identificador único y la restricción de fila única) y `assistant_settings_audit` DEBE tener un índice `(company_id, occurred_at DESC)`. Ambas claves foráneas compuestas DEBEN ser `NO ACTION` no diferibles (ver `team-access`). El esquema DEBE rechazar con `23503` un `manager_id` de otra compañía y un fichaje cuyo `company_id` no coincida con el de su empleado. Las restricciones de `time_entries` (clave primaria, `CHECK`, índices por empleado) DEBEN conservarse.

#### Scenario: Columnas company_id presentes
- DADO la base migrada
- CUANDO se consulta `information_schema.columns` para las cuatro tablas
- ENTONCES cada una tiene `company_id` de tipo `uuid` no nulo y `assistant_settings` ya no tiene la columna `id` ni la restricción de fila única

#### Scenario: Responsable de otra compañía rechazado
- DADO un usuario `m` de la compañía B y una fila nueva de la compañía A con `manager_id` = `m`
- CUANDO se inserta
- ENTONCES PostgreSQL la rechaza con `23503`

#### Scenario: Responsable de la misma compañía aceptado
- DADO un Manager de la compañía A
- CUANDO se inserta un usuario de A con ese `manager_id`
- ENTONCES se acepta; con `manager_id` nulo también

#### Scenario: Fichaje con compañía distinta a la de su empleado
- DADO el empleado `e` de la compañía A
- CUANDO se inserta un fichaje con `employee_id` = `e` y `company_id` de la compañía B
- ENTONCES PostgreSQL lo rechaza con `23503`

#### Scenario: Fichaje de empleado inexistente
- DADO un `employee_id` que no existe en `users`
- CUANDO se inserta un fichaje con ese valor
- ENTONCES PostgreSQL lo rechaza con `23503`

#### Scenario: Una fila de configuración por compañía
- DADO una fila de `assistant_settings` de la compañía A
- CUANDO se inserta otra con el mismo `company_id`
- ENTONCES PostgreSQL la rechaza con `23505`, y una fila de la compañía B se acepta

#### Scenario: Auditoría con compañía obligatoria
- DADO la base migrada
- CUANDO se inserta una fila en `assistant_settings_audit` sin `company_id`
- ENTONCES PostgreSQL la rechaza con `23502` (sin valor por defecto) y la tabla tiene el índice `(company_id, occurred_at DESC)`

#### Scenario: Compañía inexistente
- DADO un `company_id` que no existe en `companies`
- CUANDO se inserta una fila en cualquiera de las cuatro tablas
- ENTONCES PostgreSQL la rechaza con `23503`

### Requirement: Una persona pertenece a una sola compañía

El correo DEBE seguir siendo único en toda la base (índice único global sobre `email`, no por compañía). Una persona NO DEBE poder existir en dos compañías con el mismo correo.

#### Scenario: Mismo correo en otra compañía
- DADO `luis@example.test` en la compañía A
- CUANDO se inserta un usuario de la compañía B con el mismo correo
- ENTONCES PostgreSQL lo rechaza con `23505`

#### Scenario: El correo identifica la compañía en el login
- DADO `luis@example.test` en la compañía A
- CUANDO inicia sesión
- ENTONCES el token lleva el `cid` de la compañía A

### Requirement: La compañía sale solo del token

La compañía de cada operación DEBE obtenerse exclusivamente del claim `cid` del token ya validado, mediante `ICompanyContext` (puerto de Application, adaptador en `Api`, alimentado por `ICurrentUser.CompanyId`; ver `authentication`). `ICompanyContext` DEBE exponer la compañía como valor anulable (`Guid?`): sin usuario autenticado, o con un claim ausente o inválido, la compañía es nula y NO DEBE devolverse una compañía por defecto; el contexto no lanza excepción, de modo que el filtro global falla cerrado (cero filas, ver «Filtro global de consulta que falla cerrado»). Quien lanza una excepción es `ICurrentUser.CompanyId` (ver `authentication`). La compañía NO DEBE leerse de la ruta, la consulta, el cuerpo, las cabeceras ni el historial del asistente, y ningún caso de uso, herramienta ni endpoint DEBE recibirla como parámetro. La única excepción es el registro de compañías (ver `company-registration`): es anónimo, ignora cualquier token y crea la compañía en su propio ámbito (`ICompanyScopeFactory.CreateScope(nuevoId)`), sin que el contexto de la petición adopte la compañía del token en esa ruta. El contrato `docs/api/openapi.json` NO DEBE contener ningún parámetro ni propiedad llamada `companyId` (sin distinguir mayúsculas).

#### Scenario: Compañía del token
- DADO un token con `cid` = compañía A
- CUANDO se resuelve `ICompanyContext`
- ENTONCES devuelve la compañía A

#### Scenario: Parámetros del cliente ignorados
- DADO un token de la compañía A y una petición `GET /api/team/members?companyId={B}` con cabecera `X-Company-Id: {B}`
- CUANDO se procesa
- ENTONCES el resultado contiene solo personas de A

#### Scenario: Cuerpo con companyId ignorado
- DADO un token de la compañía A y un cuerpo que afirma `companyId` = `{B}`
- CUANDO se envía a cualquier endpoint que acepte cuerpo
- ENTONCES la operación se ejecuta sobre A y ningún dato de B cambia

#### Scenario: Sin compañía en el contexto
- DADO un contexto HTTP sin usuario autenticado
- CUANDO se resuelve `ICompanyContext`
- ENTONCES su compañía es nula, no se devuelve ninguna compañía por defecto y no se lanza ninguna excepción (cualquier consulta posterior devuelve cero filas)

#### Scenario: Contrato sin companyId
- DADO el contrato regenerado
- CUANDO se busca `companyId` sin distinguir mayúsculas
- ENTONCES no aparece en ningún parámetro ni esquema

### Requirement: Filtro global de consulta que falla cerrado

`TimeTrackerDbContext` DEBE aplicar un filtro global (`HasQueryFilter`) por `company_id` igual al de `ICompanyContext` a toda entidad que tenga `company_id` (`users`, `time_entries`, `assistant_settings`, `assistant_settings_audit`). Si el contexto de compañía no está disponible o es `Guid.Empty`, el filtro DEBE devolver cero filas (falla cerrado) en lugar de omitirse. La tabla `companies` NO lleva filtro (es la propia tabla de compañías). Toda entidad nueva con `company_id` DEBE exigir su filtro: una prueba DEBE recorrer `context.Model.GetEntityTypes()` y fallar si alguna entidad con `company_id` carece de filtro.

#### Scenario: Consulta acotada
- DADO usuarios en las compañías A y B y un contexto con la compañía A
- CUANDO se consulta `users` sin ningún `Where`
- ENTONCES solo se devuelven los de A

#### Scenario: Contexto sin compañía
- DADO un `DbContext` sin compañía en el contexto y filas en la base
- CUANDO se consulta cualquiera de las cuatro entidades
- ENTONCES el resultado está vacío y no se lanza error

#### Scenario: Compañía vacía
- DADO un contexto cuya compañía es `Guid.Empty`
- CUANDO se consulta cualquiera de las cuatro entidades
- ENTONCES el resultado está vacío

#### Scenario: Todas las entidades con company_id tienen filtro
- DADO el modelo de EF Core
- CUANDO la prueba recorre `GetEntityTypes()`
- ENTONCES toda entidad con propiedad `company_id` tiene filtro de consulta; una entidad de prueba sin filtro hace fallar la prueba (comprobado una vez)

#### Scenario: Unión y agregados también filtrados
- DADO filas de A y B y un contexto con la compañía A
- CUANDO se cuentan los fichajes o se leen con `Include` desde `users`
- ENTONCES nunca intervienen filas de B

### Requirement: Lista cerrada de usos de IgnoreQueryFilters

Todo uso de `IgnoreQueryFilters` en `src/` DEBE figurar en una lista cerrada declarada en una prueba de arquitectura, con su motivo. La lista DEBE limitarse a dos sitios: `UserRepository.FindByEmailAsync` (búsqueda de usuario por correo en el inicio de sesión y en la siembra, donde la compañía aún no se conoce; el correo es único global) y `AssistantSettingsStore.ListVersionsAsync` (lectura global de las versiones de configuración del asistente por el sondeo, todas las compañías, solo `company_id` y versión). No necesitan `IgnoreQueryFilters`: la lectura de `companies` (sin filtro global, incluida la del nombre y el estado de la compañía del usuario autenticado, por clave primaria), las comprobaciones de unicidad de correo y de nombre en el registro (se apoyan en las restricciones únicas), la carga de la configuración de una compañía por el registro de instantáneas y la siembra de la compañía demo (ambas actúan dentro del contexto de su compañía). Un uso nuevo fuera de la lista DEBE hacer fallar la prueba.

#### Scenario: Usos conocidos
- DADO el código de `src/`
- CUANDO la prueba busca llamadas a `IgnoreQueryFilters`
- ENTONCES todas están en la lista cerrada y la lista no contiene ninguna entrada sin uso

#### Scenario: Uso no autorizado
- DADO una llamada temporal a `IgnoreQueryFilters` en un repositorio de fichajes
- CUANDO se ejecuta la prueba de arquitectura
- ENTONCES falla indicando el archivo (y tras retirarla vuelve a pasar; comprobado una vez)

#### Scenario: Login sin compañía conocida
- DADO un correo de la compañía A y un contexto sin compañía
- CUANDO se ejecuta el inicio de sesión
- ENTONCES se encuentra al usuario por correo (uso listado) y el `cid` del token es el de su fila

### Requirement: Escrituras con la compañía del contexto

Toda entidad con `company_id` DEBE crearse con la compañía de `ICompanyContext` (o, en la siembra y el registro, con la compañía que se crea o se localiza de forma explícita). Guardar una entidad cuyo `company_id` difiere del del contexto DEBE rechazarse sin escribir nada. `TimeEntry` DEBE exponer `CompanyId` en Domain (ver `time-tracking`).

#### Scenario: Fichaje con la compañía del token
- DADO un empleado de la compañía A autenticado
- CUANDO ficha entrada
- ENTONCES la fila de `time_entries` tiene `company_id` de A

#### Scenario: Entidad de otra compañía
- DADO un contexto con la compañía A y una entidad con `CompanyId` de B
- CUANDO se intenta guardar
- ENTONCES se rechaza, no se escribe ninguna fila y la fila de B no cambia

### Requirement: Identificador ajeno indistinguible del inexistente

Cualquier endpoint o herramienta que reciba un identificador de recurso (p. ej. `memberId`) y que corresponda a una persona o recurso de OTRA compañía DEBE producir exactamente la misma respuesta que para un identificador inexistente (`404` `application/problem+json`, `title` «Recurso no encontrado», sin `detail`; en el asistente, el mismo texto genérico de «no encontrado») y NO DEBE consultar datos asociados. La diferencia de compañía NO DEBE poder observarse ni por el contenido ni por el estado.

#### Scenario: Miembro de otra compañía
- DADO `ana` de la compañía B y un Manager de la compañía A
- CUANDO el Manager envía `GET /api/team/members/{ana}/time-entries`
- ENTONCES recibe `404` y la respuesta es idéntica a la de un `memberId` inexistente

#### Scenario: Sin consulta de fichajes
- DADO un `memberId` de otra compañía y un repositorio de fichajes espía
- CUANDO se ejecuta el caso de uso del equipo
- ENTONCES el repositorio de fichajes no recibe ninguna llamada

#### Scenario: Identificador ajeno con ventana inválida
- DADO un `memberId` de otra compañía y un `from` futuro
- CUANDO el Manager consulta
- ENTONCES recibe el mismo `400` que con un miembro propio (la ventana se valida antes)

### Requirement: Pruebas de aislamiento con dos compañías

Las pruebas de integración DEBEN construir dos compañías A y B (cada una con un Manager y dos Employees, con fichajes) y comprobar el aislamiento en: la API de equipo y de fichajes, el esquema (`23503`), las herramientas del asistente, la configuración del asistente (ver `assistant-settings`), el inicio de sesión y el token, el fallo cerrado, el registro de instantáneas del asistente (ver `assistant-endpoint`) y los límites por compañía. Un escenario de aislamiento NO DEBE omitirse en silencio: sin Docker, fallan con un mensaje claro.

#### Scenario: Empleado de B no ve datos de A
- DADO fichajes en A y en B
- CUANDO un Employee de B envía `GET /api/time-entries`
- ENTONCES la lista contiene solo fichajes suyos

#### Scenario: Manager de A no ve equipo de B
- DADO un Manager de A y un Manager de B con reportes propios
- CUANDO el Manager de A envía `GET /api/team/members`
- ENTONCES la lista no contiene a nadie de B

#### Scenario: Fichar en A no escribe en B
- DADO un empleado de A que ficha entrada y salida
- CUANDO se cuentan los fichajes de B
- ENTONCES el recuento de B no cambia

#### Scenario: Pruebas presentes
- DADO `tests/`
- CUANDO se listan las pruebas de aislamiento
- ENTONCES existen casos A/B para equipo, fichajes, esquema, herramientas, configuración, login y token, fallo cerrado, instantáneas y límites

### Requirement: Migración expandir/contraer con datos existentes

El esquema DEBE migrarse en dos migraciones consecutivas. La migración A (expandir) DEBE crear `companies`, insertar la compañía por defecto, y añadir `company_id` anulable (o con valor por defecto constante) a `users`, `time_entries` y `assistant_settings`, rellenándolo con la compañía por defecto, y a `assistant_settings_audit` con un `DEFAULT` constante igual al identificador de la compañía por defecto, SIN ejecutar ningún `UPDATE` sobre la auditoría y SIN alterar ni desactivar sus triggers de solo inserción. La migración B (contraer) DEBE hacer `company_id` obligatorio, quitar el `DEFAULT` de la auditoría, crear el índice único `(company_id, id)` de `users`, las claves foráneas compuestas, los índices, cambiar la clave primaria de `assistant_settings` a `company_id` y retirar la restricción de fila única. Si en `time_entries` existen filas cuyo `employee_id` no existe en `users` (huérfanas), la migración B DEBE abortar con un mensaje claro que indique el número de filas (sin datos personales) y sin cambios parciales. Tras ambas migraciones los datos previos (usuarios, fichajes, la fila de configuración y las filas de auditoría) DEBEN conservarse íntegros y asignados a la compañía por defecto, y los triggers de auditoría DEBEN seguir activos. El `Down` DEBE existir para ambas. El de B DEBE abortar con un mensaje claro (que indique el número de compañías, sin datos personales) y sin modificar nada si existe más de una compañía (`count(*)` de `companies` mayor que 1) o si `users` contiene más de un `company_id` distinto; la condición NO DEBE depender del número de filas de `assistant_settings` (una sola fila de configuración con dos compañías existentes ya perdería la separación). Con una sola compañía, el `Down` de B restaura la fila única de `assistant_settings` (`id smallint DEFAULT 1`, clave primaria y restricción de fila única). El de A retira lo añadido. El modelo NO DEBE tener cambios sin migrar tras ambas.

#### Scenario: Datos previos conservados
- DADO una base migrada hasta `AddAssistantAuditImmutability` con usuarios, fichajes, una fila de configuración y filas de auditoría
- CUANDO se aplican las migraciones A y B
- ENTONCES todas esas filas conservan sus valores y tienen `company_id` = la compañía por defecto

#### Scenario: Triggers de auditoría activos tras migrar
- DADO la base migrada con A y B y filas de auditoría previas
- CUANDO se intenta `UPDATE` o `DELETE` sobre `assistant_settings_audit`
- ENTONCES PostgreSQL lo rechaza (el trigger sigue activo) y las filas no cambian

#### Scenario: Migración A sin actualizar la auditoría
- DADO una base con filas de auditoría y el trigger de solo inserción activo
- CUANDO se aplica la migración A
- ENTONCES termina sin error y las filas de auditoría existentes tienen `company_id` = la compañía por defecto sin haberse ejecutado ningún `UPDATE`

#### Scenario: DEFAULT retirado en B
- DADO la migración B aplicada
- CUANDO se inserta una fila de auditoría sin `company_id`
- ENTONCES PostgreSQL la rechaza con `23502`

#### Scenario: Fichajes huérfanos
- DADO una base con un fichaje cuyo `employee_id` no existe en `users`
- CUANDO se aplica la migración B
- ENTONCES falla con un mensaje que indica el número de filas huérfanas, la base queda como tras la migración A y ninguna fila se pierde

#### Scenario: Down de B con una sola compañía
- DADO la migración B aplicada, una sola compañía y una fila de configuración
- CUANDO se ejecuta el `Down` de B
- ENTONCES se restaura la restricción de fila única, los datos se conservan y la fila de configuración sigue legible

#### Scenario: Down de B con una sola compañía y sin fila de configuración
- DADO la migración B aplicada, una sola compañía y ninguna fila de configuración
- CUANDO se ejecuta el `Down` de B
- ENTONCES termina sin error y se restaura la restricción de fila única

#### Scenario: Down de B con varias compañías
- DADO la migración B aplicada, dos compañías y una sola fila de configuración (o ninguna)
- CUANDO se ejecuta el `Down` de B
- ENTONCES aborta con un mensaje claro que indica el número de compañías, no modifica nada y las compañías, usuarios, fichajes y la fila de configuración siguen presentes

#### Scenario: Down de B con varias compañías y varias filas de configuración
- DADO la migración B aplicada y filas de configuración de dos compañías
- CUANDO se ejecuta el `Down` de B
- ENTONCES aborta con el mismo mensaje claro, no modifica nada y ambas filas siguen presentes

#### Scenario: Down de A
- DADO la base con A aplicada y B revertida
- CUANDO se ejecuta el `Down` de A
- ENTONCES no existen `companies` ni las columnas `company_id` y las filas de `users`, `time_entries` y auditoría conservan sus demás valores

#### Scenario: Sin cambios pendientes del modelo
- DADO el modelo y las migraciones
- CUANDO se invoca `HasPendingModelChanges()` sobre la base migrada
- ENTONCES devuelve `false`

#### Scenario: Aplicación al arrancar
- DADO `Database__ApplyMigrationsOnStartup=true` y una base con datos previos
- CUANDO la API arranca
- ENTONCES las migraciones A y B se aplican antes de atender peticiones y, si B aborta, la API no arranca

### Requirement: Un rollback de código no revierte el esquema

Desplegar una versión anterior del código NO DEBE revertir el esquema. El procedimiento de reversión (ejecutar el `Down` de B y A antes de redesplegar el código anterior, copia de seguridad previa y ensayo con datos copiados) DEBE estar documentado en el runbook, junto con la advertencia de que con varias compañías ya creadas el `Down` pierde la separación (el de B aborta) y solo es válido antes de abrir el registro. Una vez abierto el registro NO hay `Down`: el runbook DEBE describir el plan de corrección hacia delante (fix-forward: cerrar el registro, corregir y redesplegar código nuevo, o restaurar la copia aceptando la pérdida de los datos posteriores) y que, mientras tanto, ninguna compañía registrada puede usar la clave de IA de plataforma.

#### Scenario: Runbook de reversión
- DADO el runbook del repositorio
- CUANDO se revisa
- ENTONCES describe el orden `Down` B, `Down` A, redespliegue, la copia de seguridad previa, el ensayo con datos copiados, la advertencia de pérdida de separación y el plan de corrección hacia delante tras abrir el registro

#### Scenario: El esquema no cambia al redesplegar
- DADO una base con A y B aplicadas
- CUANDO arranca una versión anterior del código sin ejecutar `Down`
- ENTONCES el esquema sigue con `company_id` (el arranque no lo revierte)
