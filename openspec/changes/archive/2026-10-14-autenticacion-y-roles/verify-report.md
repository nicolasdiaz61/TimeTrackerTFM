# Informe de verificación (preliminar): autenticacion-y-roles

Estado (actualizado 2026-10-14): C1 resuelto y veredicto **PASS WITH WARNINGS**. La sección «Verificación manual de la Web» (tareas 24.1 a 24.5, Tanda 4) es la preliminar y se conserva íntegra; el informe completo de `sdd-verify` (2026-10-14) sigue a partir de «Informe de verificación completo».

## Verificación manual de la Web

Fecha: 2026-10-14. Ejecutada de verdad, con valores ficticios (`example.test`, contraseña de demostración `Demo-Fichajes-2026`, clave de firma ficticia de más de 32 bytes).

### Entorno (24.1)

- API y PostgreSQL: `docker compose -f docker-compose.yml -f <override> --env-file <ficticio> -p ttsmoke4 up -d --build --wait` (rc 0; `postgres` y `api` en `healthy`; `127.0.0.1:5274->8080` y `127.0.0.1:5432->5432`). **Sin leer `.env`:** el `--env-file` y el override (que retira `env_file` con `!reset []` y aporta `Cors__AllowedOrigins__0=http://localhost:5172`) viven en el directorio temporal de la sesión, fuera del repositorio. Siembra: `Seed__DemoUsers=true` (compose), 4 usuarios de demostración.
- Web: `dotnet run --project src/TimeTracker.Web --no-launch-profile --urls http://localhost:5172` (`/login` responde 200).
- `Select-String ApiBaseUrl src/TimeTracker.Web/wwwroot/appsettings.json` muestra `http://localhost:5274` (UI-10 del cambio archivado; aquí condición de entorno). Ningún `.cs` ni `.razor` de la Web contiene `localhost:5274`.
- Navegador: las herramientas de navegador MCP **no estaban disponibles**. **Respaldo aplicado:** Google Chrome real instalado, en modo headless con perfil temporal propio, controlado por un script Node con el protocolo Chrome DevTools (sin instalar nada; scripts en el directorio temporal de la sesión: `cdp.mjs`, `e2e-a.mjs` a `e2e-e.mjs`). Se leyeron el DOM renderizado por Blazor WebAssembly, `sessionStorage`, `localStorage`, `document.cookie`, la consola y la red del navegador (con cabeceras `Authorization`). Es ejecución real de la Web en un navegador, no una simulación; el aspecto visual no se evalúa (ver «No verificado»).
- Para que cada bloque parta con el limitador de login a cero se reinició el contenedor `api` (`docker restart`) al principio de cada guion; la base de datos se mantuvo.

### Flujo del empleado, Luis (24.2)

