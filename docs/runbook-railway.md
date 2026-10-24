# Runbook de Railway: despliegue manual, copia de seguridad, reversión y apertura del registro

Procedimiento operativo del cambio `multi-compania` (una instancia, varias compañías). Lo ejecuta y aprueba **el responsable**; quien implementa el código no accede a producción. Todos los valores de este documento son marcadores: ninguna contraseña, clave, cadena de conexión ni código de invitación real se escribe aquí ni en el repositorio.

Decisiones de fondo: [ADR 0020](adr/0020-aislamiento-multiempresa.md) (aislamiento y rollback), [ADR 0021](adr/0021-configuracion-del-asistente-por-compania.md) (configuración por compañía), [ADR 0022](adr/0022-autorregistro-con-codigo-de-invitacion-y-contrasenas.md) (registro y contraseñas) y [ADR 0013](adr/0013-despliegue-en-railway.md) (despliegue).

## Cómo leer este documento (fases, verificaciones y preguntas)

Este runbook es autónomo: no depende de ningún otro archivo del repositorio para entender las referencias que usa.

- **Fases (PR 1 a 8 y tarea 9).** El cambio `multi-compania` se entregó en diez pull requests apiladas, que se despliegan **una a una**: **PR 1** (modelo de compañías y migración A, `AddCompanies`: añade `company_id` admitiendo nulos); **PR 1b** (migración B, `EnforceCompanyIsolation`: claves foráneas compuestas y `NOT NULL`, y el puente transitorio del asistente); **PR 2** (el token lleva la compañía en el claim `cid`); **PR 3** (filtro de consultas por compañía); **PR 4a** y **PR 4b** (configuración y ejecución del asistente por compañía); **PR 5** (autorregistro con código de invitación, protegido por `Registration__Enabled`); **PR 6** (alta de personas con contraseña temporal y cambio de contraseña); **PR 7** (Web); **PR 8** (documentación). La **tarea 9** no es una PR: es la verificación final, el despliegue por fases y la apertura del registro, que ejecuta el responsable. La **PR 5.1** endurece los límites del registro (grupos IPv6 `/64`, tope global y particiones acotadas) y no lleva migraciones.
- **Verificaciones `V-n`.** Comprobaciones de producción que solo puede hacer el responsable (se listan en «Verificaciones de producción»). Mientras no estén anotadas como hechas, no se da nada por supuesto.
- **Preguntas `Q-n`.** Decisiones abiertas del cambio que corresponden al responsable: **Q-1** (qué hacer con los datos existentes y con la compañía Demo si tiene usuarios reales: migrarlos a la compañía por defecto o reiniciar la base); **Q-8** (custodia de la clave maestra del asistente); **Q-10** (quién abre el registro y cómo se rotan los códigos de invitación); **Q-12** (resuelta: no se registra qué código de invitación se aceptó; la rotación sustituye la lista completa).
- **Esquemas.** «Esquema A» es la base con `Up A` aplicada; «esquema B», con `Up B` aplicada; «antes de A», la base sin la tabla `companies`.

## Estado de las verificaciones de producción

Lo que **no** está comprobado no se da por hecho. Las verificaciones de producción siguen **pendientes del responsable** y condicionan los despliegues, no el código.

## Verificaciones de producción

