# Runbook de Railway: despliegue manual, copia de seguridad, reversión y apertura del registro

Procedimiento operativo del cambio `multi-compania` (una instancia, varias compañías). Lo ejecuta y aprueba **el responsable**; quien implementa el código no accede a producción. Todos los valores de este documento son marcadores: ninguna contraseña, clave, cadena de conexión ni código de invitación real se escribe aquí ni en el repositorio.

Decisiones de fondo: [ADR 0020](adr/0020-aislamiento-multiempresa.md) (aislamiento y rollback), [ADR 0021](adr/0021-configuracion-del-asistente-por-compania.md) (configuración por compañía), [ADR 0022](adr/0022-autorregistro-con-codigo-de-invitacion-y-contrasenas.md) (registro y contraseñas) y [ADR 0013](adr/0013-despliegue-en-railway.md) (despliegue).

## Estado de las verificaciones de producción

Lo que **no** está comprobado no se da por hecho. Las verificaciones de producción siguen **pendientes del responsable** y condicionan los despliegues, no el código.

## Verificaciones de producción

- **V-1 (contenido de producción).** Pendiente del responsable. `SELECT count(*) FROM users`, usuarios cuyo correo no sea de demostración, `time_entries` huérfanos (`LEFT JOIN users`), filas de `assistant_settings` y recuento de `assistant_settings_audit`. Puerta previa del despliegue de la migración B y de la apertura del registro. Decide Q-1 (qué hacer con los datos existentes y con la compañía Demo si tiene usuarios reales).
- **V-3 (vía real de scripts y `Down` en Railway).** Pendiente del responsable. Comprobar cómo ejecutar scripts contra la base de datos de Railway (`railway connect`, proxy TCP público, `railway run` u otra) y la versión de `psql` disponible. Mientras no se anote aquí la vía real, **no se despliega la PR 1** y ninguna migración B ni `Down` se aplica en producción. Solo está hecha la parte local (ver «Vía de los scripts»).
- **V-4 (versión de PostgreSQL de Railway).** Pendiente del responsable (la parte local está hecha con PostgreSQL 18). Debe ser mayor o igual que 11 para que `ADD COLUMN ... DEFAULT` no reescriba la tabla de auditoría; comprobar también que `pg_dump` local es compatible con esa versión mayor.
- **V-6 (instancias solapadas y valor de `Database__ApplyMigrationsOnStartup`).** Pendiente del responsable. Comprobar si Railway solapa la instancia nueva y la antigua durante un despliegue y **leer el valor real** de `Database__ApplyMigrationsOnStartup` en producción. No se asume el valor anterior: se relee y se anota con fecha **antes de cada despliegue manual**. Hecho verificado aparte: los servicios no tienen repositorio conectado (`source.repo = null`).
- **V-7 (`ForwardedHeaders` en producción).** Pendiente del responsable. Comprobar `ForwardedHeaders__Enabled=true` y `ForwardedHeaders__Hops` de 1 o más (el valor por defecto en código es 2: «cliente, borde»). Puerta previa de la apertura del registro; el validador de arranque impide abrirlo en `Production` sin ellos.

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

## Ensayo con datos copiados

Antes de desplegar, en un PostgreSQL local de la misma versión mayor y con una copia de producción restaurada (si el responsable la facilita; si no, con una base sembrada con auditoría y un fichaje huérfano), en dos partes:

1. **Parte 1 (antes de la PR 1).** `Up A`, `Down A` y arranque de la API sobre `Up A`: los datos previos quedan intactos tras `Down A` y los triggers de la auditoría siguen en pie. Hecha en local con datos sintéticos; repetirla con la copia real es decisión del responsable.
2. **Parte 2 (antes de la PR 1b).** `Up A`, `Up B` (aborta con el recuento si hay fichajes huérfanos; se corrigen y se repite), `Down B` con una sola compañía (funciona), `Down B` con dos compañías (**aborta** sin cambiar nada), `Down A` y arranque de la API sobre `Up B`. Hecha en local con datos sintéticos.

Los resultados están en `openspec/changes/multi-compania/design.md` («Resultados de V-n en apply», V-11).

## Procedimiento de rollback de producción

**Un rollback de código no revierte el esquema.** Redesplegar la versión anterior sobre un esquema posterior puede fallar o, peor, servir datos sin separación entre compañías. Orden obligatorio, mientras **no** se haya abierto el registro:

1. Apagar el registro (`Registration__Enabled=false`) y dejar de servir escrituras.
2. Copia de seguridad nueva (`pg_dump -Fc`).
3. Aplicar `Down` B → `Down` A por la vía de V-3, con `psql` y `-v ON_ERROR_STOP=1 --single-transaction`, un script por migración. **`Down B` aborta si hay más de una compañía.**
4. Solo entonces redesplegar el código anterior con `Database__ApplyMigrationsOnStartup=false`.
5. Comprobar `/health`, el inicio de sesión y un fichaje.

**Advertencia del rollback de un clic.** El botón de Railway que vuelve a un despliegue anterior cambia solo el código y no el esquema: con el esquema B aplicado supone la pérdida de la separación entre compañías (el código antiguo no conoce `company_id`). No se usa sin ejecutar antes `Down` B → `Down` A.

**Tras abrir el registro no hay `Down`.** Con compañías registradas, `Down B` aborta y el puente del asistente nunca sirve a compañías registradas con la clave de plataforma. Se corrige hacia delante (corrección hacia delante, o fix-forward): cerrar el registro (`Registration__Enabled=false`), corregir y desplegar con `railway up`; o restaurar la copia aceptando la pérdida de los datos posteriores a ella. La restauración de una copia sustituye todos los datos: requiere aprobación explícita.

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

- **Límite por IP del registro y IPv6.** El limitador reparte por dirección completa: quien controle un bloque `/64` puede cambiar de dirección y esquivar el límite de 5 intentos por hora. Pendiente de una PR de seguimiento aparte (agrupar por prefijo `/64`), junto con la revisión de `ForwardedHeaders` (saltos de confianza, V-7). Mientras tanto, el registro se mantiene cerrado salvo apertura controlada.
- **Pasada visual de la Web.** La verificación manual de la PR 7 fue automatizada (Chrome headless); queda una pasada humana breve y el flujo con dos compañías en el navegador.
- RGPD (supresión y exportación de datos) queda fuera de alcance por decisión expresa (ADR 0022).