| Escenario | Resultado observado | Estado |
|---|---|---|
| WS-16 `/fichajes` sin sesión | Redirige a `/login?returnUrl=%2Ffichajes`; 0 peticiones a `/api/time-entries` | Verificado |
| WS-17 `/equipo` sin sesión | Redirige a `/login?returnUrl=%2Fequipo`; 0 peticiones a `/api/team` | Verificado |
| WS-25 menú sin sesión | `Home`, `Iniciar sesión`, `Counter`, `Weather` (sin Fichajes, Equipo ni Cerrar sesión) | Verificado |
| WS-7 campos vacíos | «Introduzca el correo electrónico y la contraseña.»; 0 peticiones a `/api/auth/login` | Verificado |
| WS-4 contraseña errónea y correo inexistente | Mismo mensaje «Correo o contraseña incorrectos.» en ambos casos (`401`), sigue en `/login`, campo de contraseña vacío | Verificado |
| WS-21 login sin `Authorization` | El `POST /api/auth/login` no lleva cabecera `Authorization` (el login se registra sin handler) | Verificado |
| WS-1 login correcto | Navega a `/fichajes`; menú `Fichajes` y `Cerrar sesión`; estado «Luis Ejemplo (Empleado)» | Verificado |
| WS-26 sin «Equipo» para el empleado | El menú de Luis no contiene «Equipo» | Verificado |
| WS-8 almacenamiento | `sessionStorage['timetracker.accessToken']` existe (437 caracteres); `localStorage` vacío (`[]`); `document.cookie` vacío. Claims del token: `role=Employee`, `name`, `sub`, `email`, `exp`, etc. | Verificado |
| WS-11 el token no se expone | No aparece en la URL, el texto de la página ni la consola (solo `console.info` del framework sobre autorización fallida de roles, sin token) | Verificado |
| UI-1 / UI-2 | Luis sin fichajes: «No hay fichajes registrados.» y total 0; tras fichar, el total de la interfaz (`0 minutos (0:00)`) coincide con el de la API (`{"n":1,"total":0}`) | Verificado |
| UI-5 segunda entrada | «Registre la salida antes de fichar una nueva entrada.» (409); listado sin cambios | Verificado |
| UI-6 salida sin entrada | «Registre la entrada antes de fichar la salida.» (409) | Verificado |
| WS-20 `Authorization` solo hacia la API | `GET`/`POST` a `http://localhost:5274/api/time-entries[...]` con `Authorization: Bearer`; 0 peticiones con esa cabecera a otro origen | Verificado |
| UI-12 sin `employeeId` | No aparece en ruta, consulta, cabeceras ni cuerpo de ninguna petición | Verificado |
| UI-13 sin errores de CORS | 0 peticiones fallidas ni eventos de red con error, incluidos los `POST` y los `409` | Verificado |
| WS-9 F5 conserva la sesión | Tras recargar `/fichajes`: sigue «Luis Ejemplo (Empleado)» y la tabla | Verificado |
| WS-10 clave borrada de `sessionStorage` | Sin recargar, «Fichar entrada» sigue enviando `Authorization` (`201`): la copia en memoria se conserva | Verificado |
| WS-18 `/equipo` como Luis | «No tiene permiso para ver esta página.»; 0 peticiones a `/api/team`; el menú no cambia | Verificado |
| WS-34 «Cerrar sesión» | Va a `/login`; `sessionStorage` sin token; menú sin sesión | Verificado |
| WS-35 atrás / abrir `/fichajes` tras cerrar | Se queda en `/login` (con `returnUrl`); 0 peticiones con `Authorization` | Verificado |
| UI-3 / UI-4 | Sin sesión no hay peticiones de fichajes. Luis y Ana en navegadores (perfiles) distintos: cada tabla muestra solo lo propio (Luis 2 filas, Ana 0 y luego 1 tras fichar; Luis sigue con 2) | Verificado |
| WS-36 textos en español | Login, errores, permisos, menú y fichajes en español; las páginas de la plantilla (`Home`, `Counter`, `Weather`) siguen en inglés porque la tarea no las cambia | Verificado salvo plantilla |
| WS-40 | Cumplido: el recorrido completo del empleado funciona de extremo a extremo | Verificado |

Nota de método: una primera pasada del guion llegó a `/equipo` tras un `returnUrl` que había quedado en la URL (comportamiento correcto de la Web), de modo que los botones de fichar no existían en esa pantalla; el guion se corrigió (navegar a `/login` limpio antes de entrar) y se repitió todo desde una base de fichajes vacía. Solo cuenta la segunda ejecución.

### Flujo del responsable, Marta (24.3)

| Escenario | Resultado observado | Estado |
|---|---|---|
| WS-27 / WS-12 login de Marta | Menú con `Fichajes` y `Equipo`; estado «Marta Ejemplo (Responsable)»; el token lleva `role=Manager` y `name=Marta Ejemplo` | Verificado |
| Marta ficha | Entrada → fila «en curso»; salida → fila cerrada (sin error) | Verificado |
| WS-19 / WS-28 / WS-41 lista del equipo | Opciones: «Seleccione una persona», «Ana Ejemplo», «Luis Ejemplo»; **sin Pablo** | Verificado |
| WS-29 fichajes de Luis (por SQL) | Tres filas `yyyy-MM-dd HH:mm UTC` (`2026-10-13 08:00 UTC` «en curso», `2026-10-12 10:00`→`10:30` 30 min, `2026-10-12 08:00`→`09:00` 60 min); total «90 minutos (1:30)» igual al de la API (`{"n":3,"total":90}`) | Verificado |
| WS-30 cambio de miembro sin recargar | Al elegir a Ana el contenido cambia a sus fichajes (1 fila) sin recargar | Verificado |
| WS-31 solo lectura | Sin botones de fichar ni de editar en `/equipo`, ni con miembro elegido | Verificado |
| WS-32 equipo vacío | Con `manager_id = NULL` de Luis y Ana: «No tiene personas a su cargo.» | Verificado |
| WS-33 miembro que deja de serlo | Con Luis desvinculado: «Esa persona ya no forma parte de su equipo.» (`404`); el selector sigue operativo y Ana se puede elegir sin error. Vínculos restaurados (2 miembros) | Verificado |
| TA-2 (UI) | Marta ve solo a sus reportes directos | Verificado |

