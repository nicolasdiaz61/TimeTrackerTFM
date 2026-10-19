# ADR 0017: Asistente de IA con herramientas de solo lectura

- Estado: Aceptado
- Fecha: 2026-10-18

## Contexto

Las personas usuarias quieren preguntar en lenguaje natural por sus fichajes («¿cuántas horas trabajé la semana pasada?») y, si son responsables, por los de su equipo. El proyecto fija desde el inicio que el acceso al modelo va detrás de `IChatClient` (Microsoft.Extensions.AI), que el proveedor y el modelo se eligen por configuración y que el modelo no toca la base de datos (AGENTS §6 y §7).

Tres restricciones condicionan la solución:

- **Regla de dependencias (ADR 0003):** Application no puede depender de ningún paquete de IA, y la autorización por objeto (un empleado solo ve lo suyo; un responsable, a sus reportes directos) ya vive en los casos de uso existentes.
- **Datos personales (RGPD):** los nombres y las horas de los fichajes son datos personales. Con un proveedor en la nube salen del sistema; con un modelo local, no.
- **Coste y abuso:** un asistente abierto a cualquier usuario autenticado es una vía de consumo ilimitado del presupuesto del proveedor y un blanco de inyección de instrucciones (OWASP LLM01, LLM06 y LLM10).

## Decisión

- **IA solo en Infrastructure (alternativa A).** Application gana el resumen por día en hora de España (`DailySummaryBuilder`, `LocalDateRange`), la validación de la pregunta (`AskAssistantUseCase`) y dos puertos, `IAssistantEngine` e `IAssistantQuota`, sin ningún paquete de IA. Infrastructure aloja las herramientas, el bucle de herramientas, el prompt, las opciones `Ai:*`, la fábrica de proveedores y la cuota diaria. Api solo expone `POST /api/asistente`.
- **Herramientas de solo lectura y seguridad por herramienta.** Tres funciones: `get_my_time_entries`, y, solo para el rol `Manager` del token, `get_team_members` y `get_team_member_time_entries`. El modelo nunca recibe la identidad como parámetro: sale del token (`ICurrentUser`). Un empleado ni siquiera tiene registradas las herramientas de equipo. Miembro ajeno, inexistente, propio o ilegible dan exactamente la misma respuesta. La autorización la siguen aplicando los casos de uso; el prompt no contiene reglas de seguridad ni secretos.
- **Sin estado y con historial no confiable.** El cliente envía el historial; solo se admiten los roles `user` y `assistant`, y nada del cliente se coloca como mensaje `system` ni como llamada a herramienta. Sin streaming ni conversaciones persistidas.
- **Proveedor solo por configuración.** `Ai:Provider` vale `Anthropic` (SDK oficial, nube) u `Ollama` (local); el modelo, el endpoint y la clave son configuración. Sin `Ai:Provider` (ausente, en blanco o `None`) el asistente queda **deshabilitado**: la API arranca y el endpoint responde `503` «El asistente no está disponible en este entorno.». Un valor desconocido, incluido el numérico, impide arrancar. La clave de API vive solo en variables de entorno o en el secreto de la plataforma.
- **Consumo acotado.** Salida máxima de tokens (800), como mucho 5 iteraciones de herramientas (ejecución secuencial), corte tras errores consecutivos de herramienta, tiempo máximo de 25 s y mensajes de error de herramienta sin detalle técnico. Si el bucle termina sin texto, la respuesta es `200` con un texto fijo en español; un fallo real del proveedor o el tiempo agotado dan un `503` genérico.
- **Limitador por minuto y tope diario.** La política `assistant` limita a 5 peticiones por minuto por usuario (claim `sub`, no IP). Para conocer al usuario, el limitador pasa **después de la autorización**: el orden queda SecurityHeaders → ExceptionHandler → StatusCodePages → CORS → Authentication → Authorization → RateLimiter → endpoints. CORS sigue antes, de modo que el `429` conserva las cabeceras del origen permitido (motivo original de la decisión DD-21 del cambio archivado de autenticación, que colocaba el limitador antes de la autenticación); la política `login`, anónima y por IP, no cambia. El tope diario (50 por usuario) lo lleva `InMemoryAssistantQuota`, por día natural de Europe/Madrid y con `Retry-After` hasta la medianoche local; se consume solo tras validar y comprobar que el asistente está habilitado. El cuerpo se limita a 32 KiB (`413`).
- **Registro solo de metadatos.** Quién (`EmployeeId`), proveedor, modelo, resultado, tokens, número de herramientas y duración; nunca el mensaje, el historial, la respuesta, los argumentos, el mensaje de la excepción ni la clave (de un fallo, solo el nombre del tipo).

## Consecuencias

- **Datos personales y proveedor en la nube.** Con `Anthropic`, los nombres (saneados) y las horas de los fichajes consultados viajan al proveedor. Se minimiza (resumen por día en lugar de intervalos, sin correos, identificadores ni contraseñas, solo lo que pide la herramienta) y la demostración usa datos inventados. Quien lo active en producción debe revisar el contrato de tratamiento con el proveedor; con `Ollama` los datos no salen de la máquina.
- **El tope diario en memoria es una limitación asumida.** El contador vive en cada instancia: se reinicia con cada despliegue o reinicio y no se comparte entre réplicas (Railway ejecuta una). No es un control de gasto: el techo real es el presupuesto fijado en la consola del proveedor, que debe configurarse al habilitar el asistente.
- Los modelos locales pequeños pueden fallar con herramientas o con el español; cambiar de proveedor o de modelo es solo configuración.
- El nuevo orden del pipeline cambia dónde se cuenta el límite por minuto frente a la decisión archivada: los anónimos al asistente reciben `401` sin consumir cuota de nadie, y los `400` y `503` de un usuario autenticado sí cuentan para su minuto.
- Una consulta que supera el tiempo o falla en el proveedor ya ha gastado una unidad del tope diario (se consume antes de invocar al modelo).
- El `413` por cuerpo grande no se declara en el contrato OpenAPI (lo rechaza el servidor antes de enlazar el cuerpo).
- Las pruebas del asistente nunca llaman a un modelo real: usan un `IChatClient` falso. La verificación con un modelo real es manual y opcional.

## Alternativas

- **B. IA en Application con abstracciones propias de IA:** acopla los casos de uso a conceptos de proveedor y rompe la regla de dependencias para lo que es un adaptador. Descartada.
- **C. Lógica del asistente en Api:** mezcla composición con comportamiento, impide probarlo sin servidor y duplica la autorización. Descartada.
- **D. Volcar el mes del usuario en el prompt:** envía más datos personales de los necesarios, gasta más tokens y no sirve para el responsable. Descartada.
- **Streaming y conversaciones persistidas:** más superficie (almacenar contenido y datos personales) sin necesidad en esta versión. Fuera de alcance.
- **Tope diario en PostgreSQL:** exige una migración y una escritura en un cambio de solo lectura; se prefirió la cuota en memoria con su limitación documentada.
- **Segunda ventana de 24 h en el limitador:** empieza en la primera petición y no a medianoche, y no se puede probar con un reloj falso.
- **Mantener el limitador antes de la autenticación y particionar por IP:** no distingue usuarios detrás de una misma IP y deja a un usuario agotar el cupo de otro.
