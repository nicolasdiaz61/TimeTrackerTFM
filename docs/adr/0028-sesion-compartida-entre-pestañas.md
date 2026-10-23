# ADR 0028: Sesión compartida entre pestañas del navegador

- Estado: Aceptado (sustituye la decisión de almacenamiento en `sessionStorage` del ADR 0012; el resto del 0012 sigue vigente)
- Fecha: 2026-10-23

## Contexto

El [ADR 0012](0012-sesion-web-y-riesgo-xss.md) guarda el JWT en `sessionStorage`, que es propio de cada pestaña: cada pestaña nueva del mismo navegador pide iniciar sesión otra vez. Quien trabaja con varias pestañas abiertas (fichajes en una, equipo en otra) lo percibe como un defecto. El token sigue siendo el de 30 minutos del [ADR 0011](0011-autenticacion-jwt-roles-y-equipo.md), sin refresh token.

Lo que ya existe frente a XSS, y que este cambio no toca: ningún uso de `MarkupString` ni `innerHTML` (lo vigila una prueba de arquitectura), token de solo 30 minutos, CORS restrictivo y cabeceras de seguridad en la API, y la **Política de Seguridad de Contenido (CSP)** de la Web servida como estáticos. La CSP la genera `deploy/web/40-web-config.sh` al arrancar el contenedor nginx de `deploy/web` (el de Railway): `default-src 'self'`; `script-src 'self' 'wasm-unsafe-eval'` más el hash del `importmap`; `connect-src 'self'` y el origen de la API; `object-src 'none'`; `base-uri 'self'`; `frame-ancestors 'none'`, junto con `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, `Cross-Origin-Opener-Policy` y HSTS. No hay CSP en `index.html` ni al ejecutar la Web con `dotnet run` en desarrollo.

## Decisión

- **Almacenamiento compartido.** `TokenStore` guarda el token (misma clave, `timetracker.accessToken`) en el almacenamiento local del navegador (`localStorage`) y mantiene la copia en memoria. Todas las pestañas del mismo navegador y origen comparten la sesión. Sigue sin haber refresh token: al caducar el JWT (30 minutos desde el inicio de sesión), todas las pestañas vuelven a `/login` con el aviso «Su sesión ha caducado».
- **Restauración.** Al arrancar se lee el token compartido; `JwtAuthenticationStateProvider` lo descarta (y lo borra) si está mal formado o caducado, y `/login` muestra el aviso existente.
- **Sincronización entre pestañas.** Un envoltorio JavaScript mínimo (`wwwroot/js/session-storage.js`, sin `eval`, válido con la CSP) escucha el evento `storage` del navegador, que se dispara solo en las **otras** pestañas, y lo reenvía a C# mediante el servicio `ISessionStorage` (`BrowserSessionStorage`). `SessionSynchronizer` reacciona: si otra pestaña cierra la sesión, esta vuelve a `/login`; si otra inicia sesión mientras esta espera en `/login`, continúa al Inicio; si otra renueva el token (cambio de contraseña), esta lo adopta sin navegar. Solo actualiza la copia en memoria y nunca reescribe el almacenamiento, por lo que no hay bucles. El escucha se retira al desechar el servicio.
- **Tolerancia a fallos.** Toda operación de almacenamiento está en `try/catch`: si el navegador lo bloquea (modo privado, política de la empresa), la sesión vive solo en memoria de esa pestaña y la aplicación no se cae; sin sincronización entre pestañas en ese caso.
- **Cierre de sesión y migración.** «Cerrar sesión» elimina la clave del almacenamiento compartido. Al arrancar, si queda una sesión de la versión anterior en `sessionStorage`, se mueve una sola vez al almacenamiento compartido (si ya hay una compartida, prevalece esta) y se borra la clave antigua.

## Consecuencias

- **Mayor exposición ante XSS.** Un script que se ejecute en el origen de la Web puede leer el token desde cualquier pestaña y también después de cerrar y reabrir el navegador mientras el JWT siga vigente (como máximo 30 minutos desde su emisión; el JWT no se amplía). Con `sessionStorage` ese script solo veía el token de su pestaña. Mitigaciones que se mantienen: ninguna salida HTML sin escapar, vida de 30 minutos, la CSP de la Web servida con nginx (sin `'unsafe-inline'` para scripts, sin orígenes externos) y CORS restrictivo en la API. Disparador de revisión idéntico al del 0012: antes de manejar datos reales, valorar el patrón BFF con cookies HttpOnly.
- **El cierre de sesión es global** para el navegador: cerrar en una pestaña cierra todas. Una respuesta 401 de la API en una pestaña también las cierra a todas.
- **Cerrar el navegador ya no cierra la sesión** (antes sí, al cerrar la pestaña): permanece hasta que caduque el JWT o se cierre la sesión.
- **Sin acuse de caducidad en las demás pestañas** tras un 401: vuelven a `/login` sin el aviso, porque el almacenamiento solo comunica que la clave desapareció.
- **Pruebas:** la lógica (restauración, caducidad, bloqueo, migración, cierre y eventos) se prueba con almacenamiento falso; el JavaScript se limita a delegar y se verifica a mano con dos pestañas.

## Alternativas

- **`BroadcastChannel` que entrega el token a las pestañas nuevas manteniendo `sessionStorage`:** conserva el almacenamiento por pestaña, pero una pestaña nueva solo recibe el token si otra pestaña está abierta y responde, y el token acaba igualmente en `sessionStorage` y en un canal legible por cualquier script del origen; más piezas y casos límite (carreras, ninguna pestaña viva) para una exposición casi igual.
- **Cookies HttpOnly (patrón BFF):** el JavaScript no vería el token, pero la Web y la API son orígenes distintos (ADR 0011 y 0012): exige un servidor intermedio, `SameSite` entre orígenes y protección CSRF. Rechazada por ahora; sigue como mejora si se despliega con datos reales.
- **Mantener el token por pestaña (0012):** mínima exposición, pero es justo el comportamiento que se quiere corregir.
