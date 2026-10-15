# Tareas: fichajes-vista-mensual

Cada tarea de implementación empieza por su prueba (rojo), sigue con el código mínimo (verde) y termina con la limpieza (refactor). Marcar `[x]` al completar.

## 1. Application e Infrastructure: ventana de consulta

- [ ] 1.1 `TimeWindow.TryCreate` (válida, `to <= from`, más de 32 días, futuro) con sus pruebas.
- [ ] 1.2 `ITimeEntryRepository.ListInWindowAsync` en el repositorio en memoria y en `TimeEntryRepository` (solape, límites semiabiertos, abierto, tope) con pruebas de integración.
- [ ] 1.3 `GetTimeEntriesUseCase` y `GetTeamMemberTimeEntriesUseCase` con ventana opcional y estado desde `FindOpenAsync` (`TimeEntryListResult.From(entries, open)`), adaptando las pruebas existentes.

## 2. Api: contrato

- [ ] 2.1 `TimeWindowQuery` (lectura de `from` y `to`, `400` en español) y endpoints de fichajes y de equipo; pruebas de integración (ventana, `400` por regla, sin ventana, equipo, `404` idéntico con ventana).
- [ ] 2.2 Regenerar `docs/api/openapi.json` y ampliar `OpenApiContractTests`.

## 3. Web: lógica pura

- [ ] 3.1 `DurationFormatter` (forma corta y hablada, plurales) y `SpanishDates`; se elimina `WorkedTimeFormatter`.
- [ ] 3.2 `ILocalTimeZone`, `BrowserTimeZone` (con JS falso) y zona de prueba `Europe/Madrid`.
- [ ] 3.3 `MonthRef` (etiqueta, navegación, no futuro, límites UTC en marzo y octubre).
- [ ] 3.4 Constructor de la vista mensual: agrupación por día, recorte en medianoche y fin de mes, cambio de hora, tramo abierto hasta ahora, eje mínimo y ampliado, atenuado y fines de semana, totales.
- [ ] 3.5 Instantánea del reproductor (tramo actual y total de hoy) y etiquetas de las acciones.

## 4. Web: interfaz

- [ ] 4.1 `local-time.js`, registro de `ILocalTimeZone`, clientes con ventana.
- [ ] 4.2 `ClockPlayer` en el Inicio (estados, iconos, cronómetro, 409, accesibilidad).
- [ ] 4.3 `MonthNavigator` y `MonthTimelineView` (SVG, leyenda, detalle, vacío); páginas `/fichajes` (consulta) y `/equipo`.
- [ ] 4.4 Estilos en `app.css` (colores de franja, filas, responsive, foco y contraste).

## 5. Documentación y cierre

- [ ] 5.1 ADR 0016, README y ajuste de la prueba que cuenta los ADR.
- [ ] 5.2 Verificación (build, formato, pruebas, cobertura, paquetes) y comprobación real en Chrome (incluida la imagen de la Web con su CSP); `verify-report.md`.
- [ ] 5.3 Fusión de especificaciones y archivo (`archive-report.md`).
