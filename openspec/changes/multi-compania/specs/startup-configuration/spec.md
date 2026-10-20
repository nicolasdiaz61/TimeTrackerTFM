# Delta para startup-configuration

Cambio: `multi-compania`. Capacidad EXISTENTE (`openspec/specs/startup-configuration/spec.md`). Este delta solo contiene bloques `ADDED`: las claves nuevas `Registration__*` y `Company__MaxEmployees`, su validación al arrancar, la exclusión entre siembra demo y registro, y su documentación en `.env.example`. Los requisitos vigentes (incluidos «Siembra de demostración configurada y validada» y «Documentación de claves en `.env.example`») siguen vigentes; las claves de topes por compañía del asistente (`Ai:RequestsPerMinutePerCompany`, `Ai:DailyRequestsPerCompany`, `Ai:MaxLoadedCompanies`) se especifican en `assistant-configuration`. Ningún valor secreto ni código de invitación figura en el repositorio.

Notas de verificación adicionales:
- Los fallos de arranque se evalúan sobre la excepción de validación y sus internas (puede ser un `AggregateException`).
- Los códigos de invitación de las pruebas son valores inventados y reconocibles.

## ADDED Requirements

### Requirement: Registro de compañías configurado y validado

La API DEBE leer `Registration:Enabled` (variable `Registration__Enabled`) como booleano con valor `false` si no se define; un valor no booleano DEBE impedir el arranque con un mensaje que nombre `Registration:Enabled`. DEBE leer `Registration:InviteCodes` (variable `Registration__InviteCodes`) como lista separada por comas. Con `Registration:Enabled` = `true`, DEBE exigir al menos un código y que cada código mida entre 16 y 128 caracteres tras recortar espacios (los elementos en blanco no son válidos); si no se cumple, el arranque falla con un mensaje en español que nombre `Registration:InviteCodes` y la razón, sin imprimir ningún código. Con `Registration:Enabled` = `false`, `Registration:InviteCodes` NO se exige ni se valida. Ninguna de las dos claves DEBE figurar en `appsettings*.json`, en el código ni en la imagen Docker. El efecto del registro se especifica en `company-registration`.

#### Scenario: Valores por defecto
- DADO un entorno sin `Registration__Enabled` ni `Registration__InviteCodes`
- CUANDO se enlazan las opciones
- ENTONCES `Enabled` es `false` y la API arranca sin ningún código

#### Scenario: Valor no booleano
- DADO `Registration__Enabled` = `quizas`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Registration:Enabled`

#### Scenario: Registro abierto sin códigos
- DADO `Registration__Enabled=true` y sin `Registration__InviteCodes`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Registration:InviteCodes`

#### Scenario: Código demasiado corto o elemento en blanco
- DADO `Registration__Enabled=true` y `Registration__InviteCodes` = `corto-123`, y por separado `codigo-largo-0123456789,`
- CUANDO la API intenta arrancar en cada caso
- ENTONCES cada arranque falla con un mensaje que contiene `Registration:InviteCodes`

#### Scenario: Código en los límites
- DADO `Registration__Enabled=true` y un código de 16 caracteres, y por separado uno de 128
- CUANDO la API arranca con cada uno
- ENTONCES arranca y `GET /health` responde `200`

#### Scenario: Varios códigos válidos
- DADO `Registration__InviteCodes` con dos códigos válidos separados por coma y `Registration__Enabled=true`
- CUANDO la API arranca
- ENTONCES arranca

#### Scenario: Registro cerrado ignora los códigos
- DADO `Registration__Enabled=false` y `Registration__InviteCodes` = `corto`
- CUANDO la API arranca
- ENTONCES arranca sin error

#### Scenario: El valor no se imprime
- DADO `Registration__Enabled=true` y `Registration__InviteCodes` = `corto-secreto-prueba` (demasiado corto)
- CUANDO la API falla al arrancar
- ENTONCES la excepción (y sus internas) contiene `Registration:InviteCodes` y no contiene `corto-secreto-prueba`

#### Scenario: No versionadas
- DADO los archivos `appsettings*.json`
- CUANDO se buscan claves `Registration`
- ENTONCES no existe ninguna

### Requirement: Tope de personas por compañía configurado y validado

La API DEBE leer `Company:MaxEmployees` (variable `Company__MaxEmployees`; valor por defecto 50; rango 1 a 1000, extremos incluidos) como el máximo de usuarios (Employees y Managers) de una compañía. Un valor fuera de rango o no numérico DEBE impedir el arranque con un mensaje en español que nombre `Company:MaxEmployees` y la razón sin imprimir el valor. La clave NO DEBE figurar en `appsettings*.json` ni en el código, y el Manager NO puede editarla (ver `employee-management`).

#### Scenario: Valor por defecto
- DADO un entorno sin `Company__MaxEmployees`
- CUANDO se enlazan las opciones
- ENTONCES el tope es 50

