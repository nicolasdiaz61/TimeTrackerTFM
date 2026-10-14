# ADR 0012: Sesión en la Web: token en sessionStorage y riesgo XSS

- Estado: Aceptado
- Fecha: 2026-10-14

## Contexto

La Web es una aplicación Blazor WebAssembly en `http://localhost:5172` que llama a la API en `http://localhost:5274`, un origen distinto. Tras iniciar sesión (ver el [ADR 0011](0011-autenticacion-jwt-roles-y-equipo.md)) la Web recibe un JWT de 30 minutos y debe enviarlo en cada petición. Hay que decidir dónde guardarlo y cómo la interfaz conoce al usuario y su rol.

## Decisión

- **Dónde se guarda el token:** `TokenStore` mantiene una copia en memoria y otra en `sessionStorage` del navegador, a través de `IJSRuntime` (sin archivo JavaScript propio). Sobrevive a recargar la página (F5) en la pestaña y desaparece al cerrarla. Nunca va en la URL, en `localStorage`, en cookies ni en el registro.
- **Estado de autenticación:** `JwtAuthenticationStateProvider` decodifica el payload del token, comprueba `exp` y construye la identidad con los claims `name` y `role`. **No valida la firma:** la interfaz solo oculta opciones (por ejemplo «Equipo»); quien decide es siempre la API.
- **Envío del token:** `BearerTokenHandler` (un `DelegatingHandler`) añade `Authorization: Bearer` en los clientes de fichajes y de equipo. El cliente de login no lo usa, para que un 401 por credenciales erróneas no dispare la redirección. Ante un 401 de la API la sesión se limpia y se redirige a `/login` con un `ReturnUrl` validado (solo rutas relativas).
- **Riesgo XSS aceptado, con mitigaciones:** si alguien consigue ejecutar JavaScript en la Web, puede leer `sessionStorage` y robar el token. Se acepta porque es una demostración local. Mitigaciones: token de solo 30 minutos, ningún uso de HTML sin escapar (`MarkupString` o `innerHTML`; lo comprueba una prueba de arquitectura), cabeceras de seguridad y CORS restrictivo en la API, y datos de ejemplo inventados.

## Consecuencias

- **Riesgo XSS:** cualquier fallo de XSS en la Web expondría el token guardado en `sessionStorage` durante su vida (30 minutos). Disparador de revisión: antes de un despliegue público o de manejar datos reales, valorar una Política de Seguridad de Contenido para la Web al servirla como estáticos y el patrón BFF.
- **Sin renovación:** al caducar el token, la persona vuelve a `/login` (consecuencia del ADR 0011).
- **La interfaz no es una barrera:** ocultar «Equipo» a un empleado es solo comodidad; la API devuelve 403 y 404 por su cuenta. Un token manipulado en el navegador no concede acceso.
- **Pruebas:** la Web no tiene pruebas de interfaz (sin bUnit ni E2E); la sesión se verificó a mano en un navegador real y consta en el informe de verificación.

## Alternativas

- **Cookies HttpOnly (patrón BFF):** el token no sería legible por JavaScript, pero exige un servidor intermedio, `SameSite` entre orígenes distintos y protección CSRF; queda como mejora si se despliega públicamente.
- **`localStorage`:** persiste entre pestañas y sesiones del navegador, lo que amplía la exposición ante XSS y alarga la vida efectiva del token.
- **Solo en memoria:** el token no sobrevive a F5 y obligaría a iniciar sesión tras cada recarga; mejor frente a XSS pero peor para el uso diario.