La consola mostró únicamente el aviso de red del navegador del `404` esperado.

### Sesión, 401 y expiración (24.4)

| Escenario | Resultado observado | Estado |
|---|---|---|
| WS-2 `returnUrl=/equipo` con Marta | Tras entrar queda en `/equipo` | Verificado |
| WS-3 `returnUrl` inseguro | `//ejemplo.test`, `https://ejemplo.test`, `/%5Cejemplo.test` (`/\ejemplo.test`), `javascript:alert(1)` y `fichajes`: siempre `/fichajes` y el origen sigue siendo `http://localhost:5172` | Verificado |
| WS-13 / WS-23 `exp` pasado | Con el `exp` reescrito en el payload y recarga en `/fichajes`: el token se elimina, se llega a `/login?returnUrl=%2Ffichajes`, 0 peticiones a la API, sin aviso técnico | Verificado |
| WS-14 token mal formado | `texto-cualquiera`, `aaa.bbb.ccc` y `a.b`: no autenticado, almacenamiento limpio, sin excepciones ni panel de error | Verificado |
| WS-15 / WS-22 firma alterada | `/fichajes` abre como autenticado, la API responde `401` y se llega a `/login?returnUrl=%2Ffichajes` con el token eliminado y sin texto técnico | Verificado |
| UI-8 firma alterada al pulsar «Fichar entrada» | Con la lista interceptada (`200` vacío) para poder pulsar el botón: el `POST` recibe `401`, se cierra la sesión, se llega a `/login?returnUrl=%2Ffichajes` y no hay mensaje técnico en la pantalla | Verificado (con la lista interceptada por DevTools) |
| WS-24 `403` sin cerrar sesión | `fetch` a `/api/team/members` con el token de Luis → `403`; la sesión, la URL y el menú siguen igual | Verificado |

### Errores y límites (24.5)

| Escenario | Resultado observado | Estado |
|---|---|---|
| WS-5 once logins fallidos | Los 10 primeros: «Correo o contraseña incorrectos.»; el undécimo: «Demasiados intentos. Espere un minuto e inténtelo de nuevo.»; botón habilitado y pantalla operativa; tras esperar 62 s el login correcto funciona | Verificado |
| WS-6 / UI-7 API detenida | `docker stop` de `api`: el botón «Fichar entrada» y el login muestran «No se pudo completar la operación. Inténtelo de nuevo más tarde.» sin trazas ni panel de error | Verificado |
| WS-37 PostgreSQL detenido | Con sesión abierta: `/fichajes` y `/equipo` reciben `500` y muestran el mensaje genérico (en `/equipo` hay que esperar el tiempo de espera de la base: un primer intento con 2,5 s aún no había terminado y se repitió con 6 s) | Verificado |
| UI-9 `ApiBaseUrl` ausente / `no-es-una-url` | La Web no arranca: panel de error y consola «Falta la configuración «ApiBaseUrl» o no es una URL absoluta http/https. Defínala en wwwroot/appsettings.json.» (envuelto en `AggregateException`); 0 peticiones a la API. **Hay que cambiar los dos** `appsettings.json` y `appsettings.Development.json` (el servidor de desarrollo usa `Development`); en el primer intento solo se tocó el primero y la Web arrancó normal. Archivos restaurados (comparación byte a byte) | Verificado |
| WS-36 | Mensajes de error en español, sin datos técnicos | Verificado |

### No verificado

- **Aspecto visual** (maquetación del menú, estilo del botón «Cerrar sesión», colores, espaciado, responsive): no se evaluó; ninguna captura revisada por una persona.
- **Estado intermedio del botón** «Entrar»/«Fichar» mientras la petición está en curso (deshabilitado): el DOM se leyó solo antes y después; no se midió el instante.
- Se usó solo Chrome headless (no Edge ni otros navegadores) y la expiración se simuló reescribiendo el `exp` del token, no esperando a que caduque de verdad.
- Limitación conocida de diseño: el estado de autenticación se evalúa al cargar y al iniciar o cerrar sesión; un token que caduca durante una navegación interna se detecta en la siguiente petición a la API (`401`) o al recargar, no al instante.
- Lógica de `ReturnUrl.IsSafe` y del proveedor: solo a mano (no hay bUnit por decisión de las especificaciones).