- **V-1 (contenido de producción).** Pendiente del responsable. `SELECT count(*) FROM users`, usuarios cuyo correo no sea de demostración, `time_entries` huérfanos (`LEFT JOIN users`), filas de `assistant_settings` y recuento de `assistant_settings_audit`. Puerta previa del despliegue de la migración B y de la apertura del registro. Decide Q-1 (qué hacer con los datos existentes y con la compañía Demo si tiene usuarios reales).
- **V-3 (vía real de scripts y `Down` en Railway).** Pendiente del responsable. Comprobar cómo ejecutar scripts contra la base de datos de Railway (`railway connect`, proxy TCP público, `railway run` u otra) y la versión de `psql` disponible. Mientras no se anote aquí la vía real, **no se despliega la PR 1** y ninguna migración B ni `Down` se aplica en producción. Solo está hecha la parte local (ver «Vía de los scripts»).
- **V-4 (versión de PostgreSQL de Railway).** Pendiente del responsable (la parte local está hecha con PostgreSQL 18). Debe ser mayor o igual que 11 para que `ADD COLUMN ... DEFAULT` no reescriba la tabla de auditoría; comprobar también que `pg_dump` local es compatible con esa versión mayor.
- **V-6 (instancias solapadas y valor de `Database__ApplyMigrationsOnStartup`).** Pendiente del responsable. Comprobar si Railway solapa la instancia nueva y la antigua durante un despliegue y **leer el valor real** de `Database__ApplyMigrationsOnStartup` en producción. No se asume el valor anterior: se relee y se anota con fecha **antes de cada despliegue manual**. Hecho verificado aparte: los servicios no tienen repositorio conectado (`source.repo = null`).
- **V-7 (`ForwardedHeaders` en producción).** Pendiente del responsable. Comprobar `ForwardedHeaders__Enabled=true` y `ForwardedHeaders__Hops` de 1 o más (el valor por defecto en código es 2: «cliente, borde»). Puerta previa de la apertura del registro; el validador de arranque impide abrirlo en `Production` sin ellos, pero **el código no puede saber cuántos proxies hay de verdad delante**, así que el número correcto lo comprueba el responsable: con `Hops` mayor que los proxies reales, una cabecera `X-Forwarded-For` enviada por el cliente se leería como su IP (suplantable); con `Hops` menor, todos los clientes compartirían la IP del borde. Prueba: seis peticiones a `POST /api/auth/register-company` con un código incorrecto y, en cada una, una `X-Forwarded-For` falsa distinta antepuesta por el cliente; la sexta debe responder `429` (la IP que cuenta es la que añade el borde). Comprobar también que la API solo es alcanzable a través del borde de Railway (sin otro dominio ni puerto público directo), porque con `ForwardedHeaders` activo no hay lista de proxies de confianza.

## Despliegue manual

Los servicios de Railway no tienen repositorio conectado (sin repositorio conectado, `source.repo = null`): **fusionar una PR no despliega**. Se despliega a mano con `railway up --service api --ci` (y `--service web` para la Web). Si algún día se conecta el autodespliegue, **este plan por fases debe rehacerse antes de fusionar**: las migraciones A y B no pueden desplegarse seguidas ni juntas.

Puerta previa de **cada** despliegue (el responsable la completa y aprueba cada vez):

1. V-6 leída ese mismo día: valor real de `Database__ApplyMigrationsOnStartup`, anotado con fecha.
2. Copia de seguridad previa si el despliegue lleva migración (ver «Copia de seguridad y restauración»), restaurada y comprobada en un PostgreSQL local de la misma versión mayor.
3. Comprobación de salud: `GET <API>/health` responde `200` antes (servicio sano) y después (instancia nueva sana).
4. Rollback de código preparado: versión anterior identificada y válida contra el esquema que quedará. Es válido para A; para B solo hasta aplicarla (después, `Down` o corrección hacia delante).
5. V-3 anotada: vía de scripts conocida y `psql` con `-v ON_ERROR_STOP=1` y `--single-transaction`.
6. Aprobación explícita del responsable.

Secuencia por fases, **una a una y con observación entre ellas** (la duración la fija el responsable):

1. **PR 1 (migración A, `Up A`).** Requiere V-3, V-4, V-6 y el ensayo (parte 1). Si `Database__ApplyMigrationsOnStartup` es `true`, `railway up` aplica A al arrancar tras la copia; si es `false`, se aplica antes por la vía de V-3. Humo (`/health`, inicio de sesión, fichaje) y observación con el rollback de código disponible. Tras comprobar que la instancia antigua ya no recibe tráfico, rellenar a mano las filas que el código anterior haya escrito durante un posible solape: `UPDATE time_entries SET company_id = '<id de la compañía por defecto>' WHERE company_id IS NULL`, y lo mismo para `assistant_settings`.
2. **PR 1b (migración B, `Up B`), nunca seguida ni junto con la PR 1.** Con V-1 resuelta, el ensayo (parte 2) anotado y la observación de la PR 1 cumplida: primero `Database__ApplyMigrationsOnStartup=false` (redespliega el código ya desplegado) y comprobar `/health`; copia; breve ventana de mantenimiento sin tráfico de escritura; aplicar B por la vía de V-3; `railway up` del código de la 1b; humo (incluida la lectura y el guardado de la configuración del asistente) y observación. La variable se queda en `false`: las PR 2 a 8 no llevan migraciones.
3. **PR 2** (las personas deberán iniciar sesión de nuevo), **PR 3**, **PR 4a** (definir `Ai__MasterKeys__*` antes de que un responsable guarde una clave, si no existían), **PR 4b**, y **PR 5 a 8** con `Registration__Enabled=false`. Cada una repite la puerta (sin copia si no hay migración), con humo y observación.

