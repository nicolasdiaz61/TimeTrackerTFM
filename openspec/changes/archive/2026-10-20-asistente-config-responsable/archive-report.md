# Informe de archivo: asistente-config-responsable

Fecha: 2026-10-20 · Destino: `openspec/changes/archive/2026-10-20-asistente-config-responsable/` · Archivo con verificación **PASS WITH WARNINGS** (sin CRITICAL) y sin tareas pendientes de código.

## Resumen del cambio

Un responsable (`Manager`) configura desde la Web (`/asistente/configuracion`) el proveedor del asistente (`Anthropic`, `OpenAI`, `Gemini` u `Ollama`, limitados por `Ai__AllowedProviders`), el modelo, el timeout, los tokens, el esfuerzo y su propia clave de API, sin tocar variables de entorno ni redesplegar. La clave se guarda cifrada con AES-256-GCM (clave maestra `Ai__MasterKeys__{versión}`), es de solo escritura y nunca se devuelve. La API recarga la configuración en caliente por sondeo de `xmin` (`Ai__SettingsPollSeconds`), la prueba de conexión está limitada a 3 por minuto y cada cambio queda en una auditoría de solo inserción (trigger en PostgreSQL). La configuración guardada tiene prioridad sobre la del entorno y `DELETE` restablece el entorno. Decisiones registradas en los ADR 0018 y 0019.

## Pull requests (orden real de merge)

| Orden | PR | Contenido |
|---|---|---|
| 1 | #19 | PR 1: ADR 0018 y documentación |
| 2 | #20 | PR 2: Application (modelo, reglas, resolver y casos de uso) |
| 3 | #21 | PR 2.1: correcciones de la revisión de la PR 2 |
| 4 | #22 | PR 3a: cifrado de claves y opciones de entorno |
| 5 | #23 | PR 3a.1: correcciones de la revisión de la PR 3a |
| 6 | #24 | PR 3b: persistencia, migración y auditoría |
| 7 | #25 | PR 4: catálogo de proveedores y adaptador compatible con OpenAI |
| 8 | #26 | PR 4.1: correcciones de la revisión de la PR 4 |
| 9 a 13 | #27, #28, #29, #30, #31 | PR 5 (recarga en caliente, con correcciones 4.2), 6a (prueba de conexión y límite de tasa), 6b (API y OpenAPI), 7 (Web) y 8 (endurecimiento y documentación) |
| 14 | #32 | PR 4.2 a 8 acumuladas, hacia `main` |
| 15 | #33 | Cierre de la verificación |

Nota: la columna «Orden» describe el orden de merge dentro de las ramas apiladas: #27 a #31 se fusionaron cada una contra la rama de la anterior (la #27 contra la de #26), no contra `main`. Las únicas fusiones contra `main` de este cambio a partir de #26 son #26, #32 (que trajo el contenido de #27 a #31) y #33. La fusión de #31 (`29bb3f0`) figura en el historial de `main`, pero solo porque ya estaba contenida en la rama de #32; las de #27 a #30 no aparecen como fusiones en `main`.

## Especificaciones sincronizadas

| Dominio | Acción | Detalle |
|---|---|---|
| `assistant-settings` | Creada | Copia íntegra del delta (10 requisitos) |
| `assistant-credential-protection` | Creada | Copia íntegra del delta (5 requisitos) |
| `assistant-configuration` | Actualizada | 4 MODIFIED (claves de configuración; asistente deshabilitado sin proveedor; selección de proveedor solo por configuración; validación con proveedor activo). Se conserva «La clave del proveedor nunca se expone» |
| `startup-configuration` | Actualizada | 2 MODIFIED (arranque con la configuración del asistente; documentación de claves en `.env.example`). Resto de requisitos intactos |

Sin REMOVED ni RENAMED. Cada requisito MODIFIED sustituyó al vigente con el mismo nombre; se retiraron las anotaciones `(Previously: ...)` (el histórico queda en el delta archivado) y se añadió una línea de «Actualizada por» en la cabecera de las dos especificaciones existentes. Escenarios vigentes que cambian de significado (avisados en los deltas): «Proveedor desconocido» usa ahora `GitHubModels` en lugar de `Gemini` (`Gemini` pasa a ser válido), y «Claves de IA documentadas» se limita a las nueve claves originales, con un escenario nuevo para las cinco claves nuevas.