---

# Informe de verificación completo: autenticacion-y-roles

Fecha: 2026-10-14 · Fase: sdd-verify · Modo: openspec · Strict TDD: activo (`strict_tdd: true`, xUnit) · No se ejecutó ningún comando `git` (no existe `.git`). No se leyó `.env` ni `.env.example`. No se modificó código ni specs.

## Veredicto

**PASS WITH WARNINGS.** C1 (`.env.example`) está resuelto: el usuario aplicó el archivo propuesto y la suite está en 393/393 verdes. El cambio es archivable.

Hay 0 CRITICAL, 6 WARNING (W1–W6, sin cambios) y 5 SUGGESTION. C1 quedó resuelto tras la primera pasada (FAIL por `.env.example`) y repetir la puerta de calidad. No hay ningún hallazgo CRITICAL de código, seguridad ni diseño.

## Completitud (tareas)

- 28 fases y 127 tareas según tasks.md; **todas marcadas `[x]`**. Las cinco que estaban abiertas (20.1, 21.1, 21.2, 28.1 y 28.4) se cerraron el 2026-10-14 con la evidencia real de la puerta repetida tras aplicar `.env.example`. El resto estaba marcado y su evidencia coincide con lo que he ejecutado (conteos de pruebas, hash del contrato, cobertura).
- Desviaciones D1–D16 y D-3b-*, D-4-*, D-5-* revisadas: ninguna rompe una spec salvo D-3b-4 (`.env.example`, causa del bloqueo).

## Evidencia de ejecución (ejecutada por este verificador)

**Repetición del 2026-10-14 tras aplicar `.env.example`** (entorno limpio de `Jwt__`, `Seed__` y `ConnectionStrings__`; `env.example.proposed` ya no existe): `restore` rc 0; `format --verify-no-changes` rc 0; `build -c Release -warnaserror` rc 0 (0 avisos, 0 errores); `test -c Release --no-build --collect:"XPlat Code Coverage"` rc 0 con **393/393 verdes** (Domain 49, Application 43, Arquitectura 71, Integración 230); `check-coverage.ps1` Domain 100,0 % líneas y 100,0 % ramas (mín. 100) y Application 100,0 % y 100,0 % (mín. 80), ambos rc 0; `dotnet list package --vulnerable --include-transitive` sin paquetes vulnerables en los 9 proyectos. `TestResults` eliminado. La tabla siguiente es la primera pasada (con las 5 rojas) y se conserva como historial.

Entorno limpio (sin variables `Cors__`, `ConnectionStrings__`, `Identity__`, `Database__`, `Jwt__`, `Seed__`, `ASPNETCORE_`, `DOTNET_ENVIRONMENT`).

| Comando | Resultado |
|---|---|
| `dotnet restore TimeTracker.sln` | rc 0 |
| `dotnet format TimeTracker.sln --verify-no-changes --no-restore` | rc 0 |
| `dotnet build TimeTracker.sln -c Release --no-restore -warnaserror` | rc 0, 0 avisos, 0 errores |
| `dotnet test TimeTracker.sln -c Release --no-build --collect:"XPlat Code Coverage"` | primera pasada rc 1: 393 pruebas = 388 verdes y 5 rojas; **repetición: rc 0, 393/393** |
| Domain.Tests | 49/49 |
| Application.Tests | 43/43 |
| ArchitectureTests | primera pasada 66 verdes y 5 rojas; **repetición 71/71** |
| Api.IntegrationTests (PostgreSQL 18 real, Testcontainers) | 230/230 |
| `scripts/check-coverage.ps1` Domain (mín. 100) | rc 0: líneas 100,0 %, ramas 100,0 % |
| `scripts/check-coverage.ps1` Application (mín. 80) | rc 0: líneas 100,0 %, ramas 100,0 % |
| `dotnet list package --vulnerable --include-transitive` | los 9 proyectos sin paquetes vulnerables |
| Búsqueda `FromSqlRaw`, `ExecuteSqlRaw`, `EnableSensitiveDataLogging`, `NotImplementedException` en `src` | sin coincidencias |
| `dotnet build src/TimeTracker.Api --no-incremental -warnaserror` | rc 0, 0 avisos; SHA-256 de `docs/api/openapi.json` idéntico antes y después (`f0c63377be770c93…`); 0 coincidencias de `passwordHash` y `employeeId`; hay `securitySchemes` Bearer |
| OpenAPI en ejecución (`/openapi/v1.json` del contenedor) | rutas `/health`, `/api/auth/login`, `/api/auth/me`, `/api/team/members`, `/api/team/members/{memberId}/time-entries`, `/api/time-entries`, `/clock-in`, `/clock-out`; 0 `passwordHash` y 0 `employeeId` |

