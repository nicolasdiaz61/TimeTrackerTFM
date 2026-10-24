# ADR 0031: Hora de salida en las jornadas

- Estado: Aceptado
- Fecha: 2026-10-24

## Contexto

Las horas extra del resumen de equipo ([ADR 0027](0027-horas-extra-en-el-resumen-de-equipo.md)) se calculan contra una jornada diaria **supuesta** (`dailyHours`, 8 h por defecto): la jornada ([ADR 0030](0030-jornadas-en-el-asistente.md)) guardaba la entrada, el almuerzo, la pausa esperada y los días, pero no cuándo termina el día. Así, una media jornada de 08:00 a 12:00 se comparaba con 8 h y una persona que trabaja 4 h nunca generaba horas extra aunque se quedara el doble de lo previsto.

## Decisión

- **Hora de salida opcional (`EndTime`, `TimeOnly?`) en `WorkSchedule`.** Si existe debe ser posterior a la hora de entrada y al fin del almuerzo (inicio + duración) y dejar al menos un minuto de trabajo; no hay jornadas que crucen la medianoche. Los errores se devuelven por campo (`endTime`) con mensajes en español, como el resto de validaciones.
- **Minutos de trabajo previstos derivados (`ExpectedWorkMinutes`):** salida - entrada - almuerzo; nulo sin hora de salida. No se guarda: se calcula en el dominio, que queda al 100 % de cobertura de líneas y ramas.
- **Persistencia:** una migración aditiva (`AddWorkScheduleEndTime`) con la columna nullable `end_time` (`time without time zone`) en `work_schedules`, sin relleno de datos ni restricciones nuevas; `Down` la elimina. Es el paso «expandir» de las migraciones en dos pasos de AGENTS.md: el código anterior sigue funcionando con la columna añadida.
- **API:** las peticiones de alta y edición aceptan `endTime` opcional (compatibilidad con clientes anteriores) y las respuestas devuelven `endTime` y `expectedWorkMinutes` (nulos en jornadas antiguas). El contrato OpenAPI se regenera.
- **Web:** el formulario exige «Hora de salida» (también al editar una jornada antigua, que la muestra vacía), la tabla añade «Salida» y los resúmenes y la marca «Prevista» muestran `09:00–18:00` cuando existe.
- **Horas extra:** por persona y día laborable, lo previsto es `ExpectedWorkMinutes` de su jornada si tiene hora de salida y, si no, `dailyHours` × 60 (la suposición anterior, 8 h por defecto). Los días no laborables siguen sin tiempo previsto. Cada miembro del resumen indica `expectedBasis` (`jornada` o `supuesto`) y `expectedDailyMinutes`, y `overtimeNote` cuenta cuántas personas usan cada base; `dailyHours` solo afecta a quien no tiene hora de salida.
- **Asistente:** `get_work_schedules`, `get_my_work_schedule` y `get_team_members` añaden `endTime` (`HH:mm`), `expectedWorkMinutes` y `expectedWorkText` («X h Y min») solo cuando existen. El prompt exige indicar la base de cada persona y pasar `dailyHours` únicamente si el usuario pide otra suposición para quien no tiene hora de salida.

## Consecuencias

- Las horas extra reflejan la jornada real de cada persona (media jornada, jornada partida, madrugador) sin que el usuario tenga que dar una cifra.
- Las jornadas ya creadas conservan la suposición de `dailyHours` hasta que un Manager las edite y añada la hora de salida; no hay relleno automático porque no se puede deducir con fiabilidad.
- Una jornada puede tener una entrada, un almuerzo y una salida incoherentes con lo que la persona ficha de verdad; la hora de salida define lo previsto, no limita los fichajes.
- La salida del resumen de equipo crece en dos campos por miembro.
- Volver al código anterior exige ejecutar `Down` de la migración antes si se quiere retirar la columna; dejarla es inocuo (nullable, sin restricciones).

## Alternativas

- **Campo explícito de horas por día en la jornada:** descartado; duplica lo que ya dicen entrada, almuerzo y salida y puede contradecirlos.
- **Hora de salida obligatoria con relleno de las jornadas existentes:** descartado; el relleno inventaría datos y rompería a los clientes anteriores que no la envían.
- **Mantener solo la suposición `dailyHours`:** descartado; es la causa del problema y obliga al usuario a repetirla en cada pregunta.
