# Propuesta: datos-demo-mes

## Intención

La demostración desplegada solo tiene unos pocos fichajes de prueba. Para enseñar el producto (historial, pausas, comidas, vista del responsable) hace falta un historial rico y verosímil. Se añade un sembrador de **datos de demostración**: aproximadamente un mes de fichajes inventados y distintos para cada usuario demo.

## Alcance

Dentro:

- Configuración: `Seed__DemoData` (booleano, `false` por defecto), válida solo con `Seed__DemoUsers=true`; la combinación inválida es un fallo de arranque que nombra la clave.
- Application: generador determinista de agendas por perfil (`DemoScheduleGenerator`), conversión de hora local de España a UTC con la regla de cambio de hora de la UE (`SpainTime`), caso de uso `SeedDemoTimeEntriesUseCase` y puerto `IDemoTimeEntryStore`.
- Infrastructure: `DemoTimeEntryStore` (consulta de inicios por rango e inserción en una sola transacción).
- Api: `DemoDataSeeder` (servicio hospedado que corre tras `DemoUsersSeeder`), opciones y validador.
- Cuatro perfiles: Marta (jornada regular con reuniones), Luis (madrugador), Ana (tarde, con teletrabajo los miércoles) y Pablo (media jornada).
- Idempotencia por día y empleado; ventana móvil de 30 días hasta ayer.
- Corrección mínima del límite del listado: de 50 a 300 intervalos (un mes con pausas supera los 50).
- Documentación: README, ADR 0015 y fusión de especificaciones.

Fuera: borrar o regenerar datos demo existentes, festivos oficiales, sembrar el día de hoy, paginación del listado, agrupación por día en la Web, `.env.example` (no se toca).

## Enfoque

El generador es una función pura `(empleado, perfil, fecha) -> intervalos` construida con `TimeEntry.Start`, `Pause`, `Resume` y `Close`, de modo que el dominio valida cada transición. Usa un generador pseudoaleatorio propio (SplitMix64) inicializado con la semilla del perfil y el número de día: cada día depende solo de su fecha, así que el resultado no depende de la ventana ni del orden. El caso de uso consulta, por empleado, los inicios ya existentes en la ventana y omite los días con algún intervalo. Todo se inserta con un único `SaveChanges` (transacción). Detalle y alternativas en `design.md` y en el ADR 0015.

## Riesgos y reversión

- **Demo pública con datos inventados:** son ficticios; se desactiva con `Seed__DemoData=false` (no borra lo ya creado).
- **Dos instancias arrancando a la vez** podrían duplicar días: la demo corre con una sola instancia de API; se documenta.
- **Límite del listado:** subir a 300 aumenta el tamaño de la respuesta (hasta unas 300 filas); sigue siendo acotado y no controlable por el cliente.
- **Reversión:** apagar la bandera y revertir el commit; los datos sembrados se pueden borrar a mano (`DELETE` de los intervalos de los usuarios demo).

## Criterios de éxito

Historial variado por usuario; reinicio sin duplicados; convivencia con fichajes previos del mismo día; el responsable ve a Ana y Luis y no a Pablo; la combinación inválida falla el arranque; build, formato, pruebas, cobertura (Domain y Application al 100 %) y `openapi.json` sin cambios.