### Las 5 pruebas rojas de la primera pasada (resueltas: ahora pasan)

`EnvExample_ContainsTheDocumentedKeys`, `EnvExample_AuthenticationKeysAreEmptyOrFalseAndDocumented`, `EnvExample_HasNoDevelopmentEmployeeKey`, `EnvExample_ListsEveryKeyConsumedByCompose` y `DeliveryFiles_HaveNoProvisionalIdentity(".env.example")`. Ninguna otra falla.

### El archivo propuesto cumpliría las comprobaciones (razonado contra el código de las pruebas)

Leído `env.example.proposed` del scratchpad y contrastado con `DeliveryArtifactsTests.cs` y `RepositoryConventionsTests.cs`:

- `ContainsTheDocumentedKeys`: define activas las 8 claves (`POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`, `ConnectionStrings__Default`, `Database__ApplyMigrationsOnStartup=false`, `Jwt__SigningKey`, `Seed__DemoUsers`, `Seed__DemoPassword`); `POSTGRES_PASSWORD` y `ConnectionStrings__Default` vacías.
- `AuthenticationKeysAreEmptyOrFalseAndDocumented`: `Jwt__SigningKey=` y `Seed__DemoPassword=` activas y vacías; `Seed__DemoUsers=false`; el bloque de comentarios contiguo anterior a `Seed__DemoPassword` (líneas 26 y 27; la 25 no es comentario y corta el recorrido) contiene «solo en local», «12 caracteres» y «README»; el texto no contiene `Demo-Fichajes-2026`.
- `HasNoDevelopmentEmployeeKey` y la Theory `DeliveryFiles_HaveNoProvisionalIdentity(".env.example")`: el texto no contiene `Identity__DevelopmentEmployeeId` ni `Identity:DevelopmentEmployeeId`.
- `ListsEveryKeyConsumedByCompose`: las variables de compose son `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`, `Jwt__SigningKey` y `Seed__DemoPassword` (la prueba añade `Cors__AllowedOrigins__0` y `ASPNETCORE_HTTP_PORTS`); todas figuran (la del puerto comentada, que la expresión regular de la prueba admite).
- Regresiones `HasNoRealValues` (sin GUID ni `Password=` en valores activos), `HasNoActiveAiKeys` (las `Ai__*` están comentadas) y `DoesNotDefineEmptyPortKey` (`ASPNETCORE_HTTP_PORTS` comentada): siguen en verde. No hay claves duplicadas (`ToDictionary` no fallaría).

Conclusión: la predicción se confirmó. Tras aplicar el archivo, las 5 pasan en la ejecución real (393/393). `.env.example` sigue sin leerse (permisos): se verifica solo por las pruebas.

## Verificación en ejecución (Docker y compose con variables ficticias)

Compose con `--env-file` y override del scratchpad (sin `.env`), proyecto `ttverify`: `up -d --build --wait` rc 0, `postgres` y `api` en `healthy`.

