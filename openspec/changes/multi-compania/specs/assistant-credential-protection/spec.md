# Delta para assistant-credential-protection

Cambio: `multi-compania`. Capacidad EXISTENTE (`openspec/specs/assistant-credential-protection/spec.md`). Este delta solo contiene bloques `ADDED`: añade la vinculación del texto cifrado a la compañía y el recifrado oportunista. Los requisitos vigentes («Cifrado en reposo», «Fallo cerrado sin clave maestra», «Rotación de la clave maestra», «Clave de solo escritura», «Sin filtración de secretos») siguen vigentes sin cambios.

Naturaleza: es una MEJORA NO BLOQUEANTE de defensa en profundidad (elemento recortable). El aislamiento entre compañías no depende de ella: lo garantizan las claves foráneas compuestas, el filtro global y la fila por compañía (ver `company-isolation`). Por eso se usa «DEBERÍA» (recortable) y no «DEBE». La clave maestra es única de la plataforma y vive solo en Railway (nunca en el repositorio); no hay clave maestra por compañía.

Notas de verificación:
- Los tests usan claves y valores reconocibles inventados (por ejemplo `clave-prueba-123456`).
- «Formato v1» es el vigente (AAD sin compañía); «formato v2» incluye `company_id` en los datos asociados autenticados (AAD).

## ADDED Requirements

### Requirement: Texto cifrado vinculado a la compañía (formato v2)

El cifrado AES-256-GCM DEBERÍA usar como datos asociados autenticados (AAD) un valor que incluya el `company_id` de la fila (formato v2), de modo que un texto cifrado copiado a la fila de otra compañía NO se autentique al descifrarlo. El formato NO guarda ningún identificador de formato junto al texto cifrado (sin cambio de esquema): se deduce al descifrar. El sistema DEBERÍA probar primero v2 con el `company_id` de la fila y, solo si la autenticación v2 falla y la fila pertenece a la compañía por defecto (la única donde pueden existir registros v1, creados antes del cambio), descifrar con el AAD v1; una fila v1 en cualquier otra compañía NO DEBE descifrarse. Un fallo de autenticación DEBE conservar la última configuración válida de la compañía, sin devolver ninguna clave, y NO DEBE afectar a otras compañías.

#### Scenario: Ida y vuelta v2
- DADO una clave maestra válida y la compañía A
- CUANDO se guarda una clave y luego se resuelve la configuración efectiva de A
- ENTONCES la clave descifrada es igual a la original y el registro persistido está en formato v2

#### Scenario: Texto cifrado movido a otra compañía
- DADO el texto cifrado v2 de la compañía A copiado a la fila de la compañía B
- CUANDO se resuelve la configuración de B
- ENTONCES falla la autenticación, no se devuelve ninguna clave, B conserva su última configuración válida (o queda deshabilitada) y A no se ve afectada

#### Scenario: Registro v1 heredado
- DADO una fila en formato v1 de la compañía por defecto, creada antes del cambio
- CUANDO se resuelve la configuración efectiva de su compañía
- ENTONCES se descifra correctamente con el AAD v1

#### Scenario: Registro v1 en otra compañía
- DADO una fila en formato v1 copiada a la fila de una compañía distinta de la por defecto
- CUANDO se resuelve la configuración efectiva de esa compañía
- ENTONCES no se descifra, no se devuelve ninguna clave y se conserva su última configuración válida (o queda deshabilitada)

#### Scenario: Manipulación detectada en v2
- DADO un texto cifrado v2 alterado en un byte
- CUANDO se intenta descifrar
- ENTONCES falla la autenticación y se conserva la última configuración válida

#### Scenario: Limitación v1 documentada
- DADO el ADR 0020
- CUANDO se revisa su sección de límites
- ENTONCES indica que un registro v1 no está vinculado a la compañía hasta recifrarse

### Requirement: Recifrado oportunista de v1 a v2

Cuando se guarda la fila de configuración de una compañía (con o sin clave nueva) y su clave cifrada actual es descifrable en formato v1, el sistema DEBERÍA recifrarla en formato v2 (solo en la compañía por defecto, donde existen registros v1) con la versión de clave maestra activa dentro del mismo guardado (`ReEncryptOrKeep`: si no puede descifrarla o no hay clave maestra, conserva el valor tal cual). Una lectura NO DEBE escribir. El recifrado NO DEBE registrar ni auditar la clave y NO DEBE alterar la fila de otra compañía.

#### Scenario: Recifrado al guardar
- DADO una fila v1 descifrable y un `PUT` sin clave nueva
- CUANDO se guarda
- ENTONCES el registro persistido pasa a v2, `credentialConfigured` sigue siendo `true` y la clave sigue usable

#### Scenario: Conservar si no se puede descifrar
- DADO una fila v1 con una versión de clave maestra retirada y un `PUT` sin clave nueva
- CUANDO se guarda
- ENTONCES el valor cifrado se conserva sin cambios y no se pierde la clave

#### Scenario: Sin clave maestra
- DADO una fila v1 y sin clave maestra disponible
- CUANDO se guarda un `PUT` sin clave nueva
- ENTONCES se acepta, el valor cifrado no cambia y no hay error

#### Scenario: Leer no escribe
- DADO una fila v1 descifrable
- CUANDO se resuelve la configuración efectiva sin guardar
- ENTONCES la fila no cambia (mismo `xmin`)

#### Scenario: Sin filtración ni auditoría de la clave
- DADO un recifrado con la clave reconocible `clave-prueba-123456`
- CUANDO se inspeccionan registros y filas de auditoría
- ENTONCES ninguno contiene la clave en claro ni cifrada

#### Scenario: Otras compañías intactas
- DADO filas v1 de la compañía por defecto (A) y de B (copiada manualmente) y un guardado del Manager de A
- CUANDO termina
- ENTONCES la fila de B sigue en v1 con el mismo `xmin`