## Variables del servicio `api`

Se definen con `railway variables set -s api ...`; las secretas por `stdin`.

| Variable | Valor y notas |
|---|---|
| `Registration__Enabled` | `false` por defecto. Solo se abre siguiendo «Apertura del registro». |
| `Registration__InviteCodes` | **Secreto.** Lista separada por comas, cada código de 16 a 128 caracteres. Solo en Railway, por `stdin`; nunca en el repositorio, en `.env.example` ni en este documento. |
| `Registration__GlobalHourlyLimit` | Tope global de altas por hora entre todas las IP (30 por defecto, de 1 a 1000); acota el daño de un código de invitación filtrado. Solo cuentan las compañías creadas, no los intentos rechazados. Es independiente del límite de 5 intentos por hora y por IP (las direcciones IPv6 se agrupan por prefijo `/64`). Se aplica por instancia (límites en memoria). Superado el tope, el registro responde `429` con `Retry-After` hasta que acabe la ventana de una hora. |
| `Company__MaxEmployees` | Tope de personas por compañía (50 por defecto, de 1 a 1000). |
| `Ai__RequestsPerMinutePerCompany`, `Ai__DailyRequestsPerCompany`, `Ai__MaxLoadedCompanies`, `Ai__RuntimeIdleMinutes` | Topes del asistente por compañía (30, 500, 100 y 60 por defecto). |
| `Database__ApplyMigrationsOnStartup` | Según V-6 (se relee en cada despliegue). `false` antes de aplicar la migración B y en las PR siguientes. |
| `ForwardedHeaders__Enabled`, `ForwardedHeaders__Hops` | `true` y `Hops` de 1 o más (V-7). Obligatorios para abrir el registro en `Production`. |
| `Ai__MasterKeys__1`, `Ai__ActiveMasterKeyVersion` | La clave maestra vive solo en Railway (secreto, por `stdin`; véase el README). |

## Copia de seguridad y restauración

Antes de todo despliegue con migración, de toda reversión y de la retirada de usuarios demo:

```powershell
# Copia en formato personalizado (sin credenciales en el historial: use las variables del servicio)
pg_dump -Fc --no-owner -h <host> -p <puerto> -U <usuario> -d <base> -f copia-previa.dump
# Restauración local en un PostgreSQL de la MISMA versión mayor (V-4) y comprobación
createdb -U <usuario-local> ensayo
pg_restore --no-owner -U <usuario-local> -d ensayo copia-previa.dump
```

Comprobar en la restauración el recuento de `users`, `time_entries` y `assistant_settings`. La copia gestionada de Railway solo cuenta como respaldo si V-3 lo confirma; mientras tanto vale la copia con `pg_dump -Fc`. Las copias contienen datos personales: se guardan fuera del repositorio y se borran al terminar.

## Vía de los scripts y migraciones

Las migraciones se aplican como **un script por migración** y con `psql`:

```powershell
dotnet tool run dotnet-ef migrations script <desde> <hasta> --idempotent --project src/TimeTracker.Infrastructure --startup-project src/TimeTracker.Infrastructure -o up-a.sql
psql "<conexión>" -v ON_ERROR_STOP=1 --single-transaction -f up-a.sql
```

Con `<desde>` la migración anterior y `<hasta>` `AddCompanies` (A) o `EnforceCompanyIsolation` (B); para `Down`, el orden inverso. Comprobado en local con `psql` 18: el script de EF lleva su propio `START TRANSACTION`/`COMMIT`, de modo que dentro de `--single-transaction` aparecen los avisos `there is already a transaction in progress` y `there is no transaction in progress`. Son inocuos con un script por migración (un error a mitad deshace la migración entera); **no concatene migraciones en un mismo script** ni espere atomicidad entre ellas. Lo que falta por comprobar en Railway es V-3.

## Hora de salida de las jornadas de demostración

La migración `AddWorkScheduleEndTime` (ADR 0031) añade `end_time` nula: las jornadas existentes siguen usando la suposición de 8 h hasta que se edite cada una desde `/jornadas` o se actualice por SQL. La siembra de la compañía de demostración TFM vive fuera del repositorio, así que para sus tres jornadas (por nombre; ejecútelo con la conexión del servicio de la base de datos y revise antes el número de filas):

