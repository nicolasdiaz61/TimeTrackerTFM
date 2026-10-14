# Informe de archivo: autenticacion-y-roles

Fecha: 2026-10-14 · Fase: sdd-archive · Modo: openspec · Veredicto de verificación: **PASS WITH WARNINGS** (0 CRITICAL) · No se ejecutó ningún comando `git` (no existe `.git`). No se leyó `.env` ni `.env.example`.

Destino del archivo: `openspec/changes/archive/2026-10-14-autenticacion-y-roles/` (el movimiento de la carpeta lo ejecuta el orquestador; este agente no tiene shell).

## Resumen

El cambio sustituye la identidad provisional de `Development` por autenticación real: inicio de sesión con correo y contraseña (PBKDF2 mediante `PasswordHasher`), JWT HS256 propio de 30 minutos validado de forma estricta, política de respaldo autenticada, límite de 10 intentos de login por minuto y por IP, cabeceras de seguridad, errores de acceso como ProblemDetails con CORS, roles `Employee` y `Manager`, tabla `users` (migración `AddUsers`), equipo como reportes directos, acceso de solo lectura del responsable a los fichajes de su equipo con control por rol y por objeto (404 idéntico), siembra opcional de cuatro usuarios de demostración y, en la Web, sesión en `sessionStorage`, `/login`, `/equipo`, rutas protegidas y menú según el rol. Se añaden los ADR 0011 (JWT propio, roles y equipo; sustituye al 0009) y 0012 (sesión en la Web y riesgo XSS), compose publica la API en `127.0.0.1:5274` y el README documenta los usuarios de prueba.

Evidencia final (verify-report.md): 393/393 pruebas verdes (Domain 49, Application 43, Arquitectura 71, Integración 230), cobertura Domain 100 % (líneas y ramas) y Application 100 %, sin paquetes vulnerables, `build -warnaserror` y `format --verify-no-changes` en 0, y verificación manual de la Web con Chrome real (headless). Matriz del verify: 62 requisitos y 260 escenarios de los deltas, 259 CUMPLE, 1 PARCIAL (SA-23), 0 INCUMPLE.

Puerta de tareas: según el verify-report, las 127 tareas (28 fases) están marcadas `[x]` (las cinco abiertas 20.1, 21.1, 21.2, 28.1 y 28.4 se cerraron el 2026-10-14). No se hizo ninguna reconciliación de casillas durante el archivo.

## Trazabilidad

Modo `openspec`: los artefactos viven en la carpeta del cambio; no se consultó Engram, por lo que no hay identificadores de observación que registrar.

| Artefacto | Ruta (antes del movimiento) |
|---|---|
| Exploración | `openspec/changes/autenticacion-y-roles/exploration.md` |
| Propuesta | `openspec/changes/autenticacion-y-roles/proposal.md` |
| Especificaciones delta (9) | `openspec/changes/autenticacion-y-roles/specs/{authentication,team-access,web-session,time-tracking,startup-configuration,solution-architecture,time-entries-persistence,time-entries-ui,api-health}/spec.md` |
| Diseño | `openspec/changes/autenticacion-y-roles/design.md` |
| Tareas | `openspec/changes/autenticacion-y-roles/tasks.md` |
| Informe de verificación | `openspec/changes/autenticacion-y-roles/verify-report.md` |
| Este informe | `openspec/changes/autenticacion-y-roles/archive-report.md` |

## Especificaciones fusionadas

No existe `.git` ni shell: los recuentos se obtuvieron contando a mano los requisitos (`### Requirement:`) y escenarios (`#### Scenario:`) de los textos leídos (inicial + delta), porque no había herramienta de búsqueda con expresiones regulares disponible en esta ejecución.

| Dominio | Acción | Requisitos (antes → después) | Escenarios (antes → después) | Detalle |
|---|---|---|---|---|
| `authentication` | Creada | 0 → 14 | 0 → 61 | 14 ADDED copiados íntegros |
| `team-access` | Creada | 0 → 12 | 0 → 63 | 12 ADDED copiados íntegros |
| `web-session` | Creada | 0 → 10 | 0 → 41 | 10 ADDED copiados íntegros |
| `time-tracking` | Actualizada | 11 → 11 | 48 → 52 | 1 ADDED («Identidad del empleado desde el token», 7 escenarios, en el lugar del retirado), 1 MODIFIED («Contrato REST de fichajes», +1 escenario «Token caducado», 401 declarado), 1 REMOVED («Identidad provisional mediante ICurrentUser», 4 escenarios); Purpose actualizado |
| `startup-configuration` | Actualizada | 9 → 11 | 24 → 38 | 3 ADDED (Clave de firma del JWT, Siembra de demostración, Arranque sin identidad provisional; 16 escenarios), 3 MODIFIED («Configuración por variables de entorno», «Documentación de claves en `.env.example`» con 5 escenarios, «Puerto configurable por entorno»), 1 REMOVED («Identidad provisional solo en Development», 4 escenarios) |
| `solution-architecture` | Actualizada | 12 → 14 | 32 → 42 | 2 ADDED (Paquetes de autenticación acotados, Sin restos de la identidad provisional; 6 escenarios), 5 MODIFIED (Imagen Docker, Docker Compose con 6 escenarios, Puerta de calidad, ADR y README con 6 escenarios, Exportación del contrato OpenAPI); Purpose actualizado (12 ADR, puerto 5274, Web con `/login` y `/equipo`) |
| `time-entries-persistence` | Actualizada | 9 → 10 | 21 → 25 | 1 ADDED (Sin clave foránea de fichajes a usuarios, 2 escenarios), 4 MODIFIED (Migraciones con `AddUsers` +1 escenario, Bandera de migraciones, Conexión perezosa, Pruebas con Testcontainers +1 escenario); Purpose actualizado |
| `time-entries-ui` | Actualizada | 5 → 5 | 13 → 17 | 3 MODIFIED (Página `/fichajes` +2 escenarios, Mensajes de error +1, Cliente tipado +1); Purpose actualizado |
| `api-health` | Actualizada | 4 → 4 | 8 → 9 | 1 MODIFIED («Respuesta de salud», +1 escenario «Método no permitido sin token») |
| `worked-hours` | Sin cambios | 0 delta | 0 delta | No participa en este cambio |

