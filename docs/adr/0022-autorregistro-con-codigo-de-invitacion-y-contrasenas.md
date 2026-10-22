# ADR 0022: Autorregistro con código de invitación y contraseñas

- Estado: Aceptado
- Fecha: 2026-10-21

## Contexto

Con varias compañías en una misma instancia ([ADR 0020](0020-aislamiento-multiempresa.md)), una persona responsable tiene que poder crear su propia compañía sin que el operador intervenga, y esa puerta de entrada es **anónima y pública**: es la superficie de ataque más expuesta del sistema. Hay que acotar el abuso (altas masivas, fuerza bruta del código, coste del asistente de IA, enumeración de cuentas) sin añadir piezas que el proyecto no tiene, como un proveedor de correo.

Restricciones que condicionan la solución:

- **No hay proveedor de correo.** No se puede verificar un correo ni enviar una contraseña o un enlace de recuperación.
- **Despliegue manual en Railway.** La apertura del registro es una acción del operador que se hace una vez que el aislamiento está probado y desplegado; hasta entonces el endpoint puede estar fusionado pero no activo.
- **La IP del cliente solo es fiable tras un proxy configurado.** [ADR 0013](0013-despliegue-en-railway.md): `ForwardedHeaders` es opcional y está apagado por defecto.
- **La siembra de demostración crea usuarios con una contraseña pública** (README), que no pueden coexistir con un registro abierto.

## Decisión

- **Registro cerrado por defecto.** `POST /api/auth/register-company` solo existe con `Registration__Enabled=true`. Apagado (valor por defecto), la ruta no se registra: se comporta como una ruta inexistente (`401` sin token por la política de respaldo, `404` con un token vigente), sin evaluar ningún código ni consultar la base de datos. Cambiar el valor exige reiniciar, que en Railway ya ocurre al cambiar la variable.
- **Código de invitación secreto, reutilizable y rotable.** Los códigos se definen en `Registration__InviteCodes`, una lista separada por comas (de 16 a 128 caracteres cada uno), únicamente en el entorno del despliegue (Railway), nunca en el repositorio. No se consumen: son válidos mientras estén en la lista. Rotarlos consiste en **sustituir la lista completa** y redistribuir los nuevos, porque **no se registra cuál se aceptó** ni su posición (Q-12). El código se comprueba **antes que cualquier otra regla** y en **tiempo constante**: se resume con SHA-256 y se compara con `FixedTimeEquals` recorriendo siempre todos los códigos, sin cortocircuito por longitud ni por posición. Ausente, en blanco, incorrecto o de más de 128 caracteres da el mismo `403`, sin indicar la causa. El código no aparece en respuestas, registros, contrato OpenAPI ni mensajes de error.
- **Límite por IP de 5 por hora.** Política `register-company`, anónima y particionada por IP, ventana fija, constante de código como el login. Cuentan **todos** los intentos, también los rechazados por el código; el sexto recibe `429` sin evaluar el código. Depende de `ForwardedHeaders`: sin ellos todas las peticiones comparten la IP del borde (una partición global) o el cliente podría falsear su IP con `X-Forwarded-For`. Por eso, con el registro abierto en `Production`, **la API no arranca** si `ForwardedHeaders__Enabled` no es `true` o si `Hops` es menor que 1; con `Hops = 1` solo cuenta la IP añadida por el proxy y una IP antepuesta por el cliente cae en la misma partición.
- **Exclusión con la siembra de demostración.** Con `Seed__DemoUsers=true` y el registro abierto la API no arranca (antes de cualquier migración o siembra, con un mensaje que nombra ambas claves). Apagar la siembra no retira los **usuarios de demostración** ya existentes: en `Production`, con el registro abierto, el arranque también falla mientras exista algún usuario `@example.test` en la compañía por defecto, comprobado tras las migraciones y antes de atender peticiones, sin imprimir correos. Orden de apertura (runbook): siembra apagada, comprobar que no se recrean, retirar los usuarios de demostración, definir los códigos y, al final, abrir el registro.
- **Creación atómica en el ámbito de la compañía nueva.** Compañía (`Active`, sin asistente de plataforma) y primer Manager se crean con un único guardado dentro de `CreateScope(nuevoId)`: la petición anónima no tiene compañía y la guarda de escritura (ADR 0020) acepta las filas sin ninguna ruta exenta. El endpoint **ignora cualquier token** y lleva el marcador `SkipCompanyContext` (solo él), de modo que una petición con un token vigente no hereda la compañía del token. La unicidad la deciden las restricciones (`ux_companies_name_normalized`, `ux_users_email`), sin consulta previa y sin carrera. La compañía nueva no tiene fila de configuración del asistente: está **deshabilitado** hasta que su Manager lo configure.
- **Política de contraseñas: de 12 a 128 caracteres, sin reglas de composición** y sin recortar ni normalizar los espacios. Rige para toda contraseña nueva (registro, cambio de contraseña y contraseña temporal); el inicio de sesión no la aplica, para no excluir a quien ya tiene una contraseña corta del sistema anterior.
- **Contraseña temporal de un solo uso con cambio forzado** (PR 6). El Manager que da de alta a una persona recibe una contraseña temporal generada por el servidor, que se muestra una vez y se guarda solo como hash. La persona debe cambiarla en el primer inicio de sesión (cambio forzado real, por el claim `mcp`). Se puede **regenerar** antes del primer acceso y **solo por el responsable directo** de la persona (mínimo privilegio).
- **Correo único global.** Una persona pertenece a una sola compañía; un correo repetido da `409` con un `detail` genérico que no nombra compañía ni rol.
- **Registro de solo metadatos.** Resultado (creada, rechazada por código, por validación, por duplicado o por límite), instante e identificador de la compañía creada. Nunca contraseñas, códigos, correos ni nombres.

