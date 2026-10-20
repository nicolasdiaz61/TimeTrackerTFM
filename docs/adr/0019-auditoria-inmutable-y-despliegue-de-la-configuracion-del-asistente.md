# ADR 0019: Auditoría inmutable y despliegue de la configuración del asistente

- Estado: Aceptado
- Fecha: 2026-10-20

## Contexto

El [ADR 0018](0018-configuracion-del-asistente-por-el-responsable.md) permite que un responsable (`Manager`) guarde la configuración del asistente y su clave de API. Al endurecer esa funcionalidad (PR 8 del cambio `asistente-config-responsable`) aparecieron decisiones que el 0018 no recoge o descarta:

- El 0018 descartó un disparador de base de datos para la auditoría y confió en una prueba de arquitectura sobre el único punto de escritura. Esa prueba no frena a quien tenga acceso directo a la base de datos.
- El despliegue en Railway exige un orden de configuración concreto que el 0018 solo menciona de pasada.
- Quedan riesgos aceptados y una pregunta abierta que conviene dejar por escrito.

Este ADR **complementa y matiza el ADR 0018 sin sustituirlo**: el 0018 sigue vigente en todo lo que no se dice aquí.

## Decisión

- **Auditoría de solo inserción también en la base de datos, con triggers.** La migración `AddAssistantAuditImmutability` rechaza `UPDATE`, `DELETE` y `TRUNCATE` sobre `assistant_settings_audit` mediante un trigger por fila y otro por sentencia. Se elige trigger y no `REVOKE` porque el rol con el que migra y opera la aplicación es el propietario de la tabla y un `REVOKE` no lo frena; el trigger sí. Solo afecta a esa tabla y su `Down` lo elimina. Esto matiza la alternativa descartada del 0018: la prueba de arquitectura sigue siendo la primera barrera y el trigger la segunda.
- **Orden de despliegue en Railway.** En el servicio `api`, antes de que un Manager guarde una clave, se define `Ai__MasterKeys__1` (secreto, solo por `--stdin`, nunca en archivos ni en el historial) y `Ai__ActiveMasterKeyVersion=1`. `Cors__AllowedOrigins__0` es obligatoria (sin ella la API no arranca). `Ai__AllowedProviders` abre más proveedores al Manager; sin ella solo puede usar el del entorno. Los pasos están en el README, sin valores.
- **Riesgos aceptados.** (1) Configuración única para toda la instancia: cualquier Manager de cualquier equipo puede leerla, cambiarla, restablecerla y probar conexiones. (2) `DELETE` no usa token del cliente: con una vista obsoleta puede borrar un cambio ajeno, y la Web lo advierte. (3) Un 400 de Ollama queda como `unreachable` en la prueba de conexión, porque su SDK no expone el estado HTTP en ese caso. (4) Riesgo de la API Responses de OpenAI: la v1 usa solo la API de chat y algunos modelos exigen Responses para usar herramientas; la prueba de conexión (sin herramientas) puede dar `ok` aunque el chat falle.
- **Pregunta abierta para el product owner: custodia de la clave maestra.** Quién la genera, dónde se guarda su copia de seguridad y quién puede leerla en Railway no está decidido.

## Consecuencias

- Un acceso directo a la base de datos ya no puede alterar ni borrar la auditoría sin retirar antes el trigger (un cambio de esquema visible y auditable por migración).
- Los datos personales de los fichajes salen hacia un tercero (el proveedor elegido) bajo la clave que aporta el Manager; la interfaz lo avisa y el contrato de tratamiento debe revisarse antes de usar datos reales.
- Perder la clave maestra deja ilegibles las claves guardadas (el Manager las vuelve a introducir); el asistente sigue funcionando con el entorno.
- Si la instancia pasara a servir a varias empresas o equipos aislados, habría que partir la configuración por ámbito y añadir una política por equipo; no está en esta versión.
- Las pruebas de integración usan una base propia por prueba, así que el trigger no complica su limpieza.

## Alternativas

- **`REVOKE UPDATE, DELETE` sobre la tabla:** no frena al propietario, que es el rol de la aplicación. Descartada.
- **Solo la prueba de arquitectura (0018):** no protege frente a acceso directo. Descartada como única barrera.
- **Token del cliente también en `DELETE`:** cambia el contrato (parámetro nuevo y caso de uso); se deja fuera y se advierte en la Web.
- **Generar la clave maestra desde la aplicación:** la dejaría en la misma base de datos que protege. Descartada.