#### Scenario: Fuera de rango
- DADO `Company__MaxEmployees` = `0` y, por separado, `1001`
- CUANDO la API intenta arrancar en cada caso
- ENTONCES cada arranque falla con un mensaje que contiene `Company:MaxEmployees`

#### Scenario: Extremos válidos
- DADO `Company__MaxEmployees` = `1` y, por separado, `1000`
- CUANDO la API arranca con cada uno
- ENTONCES arranca

#### Scenario: No numérico
- DADO `Company__MaxEmployees` = `muchos`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Company:MaxEmployees` y no contiene `muchos`

### Requirement: La siembra demo y el registro no pueden coexistir

Con `Seed:DemoUsers` = `true` y `Registration:Enabled` = `true`, la API NO DEBE arrancar: el mensaje en español DEBE nombrar `Seed:DemoUsers` y `Registration:Enabled`, explicar que la siembra crea usuarios con contraseña pública y NO DEBE imprimir ningún valor secreto. La validación DEBE ocurrir antes de cualquier migración o siembra. Con cualquiera de las dos en `false` (valores por defecto) NO hay conflicto. `Seed:DemoData` sigue exigiendo `Seed:DemoUsers` = `true`.

#### Scenario: Ambas activas
- DADO `Seed__DemoUsers=true`, `Seed__DemoPassword` válida y `Registration__Enabled=true` con un código válido
- CUANDO la API intenta arrancar con `Database__ApplyMigrationsOnStartup=true`
- ENTONCES el arranque falla con un mensaje que contiene `Seed:DemoUsers` y `Registration:Enabled`, no se aplica ninguna migración ni siembra y no se atiende ninguna petición

#### Scenario: Solo siembra
- DADO `Seed__DemoUsers=true`, `Seed__DemoPassword` válida y `Registration__Enabled` sin definir
- CUANDO la API arranca
- ENTONCES arranca

#### Scenario: Solo registro
- DADO `Registration__Enabled=true` con un código válido y `Seed__DemoUsers` sin definir
- CUANDO la API arranca
- ENTONCES arranca

#### Scenario: El mensaje no imprime secretos
- DADO `Seed__DemoPassword` = `contrasena-demo-prueba` y un código válido reconocible, ambas bajo la combinación inválida
- CUANDO la API falla al arrancar
- ENTONCES la excepción (y sus internas) no contiene ninguno de los dos valores

### Requirement: Documentación en `.env.example` de las claves multi-compañía

`.env.example` DEBE documentar `Registration__Enabled` como línea activa con valor `false`, `Registration__InviteCodes` como línea comentada (`#`) sin valor real y `Company__MaxEmployees` como línea comentada con su valor por defecto (50), además de `Ai__RequestsPerMinutePerCompany`, `Ai__DailyRequestsPerCompany` y `Ai__MaxLoadedCompanies` como líneas comentadas con sus valores por defecto (30, 500 y 100). Los comentarios DEBEN indicar que el registro está cerrado por defecto, que se abre solo cuando el aislamiento está probado y desplegado, que los códigos de invitación son secretos que se definen únicamente en el entorno del despliegue (Railway) y nunca en el archivo, que no puede combinarse con `Seed__DemoUsers=true`, que los topes por compañía se fijan por entorno y no los edita un Manager, y que la clave maestra de cifrado vive solo en Railway. `docker-compose.yml` NO DEBE activar `Registration__Enabled` ni definir códigos de invitación. NO DEBE aparecer ningún código de invitación con valor ni clave maestra con valor en el repositorio.

#### Scenario: Claves documentadas
- DADO `.env.example`
- CUANDO se buscan las seis claves nuevas
- ENTONCES `Registration__Enabled=false` está activa, `Registration__InviteCodes` y las demás llevan `#` inicial y ninguna lleva un valor secreto

#### Scenario: Copiar el archivo no abre el registro
- DADO un `.env` creado copiando `.env.example` sin editar
- CUANDO la API arranca
- ENTONCES arranca con el registro cerrado

#### Scenario: Compose sin registro
- DADO `docker-compose.yml`
- CUANDO se buscan `Registration__`
- ENTONCES no aparece ninguna activación ni código

#### Scenario: Comentarios
- DADO `.env.example`
- CUANDO se leen los comentarios de las claves nuevas
- ENTONCES indican registro cerrado por defecto, códigos solo en el entorno del despliegue, incompatibilidad con la siembra demo, topes fijados por entorno y no editables por un Manager, y clave maestra solo en Railway

#### Scenario: Sin secretos en el repositorio
- DADO el repositorio y la imagen Docker
- CUANDO se buscan valores de `Registration__InviteCodes` y de `Ai__MasterKeys__`
- ENTONCES no hay ninguno
