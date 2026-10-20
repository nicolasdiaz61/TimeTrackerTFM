# Especificación: assistant-credential-protection

Especificación vigente. Origen: `asistente-config-responsable` (archivado el 2026-10-20). Protege la clave del proveedor que el Manager guarda desde la Web: cifrado en reposo, exposición solo de escritura, fallo cerrado, rotación de clave maestra y ausencia de filtraciones. Complementa `assistant-settings`.

Notas de verificación:
- Los tests usan claves y valores reconocibles inventados (por ejemplo `clave-prueba-123456`).
- El contrato OpenAPI NO DEBE contener la cadena `apikey` (sin distinguir mayúsculas); el campo informativo es `credentialConfigured`.

## ADDED Requirements

### Requirement: Cifrado en reposo

La clave del proveedor DEBE almacenarse cifrada con AES-256-GCM (cifrado autenticado) usando una clave maestra versionada leída del entorno: cada versión se define en `Ai__MasterKeys__{v}` (32 bytes codificados en Base64; `v` es un entero de 1 a 32767) y la versión con la que se cifra se indica en `Ai__ActiveMasterKeyVersion`. Cada cifrado DEBE usar un nonce único y guardar el identificador de versión de la clave maestra junto al texto cifrado. La clave en claro NO DEBE persistirse.

#### Scenario: Ida y vuelta
- DADO una clave maestra válida
- CUANDO se guarda y luego se resuelve la configuración efectiva
- ENTONCES la clave descifrada es igual a la original y el valor persistido no la contiene en claro

#### Scenario: Nonce único
- DADO la misma clave cifrada dos veces
- CUANDO se comparan los resultados
- ENTONCES los textos cifrados y nonces difieren

#### Scenario: Manipulación detectada
- DADO un texto cifrado alterado en un byte
- CUANDO se intenta descifrar
- ENTONCES falla la autenticación, no se devuelve ninguna clave y se conserva la última configuración válida

### Requirement: Fallo cerrado sin clave maestra

Sin clave maestra válida, guardar una clave NUEVA DEBE rechazarse (error en español, sin guardar nada) y una clave cifrada guardada NO DEBE descifrarse ni usarse. El asistente PUEDE seguir funcionando con los valores del entorno. Los mensajes NO DEBEN imprimir la clave maestra. El arranque NO DEBE fallar por una clave maestra ausente o inválida; el rechazo de guardar una clave DEBE responder `400` con el campo `credential`, y `GET /api/asistente/configuracion` DEBE indicar `credentialStorageAvailable` = `false`.

#### Scenario: Guardar sin clave maestra
- DADO que la clave maestra no está definida
- CUANDO el Manager guarda una clave nueva
- ENTONCES responde `400` con errores por campo que incluyen `credential`, no se escribe ninguna fila y el mensaje no contiene valores secretos

#### Scenario: El arranque no falla
- DADO el entorno sin `Ai__MasterKeys__*` ni `Ai__ActiveMasterKeyVersion`
- CUANDO la API arranca
- ENTONCES arranca, el asistente sigue funcionando con los valores del entorno y `GET` devuelve `credentialStorageAvailable` = `false`

#### Scenario: Guardar sin clave nueva con almacenamiento no disponible
- DADO que la clave maestra no está disponible y un `PUT` sin `credential`
- CUANDO se procesa
- ENTONCES se acepta (no hace falta cifrar nada)

#### Scenario: Descifrar sin clave maestra
- DADO una fila con clave cifrada y sin clave maestra
- CUANDO se resuelve la configuración efectiva
- ENTONCES no se usa esa clave y se mantiene la última configuración válida o la del entorno

#### Scenario: Clave maestra de longitud inválida
- DADO una clave maestra que no equivale a 32 bytes
- CUANDO la API valida la configuración
- ENTONCES la función de cifrado se considera no disponible

#### Scenario: Base64 inválido
- DADO `Ai__MasterKeys__1` con un texto que no es Base64 válido
- CUANDO se evalúa la protección de claves
- ENTONCES esa versión se considera no disponible, el arranque no falla y ningún mensaje contiene el valor

#### Scenario: Versión activa inexistente
- DADO `Ai__ActiveMasterKeyVersion` = `2` y solo `Ai__MasterKeys__1` definida (o una versión fuera de 1 a 32767)
- CUANDO se evalúa la protección de claves
- ENTONCES la función de cifrado se considera no disponible y guardar una clave nueva responde `400` con el campo `credential`

### Requirement: Rotación de la clave maestra

El sistema DEBE admitir varias versiones de clave maestra (`Ai__MasterKeys__{v}`, Base64 de 32 bytes, `v` entero de 1 a 32767, y `Ai__ActiveMasterKeyVersion`): cifrar con la versión activa y descifrar con la versión indicada en cada registro. Una clave guardada con una versión que ya no está disponible DEBE tratarse como no descifrable.

#### Scenario: Versión antigua
- DADO una clave cifrada con la versión `1` y una activa `2` con `1` aún disponible
- CUANDO se resuelve la configuración
- ENTONCES se descifra correctamente

#### Scenario: Versión retirada
- DADO una clave cifrada con una versión no disponible
- CUANDO se resuelve la configuración
- ENTONCES se trata como no descifrable (se conserva la última configuración válida)

### Requirement: Clave de solo escritura

La clave NUNCA DEBE devolverse por la API, ni en claro ni cifrada. Solo DEBE exponerse `credentialConfigured` (y su origen en `sources.credential`); NO DEBE devolverse ningún fragmento de la clave, ni siquiera los últimos caracteres. Las peticiones `PUT` y de prueba PUEDEN recibirla; ninguna respuesta la repite.

#### Scenario: Respuestas sin clave
- DADO una clave `clave-prueba-123456` enviada en `PUT` y en la prueba
- CUANDO se inspeccionan los cuerpos de `PUT`, `GET` y prueba
- ENTONCES ninguno la contiene

#### Scenario: Indicador
- DADO una clave guardada
- CUANDO el Manager hace `GET`
- ENTONCES `credentialConfigured` es `true`; tras `DELETE` es `false` (salvo clave del entorno)

### Requirement: Sin filtración de secretos

La clave en claro, la clave cifrada y la clave maestra NO DEBEN aparecer en registros, filas de auditoría, mensajes de error, contrato OpenAPI, prompts enviados al modelo ni en la Web (HTML, estado o almacenamiento del navegador).

#### Scenario: Registros y errores
- DADO un fallo del proveedor o del cifrado con la clave `clave-prueba-123456` configurada
- CUANDO se capturan los registros y la respuesta HTTP
- ENTONCES ninguno contiene la clave ni la clave maestra

#### Scenario: Auditoría
- DADO un cambio de clave
- CUANDO se inspecciona la fila de auditoría
- ENTONCES no contiene la clave en claro ni cifrada

#### Scenario: Contrato OpenAPI
- DADO `docs/api/openapi.json`
- CUANDO se busca `apikey` sin distinguir mayúsculas
- ENTONCES no aparece y los esquemas de configuración solo exponen `credentialConfigured`

#### Scenario: Web
- DADO la página `/asistente/configuracion` tras guardar
- CUANDO se inspecciona el DOM y el almacenamiento del navegador
- ENTONCES la clave no está presente y el campo está vacío
