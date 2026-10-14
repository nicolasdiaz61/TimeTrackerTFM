# ADR 0003: Monolito modular y regla de dependencias

- Estado: Aceptado
- Fecha: 2026-10-11

## Contexto

TimeTracker es una aplicación de control de tiempos con un único equipo y un único despliegue. `AGENTS.md` pide un monolito modular con Clean Architecture y la regla de dependencias apuntando siempre hacia el dominio. Una regla que solo está escrita se rompe con el tiempo: hace falta que el compilador y las pruebas la hagan cumplir.

## Decisión

- Cuatro proyectos de producción: `Domain` (sin dependencias), `Application` (→ Domain), `Infrastructure` (→ Application) y `Api` (→ Application e Infrastructure; es el composition root). `Web` (Blazor WebAssembly) es independiente y no referencia ningún proyecto de `src/`.
- La regla se aplica en tres niveles:
  1. `ProjectReference` estrictos: una referencia hacia fuera forma un ciclo y no compila.
  2. Una prueba por reflexión (`DependencyRuleTests`) sobre los ensamblados compilados: cada capa solo referencia las permitidas y `Domain` solo el marco base.
  3. Una prueba que lee los `.csproj` (`RepositoryConventionsTests`): `Web` sin `ProjectReference`, todos en `net10.0` y ningún `PackageReference` con `Version`.
- Formato de solución clásico `TimeTracker.sln` (no `.slnx`).
- `Application` e `Infrastructure` quedan vacíos (solo `.csproj`) hasta que haya código real que alojar.

## Consecuencias

- Una violación de la regla falla en el build o en las pruebas, no en una revisión.
- El compilador elimina las referencias a ensamblados que el código no usa, por lo que la prueba por reflexión solo detecta referencias realmente usadas; la lectura de los `.csproj` cubre el resto.
- Al añadir un proyecto hay que actualizar la tabla de permitidos de la prueba.
- Un monolito se despliega y se escala como una unidad; si algún día hay necesidad demostrada de separar un módulo, se registrará en otro ADR.

## Alternativas

- **Microservicios:** coste operativo desproporcionado y prohibido por `AGENTS.md` sin necesidad demostrada.
- **NetArchTest u otra biblioteca de reglas de arquitectura:** una dependencia más para una regla que se comprueba con reflexión y XML.
- **Formato `.slnx`:** más moderno, pero sin soporte uniforme en todas las herramientas disponibles; se prioriza la compatibilidad.
- **Un único proyecto con carpetas:** no hay forma de que el compilador haga cumplir la regla.
