# Especificación: demo-data

## Purpose

Define el sembrador de fichajes de demostración: conversión de hora local de España a UTC, agendas deterministas por perfil, siembra idempotente con ventana móvil y el servicio de arranque que la ejecuta. Capacidad NUEVA (cambio `datos-demo-mes`); todos los requisitos son `ADDED`.

Nombres fijados en el diseño: `SeedOptions.DemoData`, `SpainTime`, `DemoScheduleProfile`, `DemoScheduleProfiles`, `DemoScheduleGenerator`, `SeedDemoTimeEntriesUseCase`, `IDemoTimeEntryStore`, `DemoDataSeeder`.

## ADDED Requirements

### Requirement: Hora local de España a UTC

`SpainTime` DEBE convertir una fecha y hora locales de España peninsular a un instante UTC y un instante UTC a la fecha local, con la regla de la UE: horario de verano (UTC+2) desde las `01:00Z` del último domingo de marzo hasta las `01:00Z` del último domingo de octubre, y UTC+1 el resto del año. NO DEBE depender de la base de datos de zonas horarias del sistema (la imagen Alpine no la incluye).

#### Scenario: Invierno
- DADO el `15` de enero de 2026 a las `08:00` locales
- CUANDO se convierte a UTC
- ENTONCES el instante es `07:00Z`

#### Scenario: Verano
- DADO el `15` de julio de 2026 a las `08:00` locales
- CUANDO se convierte a UTC
- ENTONCES el instante es `06:00Z`

#### Scenario: Cambio de hora de octubre
- DADO que el último domingo de octubre de 2026 es el día `25`
- CUANDO se convierten las `08:00` locales del viernes `23` y del lunes `26`
- ENTONCES los instantes son `06:00Z` y `07:00Z`

#### Scenario: Cambio de hora de marzo
- DADO que el último domingo de marzo de 2026 es el día `29`
- CUANDO se convierten las `08:00` locales del viernes `27` y del lunes `30`
- ENTONCES los instantes son `07:00Z` y `06:00Z`

#### Scenario: Fecha local desde UTC
- DADO el instante `2026-10-13T22:30:00Z` (verano, UTC+2)
- CUANDO se obtiene la fecha local
- ENTONCES es el `14` de octubre de 2026

### Requirement: Agenda determinista por perfil

`DemoScheduleGenerator` DEBE producir, para un empleado, un perfil y una fecha, la lista de intervalos de ese día, cerrados, consecutivos y sin solapes, con la secuencia `Work → (Break | MealBreak → Work)* → fin`, construidos con `TimeEntry.Start`, `Pause`, `Resume` y `Close`. El resultado NO DEBE depender de ningún reloj ni de aleatoriedad no controlada: para los mismos datos produce los mismos instantes y tipos, y cada día depende solo de la fecha y de la semilla del perfil. Sábados y domingos DEBEN quedar sin intervalos. DEBE haber un día de ausencia cada 12 días laborables (desfasado por perfil), de modo que una ventana de 30 días contiene uno o dos. Las horas de entrada, salida y pausas varían cada día en minutos alrededor de los valores del perfil; una pausa solo se incluye si cabe entre la anterior (o la entrada) y la salida con al menos 15 minutos de trabajo a cada lado.

#### Scenario: Determinismo
- DADO un perfil, un empleado y una fecha
- CUANDO se genera el día dos veces
- ENTONCES los tipos y los instantes de ambas listas son idénticos

#### Scenario: Sin solapes y todos cerrados
- DADO cualquier día generado
- CUANDO se recorren sus intervalos
- ENTONCES todos están cerrados, cada inicio coincide con el fin del anterior y el orden respeta Work → pausa → Work

#### Scenario: Fines de semana libres
- DADO un sábado y un domingo
- CUANDO se genera el día
- ENTONCES la lista está vacía

#### Scenario: Ausencias
- DADO una ventana de 30 días consecutivos
- CUANDO se cuentan los días laborables sin intervalos
- ENTONCES hay uno o dos por empleado

#### Scenario: Perfiles distintos
- DADO los cuatro perfiles y el mismo día laborable
- CUANDO se comparan sus agendas
- ENTONCES difieren entre sí en horas de entrada, salida o número de pausas

#### Scenario: Teletrabajo de Ana
- DADO el perfil de Ana en miércoles
- CUANDO se genera el día
- ENTONCES no hay ningún `MealBreak` y sí pausas `Break`

#### Scenario: Pausa que no cabe
- DADO un perfil cuya pausa se solapa con la anterior o con la salida
- CUANDO se genera el día
- ENTONCES la pausa se omite y la agenda sigue siendo válida

