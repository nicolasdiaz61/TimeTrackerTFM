# ADR 0018: Configuración del asistente por el responsable

- Estado: Aceptado
- Fecha: 2026-10-19

## Contexto

Hasta ahora el proveedor, el modelo y la clave del asistente (ADR 0017) se fijan solo con variables de entorno `Ai__*`. Cambiarlos obliga a redesplegar y a que el product owner custodie la clave. El responsable (rol `Manager`) debe poder elegir el proveedor y el modelo, aportar su propia clave de API, probar la conexión y deshabilitar el asistente sin tocar el despliegue.

Esto choca con dos reglas escritas del repositorio y matiza una decisión previa:

- AGENTS §1 y §7 dicen que el proveedor y el modelo se eligen «por configuración» y que el cambio de proveedor es «solo mediante configuración `Ai:*`».
- El ADR 0017 asume que quien habilita un proveedor en la nube es el product owner y que la clave vive solo en el entorno.

Restricciones que condicionan la solución:

- **Secretos (AGENTS §6):** una clave de API guardada en la base de datos no puede quedar en claro ni volver a salir por la API, los registros o el contrato OpenAPI.
- **Datos personales (RGPD):** con un proveedor en la nube, los nombres y las horas de los fichajes salen del sistema; ahora bajo una clave que aporta el responsable.
- **SSRF y exfiltración:** si el responsable pudiera escribir una URL, podría dirigir la clave y los datos a un servidor propio.
- **Despliegue en Railway:** sistema de archivos efímero y una sola instancia hoy, pero con base de datos compartida.

## Decisión

Este ADR **modifica AGENTS §1 y §7** (el proveedor y el modelo pasan a elegirse por «configuración de entorno o guardada por un Manager»; el cambio de proveedor es por `Ai:*` o por la configuración guardada, ADR 0018) y **matiza el ADR 0017**: el proveedor en la nube puede recibir datos personales con una clave aportada por el responsable, no solo por el product owner. El ADR 0017 no se edita; el cupo diario, los límites por minuto, de tokens, de iteraciones y de tiempo siguen siendo de entorno.

- **Configuración guardada en PostgreSQL.** Una única fila para toda la empresa, con proveedor, modelo, tiempo máximo, tokens de salida y esfuerzo de razonamiento. Se fusiona sobre el entorno valor a valor: la fila gana; `None` guardado deshabilita el asistente; borrar la fila restablece el entorno. Sin fila o sin tabla, el comportamiento es exactamente el actual.
- **Catálogo cerrado y URL solo en el servidor.** Proveedores: `Anthropic`, `OpenAI`, `Gemini` y `Ollama`. Las URL de OpenAI y Gemini son constantes del servidor; la de Ollama sale solo de `Ai__Endpoint`. Ninguna entidad guardada contiene una URL y el contrato rechaza propiedades desconocidas.
- **`Ai__AllowedProviders`.** Lista del product owner con los proveedores que el responsable puede elegir. Sin la lista, solo el proveedor del entorno; el entorno por defecto es seguro. Un nombre desconocido impide arrancar con un mensaje que nombra la clave sin imprimir su valor.
- **Cifrado de la clave: AES-256-GCM con clave maestra versionada.** La clave maestra (32 bytes en Base64) vive en el entorno (`Ai__MasterKeys__{versión}` y `Ai__ActiveMasterKeyVersion`), nunca en la base de datos. Cada cifrado usa un nonce aleatorio y datos autenticados asociados con el proveedor y la versión, de modo que el texto cifrado no puede redirigirse a otro proveedor. La rotación añade una versión nueva y la activa; las anteriores siguen descifrando mientras existan. Fallo cerrado: sin clave maestra válida no se puede guardar una clave nueva, pero el arranque no falla.
- **Clave de solo escritura.** Se envía al guardar y nunca se devuelve: las respuestas solo informan de si hay clave configurada. Cambiar de proveedor descarta la clave guardada anterior.
- **Auditoría de solo inserción, en dos barreras.** Cada cambio (guardar, restablecer) registra quién, cuándo y los valores anterior y nuevo sin secretos, con un indicador de si cambió la clave. Primera barrera: el puerto solo expone inserción y una prueba de arquitectura busca cualquier modificación. Segunda barrera, en la base de datos: un trigger (migración `AddAssistantAuditImmutability`) rechaza `UPDATE`, `DELETE` y `TRUNCATE` sobre `assistant_settings_audit`, también para el propietario de la tabla, que un `REVOKE` no frena; solo afecta a esa tabla y su `Down` lo elimina.
- **Concurrencia optimista** con `xmin`, como en los fichajes: un token obsoleto responde `409`.
- **Recarga en caliente.** El asistente trabaja sobre una instantánea inmutable que se sustituye de forma atómica; cada instancia sondea la base de datos (por defecto cada 30 s) y conserva la última configuración válida si la base de datos cae o la fila no es válida.
- **Prueba de conexión** con un candidato sin guardar, con límite de tasa por usuario y sin devolver texto del proveedor ni la clave.
- **Acceso exclusivo del `Manager`**; el contrato OpenAPI no contiene la clave ni variables de entorno.
- **Fuera de alcance:** GitHub Models (servicio retirado) y OpenCode Zen (varias APIs por modelo).

