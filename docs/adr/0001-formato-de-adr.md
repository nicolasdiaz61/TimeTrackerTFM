# ADR 0001: Formato de los registros de decisiones de arquitectura

- Estado: Aceptado
- Fecha: 2026-10-11

## Contexto

`AGENTS.md` exige registrar en `docs/adr/` toda decisión de arquitectura relevante, con contexto, decisión, consecuencias y alternativas. Hace falta un formato único, corto y fácil de revisar en una pull request, para que cada decisión se escriba con poco esfuerzo y no se quede sin documentar.

## Decisión

Se usa un formato reducido inspirado en MADR. Cada ADR es un archivo `docs/adr/NNNN-titulo-en-minusculas.md` con:

- Un encabezado con el título, el **Estado** (Propuesto, Aceptado o Sustituido por el ADR NNNN) y la **Fecha**.
- Cuatro secciones, en este orden: `## Contexto`, `## Decisión`, `## Consecuencias` y `## Alternativas`.

Los ADR no se reescriben: si una decisión cambia, se crea otro ADR que sustituye al anterior y el original pasa a "Sustituido por". Solo se corrigen erratas.

## Consecuencias

- Hay trazabilidad de las decisiones con poco coste: cuatro secciones fijas y un archivo por decisión.
- El historial completo se conserva, incluidas las decisiones abandonadas.
- Una decisión implícita que no se escriba no queda registrada; la revisión de cada cambio debe preguntar si requiere un ADR.

## Alternativas

- **No escribir ADR:** las decisiones se perderían y el motivo de cada una tendría que reconstruirse a partir del código.
- **MADR completo:** añade campos (impulsores, opciones con pros y contras, confirmación) que para este tamaño de proyecto son más burocracia que valor.
