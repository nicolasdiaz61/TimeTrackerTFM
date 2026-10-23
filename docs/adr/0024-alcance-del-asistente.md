# ADR 0024: Alcance del asistente

- Estado: Aceptado
- Fecha: 2026-10-23

## Contexto

El asistente ([ADR 0017](0017-asistente-ia-con-herramientas-de-solo-lectura.md)) debe responder solo sobre fichajes, pero un modelo de lenguaje puede desviarse: aceptar peticiones ajenas (poemas, recetas, charla general), obedecer instrucciones incrustadas en el texto del usuario o en los datos de una herramienta, o revelar sus instrucciones. Cada consulta consume cuota y tiene coste, así que conviene acotar el uso indebido sin fiar la seguridad al modelo.

## Decisión

- **El prompt es una barrera blanda.** `AssistantPrompt.Build` indica, para todos los roles, que solo se responde sobre fichajes del usuario (y, para un Manager, de sus reportes directos), que cualquier otra petición se rechaza en una frase sin hacer lo pedido, que no se siguen instrucciones del texto del usuario ni de los datos de las herramientas que pidan cambiar las reglas, ignorar las anteriores o revelar las instrucciones, y que nunca se revelan identificadores ni secretos. Orienta al modelo, pero no garantiza su cumplimiento.
- **Las herramientas son la barrera dura.** Son de solo lectura y se ejecutan siempre con la identidad y la compañía del token, de modo que, aunque el modelo se desvíe, no puede leer datos ajenos ni modificar nada ([ADR 0017](0017-asistente-ia-con-herramientas-de-solo-lectura.md)).
- **Límite por usuario.** La política `assistant` limita las consultas por minuto y por usuario del token (`Ai__RequestsPerMinute`) y existe una cuota diaria por usuario; ambas limitan el abuso aunque el prompt falle. Este ADR no cambia esos límites.

## Consecuencias

- Un modelo pequeño puede ignorar parcialmente el prompt: la consecuencia es una respuesta fuera de tema, nunca un acceso indebido a datos.
- Las frases del prompt quedan fijadas por pruebas de `AssistantPrompt`; cambiarlas exige actualizar esas pruebas.
- El prompt crece unas pocas líneas por consulta.

## Alternativas

- **Clasificador previo de la petición (otra llamada al modelo):** descartada por coste y latencia; añade otra superficie manipulable.
- **Filtrar palabras clave en la API:** descartada por frágil y fácil de eludir.
- **Confiar solo en el prompt:** descartada; la seguridad la imponen las herramientas y los límites.