## Consecuencias

- **Enumeración por `409` aceptada.** El `409` por correo o nombre repetido revela que existe. Solo lo observa quien conoce un código válido (el código se comprueba antes), está limitado por IP y por la bandera; se acepta como riesgo residual.
- **Sin verificación de correo.** Cualquiera con un código puede registrar cualquier correo, también uno ajeno; no hay recuperación de contraseña. Un código filtrado permite altas hasta que se rote la lista completa.
- **El límite por IP es tan bueno como `ForwardedHeaders`.** Detrás de un proxy mal configurado sería global (se agota para todos) o falseable; el validador de arranque lo impide en `Production`. Pendiente del responsable: confirmar `ForwardedHeaders__Enabled=true` y `Hops >= 1` en Railway antes de abrir el registro (V-7).
- **Los contadores del limitador viven en memoria de cada instancia** (ADR 0017): se reinician al desplegar y no se comparten entre réplicas.
- **Límite de cuerpo de 4 KiB** (`413`): decisión de diseño explícita, no un requisito de la especificación; protege el endpoint anónimo y puede ajustarse sin cambiar la especificación.
- **Tras abrir el registro no hay `Down`** de las migraciones (ADR 0020): se corrige hacia delante.
- **RGPD pospuesto.** La supresión y la exportación de datos de las personas registradas quedan fuera de este cambio y pendientes de una decisión posterior.
- **Panel de administración de plataforma, baja o suspensión de compañías desde la interfaz y códigos de un solo uso gestionados en base de datos** quedan fuera de alcance.

## Alternativas

- **Registro abierto sin código:** abuso, coste del asistente y enumeración sin ninguna barrera. Descartado.
- **Código de un solo uso guardado en base de datos:** más piezas (tabla, emisión, caducidad) para un volumen pequeño; el código reutilizable y rotable por el entorno basta y se revoca sustituyendo la lista. Descartado.
- **Verificación de correo:** requiere un proveedor de correo que el proyecto no tiene. Descartada y documentada como riesgo.
- **Comparar con `==` o `string.Equals`:** filtra por tiempo la longitud y el prefijo del código. Descartado: resumen SHA-256 y `FixedTimeEquals` sobre todos los códigos.
- **Registrar el índice del código aceptado para saber cuál rotar:** delata una posición y cuál de los códigos se usa. Descartado (Q-12): se rota la lista completa.
- **Reglas de composición de contraseña (mayúsculas, dígitos, símbolos):** no aumentan la entropía de forma fiable y empujan a patrones previsibles. Descartadas: longitud de 12 a 128 sin reglas.
- **Fijar la compañía del registro desde Application o desde el middleware:** mezclaría la compañía del token con la nueva. Descartado: el almacén abre su propio ámbito y el endpoint ignora el token.
