# Informe de verificación: fichajes-vista-mensual

Fecha: 2026-10-14 · Rama `feat/vista-mensual-fichajes` (desde `origin/main`) · Modo: openspec · Strict TDD: sí · Veredicto: **PASS** (0 CRITICAL).

## Resultados automáticos

| Comprobación | Resultado |
|---|---|
| `dotnet build TimeTracker.sln -c Release -warnaserror` | 0 advertencias, 0 errores |
| `dotnet format TimeTracker.sln --verify-no-changes --no-restore` | Sin cambios pendientes |
| `dotnet test` completo | **766/766** en verde: Domain 67, Application 131, Web 180, Arquitectura 74, Integración 314 (PostgreSQL 18 con Testcontainers). Antes del cambio: 579 |
| `check-coverage.ps1` Domain y Application | Líneas 100,0 %, ramas 100,0 % en ambos |
| `dotnet list package --vulnerable --include-transitive` | Ningún proyecto con paquetes vulnerables |
| `docs/api/openapi.json` | Regenerado: `from` y `to` (consulta) en los dos listados y `400` declarado; `OpenApiContractTests` ampliado y en verde |

## Comprobación real

Entorno: API y PostgreSQL con Compose (proyecto `tt-vista`, valores ficticios, `Seed__DemoUsers` y `Seed__DemoData`), Web con `dotnet run --launch-profile http` y, después, la imagen real de la Web (`deploy/web/Dockerfile`, contenedor `tt-vista-web`) con su CSP (`style-src 'self'`, sin `unsafe-inline`). Chrome headless controlado con DevTools, zona `Europe/Madrid` (y `Asia/Tokyo` para contrastar). Capturas en el espacio temporal de la sesión.

### Reproductor del Inicio

- Anónimo: sin reproductor, texto y «Iniciar sesión»; ninguna petición a `/api/time-entries`.
- Responsable (Marta): ciclo completo en vivo. Sin fichar (un solo botón «Iniciar jornada»), trabajando (cronómetro del tramo y total de hoy avanzan por segundo; «Pausa», «Comida», «Finalizar jornada»), en pausa y en comida («Reanudar» y «Finalizar jornada»; el total de hoy deja de avanzar y el tramo cuenta la pausa), reanudar y finalizar. Los `aria-label` contienen el texto visible; foco visible de 3 px; botones de 52 px de alto; legible en 390 px (botones de dos en dos) y en escritorio.
- Recargar la página en comida: el cronómetro continúa desde las marcas del servidor.
- Estado obsoleto: otra pestaña termina la pausa por la API y la vista antigua pulsa «Reanudar»: la API responde `409`, se muestra «Solo puede terminar una pausa si hay una en curso.» y el reproductor se recarga con «Trabajando».
- Tras iniciar sesión se aterriza en `/` (sin pasar por `/fichajes`).

### `/fichajes`

- Mes actual y meses anteriores con las ventanas esperadas (octubre de 2026 en Madrid: `from=2026-09-30T22:00:00Z`, `to=2026-10-31T23:00:00Z`; en Tokio: `2026-09-30T15:00:00Z` a `2026-10-31T15:00:00Z`). «Mes siguiente» y «Mes actual» deshabilitados en el mes actual; no se pide ningún mes futuro. Agosto de 2026 sin datos: «No hay fichajes en agosto de 2026.» con totales a cero.
- Coherencia con la base de datos (Luis, octubre): trabajo 70 h 7 min, pausas 2 h 2 min, comidas 8 h 54 min y 9 días con fichajes, idénticos a la consulta SQL; las franjas del 14 de octubre (07:57–10:28, 10:28–10:45, 10:45–12:55, 12:55–13:55, 13:55–17:00 en hora local) coinciden con `time_entries` en `Europe/Madrid`.
- Barras: a escala sobre un eje común (06:00–22:00; ampliado a 0–24 con una franja nocturna y a 5–24 con una de 05:30 a 23:30), marcas legibles, leyenda, total del día (trabajado, pausas y comidas), días sin fichajes atenuados, fines de semana diferenciados, hoy resaltado, tooltip (`<title>`) y `aria-label` por franja y por día, y detalle desplegable con tabla.
- Intervalo que cruza medianoche (22:00–02:00): 22:00–24:00 el primer día y 00:00–02:00 el siguiente. Cambio de hora: 29 de marzo de 2026 (pausa de 01:30 a 03:30, 1 hora real) y 26 de octubre de 2025 (pausa de 02:30 a 02:30, 1 hora real; franja mínima visible); es el caso marginal documentado en el ADR 0016.
- Tramo abierto de hoy dibujado hasta «ahora» con el total de hoy incluido; estado «Trabajando» y enlace «Ir al Inicio para fichar»; sin botones de fichar.
- Aislamiento: Ana ve en `/fichajes` solo lo suyo (68 h 58 min, igual que cuando Marta la consulta) y en `/equipo` obtiene «No tiene permiso para ver esta página.».

### `/equipo`

- Marta ve a Ana y a Luis y no a Pablo; al elegir a Luis aparece su mes; «Mes anterior» conserva el selector; cambiar a Ana conserva el mes; «Mes actual» vuelve; sin botones de fichar ni de editar. Las peticiones llevan `team/members/{id}/time-entries?from=…&to=…`.

### Consola y CSP

- Con la imagen real de la Web (CSP con `style-src 'self'`), las barras se pintan con color (atributos SVG y clases CSS) y no hay errores de consola, violaciones de CSP ni errores de CORS (solo los avisos informativos de autorización de Blazor ya existentes). Con el servidor de desarrollo el resultado es el mismo.

## Matriz de especificaciones

| Capacidad | Requisitos del delta | Evidencia |
|---|---|---|
| `time-tracking` | Ventana de consulta; GetTimeEntries y total; Contrato REST | `TimeWindowTests`, `GetTimeEntriesUseCaseTests`, `TimeEntryListResultTests`, `TimeEntryRepositoryTests`, `TimeEntryWindowTests`, `OpenApiContractTests` |
| `team-access` | Fichajes de un miembro | `GetTeamMemberTimeEntriesUseCaseTests`, `TeamWindowTests` (incluye `404` idéntico y `400` previo al miembro) |
| `time-entries-ui` | Reproductor, `/fichajes` mensual, barra de franjas, hora local, errores, cliente tipado | `TimelineBuilderTests`, `TimelineAxisTests`, `TimelineTextTests`, `PlayerSnapshotTests`, `MonthRefTests`, `DurationFormatterTests`, `SpanishDatesTests`, `BrowserTimeZoneTests`, `TimeWindowQueryTests`, `TimeEntryPresentationTests` y la comprobación real |
| `web-session` | Página Equipo, Página de inicio, Página de inicio de sesión | `ReturnUrlTests` y la comprobación real |

## Advertencias

- Un navegador con la zona o el reloj mal configurados desplaza los días; el servidor no se ve afectado (ADR 0016).
- Los totales de la API (intervalos cerrados devueltos, sin recortar) pueden diferir de los de la vista mensual en intervalos que cruzan medianoche o el fin de mes; la Web usa los suyos.
- Despliegue: publicar primero la API (el cambio es aditivo) y después la Web; una Web nueva contra una API antigua pediría el mes pero recibiría el listado antiguo.
- No se probó con un lector de pantalla real; la accesibilidad se verificó por nombres accesibles, foco, tamaños y contraste calculado (texto de botones sobre su color entre 5,1:1 y 6,5:1; texto atenuado entre 5,8:1 y 7,4:1; franjas frente a la pista 3,7:1 o más). La comida se distingue también por un rayado (patrón SVG, comprobado con la CSP real).