Totales de las specs principales tras la fusión: 10 specs en `openspec/specs/` (las nueve afectadas más `worked-hours`, sin tocar). Las nueve afectadas suman **91 requisitos** (14+12+10+11+11+14+10+5+4) y **348 escenarios** (61+63+41+52+38+42+25+17+9).

Coherencia con los deltas: los deltas aportan 62 requisitos y 260 escenarios propios (los de la matriz del verify). Los 62 son 14+12+10 de las specs nuevas más los 26 restantes distribuidos entre las demás (time-tracking 3, startup-configuration 7, solution-architecture 7, time-entries-persistence 5, time-entries-ui 3, api-health 1 = 26) y los 260 escenarios son 61+63+41 de las nuevas más 95 de las modificadas (time-tracking 14, startup-configuration 24, solution-architecture 27, time-entries-persistence 14, time-entries-ui 13, api-health 3). Los escenarios de MODIFIED que sustituyen a otros ya existentes explican que el aumento neto sobre las specs principales sea menor que el recuento del delta.

Pasos de fusión relevantes:
- Los dos requisitos REMOVED se eliminaron tras comprobar sus notas `Reason` y `Migration`; su contenido migra a «Identidad del empleado desde el token» y «Arranque sin identidad provisional».
- Los requisitos MODIFIED sustituyen al texto completo (descripción y escenarios); los no tocados se conservan sin cambios (p. ej. «Errores 409 como ProblemDetails en español», «Flujo completo y pruebas del caso de uso», «Opciones tipadas validadas al arrancar», «CORS restrictivo sin comodines», «Cadena de conexión obligatoria», «Sin información sensible»).
- No hubo RENAMED en este cambio.
- Pequeñas actualizaciones de texto fuera de los deltas, necesarias para que los Purpose y las notas no queden obsoletos: se retiró «autenticación real y roles» de los «Fuera de alcance» de `time-tracking`; se sustituyó «Supuesto (a confirmar en diseño)» de `api-health` por la descripción vigente de `/health`; en `startup-configuration` la nota del puerto cita solo `verify-report.md`.

## Hallazgos abiertos del verify (ninguno bloqueante)

WARNING:
- **W1.** Tamaño: ≈ 6.258 líneas escritas a mano frente a la referencia de 2.400–3.150 (≈ 2×); `size:exception` aceptado por el usuario.
- **W2.** `git --version` ejecutado por descuido por agentes (D-5-2), contra la convención «sin Git»; informativo: no hay repositorio y no cambió nada.
- **W3.** SA-23 literal (seguir el README con un `.env` real hasta el login) sin ejecutar; cubierto de forma equivalente con compose y variables ficticias. Repetible cuando haya un `.env` real.
- **W4.** Web sin pruebas automáticas (decisión de las specs): 54 escenarios (WS y UI) verificados solo a mano en Chrome headless; sin evaluación del aspecto visual, sin estado intermedio de botones, sin otros navegadores y con la expiración simulada reescribiendo `exp`.
- **W5.** `NavMenu.razor.css` reconstruido sin copia del original (D-4-1); sin pérdida funcional detectada y sin comparación visual.
- **W6.** La matriz de cumplimiento no re-trazó uno a uno los 260 escenarios; se apoya en la trazabilidad de `tasks.md`, la suite completa y comprobaciones en vivo (los escenarios WS y UI no se repitieron en esa fase).

SUGGESTION:
- **S1.** Probar `ReturnUrl.IsSafe` de forma automática enlazando `ReturnUrl.cs` en `ArchitectureTests` con `Compile Include`.
- **S2.** Recordar en el README que en `Development` (modo de compose) `/openapi/v1.json` es anónimo (el puerto solo escucha en `127.0.0.1`).
- **S3.** Las pruebas de entrega son de texto (sin analizador YAML); candidatas a consolidar.
- **S4.** Las páginas de plantilla (`Home`, `Counter`, `Weather`) siguen en inglés junto a las páginas en español.
- **S5.** Eliminar `..\TimeTracker.rollback` al archivar (decisión ya tomada por el usuario).