#### Scenario: Conversión correcta a UTC
- DADO un día laborable de verano y otro de invierno con el mismo perfil
- CUANDO se generan
- ENTONCES las entradas locales son iguales y las horas UTC difieren una hora

### Requirement: Siembra idempotente con ventana móvil

`SeedDemoTimeEntriesUseCase` DEBE generar, para cada empleado demo, los días de los últimos 30 días hasta AYER según la fecha local de España del `TimeProvider` inyectado (hoy NO se siembra). Si el empleado ya tiene algún intervalo cuyo inicio cae en el día local, DEBE omitir ese día completo. DEBE persistir todos los intervalos nuevos con una única llamada a `IDemoTimeEntryStore.AddRangeAsync` (atómica) y devolver solo contadores: días creados, días omitidos e intervalos creados. Los días sin actividad por fin de semana o ausencia no cuentan como creados ni omitidos.

#### Scenario: Primera siembra
- DADO un empleado sin intervalos y hoy `2026-10-14`
- CUANDO se ejecuta la siembra
- ENTONCES hay intervalos entre el `14` de septiembre y el `13` de octubre y ninguno el `14`

#### Scenario: Reejecución
- DADO la siembra ya ejecutada
- CUANDO se ejecuta otra vez el mismo día
- ENTONCES no se crea ningún intervalo y los días ya sembrados cuentan como omitidos

#### Scenario: Ventana móvil
- DADO la siembra ejecutada y que pasan dos días
- CUANDO se ejecuta otra vez
- ENTONCES solo se añaden los días nuevos hasta ayer

#### Scenario: Día con fichajes previos
- DADO un empleado con un intervalo ya existente un día de la ventana
- CUANDO se ejecuta la siembra
- ENTONCES ese día se omite y el resto se crean

### Requirement: Perfiles de los usuarios demo

Cada usuario demo DEBE tener un perfil propio: `marta.responsable` jornada regular de 09:00 a 17:30 con comida y reuniones (pausas variables); `luis.empleado` madrugador de 08:00 a 17:00 con comida de 60 minutos y una pausa de unos 15 minutos casi todos los días; `ana.empleada` de 09:30 a 18:30 con comida más larga, alguna pausa de tarde y teletrabajo los miércoles sin `MealBreak` (la comida se registra como pausa normal); `pablo.empleado` media jornada de 08:00 a 14:00 con una sola pausa corta y sin comida.

#### Scenario: Pablo sin comida
- DADO el perfil de Pablo
- CUANDO se generan 30 días
- ENTONCES ningún intervalo es `MealBreak` y cada día tiene como máximo una pausa

#### Scenario: Luis con comida de una hora
- DADO el perfil de Luis
- CUANDO se genera un día laborable
- ENTONCES hay un `MealBreak` de unos 60 minutos (con variación de minutos)

### Requirement: Servicio de siembra de datos de demostración

Con `Seed:DemoData` = `true` (que exige `Seed:DemoUsers` = `true`), la API DEBE sembrar los fichajes tras la siembra de usuarios y las migraciones, antes de atender peticiones; los fallos (por ejemplo un usuario demo ausente o la base no disponible) DEBEN impedir el arranque con un mensaje claro. DEBE registrar solo contadores, nunca datos personales ni credenciales. Con `Seed:DemoData` = `false` (valor por defecto) NO DEBE crear ningún intervalo.

#### Scenario: Siembra activada
- DADO una base migrada vacía, `Seed__DemoUsers=true` y `Seed__DemoData=true`
- CUANDO la API arranca
- ENTONCES cada usuario demo tiene intervalos de días pasados, todos cerrados, ninguno de hoy y ninguno en fin de semana

#### Scenario: Idempotente al reiniciar
- DADO la siembra ya ejecutada
- CUANDO la API arranca otra vez
- ENTONCES el número de intervalos no cambia

#### Scenario: Convivencia con fichajes previos
- DADO un fichaje previo de Luis un día de la ventana
- CUANDO la API arranca con la siembra activa
- ENTONCES ese día conserva solo su fichaje previo

#### Scenario: Aislamiento del equipo
- DADO la base sembrada
- CUANDO `marta` consulta los fichajes de Luis, de Ana y de Pablo
- ENTONCES recibe datos de Luis y de Ana y `404` para Pablo

#### Scenario: Desactivada por defecto
- DADO `Seed__DemoUsers=true` sin `Seed__DemoData`
- CUANDO la API arranca
- ENTONCES no existe ningún intervalo

#### Scenario: Registro sin datos personales
- DADO un registrador que captura los mensajes
- CUANDO la API arranca con la siembra activa
- ENTONCES los mensajes contienen contadores y ningún correo ni contraseña
