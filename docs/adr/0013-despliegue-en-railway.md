# ADR 0013: Despliegue de la demostración en Railway

- Estado: Aceptado
- Fecha: 2026-10-14

## Contexto

La demostración del TFM debe estar accesible públicamente para los evaluadores. Hay una API en .NET, una Web Blazor WebAssembly estática y PostgreSQL. La API ya se empaqueta en una imagen Docker ([ADR 0007](0007-configuracion-y-validacion-al-arrancar.md)); el [ADR 0011](0011-autenticacion-jwt-roles-y-equipo.md) dejó diferido `ForwardedHeaders` «antes de desplegar tras un proxy» y el [ADR 0012](0012-sesion-web-y-riesgo-xss.md) la CSP de la Web «al servirla como estáticos».

## Decisión

- **Plataforma:** Railway, un proyecto con tres servicios: `Postgres` (plantilla de base de datos), `api` (Dockerfile de la raíz) y `web` (`deploy/web/Dockerfile`, elegido con `RAILWAY_DOCKERFILE_PATH`). `railway.json` fija el constructor Docker y la comprobación de salud `/health`, que sirven ambos servicios.
- **Configuración:** todo por variables de entorno del servicio; ningún valor ni dominio real en el repositorio. La cadena de conexión usa referencias entre servicios (`${{Postgres.PGHOST}}`, etc.) y la red privada de Railway.
- **Puerto:** la imagen de la API escucha en `PORT` si existe (Railway) y, si no, en `ASPNETCORE_HTTP_PORTS` (8080 en Compose).
- **Web:** nginx sin privilegios (imagen oficial). Al arrancar genera `appsettings.json` con `API_BASE_URL` y las cabeceras de seguridad, con la CSP abierta (`connect-src`) solo al origen de la API y `'wasm-unsafe-eval'` para WebAssembly. El `importmap` en línea de la publicación se admite por su hash SHA-256, calculado al construir la imagen.
- **`ForwardedHeaders`:** opcional (`ForwardedHeaders__Enabled`, apagado por defecto). El borde de Railway entrega `X-Forwarded-For: <cliente>, <borde>` con direcciones de borde públicas y cambiantes, así que no se puede fijar una lista de proxies: se confía en dos saltos (`ForwardedHeaders__Hops`) y el cliente es la penúltima entrada. Lo que se antepone a su izquierda no cuenta.
- **Demostración:** siembra de usuarios activada con la contraseña pública del README.

## Consecuencias

- **Seguridad del límite por IP:** solo es válido si la API únicamente es alcanzable a través del borde de Railway, que descarta el `X-Forwarded-For` del cliente. Con acceso directo al contenedor las cabeceras serían falsificables. Un ataque distribuido no queda cubierto por el límite por IP.
- **Credenciales públicas:** mientras `Seed__DemoUsers` sea `true`, cualquiera puede iniciar sesión con los usuarios de prueba. Debe apagarse al terminar la evaluación (README, «Apagar la siembra demo»). El riesgo XSS del ADR 0012 sigue aceptado: la CSP lo reduce, no lo elimina.
- **Autodespliegue:** requiere autorizar la aplicación de GitHub en el panel de Railway (acción manual); mientras tanto se despliega con `railway up`.
- **Dependencia del proveedor:** el comportamiento del borde se verificó empíricamente; si Railway lo cambia, hay que revisar `ForwardedHeaders__Hops`.

## Alternativas

- **Confiar en todas las direcciones sin límite de saltos:** deja pasar cualquier valor antepuesto por el cliente si el proxy no lo descartara.
- **Listar las redes del borde:** son públicas y cambian; la lista quedaría obsoleta.
- **Servir la Web desde la API:** mezcla dos artefactos con ciclos de vida distintos y complica la CSP y el CORS.
- **Otro proveedor o máquina virtual propia:** más control, pero más operación para una demostración.