```sql
UPDATE work_schedules SET end_time = '18:00' WHERE company_id = '<id-compañía-demo>' AND name = 'Oficina'; -- almuerzo 60 min: 8 h
UPDATE work_schedules SET end_time = '16:45' WHERE company_id = '<id-compañía-demo>' AND name = 'Madrugador'; -- almuerzo 45 min: 8 h
UPDATE work_schedules SET end_time = '12:00' WHERE company_id = '<id-compañía-demo>' AND name = 'Media jornada'; -- sin almuerzo: 4 h
```

Sustituya `<id-compañía-demo>` por el id de la compañía de demostración; cada `UPDATE` debe afectar a una sola fila. Para deshacerlo, `UPDATE work_schedules SET end_time = NULL WHERE company_id = '<id-compañía-demo>'`.

## Ensayo con datos copiados

Antes de desplegar, en un PostgreSQL local de la misma versión mayor y con una copia de producción restaurada (si el responsable la facilita; si no, con una base sembrada con auditoría y un fichaje huérfano), en dos partes:

1. **Parte 1 (antes de la PR 1).** `Up A`, `Down A` y arranque de la API sobre `Up A`: los datos previos quedan intactos tras `Down A` y los triggers de la auditoría siguen en pie. Hecha en local con datos sintéticos; repetirla con la copia real es decisión del responsable.
2. **Parte 2 (antes de la PR 1b).** `Up A`, `Up B` (aborta con el recuento si hay fichajes huérfanos; se corrigen y se repite), `Down B` con una sola compañía (funciona), `Down B` con dos compañías (**aborta** sin cambiar nada), `Down A` y arranque de la API sobre `Up B`. Hecha en local con datos sintéticos.

Resultado del ensayo local con datos sintéticos (V-11): `Up A`, `Down A` y el arranque sobre `Up A` conservan los datos y los triggers; `Up B` aborta con el recuento si hay fichajes huérfanos; `Down B` funciona con una sola compañía y aborta con dos; la API arranca sobre `Up B`. Repetirlo con la copia real es decisión del responsable.

## Procedimiento de rollback de producción

**Un rollback de código no revierte el esquema.** Redesplegar la versión anterior sobre un esquema posterior puede fallar o, peor, servir datos sin separación entre compañías. Orden obligatorio, mientras **no** se haya abierto el registro:

1. Apagar el registro (`Registration__Enabled=false`) y dejar de servir escrituras.
2. Copia de seguridad nueva (`pg_dump -Fc`).
3. Aplicar `Down` B → `Down` A por la vía de V-3, con `psql` y `-v ON_ERROR_STOP=1 --single-transaction`, un script por migración. **`Down B` aborta si hay más de una compañía.**
4. Solo entonces redesplegar el código anterior con `Database__ApplyMigrationsOnStartup=false`.
5. Comprobar `/health`, el inicio de sesión y un fichaje.

**Advertencia del rollback de un clic.** El botón de Railway que vuelve a un despliegue anterior cambia solo el código y no el esquema: con el esquema B aplicado supone la pérdida de la separación entre compañías (el código antiguo no conoce `company_id`). No se usa sin ejecutar antes `Down` B → `Down` A.

**Tras abrir el registro no hay `Down`.** Con compañías registradas, `Down B` aborta y el puente del asistente nunca sirve a compañías registradas con la clave de plataforma. Se corrige hacia delante (corrección hacia delante, o fix-forward): cerrar el registro (`Registration__Enabled=false`), corregir y desplegar con `railway up`; o restaurar la copia aceptando la pérdida de los datos posteriores a ella. La restauración de una copia sustituye todos los datos: requiere aprobación explícita. El procedimiento paso a paso está en «Tras abrir el registro: restauración o corrección hacia delante».

## Tras abrir el registro: restauración o corrección hacia delante

Con compañías registradas **no hay `Down`**: `Down B` aborta si hay más de una compañía. Ante un fallo grave (datos dañados, fuga entre compañías, esquema roto) hay dos caminos; los pasos siguientes son el procedimiento del responsable y requieren **aprobación explícita** antes de cada paso destructivo.

