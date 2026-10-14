# Delta para startup-configuration

Cambio: `datos-demo-mes`. Se añade `Seed:DemoData` a la siembra de demostración. El requisito modificado reemplaza por completo al vigente del mismo nombre.

## MODIFIED Requirements

### Requirement: Siembra de demostración configurada y validada

La API DEBE leer `Seed:DemoUsers` (variable `Seed__DemoUsers`) y `Seed:DemoData` (variable `Seed__DemoData`) como booleanos con valor `false` si no se definen; un valor no booleano DEBE impedir el arranque con un mensaje que nombre la clave afectada. `Seed:DemoData` = `true` exige `Seed:DemoUsers` = `true`: con la combinación inversa el arranque falla con un mensaje en español que nombra `Seed:DemoData`. Cuando `Seed:DemoUsers` sea `true`, DEBE exigir `Seed:DemoPassword` (variable `Seed__DemoPassword`) con al menos 12 caracteres, validada con `ValidateOnStart()`; si falta o es más corta, el arranque falla con un mensaje en español que nombre `Seed:DemoPassword` sin imprimir su valor. Cuando `Seed:DemoUsers` sea `false`, `Seed:DemoPassword` NO se exige ni se valida. Ninguna de las tres claves DEBE figurar en `appsettings*.json`. El valor de demostración de `Seed:DemoPassword` NO DEBE figurar en `.env.example` (vacío) ni en `docker-compose.yml`; solo se documenta en el README, y `Seed__DemoData` también solo en el README. El efecto de la siembra se especifica en `team-access` (usuarios) y `demo-data` (fichajes).

#### Scenario: Valor por defecto
- DADO un entorno sin `Seed:DemoUsers` ni `Seed:DemoData`
- CUANDO se enlazan las opciones
- ENTONCES ambos valores son `false` y la API arranca sin `Seed:DemoPassword`

#### Scenario: Valor no booleano
- DADO `Seed__DemoUsers` = `quizas`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Seed:DemoUsers`

#### Scenario: Datos demo con valor no booleano
- DADO `Seed__DemoData` = `quizas`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Seed:DemoData`

#### Scenario: Datos demo sin usuarios demo
- DADO `Seed__DemoData` = `true` y `Seed__DemoUsers` = `false` (o sin definir)
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Seed:DemoData` y `Seed:DemoUsers`

#### Scenario: Datos demo con usuarios demo
- DADO `Seed__DemoData` = `true`, `Seed__DemoUsers` = `true` y una contraseña válida
- CUANDO la API arranca con la base migrada
- ENTONCES arranca y `GET /health` responde `200`

#### Scenario: Siembra activa sin contraseña
- DADO `Seed__DemoUsers` = `true` y sin `Seed:DemoPassword`
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Seed:DemoPassword`

#### Scenario: Contraseña de demostración demasiado corta
- DADO `Seed__DemoUsers` = `true` y `Seed__DemoPassword` de 11 caracteres
- CUANDO la API intenta arrancar
- ENTONCES el arranque falla con un mensaje que contiene `Seed:DemoPassword`

#### Scenario: Contraseña de demostración en el límite
- DADO `Seed__DemoUsers` = `true` y `Seed__DemoPassword` de 12 caracteres
- CUANDO la API arranca con la base migrada
- ENTONCES arranca y `GET /health` responde `200`

#### Scenario: Siembra desactivada ignora la contraseña
- DADO `Seed__DemoUsers` = `false` y `Seed__DemoPassword` de 3 caracteres
- CUANDO la API arranca
- ENTONCES arranca sin error

#### Scenario: El valor no se imprime
- DADO `Seed__DemoUsers` = `true` y `Seed__DemoPassword` = `corta-demo` (10 caracteres)
- CUANDO la API falla al arrancar
- ENTONCES la excepción (y sus internas) no contiene `corta-demo`
