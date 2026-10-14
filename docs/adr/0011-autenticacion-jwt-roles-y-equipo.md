# ADR 0011: Autenticación con JWT propio, roles y equipo

- Estado: Aceptado; sustituye al 0009
- Fecha: 2026-10-14

## Contexto

El [ADR 0009](0009-identidad-provisional.md) fijó una identidad provisional (un empleado ficticio, solo en `Development`) a la espera de una autenticación real. `AGENTS.md` §6 exige que el identificador del usuario salga siempre del token, contraseñas con hash seguro, expiración de los tokens, CORS restrictivo, cabeceras de seguridad y límite de tasa, y pide control de acceso por rol y por objeto. Además, un responsable debe poder consultar los fichajes de su equipo sin ver los de nadie más.

## Decisión

- **Mecanismo:** JWT propio firmado con HS256, emitido por la API y validado con `JwtBearer`. Los usuarios viven en PostgreSQL (tabla `users`). No se usa ASP.NET Core Identity ni `MapIdentityApi`. El puerto `ICurrentUser` no cambia: `HttpContextCurrentUser` (en `Api`) lee el claim `sub`, así que `Application` y los casos de uso de fichaje no se tocan.
- **Hash de contraseñas:** `PasswordHasher<object>` del marco compartido (sin paquete adicional). Formato V3 verificado en la implementación: PBKDF2 con **HMAC-SHA512** y **100.000 iteraciones**, con sal aleatoria por hash. Si cambian los parámetros, `SuccessRehashNeeded` se acepta como válido.
- **Usuario inexistente:** `IPasswordHasher.Verify` recibe `null` y verifica contra un hash ficticio calculado una vez, para que el tiempo sea similar exista o no el correo. Las entradas fuera de forma (correo vacío o de más de 254 caracteres; contraseña vacía o de más de 128) devuelven el mismo 401 sin consultar ni hashear. El mensaje del 401 es idéntico en todos los casos.
- **Claims y validación:** `sub`, `name`, `email`, `role` (`Employee` o `Manager`), `jti`, `iat`, `nbf`, `exp`; el emisor `timetracker-api` y la audiencia `timetracker-web` son constantes de código. Solo se acepta HS256, con firma obligatoria, `ClockSkew` de 30 s, sin mapeo de claims entrantes y con rechazo de un `sub` que no sea un GUID válido y de roles desconocidos. La única clave de configuración es `Jwt__SigningKey` (obligatoria, mínimo 32 bytes; la API no arranca sin ella).
- **Duración:** 30 minutos, **sin refresh token**. Es una desviación consciente de `AGENTS.md` §6 («renovación controlada»); ver consecuencias.
- **Roles y equipo:** `UserRole { Employee, Manager }`. `UserAccount.ManagerId` (opcional) apunta al responsable directo; el equipo de un responsable son sus reportes directos. `FallbackPolicy = RequireAuthenticatedUser` (un endpoint nuevo nace protegido), política `Manager` en `/api/team` y `AllowAnonymous` solo en `/health`, `POST /api/auth/login` y el documento OpenAPI. El control por objeto vive en `FindDirectReportAsync`: un miembro inexistente, ajeno, sin responsable o el propio responsable devuelven el mismo 404 sin cuerpo propio, y el rol se exige antes que el miembro (403 al empleado). La lista del equipo expone solo `id` y `displayName`, con un máximo de 100.
- **Límite de tasa:** política `login` de ventana fija, 10 intentos por minuto y por IP de origen; el undécimo recibe 429. Cabeceras de seguridad (`nosniff`, `DENY`, `no-referrer`, CSP restrictiva y `Cache-Control: no-store` en `/api/*`) y errores en `application/problem+json` sin trazas.
- **Sin clave foránea desde `time_entries`:** `time_entries.employee_id` no tiene clave foránea hacia `users`. `users.manager_id` sí la tiene (`NO ACTION`, con `CHECK` contra ser el propio responsable). Los fichajes anteriores pertenecían a un GUID provisional que no corresponde a ningún usuario; con una clave foránea habría que borrarlos o migrarlos.
- **Siembra de demostración:** `DemoUsersSeeder` (un `IHostedService` que trabaja en `StartAsync`, después de aplicar las migraciones) crea cuatro usuarios inventados (`example.test`) con la contraseña de `Seed__DemoPassword`, de forma idempotente por correo. Está desactivada por defecto (`Seed__DemoUsers=false`); compose la activa. No se prohíbe fuera de `Development`: la contraseña de demostración es pública (figura en el README) y no debe usarse en un despliegue real.
- **Compose:** `api` se publica en `127.0.0.1:5274:8080`, que es el `ApiBaseUrl` por defecto de la Web, con `Jwt__SigningKey` y `Seed__DemoPassword` obligatorias (sin literales en el archivo).