Resuelto durante el cambio: C1 (`.env.example` sin actualizar; el usuario aplicó el archivo propuesto y las 5 pruebas rojas pasaron).

## Desviaciones principales respecto a la especificación y el diseño

- **xUnit v2 heredado:** se mantiene la pila de pruebas del proyecto (xUnit v2) en lugar de migrar a v3.
- **`LifetimeValidator` propio:** la vigencia del token se valida con un validador propio basado en el `TimeProvider` de la aplicación (no con el reloj del sistema), para poder simular el tiempo en las pruebas.
- **Clave foránea `NO ACTION`:** la FK autorreferencial `manager_id` → `users(id)` queda en `NO ACTION` en la migración generada por EF Core en lugar de `ON DELETE RESTRICT` literal; el comportamiento observable es el mismo (PostgreSQL rechaza el borrado con `23503`).
- **`ThrowOnBadRequest`:** el cuerpo ausente o ilegible del login se resuelve haciendo que el enlazador lance en lugar de devolver la respuesta por defecto, para conservar `400` `application/problem+json`.
- **`OnRejected` del 429:** la respuesta del limitador se escribe a mano en `OnRejected` para obtener `application/problem+json` con `Retry-After` y conservar las cabeceras CORS.
- **Hash PBKDF2 HMAC-SHA512 con 100.000 iteraciones:** parámetros efectivos del `PasswordHasher` de ASP.NET Core usado (formato V3); la especificación solo exige «PBKDF2 con sal aleatoria».
- Otras desviaciones menores (D1–D16, D-3b-*, D-4-*, D-5-*) están registradas en `tasks.md` y revisadas en el verify: ninguna rompe una spec salvo D-3b-4 (`.env.example`), ya resuelta.

## Riesgos aceptados

- Token en `sessionStorage`: un XSS podría leerlo (ADR 0012); mitigado por CSP en la API, sin `MarkupString` en la Web y sesión que termina al cerrar la pestaña.
- Sin refresh token: la sesión dura 30 minutos fijos y exige volver a iniciar sesión (desviación consciente de AGENTS §6, ADR 0011).
- Contraseña de demostración pública en el README (solo para la siembra local; el aviso de no activar la siembra en un despliegue real figura en el README).
- Sin clave foránea de `time_entries.employee_id` a `users` (ADR 0011): los fichajes con identidad provisional quedaron huérfanos y `docker compose down -v` los elimina.
- Limitador de login por IP tras un proxy sin `ForwardedHeaders`: todas las peticiones compartirían partición hasta configurarlo antes de desplegar.
- Un cambio de rol en base de datos no surte efecto hasta el siguiente login (el rol sale del claim del token, vigente 30 minutos).
- Estado de autenticación de la Web evaluado al cargar y al iniciar o cerrar sesión: un token que caduca durante una navegación interna se detecta en la siguiente petición (`401`) o al recargar.

## Trabajo futuro recomendado

1. Ausencias (siguiente capacidad del producto).
2. Asistente de IA de solo lectura, con herramientas que apliquen la autorización del usuario autenticado.
3. Despliegue en Railway (incluido el alojamiento estático de la Web con CSP y TLS en el ingreso).
4. Refresh token o renovación controlada de la sesión.
5. `ForwardedHeaders` antes de desplegar tras un proxy (limitador por IP real).
6. Clave foránea de `time_entries.employee_id` a `users` cuando se cumpla el disparador del ADR 0011.
7. Conectar el repositorio Git cuando el usuario lo decida (hoy no existe `.git` ni remoto).
8. Ejecutar la CI (`ci.yml` existe, nunca se ha ejecutado), y añadir hooks de Git y Dependabot.
9. E2E automático con Playwright para los escenarios de la Web (hoy solo manuales, W4) y prueba automática de `ReturnUrl.IsSafe` (S1).
10. Traducir al español las páginas de plantilla (`Home`, `Counter`, `Weather`; S4).
11. Borrar `..\TimeTracker.rollback` (copias de las tandas; S5).
12. Repetir SA-23 literal con un `.env` real (W3) y recordar en el README que `/openapi/v1.json` es anónimo en `Development` (S2).

## Verificación del archivo

- [x] Specs principales actualizadas (`openspec/specs/{authentication,team-access,web-session}` creadas; `time-tracking`, `startup-configuration`, `solution-architecture`, `time-entries-persistence`, `time-entries-ui` y `api-health` actualizadas).
- [ ] Carpeta del cambio movida a `openspec/changes/archive/2026-10-14-autenticacion-y-roles/` (pendiente del orquestador).
- [x] La carpeta contiene proposal, exploration, specs delta, design, tasks, verify-report y este informe.
- [x] `tasks.md` sin tareas sin marcar según el verify-report (127/127).