| Comprobación | Resultado |
|---|---|
| Login de demostración de Luis (Employee), Marta (Manager), Ana y Pablo | 200 en los cuatro; cuerpo `accessToken`, `expiresAt`, `user{id,email,displayName,role}` (sin hash) |
| `GET /api/team/members` con Marta | 200: «Ana Ejemplo» y «Luis Ejemplo»; sin Pablo |
| Mismo `GET` con Luis | 403 `application/problem+json` («Acceso denegado») |
| Mismo `GET` sin token | 401 (`WWW-Authenticate: Bearer`, `Cache-Control: no-store`) |
| Miembro inexistente frente a miembro ajeno (Pablo), con Marta | ambos 404; `type`, `title`, `status` y `detail` idénticos (solo cambia `traceId`) |
| Empleado en `/api/team/members/{id}/time-entries` | 403 (el rol se exige antes que el miembro); sin token 401 |
| Marta a un reporte directo (Ana) | 200 |
| Aislamiento de fichajes | Luis y Pablo ficharon entrada (201); cada `GET /api/time-entries` devuelve solo lo suyo; Ana, vacía; `?employeeId=<Pablo>` con el token de Luis se ignora (devuelve lo de Luis); Marta ve lo de Luis solo por la ruta de equipo; salida de Pablo sin entrada previa: 409 |
| 429 | tras reiniciar la API: 10 logins erróneos con 401 y el **11.º con 429** (`application/problem+json`, `Retry-After: 60`, cabeceras de seguridad); ventana fija de un minuto |
| Cabeceras de seguridad (401, 429 y `/health`) | `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `X-Frame-Options: DENY`, `Content-Security-Policy` con `default-src` y `frame-ancestors` en `none`; `Cache-Control: no-store` en los errores de autenticación |
| `GET /api/auth/me` | 200 con la identidad del token |
| Registro del contenedor | 0 apariciones de la contraseña de demostración, la clave de firma y la contraseña de PostgreSQL |
| `docker build` | rc 0; `User=1654` (no root); `HEALTHCHECK` sobre el `/health` anónimo |
| API en Production (contenedor sin `ASPNETCORE_ENVIRONMENT`) sin `Jwt__SigningKey` | `OptionsValidationException: Jwt:SigningKey: debe definirse.`, salida con código 1; nombra la clave y no imprime valores |

Limpieza: `compose down -v` (contenedores, red y volumen `ttverify_pgdata` eliminados), imágenes `timetracker-api:verify` y `timetracker-api:local` eliminadas, `TestResults/` eliminado, `dotnet build-server shutdown`. Las imágenes base (`dotnet/sdk`, `dotnet/aspnet`, `postgres:18-alpine`, `testcontainers/ryuk`) se conservan por ser compartidas. No quedan contenedores ni volúmenes del proyecto.

## Matriz de cumplimiento (62 requisitos, 260 escenarios)

Método: la trazabilidad escenario→tarea→prueba está en tasks.md. Yo ejecuté la suite completa (388 de 393 verdes), la cobertura y las comprobaciones en vivo de arriba. Los escenarios de la Web (WS y UI) no se repitieron aquí: se apoyan en la verificación manual de la sección anterior (Chrome real, otro ejecutor). **No re-tracé uno a uno los 260 escenarios contra su prueba** (W6).

| Especificación | Requisitos | Escenarios | CUMPLE | PARCIAL | INCUMPLE | SIN EVIDENCIA | Tipo de evidencia |
|---|---|---|---|---|---|---|---|
| authentication | 14 | 61 | 61 | 0 | 0 | 0 | automática; login, 401, 429 y cabeceras repetidos por mí en vivo |
| team-access | 12 | 63 | 63 | 0 | 0 | 0 | automática; 403, 404 idéntico, aislamiento y siembra repetidos por mí |
| web-session | 10 | 41 | 41 | 0 | 0 | 0 | manual (Chrome headless real, sección anterior; sin bUnit por decisión de las specs) |
| time-entries-ui | 3 | 13 | 13 | 0 | 0 | 0 | manual (misma sección) |
| time-tracking | 3 | 14 | 14 | 0 | 0 | 0 | automática |
| time-entries-persistence | 5 | 14 | 14 | 0 | 0 | 0 | automática (PostgreSQL real) |
| api-health | 1 | 3 | 3 | 0 | 0 | 0 | automática; `/health` anónimo repetido por mí |
| startup-configuration | 7 | 24 | 24 | 0 | 0 | 0 | automática; SC-24 (puerto) manual en 20.5, no repetida |
| solution-architecture | 7 | 27 | 26 | **1 (SA-23)** | 0 | 0 | automática y manual (imagen y compose repetidos por mí); SA-23 literal con `.env` real no ejecutado |
| **Total** | **62** | **260** | **259** | **1** | **0** | **0** | |

Resolución de `.env.example`: SC-19, SC-20, SC-21, SC-22 y SC-23 pasan de INCUMPLE a **CUMPLE** (las 5 pruebas de `.env.example` en verde con 393/393) y la parte de `.env.example` de SA-5 también. Recuento real: **259 CUMPLE, 1 PARCIAL (SA-23: README con `.env` real sin ejecutar literalmente, W3), 0 INCUMPLE, 0 SIN EVIDENCIA** (de 260 escenarios).

## Cumplimiento Strict TDD

| Comprobación | Resultado |
|---|---|
| Evidencia TDD reportada | Sí: cada tarea de tasks.md lleva RED real (mensaje), GREEN y, para las «verdes a la primera», mutación revertida (índice en 28.2) |
| Todas las tareas con pruebas | Sí; 49 archivos de prueba con 321 atributos `[Fact]` o `[Theory]` que producen 393 casos |
| GREEN confirmado | 393/393 pasan (primera pasada 388/393 con las 5 de `.env.example`, ya resueltas) |
| Auditoría de aserciones | Sin tautologías (`Assert.True(true)`) en la búsqueda; las pruebas de entrega son de texto (S3) |
| Cobertura de lo cambiado | Domain 100/100, Application 100/100 (líneas/ramas) |

## Coherencia diseño ↔ código

| Decisión | Resultado |
|---|---|
| JWT propio HS256, 30 min, sin refresh (`JwtDefaults.TokenLifetime`, `ValidAlgorithms = [HmacSha256]`) | Coherente |
| Hash con `PasswordHasher` (PBKDF2, V3), hash ficticio para igualar trabajo con usuario inexistente, hash ilegible = credencial inválida (AU-14) | Coherente |
| Identidad desde el token (`HttpContextCurrentUser`); sin `employeeId` en endpoints ni contrato | Coherente (parámetro `employeeId` ignorado en vivo; 0 en OpenAPI) |
| Control por objeto en equipo (404 idéntico para ajeno e inexistente) | Coherente (probado en vivo) |
| Limitador 10/min por IP, cabeceras de seguridad y 429 `problem+json` | Coherente |
| Esquema Bearer en OpenAPI | Coherente |
| Siembra tras migraciones y antes de escuchar; desactivada por defecto en la imagen y `.env.example` | Coherente |
| `Api/Identity` retirada, sin restos de la identidad provisional | Coherente (pruebas de restos en verde) |
| Desviaciones documentadas | Sin impacto en specs salvo D-3b-4 |

## AGENTS.md

| Regla | Resultado |
|---|---|
| Identidad desde el token; sin `employeeId` en endpoints | Cumple |
| Hash seguro de contraseñas | Cumple (PBKDF2 con sal) |
| Errores sin trazas al cliente | Cumple (`problem+json` genérico; en la Web, mensajes en español sin datos técnicos) |
| Secretos fuera del repositorio | Cumple (compose con referencias obligatorias y sin literales; 0 apariciones en imagen y registro) |
| Cabeceras de seguridad, rate limiting, CORS restrictivo | Cumple |
| Sin menciones del curso ni datos de empresas reales | Cumple (README, docs, src, tests, config.yaml; solo `example.test` e identificadores inventados) |
| Sin Git | Incumplimiento anotado D-5-2: un `git --version` encadenado por error en la Tanda 5 (el orquestador indica que otros agentes también lo hicieron); informativo, no cambia nada |
| Cobertura Domain 100 % y Application ≥ 80 % | Cumple |
| ADR 0001–0012 y README al día | Cumple |

## Revisión de seguridad de la Web

- Token solo en `sessionStorage` (`timetracker.accessToken`) y memoria; sin `localStorage` ni cookies (navegador y búsqueda en el código); riesgo XSS aceptado en el ADR 0012.
- Sin `MarkupString` en `src/TimeTracker.Web`.
- Redirección abierta: `ReturnUrl.IsSafe` exige ruta que empiece por `/`, rechaza `//`, la barra invertida tras `/` y caracteres de control; probado en el navegador con `//ejemplo.test`, `https://…`, la variante con barra invertida, `javascript:` y `fichajes` (siempre acaba en `/fichajes` y en el mismo origen). `RedirectToLogin` y el handler codifican `returnUrl` con `Uri.EscapeDataString`.
- `Authorization` solo hacia la API; el login se registra sin handler; sin URL absolutas en los `.cs` y `.razor` de la Web.

