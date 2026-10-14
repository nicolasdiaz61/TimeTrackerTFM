# ADR 0010: Tiempo en UTC, instantes en segundos y minutos truncados

- Estado: Aceptado
- Fecha: 2026-10-12

## Contexto

Los fichajes manejan tiempo, uno de los datos críticos de `AGENTS.md`: el cálculo de horas debe ser exacto, determinista y probable sin depender del reloj del sistema. Hay que decidir cómo se obtiene la hora, en qué zona se guarda y se transmite, con qué precisión y cómo se convierte en minutos trabajados.

## Decisión

- **Reloj:** los casos de uso reciben `TimeProvider` (tipo del marco base, así `Application` sigue sin paquetes). La API registra `TimeProvider.System`; las pruebas usan `FakeTimeProvider`. No se crea un `IClock` propio.
- **UTC validado:** `TimeEntry` solo acepta instantes con desfase cero y lanza `ArgumentException` si no lo son. El dominio no convierte zonas.
- **Instantes en segundos:** al crear o cerrar un fichaje el instante se trunca a segundos enteros, de modo que lo que devuelve el `POST` coincide con lo que lee el `GET`.
- **Minutos truncados:** `CalculateWorked` devuelve los minutos completos, truncando los segundos sobrantes (por ejemplo, 90 min 59 s son 90 minutos y menos de un minuto son 0). Se calcula sobre instantes absolutos, así que cruzar la medianoche no cambia el resultado. El total de la lista suma solo fichajes cerrados, y solo los 50 devueltos.
- **JSON:** los instantes se serializan en UTC con sufijo `Z` y el contrato OpenAPI los declara como `date-time`. En PostgreSQL se guardan como `timestamp with time zone`.
- **Interfaz:** la Web muestra las horas en UTC con la etiqueta, en el formato `yyyy-MM-dd HH:mm 'UTC'` y cultura invariante, y no las convierte a hora local. Un fichaje abierto muestra «en curso».

## Consecuencias

- Los cálculos son deterministas y se prueban con un reloj controlado; el dominio queda al 100 % de cobertura.
- Los usuarios ven la hora en UTC, que puede no coincidir con su hora local. La zona horaria local llegará con los saldos de horas y las ausencias, que sí necesitan jornadas locales.
- La imagen Docker no incluye `tzdata` ni cultura (globalización invariante), lo que es coherente con trabajar solo en UTC.
- Truncar (en vez de redondear) puede restar hasta 59 segundos por fichaje a la suma de minutos; es una regla explícita y probada, no un error.

## Alternativas

- **`IClock` propio:** duplica `TimeProvider`, que ya es una abstracción estándar con doble de pruebas oficial.
- **Hora local o `DateTime` sin zona:** ambiguo en cambios de horario y entre servidor y navegador.
- **Redondear los minutos (`Math.Round`):** contabilizaría minutos que aún no se han trabajado.
- **Guardar la precisión completa del instante:** dos lecturas del mismo fichaje podrían diferir en fracciones de segundo y complica comparar y probar.
