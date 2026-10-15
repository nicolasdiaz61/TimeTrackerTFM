# Propuesta: fichajes-vista-mensual

## Intención

La interfaz de fichajes es funcional pero poco clara: los botones de fichar viven en una tabla de intervalos sueltos mostrados en UTC, con un listado plano de los últimos 300 intervalos. Se rediseña para que fichar sea inmediato (un control tipo reproductor en el Inicio) y para que el historial se entienda de un vistazo (una barra de franjas horarias por día, agrupada por mes, en hora local).

## Alcance

Dentro:

- **Inicio (`/`)**: control tipo reproductor para cualquier persona con sesión (empleado o responsable): «Iniciar jornada», «Pausa», «Comida», «Reanudar» y «Finalizar jornada», con indicador de estado y cronómetro en vivo (tramo actual y total trabajado hoy). Un anónimo no lo ve.
- **`/fichajes`**: pasa a ser de consulta. Muestra el estado actual, un enlace claro al Inicio para fichar y el historial **por mes**, un día por fila, con una barra de franjas (Trabajo, Pausa, Comida) a escala sobre un eje horario común, leyenda, texto accesible, total del día y totales del mes.
- **Navegación por mes** (anterior, siguiente, mes actual; sin meses futuros) y mes en español («octubre de 2026»). La vista del responsable en `/equipo` es la misma, de solo lectura, y el selector de miembro se conserva al cambiar de mes.
- **API**: `GET /api/time-entries` y `GET /api/team/members/{memberId}/time-entries` aceptan `from` y `to` (instantes UTC, intervalo semiabierto) con validación en español; el estado sale siempre del intervalo abierto, no de la ventana.
- **Hora local**: la Web deja de mostrar UTC y presenta días y horas en la zona horaria del navegador (JS interop mínimo, sin base de datos de zonas en WebAssembly).
- Lógica de maquetación en clases puras con pruebas xUnit (franjas, agrupación por día, límites de mes, navegación, formato de duración).
- Documentación: ADR 0016, README y especificaciones (`time-entries-ui`, `time-tracking`, `team-access`, `web-session`).

Fuera: edición o corrección de fichajes, festivos, exportación, paginación dentro del mes, preferencia de zona horaria distinta de la del navegador, pruebas de componentes (sin bUnit), librerías de gráficos.

## Enfoque

La API sigue en UTC (ADR 0010) y no conoce zonas horarias: la Web calcula en hora local los límites del mes (`00:00` local del primer día y del mes siguiente) y los envía como instantes UTC. La API devuelve los intervalos que **solapan** la ventana y la Web recorta cada uno por día local. La barra es un SVG en línea con atributos numéricos (`x`, `width`) y color por clases CSS: compatible con la CSP `style-src 'self'` sin `unsafe-inline`. El desfase por instante lo da una función JS de pocas líneas (`getTimezoneOffset`) detrás de la interfaz `ILocalTimeZone`, de modo que el cambio de hora de marzo y octubre se resuelve con el calendario real del navegador y la lógica pura se prueba con una zona de prueba. Detalle y alternativas en `design.md` y en el ADR 0016.

## Riesgos y reversión

- **Cambio de contrato aditivo:** `from`/`to` son opcionales; sin ellos la API conserva el comportamiento previo (últimos 300 intervalos), de modo que una Web antigua sigue funcionando durante un despliegue escalonado. Una Web nueva contra una API antigua ignoraría `from`/`to`: se despliega la API primero.
- **Hora local depende del navegador:** si el reloj o la zona del dispositivo son incorrectos, los días se desplazan; el servidor no se ve afectado.
- **Intervalos que cruzan medianoche:** se reparten entre días en la vista; los totales de la API (de los intervalos devueltos, sin recortar) pueden diferir de los de la Web en esos casos; la vista mensual usa siempre los recortados.
- **Reversión:** revertir los commits de la rama; la API sin `from`/`to` no cambia y no hay migraciones ni datos nuevos.

## Criterios de éxito

Build sin avisos, formato, pruebas, cobertura de Domain y Application al 100 %, auditoría de paquetes limpia, contrato OpenAPI regenerado y verde; comprobación visual real en Chrome (Inicio en los cuatro estados, ciclo en vivo con cronómetro, meses anteriores y vacíos, vista del responsable, aislamiento, móvil y escritorio, consola sin errores CORS ni CSP, imagen real de la Web con su CSP).