## Veredicto de verify

**PASS WITH WARNINGS**: CRITICAL 0, WARNING 6, SUGGESTION 7. 21 requisitos y 127 escenarios (125 CUMPLE, 2 PARCIAL con desviación declarada o evidencia manual, 0 NO CUMPLE). Puerta en verde: build con `-warnaserror`, `dotnet format`, 1.769 pruebas sin fallos, cobertura Application 99,8 % y Domain 100 %, sin paquetes vulnerables. Detalle en `verify-report.md`. W-1, W-2, W-3 y S-1, S-2, S-3, S-7 se cerraron en la PR #33.

## Desviaciones aceptadas

- W-4: Ollama responde 400 y se clasifica `unreachable` (la spec pide `rejected` para 400/422). Declarado en el diseño (§14), el ADR 0019 y V-3.
- W-5: los DTO numéricos usan `int?`, así que un valor no entero recibe el 400 general sin mapa de errores por campo. Aceptado hasta publicar la API a terceros.
- W-6: `DELETE` sin token de concurrencia del cliente. Cumple la spec (409 si cambió entre la lectura y el borrado en el servidor), pero un Manager con vista obsoleta puede restablecer lo guardado por otro; avisado en la Web.

## Sugerencias abiertas

- S-4: falta una prueba de integración con PostgreSQL del truncado de `updatedAt` con resto de ticks.
- S-5: `AssistantConnectionTestRequest` repite el `PrintMembers` de `AssistantConfigurationRequest`; extraer un ayudante común.
- S-6: `AssistantSettingsStore.CommitSaveAsync` recibe un booleano; preferir dos métodos o un enum.
- Revisión de #33 (1): la prueba del suscriptor de `PollCompleted` que lanza no comprueba el tipo registrado ni el `EventId`.
- Revisión de #33 (2): una fila de la matriz de `verify-report.md` conserva el texto previo a W-2 (el comentario engañoso de `.env.example`), ya corregido; el archivo se conserva como registro histórico.

## Riesgos aceptados

- Configuración única de la instancia: cualquier Manager puede cambiar la configuración que afecta a todos los usuarios del asistente.
- El trigger de auditoría no frena a quien tenga privilegios de propietario de la base de datos (documentado en el ADR 0019 y en la migración).

## Pendientes fuera del código

- Custodia de la clave maestra `Ai__MasterKeys__{versión}` (quién la genera y dónde se guarda la copia de seguridad; punto abierto del diseño, decisión del responsable del producto, no resuelta por este cambio).
- Variables de Railway (ver la sección Railway del README y el ADR 0019):
  - `Cors__AllowedOrigins__0` es obligatoria: sin ella la API no arranca. Debe ser la URL de la Web.
  - `Ai__MasterKeys__1` (32 bytes aleatorios en Base64), introducida solo por `--stdin`, y `Ai__ActiveMasterKeyVersion=1`. Ambas deben estar definidas **antes** de que un Manager guarde una clave de API; si no, el guardado falla cerrado.
  - Si se desea, `Ai__AllowedProviders`.
- Pérdida de la clave maestra: las claves de API guardadas quedan ilegibles y hay que reintroducirlas desde la Web.
- Rotación de la clave maestra: añadir una versión nueva (`Ai__MasterKeys__2`), activarla con `Ai__ActiveMasterKeyVersion=2` y conservar la anterior hasta que ninguna fila la use (`credential_key_version` en `assistant_settings`); ver el README y el ADR 0018 (consecuencias).
- Alta en Railway por el usuario (despliegue y orden descrito en el ADR 0019).

## Tareas

`tasks.md` archivado queda con todas las tareas marcadas tras la PR #33; no hay tareas de código pendientes.

## Contenido archivado

`exploration.md`, `proposal.md`, `design.md`, `specs/` (4 capacidades), `tasks.md`, `apply-progress.md`, `verify-report.md` y este `archive-report.md`. La carpeta ya no existe en `openspec/changes/`.

## Estado de Engram

Modo `openspec`: no se consultaron observaciones de Engram, así que no se registran identificadores.
