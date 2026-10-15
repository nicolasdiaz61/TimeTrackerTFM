# ADR 0016: Vista mensual de fichajes y hora local en la Web

- Estado: Aceptado
- Fecha: 2026-10-14

## Contexto

La pantalla de fichajes mostraba una tabla plana de intervalos en UTC, limitada a los últimos 300, con los botones de fichar mezclados con el historial. Con un mes de datos (ADR 0015) resulta difícil de leer, no responde a «¿qué hice el martes?» y obliga a convertir las horas mentalmente. Hace falta una vista por mes, un día por fila, en la hora del usuario y un control de fichaje inmediato en el Inicio. La Web es Blazor WebAssembly estática, servida con una CSP sin `unsafe-inline`, y no puede apoyarse en una base de zonas horarias fiable.

## Decisión

- **Contrato:** `GET /api/time-entries` y `GET /api/team/members/{id}/time-entries` aceptan `from` y `to` opcionales (instantes UTC, ventana semiabierta `[from, to)`), con `400` en español si falta uno, no se leen, `to <= from`, la ventana supera 32 días o `from` es futuro. Devuelven los intervalos que solapan la ventana (tope 1000). Sin parámetros se conserva el listado de los últimos 300. El `status` sale siempre del intervalo abierto, no de la ventana.
- **Zona horaria:** la API sigue en UTC (ADR 0010). La Web obtiene el desfase de cada instante y el inicio de cada día local del navegador con un script propio mínimo (`js/local-time.js`) detrás de `ILocalTimeZone`; los límites del mes son las 00:00 locales del día 1 y del día 1 siguiente, en UTC. La lógica de recorte por día es pura y se prueba con `Europe/Madrid`.
- **Reparto por día:** el recorte de intervalos por día local se hace en la Web; los totales del mes son la suma de los tramos recortados (`floor(segundos/60)` por tramo). Los totales de la API siguen siendo los de los intervalos cerrados devueltos.
- **Barra de franjas en SVG** con `x`/`width` como atributos y color por clases CSS: compatible con `style-src 'self'`. Eje común del mes, de 06:00 a 22:00 como mínimo, ampliado a horas enteras.
- **Un único lugar de acciones:** el reproductor del Inicio. `/fichajes` y `/equipo` son de consulta.
- **Sin dependencias nuevas:** ni librerías de gráficos ni de pruebas de componentes.

## Consecuencias

- Una Web nueva contra una API antigua no acotaría la ventana: se despliega primero la API (el cambio es aditivo, así que una Web antigua sigue funcionando).
- La hora mostrada depende de la zona del navegador; el servidor no la conoce ni la guarda.
- En los dos días de cambio de hora, los tramos que cruzan el salto se dibujan con el reloj de pared (una hora de más o de menos en la escala); la duración mostrada es la real.
- Un intervalo largo repartido entre meses aparece en ambos; la API puede sumar en sus totales el intervalo entero, por lo que la vista mensual no usa esos totales.
- La Web no tiene pruebas de componentes: la maquetación se verifica con navegador real y la lógica pura con xUnit.

## Alternativas

- **`month=YYYY-MM` más desfase:** un desfase fijo falla en los meses con cambio de hora y obliga a la API a conocer zonas.
- **Reglas de zona codificadas en la Web (`SpainTime`):** solo valen para España.
- **`TimeZoneInfo.Local` con datos de zona en WebAssembly:** aumenta la descarga y no se puede probar sin navegador.
- **Estilos en línea (`style="width: …%"`):** bloqueados por la CSP; permitirlos con `unsafe-inline` debilitaría la política.
- **Botones de fichar también en `/fichajes`:** duplica los flujos de error y mezcla consulta con acción.
- **Paginar en lugar de filtrar por mes:** no ofrece la agrupación por día que se pide.
