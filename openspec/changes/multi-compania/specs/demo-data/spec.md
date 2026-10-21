# Delta para demo-data

Cambio: `multi-compania`. Capacidad EXISTENTE (`openspec/specs/demo-data/spec.md`). Este delta solo contiene bloques `ADDED`: la compañía demo, aislada e idempotente, y la relación de la siembra con el registro abierto. Los requisitos vigentes (hora local de España, agenda determinista, ventana móvil, perfiles, servicio de siembra) siguen vigentes; los intervalos sembrados ahora llevan `CompanyId` (ver `time-tracking`).

Notas de verificación adicionales:
- «Compañía demo» es la compañía por defecto de identificador fijo creada por la migración (ver `company-isolation`); es la única que puede usar la clave de IA de plataforma `Ai__ApiKey` (ver `assistant-configuration`).
- Las pruebas usan una compañía B adicional con un Manager y dos Employees.

## ADDED Requirements

### Requirement: Compañía demo aislada e idempotente

`DemoUsersSeeder` y `DemoDataSeeder` DEBEN localizar o crear la compañía demo (identificador fijo) de forma idempotente y sembrar usuarios y fichajes exclusivamente dentro de ella, con `company_id` de la demo en cada fila. Reejecutar la siembra NO DEBE duplicar la compañía ni sus filas. La compañía demo NO DEBE poder ver datos de otra compañía ni a la inversa, aunque comparta la base de datos. Una compañía registrada después NO DEBE recibir datos demo.

#### Scenario: Compañía creada una sola vez
- DADO una base migrada y la siembra ejecutada dos veces
- CUANDO se cuentan las compañías con el identificador de la compañía por defecto
- ENTONCES existe exactamente una

#### Scenario: Filas dentro de la demo
- DADO la base sembrada
- CUANDO se leen los `company_id` de los usuarios y fichajes sembrados
- ENTONCES todos son el identificador de la compañía demo

#### Scenario: Idempotente con compañías ajenas
- DADO la base sembrada y una compañía B con datos propios
- CUANDO la API arranca otra vez con la siembra activa
- ENTONCES el número de intervalos y usuarios de la demo y de B no cambia

#### Scenario: Demo aislada de otra compañía
- DADO la base sembrada y un Manager de B
- CUANDO `marta` consulta `GET /api/team/members/{id de un empleado de B}/time-entries` y el Manager de B consulta los de `luis`
- ENTONCES ambas respuestas son `404` idénticas a las de un identificador inexistente

#### Scenario: Fichajes demo en la demo
- DADO la siembra de fichajes sobre un `luis` de la demo
- CUANDO se listan los fichajes de `luis` con el contexto de la demo y con el de otra compañía
- ENTONCES el primero devuelve los intervalos y el segundo una lista vacía

#### Scenario: Compañía registrada sin datos demo
- DADO una compañía registrada tras la siembra
- CUANDO se consultan sus usuarios y fichajes
- ENTONCES contiene solo su Manager y ningún fichaje

### Requirement: La siembra demo se apaga al abrir el registro

La siembra de usuarios demo (contraseña pública documentada en el README) NO DEBE ejecutarse cuando `Registration:Enabled` = `true`: la combinación `Seed:DemoUsers` = `true` y `Registration:Enabled` = `true` DEBE impedir el arranque con un mensaje en español que nombre ambas claves (ver `startup-configuration`), de modo que nunca coexistan usuarios con contraseña pública y registro abierto. `Seed:DemoData`, que ya exige `Seed:DemoUsers` = `true`, queda apagada por la misma razón. Con `Registration:Enabled` = `false` (valor por defecto) la siembra conserva su comportamiento. Apagar la siembra no retira los usuarios demo ya existentes: en `Production`, con el registro abierto, el arranque también falla mientras exista algún usuario del dominio de demostración en la compañía por defecto (ver `startup-configuration`).

#### Scenario: Registro abierto con siembra de datos
- DADO `Seed__DemoUsers=true`, `Seed__DemoData=true`, `Seed__DemoPassword` válida y `Registration__Enabled=true`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla antes de sembrar, con un mensaje que contiene `Seed:DemoUsers` y `Registration:Enabled`, y no se crea ningún usuario ni fichaje

#### Scenario: Registro cerrado con siembra
- DADO `Seed__DemoUsers=true`, `Seed__DemoData=true` y `Registration__Enabled` sin definir
- CUANDO la API arranca con la base migrada
- ENTONCES arranca y la siembra crea los datos demo en la compañía demo

#### Scenario: Registro abierto sin siembra
- DADO `Registration__Enabled=true` con código de invitación válido y `Seed__DemoUsers` sin definir
- CUANDO la API arranca
- ENTONCES arranca y no existe ningún usuario demo

#### Scenario: El mensaje no imprime la contraseña
- DADO `Seed__DemoPassword` = `contrasena-demo-prueba` y el registro abierto
- CUANDO la API falla al arrancar
- ENTONCES la excepción (y sus internas) no contiene ese valor

### Requirement: Clave de IA de plataforma solo para la compañía demo

El asistente de la compañía demo PUEDE usar la configuración y la clave de plataforma `Ai:*` del entorno de forma explícita; ninguna otra compañía DEBE usarla (ver `assistant-configuration` y `assistant-settings`). La condición se expresa con `companies.uses_platform_assistant` (ver `company-isolation`): la compañía demo nace con el indicador en `true` y es la única que lo tiene. Los usuarios y fichajes de la demo NO DEBEN otorgar acceso al asistente de otra compañía.

#### Scenario: Demo con la clave de plataforma
- DADO un entorno con `Ai__Provider` y `Ai__ApiKey` válidos, la compañía demo sin fila y un `IChatClient` falso
- CUANDO `marta` envía `POST /api/asistente` con un cuerpo válido
- ENTONCES recibe `200` usando la configuración del entorno

#### Scenario: Otra compañía con el mismo entorno
- DADO el mismo entorno y una compañía registrada sin fila
- CUANDO su Manager envía `POST /api/asistente` con un cuerpo válido
- ENTONCES recibe `503` y el cliente falso no recibe ninguna llamada