## `NavMenu.razor.css` (reconstruido en la Tanda 4, D-4-1)

El archivo se sobrescribió por error y se reconstruyó desde el CSS con ámbito compilado. Comprobado: el CSS compilado actual (`obj/Release/net10.0/scopedcss/Layout/NavMenu.razor.rz.scp.css`) contiene las 20 reglas esperadas (`navbar-toggler`, `top-row`, `navbar-brand`, `.bi` y los tres iconos, `.nav-item` con `first-of-type` y `last-of-type`, `a`, `a.active`, `a:hover`, el `@media (min-width: 641px)` con `.collapse` y `.nav-scrollable`, y las dos nuevas `session-info` y `button.nav-link`), y `NavMenu.razor` usa clases que existen. La Web compila con `-warnaserror` y el recorrido manual en Chrome funciona (menú y «Cerrar sesión»). No hay copia del original para comparar y el aspecto visual no se evaluó (se desconoce si la plantilla llevaba alguna regla más, por ejemplo `width: 100%` en los enlaces); no se detecta pérdida funcional.

## README

Contrastado con el código y con mis ejecuciones: puertos 5274 y 5432 solo en `127.0.0.1`, claves obligatorias de compose, generación de clave con `pwsh`, cuatro usuarios de demostración con contraseña pública, JWT HS256 de 30 minutos, 12 ADR, `ci.yml` y `.config/dotnet-tools.json` existen, y la CI figura como «pendiente». Veraz. Único punto sin ejecución literal: seguir los pasos con un `.env` real (SA-23).