## Consecuencias

- **Datos personales a un tercero bajo una clave del responsable.** La interfaz avisa de ello y de que el cambio afecta a toda la empresa; se mantiene la minimización del ADR 0017. Quien habilite un proveedor en la nube debe revisar el contrato de tratamiento con el proveedor.
- **Coste en la clave del responsable.** El techo real sigue siendo el presupuesto fijado en la consola del proveedor.
- **Gestión de la clave maestra.** Hay que definir `Ai__MasterKeys__1` y `Ai__ActiveMasterKeyVersion` antes de que un responsable guarde una clave; perderla deja ilegibles las claves guardadas (se vuelven a introducir). Quién la genera y custodia en Railway queda como decisión de despliegue pendiente del product owner.
- **Rol obsoleto en el JWT.** El token dura 30 minutos; un responsable degradado conserva el acceso a la configuración hasta que caduque. Se acepta.
- **Consistencia eventual entre instancias:** un cambio se aplica en la instancia local al instante y en las demás tras el siguiente sondeo.
- **Un guardado erróneo afecta a toda la empresa.** Se mitiga con la prueba de conexión, el restablecimiento al entorno y la auditoría.
- **Despliegue en Railway.** Antes de que un responsable guarde una clave hay que definir `Ai__MasterKeys__1` (secreto, por `stdin`) y `Ai__ActiveMasterKeyVersion=1` en el servicio `api`; `Cors__AllowedOrigins__0` es obligatoria (sin ella la API no arranca) y `Ai__AllowedProviders` abre más proveedores al responsable. Los pasos están en el README, sin ningún valor.
- **Configuración única y acceso amplio.** Hay una sola configuración para toda la instancia y cualquier Manager de cualquier equipo puede leerla, cambiarla, restablecerla y probar conexiones (riesgo aceptado; si hubiera varias empresas o equipos aislados habría que partirla por ámbito). El restablecimiento no usa un token del cliente: con una vista obsoleta puede borrar un cambio ajeno, y la Web lo advierte.
- **Riesgo de la API Responses de OpenAI.** La versión 1 usa solo la API de chat; algunos modelos de OpenAI exigen Responses para usar herramientas, y la prueba de conexión (sin herramientas) puede dar `ok` aunque el chat falle con ese modelo.
- **Un 400 de Ollama queda como `unreachable`** en la prueba de conexión: su SDK no expone el estado HTTP en ese caso y no se interpreta el texto del mensaje.
- **Pregunta abierta para el product owner: custodia de la clave maestra.** Quién la genera, dónde se guarda su copia de seguridad y quién puede leerla en Railway no está decidido. Perderla deja ilegibles las claves guardadas, no el asistente del entorno.
- Las pruebas nunca llaman a un proveedor real: usan dobles de `IChatClient` y manejadores HTTP de grabación.

## Alternativas

- **Solo por entorno (estado actual):** obliga a redesplegar y a que el product owner custodie la clave del responsable. Descartada.
- **URL escrita por el responsable:** permite exfiltrar la clave y los datos a un servidor ajeno (SSRF). Descartada.
- **Solo la prueba de arquitectura para la inmutabilidad de la auditoría:** no frena a quien tenga acceso directo a la base de datos; se completó con el trigger. Descartada como única barrera.
- **ASP.NET Data Protection para la clave:** exige persistir el anillo de claves en una plataforma con disco efímero, junto al secreto que protege; el formato es opaco y la rotación no es auditable. Descartada.
- **Responses API de OpenAI:** superficie mayor y menos compatible con el resto de proveedores; se usa la API de chat. Descartada.
- **GitHub Models y OpenCode Zen:** el primero está retirado y el segundo expone varias APIs según el modelo. Descartados.
