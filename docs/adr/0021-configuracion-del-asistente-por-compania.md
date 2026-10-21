# ADR 0021: Configuración del asistente por compañía

- Estado: Aceptado
- Fecha: 2026-10-21
- Sustituye parcialmente a: ADR 0018 y ADR 0019, solo en «fila única / toda la instancia / cualquier Manager de cualquier equipo»

## Contexto

El [ADR 0018](0018-configuracion-del-asistente-por-el-responsable.md) guarda la configuración del asistente en **una única fila para toda la empresa**, y el [ADR 0019](0019-auditoria-inmutable-y-despliegue-de-la-configuracion-del-asistente.md) acepta como riesgo que cualquier Manager de cualquier equipo la lea y la cambie. Con varias compañías en la misma instancia ([ADR 0020](0020-aislamiento-multiempresa.md)) eso ya no vale: la configuración y la clave de una compañía no pueden afectar ni ser visibles para otra, y la clave de plataforma (`Ai__ApiKey`, que paga el operador) no puede servir a compañías registradas.

**Cómo se señala la sustitución sin reescribir los ADR 0018 y 0019** (Q-9, convención del [ADR 0001](0001-formato-de-adr.md): los ADR aceptados no se editan): este ADR lleva en su encabezado «Sustituye parcialmente a» y el índice del README marca el 0018 y el 0019 como «sustituido parcialmente por el 0021». Los archivos 0018 y 0019 no se modifican (una prueba compara su contenido con el fusionado).

## Decisión

Este ADR **sustituye parcialmente al 0018 y al 0019 solo en lo relativo a la fila única, el alcance de toda la instancia y el acceso de cualquier Manager de cualquier equipo**. Todo lo demás de ambos sigue vigente: catálogo cerrado de proveedores y URL solo en el servidor, cifrado AES-256-GCM con clave maestra versionada, clave de solo escritura, auditoría de solo inserción (con su trigger), concurrencia optimista con `xmin`, prueba de conexión, recarga en caliente y la pregunta abierta de la custodia de la clave maestra.

- **Fila por compañía.** `assistant_settings` tiene como clave primaria `company_id`: a lo sumo una fila por compañía y un token de concurrencia (`xmin`) por fila. `GET`, `PUT`, `DELETE` y la prueba de conexión actúan solo sobre la fila de la compañía del token; no existe ningún identificador de compañía en el contrato. Un guardado de A nunca lee, sobrescribe ni cambia el `xmin` de la fila de B.
- **Auditoría con `company_id`.** Cada guardado y restablecimiento inserta su fila de auditoría con la compañía del token, en la misma transacción. Las filas de una compañía no son visibles desde otra. Los triggers de solo inserción no cambian.
- **Asistente deshabilitado sin fila.** Una compañía sin fila tiene el proveedor `None` (origen `default`), sin modelo ni clave, con el tiempo máximo y los tokens por defecto de plataforma (origen `env`). El chat responde `503`. «Sin fila» y «`None` guardado» son indistinguibles para el chat.
- **La clave de plataforma solo sirve a la compañía con `uses_platform_assistant`** (la demo). Los valores del entorno `Ai:*` que identifican al proveedor de plataforma (proveedor, modelo, esfuerzo y `Ai__ApiKey`) los usa solo esa compañía; las demás resuelven proveedor, modelo y clave únicamente desde su fila. La prueba de conexión de una compañía nunca usa la clave guardada por otra ni la de plataforma, salvo en la demo.
- **Formato de cifrado AAD v2, deducido sin guardar el formato.** El texto cifrado se vincula a la compañía (`tt-assistant-credential|v2|{companyId}|{provider}|{keyVersion}`): un texto cifrado de una compañía no se descifra con el identificador de otra. No se guarda un identificador de formato ni cambia el esquema: se prueba v2 y, solo para la compañía por defecto, el v1 anterior. Es una **mejora no bloqueante** (el aislamiento no depende de ella) y el primer guardado que conserve la clave la recifra en v2; leer no escribe (mismo `xmin`).
- **Instantáneas por compañía con sondeo global** (PR 4b): un registro de instantáneas del runtime construidas de forma perezosa, un único sondeo que lee `(company_id, xmin)` de todas las compañías, cuotas y topes por compañía. Hasta entonces rige el **puente transitorio**: el runtime singleton sigue cargando la fila de la compañía por defecto y **solo sirve a esa compañía**; cualquier otra recibe el `503` de asistente deshabilitado, tenga o no fila propia, y nunca la instantánea ni la clave de plataforma.
- **`Ai__AllowedProviders` como lista de plataforma.** La lista sigue siendo del operador: las compañías nuevas eligen entre ella, cada una con su propia clave. **Riesgo de `Ollama`:** incluirlo en la lista da a todas las compañías acceso al `Ai__Endpoint` del operador; es una decisión explícita del operador al incluirlo.
- **Cualquier Manager de la compañía edita la configuración de su compañía** (riesgo aceptado, ahora acotado a la compañía; el aviso de la página nombra la compañía). Antes del 0021 era cualquier Manager de la instancia.

## Consecuencias

- **Rollback.** Revertir esta PR tras un guardado en v2 deja esas filas ilegibles para el protector anterior (v1): hay que **volver a introducir la clave** de las compañías afectadas. Las filas v1 siguen siendo legibles para el código nuevo en la compañía por defecto.
- **Una clave v1 no está vinculada a la compañía hasta recifrarse**; un texto cifrado v1 copiado a otra compañía no se descifra.
- **Coste de plataforma.** Solo la compañía con `uses_platform_assistant` consume la clave de plataforma; el resto paga con su propia clave. Con la lista `Ai__AllowedProviders` y `Ollama` el operador asume que todas las compañías pueden apuntar al endpoint que él configura.
- **Custodia de la clave maestra.** La clave maestra sigue siendo global por entorno y ahora protege a todas las compañías: la pregunta abierta del ADR 0019 (quién la genera y custodia) gana importancia.
- **Mientras exista el puente**, una compañía distinta de la demo no tiene asistente aunque guarde su configuración; el runtime por compañía llega en la PR 4b, que retira el puente y la guarda del motor.

## Alternativas

- **Seguir con la fila única:** una compañía vería y cambiaría la configuración de otra. Descartada.
- **Una compañía sin fila usa los valores de entorno:** costearía a todas las compañías con la clave de plataforma. Descartada: sin fila, el asistente está deshabilitado.
- **Guardar un identificador de formato del texto cifrado:** exige un cambio de esquema y migrar los registros; se prefiere deducirlo probando v2 y luego v1 solo en la compañía por defecto.
- **Editar los ADR 0018 y 0019 para marcarlos como sustituidos:** viola «los ADR no se reescriben» (ADR 0001). Descartada: la sustitución parcial se señala en este ADR y en el índice del README.
- **Una instantánea compartida con la clave de plataforma para todas las compañías:** fuga de coste y de datos entre compañías. Descartada.