## Consecuencias

- **Sin refresh token:** al caducar el token (30 minutos) la persona debe iniciar sesión de nuevo. Es una desviación consciente de `AGENTS.md` §6, aceptable en una demostración local. Disparador de revisión: antes de sesiones largas o de un despliegue público.
- **Sin clave foránea en `time_entries`:** se pueden guardar fichajes de un GUID sin usuario (huérfanos) y un usuario borrado no arrastra los suyos. Disparador de revisión: al añadir alta y baja de usuarios (entonces se añade la clave foránea y se decide el borrado). Los fichajes de desarrollo anteriores quedan huérfanos; `docker compose down -v` los elimina.
- **Usuario borrado:** conserva su token hasta 30 minutos, porque la API no consulta la base de datos para validar el token.
- **Detrás de un proxy:** el límite de tasa usa la IP de la conexión; sin `ForwardedHeaders` todos los clientes parecerían uno solo. Disparador de revisión: configurar `ForwardedHeaders` antes de desplegar tras un proxy. Un ataque distribuido no queda cubierto por el límite por IP; lo mitigan el coste de PBKDF2 y el tope de 128 caracteres.
- **Rehash:** si cambian los parámetros de PBKDF2, los hashes antiguos siguen siendo válidos (`SuccessRehashNeeded`); no se reescriben hasta que un cambio futuro lo implemente. Disparador de revisión: cualquier cambio de los parámetros.
- **Contraseña de demostración pública:** quien active la siembra en un entorno accesible expone cuatro cuentas conocidas. Mitigaciones: desactivada por defecto, compose solo en `127.0.0.1`, `.env.example` sin valor y aviso en el README.
- **Clave de firma:** HS256 usa una clave simétrica compartida; si se filtra, cualquiera puede emitir tokens. La clave solo llega por variable de entorno y no se registra.
- **Contrato OpenAPI:** el documento declara el esquema `Bearer` y las respuestas 401, 403, 404 y 429; el endpoint de login es anónimo.

## Alternativas

- **ASP.NET Core Identity / `MapIdentityApi`:** tokens opacos y siete tablas para un uso que necesita una; añade superficie y paquetes.
- **Cookies de sesión:** la Web (`localhost:5172`) y la API (`localhost:5274`) son orígenes distintos; exigiría `SameSite`, protección CSRF y un modelo distinto en la Web. Ver también el [ADR 0012](0012-sesion-web-y-riesgo-xss.md).
- **Proveedor externo OIDC:** dependencia externa y configuración que quedan fuera de alcance para una demostración local.
- **Refresh token:** más piezas (almacén, rotación, revocación) que no se justifican con sesiones de 30 minutos; se revisará con el disparador indicado.
- **Clave foránea desde `time_entries` ahora:** obliga a borrar o migrar los fichajes existentes y a decidir el borrado de usuarios antes de que exista su alta y baja.
- **Prohibir la siembra fuera de `Development`:** impediría usar un usuario de prueba en un despliegue de demostración, que `AGENTS.md` §8 pide.