## Hallazgos

### CRITICAL

Ninguno abierto.

- **C1 (RESUELTO, 2026-10-14).** `.env.example` sin actualizar (bloqueo externo, D-3b-4). El usuario lo sustituyó por el contenido propuesto, se repitió la puerta de calidad completa (393/393, cobertura 100 %, sin vulnerables) y se marcaron 20.1, 21.1, 21.2, 28.1 y 28.4. SC-19 a SC-23 pasan a CUMPLE. Sin cambios de código ni de pruebas.

### WARNING

- **W1.** Tamaño: ≈ 6.258 líneas a mano frente a la referencia de 2.400–3.150 (≈ 2×); `size:exception` aceptado por el usuario.
- **W2.** D-5-2: `git --version` ejecutado por descuido; infringe la convención «sin Git» (informativo: no hay repositorio y no cambió nada).
- **W3.** SA-23 literal (seguir el README con un `.env` real hasta el login) sin ejecutar; cubierto de forma equivalente con compose y variables ficticias. Repetible tras aplicar `.env.example`.
- **W4.** Web sin pruebas automáticas (decisión de las specs): 54 escenarios (WS y UI) se sostienen solo con verificación manual en Chrome headless; sin evaluación visual, sin estado intermedio de botones, sin otros navegadores y con expiración simulada reescribiendo `exp`.
- **W5.** `NavMenu.razor.css` reconstruido sin copia del original (D-4-1); sin pérdida funcional detectada, sin comparación visual.
- **W6.** Mi matriz se apoya en la trazabilidad de tasks.md y en la ejecución de la suite completa, no en una re-trazabilidad uno a uno de los 260 escenarios; los escenarios WS y UI no se repitieron en esta fase.

### SUGGESTION

- **S1.** Probar `ReturnUrl.IsSafe` de forma automática enlazando `ReturnUrl.cs` en `ArchitectureTests` con `Compile Include` (decisión del usuario; ya anotada en Riesgos).
- **S2.** En `Development` (el modo de compose) `/openapi/v1.json` es anónimo; es el comportamiento diseñado y el puerto solo escucha en `127.0.0.1`, pero conviene recordarlo en el README.
- **S3.** Las pruebas de entrega son de texto (sin analizador YAML); candidatas a consolidar.
- **S4.** Las páginas de plantilla (`Home`, `Counter`, `Weather`) siguen en inglés junto a las páginas en español.
- **S5.** Eliminar `..\TimeTracker.rollback` al archivar (decisión ya tomada por el usuario).

## Conclusión y archivado

- **Veredicto: PASS WITH WARNINGS.** C1 resuelto; W1–W6 y S1–S5 se conservan sin cambios (ninguno bloqueante).
- **Recuentos finales:** 62 requisitos, 260 escenarios: 259 CUMPLE, 1 PARCIAL (SA-23), 0 INCUMPLE. Pruebas: 393/393 (Domain 49, Application 43, Arquitectura 71, Integración 230).
- **¿Archivable?** Sí. Pendiente opcional: repetir SA-23 literal con un `.env` real (W3).
