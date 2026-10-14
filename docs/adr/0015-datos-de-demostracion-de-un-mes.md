# ADR 0015: Datos de demostración de un mes

- Estado: Aceptado
- Fecha: 2026-10-14

## Contexto

La demostración desplegada tenía cuatro usuarios pero casi ningún fichaje: no enseñaba el historial, las pausas, las comidas ni la vista del responsable. Hace falta un mes de fichajes inventados, distintos para cada usuario, que se mantenga actualizado sin duplicar datos y sin romper los fichajes que alguien haga en vivo.

## Decisión

- **Bandera propia `Seed__DemoData`** (por defecto `false`), válida solo con `Seed__DemoUsers=true`; la combinación inversa falla el arranque nombrando la clave. No se toca `.env.example`: se documenta solo en el README.
- **Generador puro en Application** (`DemoScheduleGenerator`): `(empleado, perfil, fecha) -> intervalos`, construido con `TimeEntry.Start/Pause/Resume/Close`, de modo que el dominio valida cada transición. Los perfiles (Marta, Luis, Ana, Pablo) son datos (`DemoScheduleProfile`), no ramas por usuario.
- **Determinismo sin `System.Random`:** SplitMix64 propio, con estado inicial `semilla ^ día`. Cada día depende solo de su fecha, así que la ventana móvil reproduce exactamente los días ya sembrados.
- **Hora local de España sin base de datos de zonas horarias** (`SpainTime`): la imagen `aspnet:10.0-alpine` no incluye `tzdata`. Se implementa la regla de la UE (último domingo de marzo y de octubre a las 01:00Z). Alternativa: instalar `tzdata` en la imagen (más superficie y cambia el despliegue).
- **Ventana móvil de 30 días hasta ayer; hoy queda libre.** Idempotencia por día y empleado: si hay algún intervalo cuyo inicio cae en el día local, ese día se omite (cubre fichajes manuales previos y reinicios). Sin columnas nuevas ni migración.
- **Una transacción:** un único `SaveChanges` (`IDemoTimeEntryStore.AddRangeAsync`). Un fallo (usuario ausente, base caída) impide el arranque. Solo se registran contadores.
- **Servicio hospedado `DemoDataSeeder`** registrado tras `DemoUsersSeeder` (los servicios hospedados arrancan en orden de registro) y tras las migraciones (`StartingAsync`).
- **Límite del listado de 50 a 300 intervalos.** Un mes con pausas genera de 100 a 150 intervalos por persona; con 50 la demostración mostraría solo la última semana y media y los totales serían parciales. El límite sigue siendo fijo y no controlable por el cliente; el contrato OpenAPI no cambia.

## Consecuencias

- Cada arranque añade solo los días nuevos hasta ayer; los datos antiguos no se borran y, con el tiempo, los más viejos salen del listado (no de la base de datos).
- Dos instancias de la API arrancando a la vez podrían duplicar días (no hay candado). La demostración usa una sola instancia; si se escalara, habría que añadir un candado consultivo o una restricción de unicidad por día.
- La respuesta del listado puede llegar a unas 300 filas. La siguiente mejora natural es paginar o filtrar por fechas con totales por día (ya anotado en el ADR 0014).
- Desactivar `Seed__DemoData` no borra lo sembrado.
- La regla de cambio de hora está codificada: si la UE la modificara, habría que actualizar `SpainTime`.

## Alternativas

- **Que `Seed__DemoUsers` implique los datos:** cambiaría el comportamiento de los despliegues existentes sin avisar.
- **`System.Random` con semilla:** la secuencia no está garantizada entre versiones de .NET.
- **`TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid")`:** depende de `tzdata`/ICU del sistema; fallaría en la imagen Alpine.
- **Un script SQL o una migración con datos:** mezcla datos de demostración con el esquema y no se puede apagar por configuración.
- **Una clave de idempotencia por intervalo:** exigiría una columna nueva y una migración.
