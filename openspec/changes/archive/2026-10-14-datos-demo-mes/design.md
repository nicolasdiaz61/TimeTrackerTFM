# Diseño: datos-demo-mes

## Resumen

Un servicio hospedado (`DemoDataSeeder`, Api) corre justo después de `DemoUsersSeeder`, localiza a los cuatro usuarios demo por correo y llama a `SeedDemoTimeEntriesUseCase` (Application). El caso de uso calcula la ventana (30 días hasta ayer, fecha local de España), pide al puerto `IDemoTimeEntryStore` los inicios ya existentes de cada empleado, genera con `DemoScheduleGenerator` los días que faltan y los inserta con un único `SaveChanges`.

```
Program -> DemoUsersSeeder.StartAsync -> DemoDataSeeder.StartAsync
              DemoDataSeeder: busca usuarios por correo (IUserRepository)
              -> SeedDemoTimeEntriesUseCase.ExecuteAsync(empleados)
                   por empleado: store.ListStartsBetweenAsync(ventana)
                   por día laborable: DemoScheduleGenerator.GenerateDay -> TimeEntry (dominio)
                   store.AddRangeAsync(todos los nuevos)   # una transacción
              log: días creados / omitidos / intervalos
```

## Decisiones

- **DD-1 Bandera `Seed:DemoData` separada.** Los usuarios demo son un requisito previo, no al revés. La validación (`SeedOptionsValidator`) falla con un mensaje que nombra `Seed:DemoData` y `Seed:DemoUsers` si se activa sola. Alternativa descartada: que `DemoUsers` implique datos (cambiaría el comportamiento de despliegues existentes).
- **DD-2 Generador puro en Application.** `(empleado, perfil, fecha) -> TimeEntry[]`, sin reloj ni E/S: testable al 100 %. Reutiliza `TimeEntry.Start/Pause/Resume/Close`, así que el dominio valida cada transición y nunca se insertan datos que rechazaría. Los perfiles son datos (`DemoScheduleProfile`), no código con `if` por usuario (OCP). El mapeo correo → perfil vive en `Api` (`DemoUsers`), donde ya están los correos; Application no conoce correos.
- **DD-3 Determinismo con SplitMix64 propio.** `System.Random` con semilla no garantiza la misma secuencia entre versiones de .NET. SplitMix64 son pocas líneas, estable y barato. El estado inicial mezcla la semilla del perfil con el número de día (`DateOnly.DayNumber`): cada día es independiente de la ventana y del orden, así que generar «hoy-29..ayer» un día y «hoy-28..ayer+1» al siguiente produce exactamente los mismos días solapados. Alternativa descartada: una sola secuencia por empleado (dependería del primer día de la ventana).
- **DD-4 Hora local de España sin base de datos de zonas.** La imagen final es `aspnet:10.0-alpine`, que no incluye `tzdata`; `TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid")` fallaría en producción (y quizá en CI). `SpainTime` implementa la regla de la UE vigente desde 1996 (último domingo de marzo y de octubre a las 01:00Z). Las horas ambiguas o inexistentes de la madrugada del cambio no se usan (fin de semana sin datos) y, si se pidieran, se resuelven de forma determinista. Alternativas: añadir `tzdata` a la imagen (cambia el Dockerfile y el despliegue) o `TimeZoneInfo` con la base ICU (depende del entorno).
- **DD-5 Ausencias por regla, no por azar.** Un día laborable de cada 12 (`(semanasDesdeEpoca*5 + díaLaborable + desfase) % 12 == 0`) no tiene fichajes; con desfase distinto por perfil. Una ventana de 30 días tiene 20 a 22 laborables, así que cada empleado tiene uno o dos días libres, independientemente del día en que se arranque.
- **DD-6 Pausas condicionadas.** Cada pausa del perfil tiene probabilidad, variación de inicio y de duración. Se descarta si no deja 15 minutos de trabajo antes (desde la entrada o la pausa anterior) y después (hasta la salida). Se consumen siempre los mismos números aleatorios por pausa, de modo que omitir una no cambia la variación de las demás.
- **DD-7 Idempotencia por día y empleado.** Consulta por empleado de los `ClockInAt` del rango `[00:00 local del primer día, 00:00 local de hoy)`; un día con algún inicio se omite entero. Cubre los fichajes manuales previos (por ejemplo, los de la prueba E2E) y los reinicios. Alternativa descartada: clave de idempotencia por intervalo (obligaría a una columna nueva y migración).
- **DD-8 Una transacción.** `AddRangeAsync` hace un único `SaveChanges` (EF lo envuelve en una transacción): o se siembra todo o nada. Los intervalos están cerrados, así que no interactúan con el índice único de intervalo abierto. Riesgo conocido: dos instancias arrancando a la vez podrían duplicar días; la demo usa una sola instancia (Railway) y no se añade un candado (YAGNI).
- **DD-9 Hoy queda libre.** La ventana acaba ayer para que la persona fiche en vivo sin choques con un día ya sembrado.
- **DD-10 Registro mínimo.** Un único mensaje con contadores (`{CreatedDays}`, `{SkippedDays}`, `{CreatedEntries}`); ni correos ni contraseña. Un usuario demo ausente lanza `InvalidOperationException` genérica.
- **DD-11 Límite del listado 50 → 300.** Un mes con pausas genera unos 100 a 150 intervalos por empleado: con 50 la demostración mostraría solo la última semana y media y los totales serían parciales. Se sube `GetTimeEntriesUseCase.MaxEntries` a 300 (cubre ~45 días de Luis o Ana, que tienen 5 a 7 intervalos al día) con el mismo contrato y sin parámetros de cliente; `openapi.json` no cambia. Una paginación o un filtro por fechas con totales por día es el siguiente paso natural (ADR 0014) y queda fuera de este cambio. Consecuencia: a medida que la demo envejece los días más antiguos salen de la lista, no de la base de datos.

