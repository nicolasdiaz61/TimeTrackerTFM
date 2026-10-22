# Informe de verificación — multi-compania

Informe parcial: acumula las verificaciones manuales registradas durante apply. El informe completo lo redacta `sdd-verify` con contexto limpio (tarea 9.1). Valores ficticios en todo el documento.

## PR 7 — Web (tarea 7.8)

Fecha: 2026-10-22 · Rama `feat/multi-compania-pr7-web`.

### Entorno

- API local (`dotnet TimeTracker.Api.dll`, `Release`) en `http://localhost:5274` contra PostgreSQL 18 en un contenedor Docker local con datos desechables; migraciones al arrancar; **sin** `Seed__DemoUsers`; `Registration__Enabled=true` con el código de prueba `codigo-prueba-0123456789` (solo en el entorno del proceso).
- Web local (`dotnet run --project src/TimeTracker.Web`, servidor de desarrollo) en `http://localhost:5172`.
- Navegador: Chrome headless (la instalación local) dirigido por el protocolo de depuración (CDP) con un guion de Node que escribe en los campos, pulsa botones y lee el DOM, el almacenamiento del navegador y las peticiones de red. El guion no se versiona (vive en el directorio temporal de la sesión).
- Nada contra producción ni Railway. El contenedor, la API y la Web se eliminaron al terminar.

> Limitación: la pasada es automatizada, no una inspección visual con Chrome visible. Se recomienda una pasada humana breve (registro, alta, regeneración, primer inicio) antes de fusionar la PR.

### Resultado: 31 de 31 comprobaciones correctas (base nueva) y 2 de 2 con el registro cerrado

| Escenario | Comprobación | Resultado |
|---|---|---|
| Menú sin sesión | Solo «Inicio» y «Iniciar sesión»; el HTML no contiene ningún enlace a `/registro` | Correcto |
| Registro por URL directa | Código equivocado: mensaje genérico «El código de invitación no es válido.»; contraseña, confirmación y código vaciados; compañía, nombre y correo conservados | Correcto |
| Registro | Confirmación distinta: aviso junto al campo y no se envía ninguna petición a `register-company` | Correcto |
| Registro correcto | Navega a `/`; el menú muestra «Ana Ejemplo (Responsable)» y «Compañía Ejemplo»; aparecen «Alta de personas» y «Cambiar contraseña» | Correcto |
| Alta de Employee | Contraseña temporal de 16 caracteres con el aviso «no se volverá a mostrar»; no aparece en `localStorage`, `sessionStorage` ni en la URL; desaparece del DOM al cerrar el aviso | Correcto |
| Alta con correo repetido | Se muestra el `detail` del `409` («Ya existe una cuenta con ese correo electrónico.») y la pantalla sigue operativa; sin controles de baja ni edición | Correcto |
| Regeneración desde `/equipo` | Confirmación previa; contraseña nueva distinta mostrada una vez; sin rastro en almacenamiento ni URL; desaparece al cerrar el aviso | Correcto |
| Primer inicio de Luis | Con la temporal navega a `/cambiar-contrasena`; menú reducido a «Cambiar contraseña» (y «Cerrar sesión»); la página indica que el cambio es obligatorio | Correcto |
| Cambio pendiente | Abrir `/fichajes` redirige a `/cambiar-contrasena` y no se observa ninguna petición a `/api/time-entries` | Correcto |
| Cambio de contraseña | Actual incorrecta: error junto al campo y campos vaciados; actual correcta: se guarda el token nuevo, el menú vuelve al estado completo y se navega al `returnUrl` conservado (`/fichajes`) | Correcto |
| Employee | Sin «Equipo» ni «Alta de personas» en el menú; `/equipo/alta` por URL directa muestra «No tiene permiso para ver esta página.» y no hay ninguna petición a `/api/employees` | Correcto |
| Aviso de configuración | El Manager ve «Este cambio afecta a todas las personas de la compañía Compañía Ejemplo»; la página no afirma «toda la empresa» ni «toda la instancia» | Correcto |
| Cierre de sesión | El menú vuelve a «Inicio» y «Iniciar sesión» | Correcto |
| DOM y almacenamiento | Tras el recorrido no queda ninguna de las contraseñas (ni las temporales) en el almacenamiento del navegador | Correcto |
| Registro cerrado (API sin `Registration__Enabled`) | «El registro no está disponible.», sin navegar a `/login`, contraseña y código vaciados | Correcto |

### Pendiente de esta verificación

- Flujo con **dos compañías** y cada Manager abriendo `/equipo` (escenario «Flujo manual» de la spec): el aislamiento entre compañías está cubierto por las pruebas de integración de las PR 1 a 6; no se repitió en el navegador.
- Pasada visual con Chrome visible (maquetación, foco, lectores de pantalla): no cubierta por la automatización.