**Elegir el camino.** Preferir siempre la **corrección hacia delante**: corregir el código y desplegarlo con `railway up`, sin tocar los datos. Restaurar una copia solo cuando los datos o el esquema están dañados y no se pueden corregir con un script revisado. **Restaurar sustituye todos los datos por los de la copia**: se pierden las compañías, las cuentas, los fichajes y los cambios de configuración posteriores a ella. Quienes se registraron después tendrán que volver a registrarse (el código de invitación es reutilizable mientras no se rote) y los tokens ya emitidos de compañías que ya no existan dejarán de funcionar.

1. **Parar las escrituras.** Poner `Registration__Enabled=false` (esto redespliega el servicio `api`) y dejar de servir tráfico de escritura: retirar el despliegue activo de `api` en Railway o quitar temporalmente su dominio público, y avisar a las personas. Comprobar que no quedan conexiones de escritura: `SELECT pid, state, left(query, 60) FROM pg_stat_activity WHERE datname = current_database() AND pid <> pg_backend_pid();` solo debe mostrar conexiones inactivas ajenas a la aplicación. Mientras dure el procedimiento, `Database__ApplyMigrationsOnStartup=false`.
2. **Copia de seguridad nueva de seguridad.** Aunque el estado actual esté dañado, antes de cambiar nada: `pg_dump -Fc --no-owner` con un nombre propio (por ejemplo `seguridad-<fecha-hora>.dump`) usando las variables del servicio (sin escribir credenciales en el historial). Comprobarla: `pg_restore --list seguridad.dump` y restauración en un PostgreSQL local de la misma versión mayor (V-4) con el recuento de `companies`, `users`, `time_entries` y `assistant_settings`. Anotar su suma de comprobación. Esta copia es la vuelta atrás de la propia restauración.
3. **Elegir la copia a restaurar.** La última copia buena y verificada (restaurada antes en local, con sus recuentos anotados). Confirmar su esquema en esa restauración local: `SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 3;`.
4. **Restaurar con `pg_restore`.** Método recomendado: **a una base nueva**. Crear un servicio PostgreSQL nuevo en Railway de la misma versión mayor (V-4), restaurar en él y, tras las comprobaciones del paso 6, apuntar a él la cadena de conexión de `api` (la variable `ConnectionStrings__Default`). La base antigua queda intacta como respaldo: el cambio es reversible apuntando de nuevo a ella. Ejemplo (variables del servicio, sin credenciales en el historial): `pg_restore --no-owner --no-privileges --exit-on-error --single-transaction -h <host> -p <puerto> -U <usuario> -d <base-nueva> copia-buena.dump`. Método alternativo, **en el sitio**, solo si no se puede crear una base nueva: `pg_restore --clean --if-exists --no-owner --no-privileges --exit-on-error --single-transaction -d <base>` (todo o nada: si falla, no cambia nada). Riesgos: borra y recrea los objetos de la base actual, de modo que el único respaldo es la copia del paso 2; falla si la versión mayor del servidor es anterior a la de `pg_dump`; sin superusuario pueden dar error las extensiones y los propietarios (de ahí `--no-owner` y `--no-privileges`); la restauración por el proxy TCP público es lenta y consume tráfico; el disparador de solo inserción de la auditoría del asistente se elimina y se recrea con la tabla (comprobar después, dentro de una transacción con `ROLLBACK`, que un `UPDATE` sobre `assistant_settings_audit` sigue rechazándose); `__EFMigrationsHistory` se restaura también y fija el estado del esquema, que debe coincidir con el código del paso 5. Con una restauración fallida, volver a la base anterior (cadena de conexión) o a la copia del paso 2.
5. **Qué versión de código desplegar según el esquema restaurado.** Nunca el botón de rollback de un clic ni una versión más antigua de lo que el esquema espera. Con `Database__ApplyMigrationsOnStartup=false`:
   - **Antes de A** (no existe la tabla `companies`): la versión anterior a la PR 1. Solo es posible si la copia es anterior a cualquier compañía registrada.
   - **Esquema A** (última migración `AddCompanies`): el código de la PR 1, el único compatible con A sola.
   - **Esquema B** (última migración `EnforceCompanyIsolation` o posterior): la versión actual desplegada y observada (PR 1b o posterior, hasta la PR 5.1). Si el esquema restaurado es anterior al que espera el código, aplicar las migraciones que falten **por la vía de V-3**, una a una con su script, antes de arrancar; el arranque no las aplica.
