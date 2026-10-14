# ADR 0008: Persistencia con PostgreSQL 18 y EF Core 10

- Estado: Aceptado
- Fecha: 2026-10-12

## Contexto

`AGENTS.md` fija PostgreSQL con EF Core, migraciones versionadas y pruebas de infraestructura contra la base real. El primer caso de uso con estado (fichajes, cambio `fichaje-extremo-a-extremo`) necesita guardar entradas y salidas, impedir dos fichajes abiertos del mismo empleado y poder probarlo sin sustitutos de la base de datos.

## Decisión

- **Base de datos:** PostgreSQL 18 (`postgres:18-alpine`), la misma imagen en `docker-compose.yml` y en las pruebas. PostgreSQL 18 guarda los datos en una subcarpeta versionada, por lo que el volumen se monta en `/var/lib/postgresql`.
- **Acceso:** EF Core 10 con `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3. Toda la familia EF se fija en 10.0.12 (referencia directa a `Microsoft.EntityFrameworkCore.Relational` para elevar la versión transitiva) y los paquetes viven solo en `Infrastructure`; `Domain` y `Application` no referencian paquetes y una prueba de arquitectura lo comprueba.
- **Mapeo:** configuración Fluent en `TimeEntryConfiguration` (sin atributos en el dominio): tabla `time_entries`, columnas en `snake_case` y `timestamp with time zone` para los instantes.
- **Integridad en la base:** índice único parcial `ux_time_entries_employee_id_open` (`employee_id` donde `clock_out_at IS NULL`), restricción `CHECK` de salida no anterior a la entrada e índice de listado `ix_time_entries_employee_id_clock_in_at` (descendente). El repositorio traduce la violación única (`23505`) a la excepción de dominio de fichaje abierto existente.
- **Migraciones:** versionadas en `Infrastructure/Persistence/Migrations` y generadas con la herramienta local `dotnet-ef` 10.0.12 (`.config/dotnet-tools.json`, `dotnet tool restore`). Se aplican con `MigrateAsync`, nunca con `EnsureCreated`. Solo se aplican al arrancar la API si `Database:ApplyMigrationsOnStartup` es `true` (por defecto no); `docker-compose.yml` la activa y `.env.example` la deja en `false`.
- **Pruebas:** las de integración usan PostgreSQL real con Testcontainers (`Testcontainers.PostgreSql` 4.16.0), una instancia por ejecución y `MigrateAsync` una vez. Un `EmployeeId` nuevo por prueba evita limpiar datos. Se mantiene xUnit v2 sobre VSTest ([ADR 0006](0006-pila-de-pruebas.md)): el fixture usa `IAsyncLifetime` y la colección `Postgres`, y no existe `TestContext.Current` (propio de xUnit v3), por lo que las pruebas pasan `CancellationToken.None`.
- **Concurrencia de la salida diferida:** no se añade token de concurrencia (`xmin`). Dos `clock-out` simultáneos del mismo fichaje pueden devolver dos `200` y la última escritura fija la hora de salida; la restricción `CHECK` y las reglas de dominio se siguen cumpliendo. La entrada sí está protegida por el índice único parcial. Se acepta como riesgo residual y queda para un cambio de seguimiento (decisión de diseño DD-9 diferida, 2026-10-12).

## Consecuencias

- Las pruebas de integración **requieren Docker** en marcha; sin él fallan con un mensaje claro y no se omiten en silencio. La integración continua también necesita Docker, y los pasos de la CI siguen sin ejecutarse en un servidor remoto.
- Las reglas críticas (un solo fichaje abierto, salida no anterior a la entrada) se garantizan en la base de datos y no solo en el código.
- La migración automática al arrancar es cómoda en local, pero no es una decisión de despliegue: el cambio de despliegue decidirá si migra al arrancar o en un paso aparte.
- Mantener la familia EF alineada exige revisar las versiones al actualizar el runtime (el runtime instalado era 10.0.10 y los paquetes `Microsoft.Extensions.*` suben a 10.0.12).
- El riesgo residual de la doble salida simultánea queda documentado y sin prueba automática.

## Alternativas

- **`EnsureCreated`:** no versiona el esquema ni admite cambios posteriores.
- **SQLite o base en memoria en las pruebas:** no ejecutan el índice único parcial ni las restricciones de PostgreSQL, justo lo que se quiere probar.
- **Migrar siempre al arrancar:** la salud (`/health`) y el arranque dependerían de la base de datos y una migración fallida tiraría la API en producción.
- **Control de concurrencia con `xmin` ahora:** se difiere para acotar el alcance; queda como seguimiento.
- **Atributos de EF en las entidades de dominio:** acoplarían `Domain` a un paquete y rompería la regla de dependencias.