## Perfiles (horas locales de España)

| Usuario | Jornada | Pausas | Variación |
|---|---|---|---|
| `marta.responsable` | 09:00-17:30 | Reuniones: pausa ~11:00 de 20 min (70 %) y ~16:00 de 10 min (50 %); comida ~14:00 de 45 min | ±12 min |
| `luis.empleado` | 08:00-17:00 | Pausa ~10:30 de 15 min (90 %); comida ~13:00 de 60 min | ±6 min |
| `ana.empleada` | 09:30-18:30 | Comida ~14:00 de 75 min; pausa de tarde ~17:00 de 10 min (40 %). Miércoles (teletrabajo): 09:00-17:00, pausa ~11:00 de 15 min (80 %) y pausa ~14:00 de 30 min registrada como `Break` (sin `MealBreak`) | ±10 min |
| `pablo.empleado` | 08:00-14:00 | Una pausa ~10:30 de 15 min; sin comida | ±5 min |

## Archivos

- Application: `DemoData/SpainTime.cs`, `DemoRandom.cs`, `DemoBreakPlan.cs`, `DemoDayPlan.cs`, `DemoScheduleProfile.cs`, `DemoScheduleProfiles.cs`, `DemoScheduleGenerator.cs`, `IDemoTimeEntryStore.cs`, `DemoEmployee.cs`, `DemoSeedResult.cs`, `SeedDemoTimeEntriesUseCase.cs`.
- Infrastructure: `Persistence/DemoTimeEntryStore.cs` y su registro en `AddPersistence`.
- Api: `Seeding/DemoDataSeeder.cs`, `SeedOptions.DemoData`, validador, `DemoUsers` con su perfil, registro.
- Application (límite): `GetTimeEntriesUseCase.MaxEntries = 300`.
- Documentación: README, ADR 0015, specs.

## Pruebas

- Unitarias (Application.Tests): `SpainTime` (invierno, verano, cambios de marzo y octubre, fecha local), generador (determinismo, sin solapes, orden de tipos, fines de semana, ausencias, perfiles distintos, miércoles de Ana, pausa que no cabe, UTC verano/invierno) y caso de uso (primera siembra, reejecución, ventana móvil, día con fichajes previos, hoy libre) con un almacén en memoria.
- Integración (Testcontainers): siembra real, idempotencia al reiniciar, convivencia con un fichaje previo, responsable ve a Ana y Luis y no a Pablo, `DemoData` sin `DemoUsers` falla, no booleano falla, `DemoData=false` no siembra, registro sin datos personales, almacén (rango y atomicidad).
- Límite 300: pruebas existentes de 50 pasan a 300.

## Reversión

Apagar `Seed__DemoData`; revertir el commit. Los datos ya sembrados son intervalos normales (borrables por SQL por empleado).
