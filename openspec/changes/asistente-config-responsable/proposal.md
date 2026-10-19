# Propuesta: asistente de IA configurable por el Manager (asistente-config-responsable)

## Intención

Hoy el proveedor, el modelo y la clave salen solo de variables de entorno `Ai__*`; cambiarlos requiere al product owner y un redespliegue. Un Manager debe poder configurar el asistente desde la Web (proveedor, clave propia de pago, modelo en texto libre, timeout, máximo de tokens de salida, esfuerzo) al estilo de la configuración de proveedores de OpenCode; el resto de usuarios solo conversa.

## Alcance

### Dentro del alcance
- ADR 0018 (modifica AGENTS §1/§7 y el ADR 0017): configuración almacenada, datos personales a un tercero con la clave del Manager.
- Catálogo de proveedores en servidor con URL base fijas: Anthropic, OpenAI, Gemini y Ollama (endpoint solo por entorno). `Ai__AllowedProviders` limita las opciones (por defecto: solo el proveedor del entorno). GitHub Models y OpenCode Zen quedan fuera (ver exploration.md).
- Configuración efectiva = fila guardada > entorno; se muestra el origen de cada valor; deshabilitar (`None`); restablecer (borrar la fila).
- Clave cifrada en reposo (AES-256-GCM, clave maestra versionada por entorno); solo escritura (`credentialConfigured` + últimos 4 como máximo); falla cerrado sin clave maestra.
- Auditoría de solo inserción (sin secretos); concurrencia optimista con `xmin` (409).
- `GET/PUT/DELETE /api/asistente/configuracion` + `POST .../prueba` (3/min/usuario, resultados fijos en español). Política Manager; todos los Managers pueden editar.
- Recarga en caliente: cambio de instantánea en `AssistantChatClient`, recarga local al guardar, poller de versión de ~30 s, liberación diferida, última configuración válida.
- Página Web `/asistente/configuracion` con aviso de compartición de datos.

### Fuera del alcance
- URL escritas por el Manager; lista o listado de modelos permitidos; edición de cuotas, límites de tasa e iteraciones de herramientas (solo entorno); configuraciones por equipo; rol Admin; revocación de tokens; GitHub Models (servicio retirado el 2026-07-30); OpenCode Zen (sin superficie única compatible con Chat Completions); notificar a los usuarios del chat; exigir una prueba de conexión antes de guardar.

## Capacidades

### Capacidades nuevas
- `assistant-settings`: resolución de la configuración efectiva, catálogo y lista permitida, API/UI del Manager, auditoría, concurrencia, prueba de conexión, recarga en caliente. (Nombre distinto de `assistant-configuration`, capacidad del cambio `asistente-ia`, para evitar la colisión al archivar.)
- `assistant-credential-protection`: cifrado en reposo, exposición solo de escritura, fallo cerrado, rotación de clave, sin filtración de secretos (logs, auditoría, OpenAPI).

### Capacidades modificadas
- `assistant-configuration` (definida por `asistente-ia`): `Ai:Provider` admite `OpenAI` y `Gemini`; selección de proveedor por entorno o por configuración guardada por el Manager (ADR 0018); validación de `Ai:ApiKey`/`Ai:Model` con los nuevos proveedores y de las claves nuevas (`Ai:Effort`, `Ai:AllowedProviders`, `Ai:SettingsPollSeconds`); las claves maestras no se validan al arrancar.
- `startup-configuration` (definida por `asistente-ia`): el escenario de proveedor desconocido pasa de `Gemini` a `GitHubModels`; `.env.example` documenta las claves nuevas (comentadas, sin claves maestras con valor).
- Orden de archivo: archivar `asistente-ia` primero; si no fuera posible, fusionar estos deltas MODIFIED a mano en el informe de archivo y avisar.

## Enfoque

Enfoque 3 recomendado de `exploration.md`: un adaptador compatible con OpenAI (`Microsoft.Extensions.AI.OpenAI` 10.10.0, con `OpenAIClientOptions.Endpoint` para Gemini) para OpenAI y Gemini, más los adaptadores existentes de Anthropic y Ollama; `AssistantChatClient`/`AiOptions` siguen siendo compatibles a nivel de código fuente (79 usos en tests); el esfuerzo es opcional y condicionado a la capacidad del proveedor/modelo (`ChatOptions.Reasoning.Effort`; el mapeo por adaptador se confirma en diseño). URL y autenticación del catálogo verificadas contra documentación oficial (2026-10-18).