6. **Comprobaciones antes de reabrir el tráfico.** `GET <API>/health` responde `200`; inicio de sesión con una cuenta Manager conocida de una compañía y con la de otra (el esquema B distingue compañías); un fichaje completo (entrada y salida) y su lectura; que las dos compañías no ven datos de la otra; y los recuentos de `companies`, `users` y `time_entries` coinciden con los anotados de la copia. Si se restauró una base con configuración del asistente, comprobar que se lee con la clave maestra actual (`Ai__MasterKeys__N`; no se cambia en el procedimiento). Solo entonces devolver el dominio público.
7. **Reabrir el registro.** Con `Registration__Enabled=true` únicamente tras el paso 6 y, si el origen del incidente fue un código filtrado, tras sustituir la lista completa de `Registration__InviteCodes` («Rotación de códigos de invitación»).
8. **Cuándo borrar las copias.** Las copias contienen datos personales: se guardan fuera del repositorio y con acceso restringido. Conservar la copia de seguridad del paso 2 y la base antigua de Railway (si se restauró a una base nueva) durante la observación posterior (la duración la fija el responsable, nunca menos de lo necesario para detectar el fallo); después, borrarlas todas (ficheros `.dump`, restauraciones locales y el servicio de base de datos antiguo), anotando la fecha de borrado. La copia buena de origen se borra cuando deje de ser la última copia válida.

## Apertura del registro

Acción del operador, no de una PR. Solo cuando las PR 1, 1b, 2, 3, 4a y 4b estén fusionadas **y desplegadas** con sus pruebas de aislamiento en verde, V-1 y V-7 comprobadas y Q-1 decidida. **En este orden:**

1. Poner `Seed__DemoUsers=false`.
2. Comprobar que los usuarios demo no se recrean al reiniciar.
3. Si hay que retirar los usuarios demo: copia previa y script revisado con `psql`, borrando antes sus fichajes por la clave foránea; o reiniciar la base si solo contiene datos de demostración. Decisión Q-1: qué hacer con la compañía Demo si tiene usuarios reales.
4. Definir `Registration__InviteCodes` (16 a 128 caracteres cada código; solo en Railway, por `stdin`).
5. Poner `Registration__Enabled=true`.

Prueba de humo en producción: dos registros (A y B), aislamiento de equipo y fichajes entre ellos, asistente deshabilitado en la compañía nueva. El validador de arranque de usuarios demo y el de `ForwardedHeaders` impiden arrancar si algún paso falta. Si algo falla: `Registration__Enabled=false` y revisar.

## Rotación de códigos de invitación

Proceso del responsable (Q-10; quién abre el registro y rota los códigos lo decide él). No se registra qué código se aceptó (Q-12). Ante un código filtrado o cada cierto tiempo, se **sustituye la lista completa** de `Registration__InviteCodes` por códigos nuevos (por `stdin`) y se redistribuyen a quien corresponda; el redespliegue invalida los anteriores. Para cerrar sin rotar: `Registration__Enabled=false`.

## Revisiones periódicas

- **Cuentas con `must_change_password` antigua.** La contraseña temporal **no caduca**: una persona dada de alta que nunca entra conserva su contraseña temporal. Revise periódicamente las cuentas con `must_change_password = true` antigua (por ejemplo, con un `SELECT` de solo lectura por compañía, ordenado por fecha de alta) y regenere la contraseña o contacte a la persona. Solo el responsable directo puede regenerarla, y solo mientras no haya entrado.
- **Token `mcp`.** Un token con el claim `mcp` ya emitido **no se invalida** al regenerar la contraseña temporal: caduca como máximo a los 30 minutos y, mientras tanto, solo permite consultar `me` y cambiar la contraseña. No hay revocación de tokens.
- **Custodia de la clave maestra (Q-8).** Pendiente de decisión del responsable (la custodia de la clave): quién genera `Ai__MasterKeys__1`, dónde se guarda su copia de seguridad y quién puede leerla en Railway. Ahora protege las claves de todas las compañías; si se pierde, cada responsable vuelve a introducir la suya.

## Seguimiento abierto

- **Pasada visual de la Web.** La verificación manual de la PR 7 fue automatizada (Chrome headless); queda una pasada humana breve y el flujo con dos compañías en el navegador.
- RGPD (supresión y exportación de datos) queda fuera de alcance por decisión expresa (ADR 0022).
