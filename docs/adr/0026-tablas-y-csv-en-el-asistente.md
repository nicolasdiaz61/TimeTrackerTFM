# ADR 0026: Tablas y CSV en el asistente

- Estado: Aceptado
- Fecha: 2026-10-23

## Contexto

Las comparaciones del equipo ([ADR 0025](0025-resumen-de-equipo-del-asistente.md)) se leen mal como texto corrido y los responsables quieren llevarse las cifras a una hoja de cálculo. El prompt exigía texto plano sin Markdown ni HTML. La salida del modelo no es de confianza (AGENTS.md §6) y la Web es solo presentación, así que no puede renderizarse como HTML ni como Markdown completo.

## Decisión

- **Prompt.** Sigue siendo texto plano, salvo que al listar o comparar varias personas o periodos el modelo puede usar una única tabla Markdown sencilla (encabezado, fila `|---|---|`, una fila por elemento, como máximo 12 columnas) y nada más de Markdown ni HTML. El resto de reglas (alcance, anti-inyección, cifras copiadas tal cual) no cambia.
- **Analizador puro en la Web (`AssistantMessageParser`).** Divide la respuesta en párrafos y tablas de tuberías al estilo GitHub; tolera tuberías inicial y final opcionales, filas irregulares (se rellenan o recortan al ancho del encabezado) y `\|` escapado. Límites: 200 filas, 20 columnas y 200 caracteres por celda (se recorta); si se superan, el bloque queda como texto. Los mensajes del usuario nunca se analizan.
- **Renderizado solo con expresiones Razor** (`@cell`) dentro de `<table class="assistant-table">` en un contenedor con `overflow-x: auto`. Nunca `MarkupString` ni HTML crudo; una prueba de arquitectura lo vigila.
- **CSV (`CsvBuilder`).** UTF-8 con BOM, separador `;`, saltos CRLF, comillado RFC 4180 y protección frente a inyección de fórmulas: una celda que empieza por `=`, `+`, `-`, `@`, tabulador o CR lleva un apóstrofo delante, salvo los números simples. La descarga (Blob y enlace temporal) pasa por `ChatViewport` y `chat.js`; no hay dependencias nuevas.

## Consecuencias

- Las comparaciones se leen como tabla y se abren en Excel sin ajustes de codificación.
- Un modelo que no respete el formato solo produce texto: el analizador degrada a párrafo, nunca a marcado.
- El CSV se genera en el navegador a partir de lo mostrado; el servidor no cambia y no se amplía ningún permiso.
- Una celda con texto que empieza por `-` o `=` aparece con apóstrofo en la hoja de cálculo: es el coste de evitar fórmulas inyectadas.

## Alternativas

- **Renderizar Markdown completo con una biblioteca:** descartada; amplía la superficie de ataque y añade una dependencia.
- **Generar el CSV en la API:** descartada; requeriría un endpoint nuevo y repetir la autorización para algo que ya está en pantalla.
- **Devolver datos estructurados desde las herramientas hasta la Web:** descartada por ahora; cambia el contrato de `POST /api/asistente`.