## Áreas afectadas

| Área | Impacto |
|---|---|
| `docs/adr/0018-*`, `AGENTS.md`, `.env.example`, README | Nuevo/Modificado |
| `src/TimeTracker.Application` (puertos de configuración, casos de uso) | Nuevo |
| `src/TimeTracker.Infrastructure/Assistant`, `/Persistence` (+migración) | Modificado/Nuevo |
| `src/TimeTracker.Api` (endpoints, política, límite de tasa) | Modificado |
| `src/TimeTracker.Web` (página, NavMenu) | Nuevo/Modificado |
| `docs/api/openapi.json`, tests de arquitectura y contrato | Modificado |
| `Directory.Packages.props` (`Microsoft.Extensions.AI.OpenAI` 10.10.0) | Modificado |

## Riesgos

| Riesgo | Probabilidad | Mitigación |
|---|---|---|
| Datos personales enviados a un tercero con la clave del Manager | Alta | Aviso en la UI, ADR 0018, minimización del ADR 0017 |
| Coste en la clave del Manager | Media | Cuotas por entorno sin cambios; el ADR recomienda presupuesto en el proveedor |
| Clave en reposo / filtración | Media | AES-GCM, solo escritura, tests de contrato, sin logs |
| SSRF | Baja | Sin entrada de URL; catálogo fijo |
| Rol obsoleto en el JWT tras una degradación | Media | Aceptado (expira a los 30 min), documentado |
| Un guardado erróneo rompe el asistente para toda la empresa | Media | Acción de prueba, restablecer al entorno, auditoría |
| Consistencia eventual entre instancias | Baja | Poll de ≤30 s; una instancia en Railway |
| Esfuerzo no traducido por el adaptador OpenAI 10.10.0 en Chat Completions | Media | Verificar en diseño; si no, ignorar el esfuerzo para ese proveedor |
| Valores de esfuerzo/modelo rechazados por el proveedor (400) | Media | Esfuerzo opcional; error de prueba claro en español |

## Plan de reversión

Restablecer (borrar la fila) devuelve el comportamiento del entorno al instante; sin `Ai__AllowedProviders` se restringe al proveedor del entorno. Reversión de código: revertir los PR en orden inverso; la migración solo añade tablas (la migración inversa las elimina).

## Dependencias

- `Microsoft.Extensions.AI.OpenAI` 10.10.0 (alineado con `Microsoft.Extensions.AI` 10.10.0; arrastra el SDK `OpenAI` >= 2.13.0); nueva clave maestra en Railway.

## División aproximada en PR (apilados sobre main)

| # | PR | ~Líneas |
|---|---|---|
| 1 | ADR 0018 + documentación | 150 |
| 2 | Modelo de Application, validación, puertos, casos de uso | 380 |
| 3 | Persistencia, migración, auditoría, cifrado | 400 |
| 4 | Catálogo + adaptador compatible con OpenAI + factoría | 350 |
| 5 | Recarga en caliente de la instantánea + poller | 380 |
| 6 | Endpoints de API, política, límite de tasa, OpenAPI | 400 |
| 7 | Página Web + NavMenu | 350 |
| 8 | Endurecimiento de integración + documentación | 200 |

## Criterios de éxito

- [ ] El Manager guarda la configuración; el siguiente chat la usa sin reiniciar.
- [ ] La clave nunca aparece en respuestas, logs, filas de auditoría ni en `openapi.json`.
- [ ] Sin clave maestra, guardar una clave se rechaza.
- [ ] Restablecer vuelve a los valores del entorno; un proveedor no permitido se rechaza.
- [ ] Un no Manager recibe 403; un `xmin` obsoleto recibe 409.
- [ ] Todas las guardas (`DependencyRuleTests`, `Src_DoesNotHardcodeModelNames`, `OpenApiContractTests`) en verde.

## Ronda de preguntas de la propuesta

Modo automático; el usuario delegó las decisiones. Valores por defecto aceptados: (1) no es obligatorio probar la conexión antes de guardar; (2) todos los Managers pueden ver y editar; (3) OpenCode Zen se descarta por compatibilidad solo parcial (confirmado por la verificación); (4) no se notifica a los usuarios del chat al cambiar la configuración.
