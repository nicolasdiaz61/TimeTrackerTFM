# Especificación: worked-hours

## Purpose

Define el objeto de valor `WorkedHours` del proyecto `TimeTracker.Domain`: una cantidad de tiempo trabajado expresada en **minutos enteros** (sin decimales ni redondeo). Es mínimo: sirve de demostrador TDD y los fichajes lo reutilizarán. Capacidad NUEVA (no existen especificaciones previas); todos los requisitos son `ADDED`.

Supuesto de nombres (a confirmar en diseño): los miembros públicos se llaman `WorkedHours.FromMinutes(int)`, `Minutes` y el operador `+`.

## ADDED Requirements

### Requirement: Construcción desde minutos enteros

`WorkedHours` DEBE construirse a partir de un número entero de minutos mayor o igual que cero y DEBE exponer ese mismo valor en `Minutes`. NO DEBE aceptar ni almacenar decimales.

#### Scenario: Valor positivo válido
- DADO el entero `90`
- CUANDO se invoca `WorkedHours.FromMinutes(90)`
- ENTONCES `Minutes` es igual a `90`

#### Scenario: Cero es válido
- DADO el entero `0`
- CUANDO se invoca `WorkedHours.FromMinutes(0)`
- ENTONCES no se lanza ninguna excepción y `Minutes` es igual a `0`

### Requirement: Rechazo de valores negativos

`WorkedHours` NO DEBE permitir crear una instancia con minutos negativos; DEBE lanzar `ArgumentOutOfRangeException` (a confirmar en diseño) cuyo nombre de parámetro identifique el valor inválido.

#### Scenario: Un minuto negativo
- DADO el entero `-1`
- CUANDO se invoca `WorkedHours.FromMinutes(-1)`
- ENTONCES se lanza `ArgumentOutOfRangeException`

#### Scenario: Valor mínimo del entero
- DADO `int.MinValue`
- CUANDO se invoca `WorkedHours.FromMinutes(int.MinValue)`
- ENTONCES se lanza `ArgumentOutOfRangeException`

### Requirement: Suma de dos valores

`WorkedHours` DEBE permitir sumar dos instancias y devolver una nueva instancia cuyos minutos son la suma exacta de los operandos. Los operandos NO DEBEN modificarse. El cero DEBE ser elemento neutro.

#### Scenario: Suma de dos valores
- DADO `WorkedHours` de `90` minutos y de `45` minutos
- CUANDO se suman
- ENTONCES el resultado tiene `Minutes` igual a `135` y los operandos conservan `90` y `45`

#### Scenario: Cero como elemento neutro
- DADO `WorkedHours` de `0` minutos y de `30` minutos
- CUANDO se suman en cualquier orden
- ENTONCES el resultado tiene `Minutes` igual a `30`

### Requirement: Desbordamiento explícito

La suma DEBE usar aritmética comprobada: si el resultado supera `int.MaxValue` minutos, DEBE lanzar `OverflowException`. NO DEBE producir un valor envuelto (negativo) ni saturado.

#### Scenario: Suma que desborda
- DADO `WorkedHours` de `int.MaxValue` minutos y de `1` minuto
- CUANDO se suman
- ENTONCES se lanza `OverflowException`

#### Scenario: Suma en el límite exacto
- DADO `WorkedHours` de `int.MaxValue - 1` minutos y de `1` minuto
- CUANDO se suman
- ENTONCES `Minutes` del resultado es igual a `int.MaxValue`

### Requirement: Igualdad por valor

Dos instancias con los mismos minutos DEBEN ser iguales y tener el mismo código hash; instancias con minutos distintos NO DEBEN ser iguales.

#### Scenario: Mismos minutos
- DADO dos instancias creadas con `60` minutos
- CUANDO se comparan con `Equals` y `==`
- ENTONCES ambas comparaciones son verdaderas y sus códigos hash coinciden

#### Scenario: Minutos distintos
- DADO instancias de `60` y de `61` minutos
- CUANDO se comparan con `Equals`
- ENTONCES el resultado es falso

### Requirement: Dominio sin dependencias y cobertura total

`WorkedHours` DEBE residir en `TimeTracker.Domain` sin referencias a paquetes ni proyectos externos. NO DEBE incluir conversión a horas decimales ni formato de texto (YAGNI) hasta que un caso de uso lo exija. `TimeTracker.Domain` DEBE alcanzar 100 % de cobertura de líneas y ramas.

#### Scenario: Sin dependencias
- DADO el ensamblado `TimeTracker.Domain`
- CUANDO se listan sus `ReferencedAssemblies` y paquetes
- ENTONCES solo aparecen ensamblados del marco base de .NET

#### Scenario: Cobertura del dominio
- DADO `dotnet test` con recolección de cobertura en formato Cobertura
- CUANDO se evalúa el informe de `TimeTracker.Domain.Tests` para el paquete `TimeTracker.Domain`
- ENTONCES `line-rate` y `branch-rate` son `1`

#### Scenario: Ciclo TDD demostrable
- DADO el cambio aplicado
- CUANDO se revisa el orden de trabajo registrado en `tasks.md`
- ENTONCES cada prueba de `WorkedHours` que define comportamiento nuevo figura como roja antes de su implementación (incluido el rojo de compilación), y las pruebas de borde o de contrato ya cubierto que pasan a la primera (cero válido, minutos distintos no iguales, cero neutro, suma hasta `int.MaxValue`) figuran como verdes a la primera, con su rojo demostrado mediante una mutación temporal o con la justificación de que pertenecen al mismo contrato que una prueba previa

Nota de verificación: la medición de cobertura depende de xUnit v3 + Coverlet (punto NO verificado en la exploración). Respaldo: xUnit v2.
