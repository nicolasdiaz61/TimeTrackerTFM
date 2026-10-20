# Delta para solution-architecture

Cambio: `multi-compania`. Capacidad EXISTENTE (`openspec/specs/solution-architecture/spec.md`). Este delta solo contiene bloques `ADDED`: pruebas de arquitectura del aislamiento por compañía, generación del contrato OpenAPI con el registro habilitado y documentación (ADR 0020 y 0021, `AGENTS.md`, README y runbook). Ningún requisito vigente se modifica.

Nota de coherencia (no es parte del delta): el requisito vigente «ADR y README» afirma que `docs/adr/` contiene los ADR 0001 a 0017, pero el repositorio ya contiene los ADR 0018 y 0019 (cambio `asistente-config-responsable`, que no actualizó esta especificación). Los requisitos nuevos de este delta no dependen de esa cifra; conviene corregir el requisito vigente al archivar.

Notas de verificación adicionales:
- Las pruebas de arquitectura se escriben en `TimeTracker.ArchitectureTests` sin NetArchTest (reflexión y lectura de archivos, como las vigentes).
- «Comprobado una vez» significa que la prueba se demuestra roja introduciendo temporalmente la violación y vuelve a pasar al retirarla.

## ADDED Requirements

### Requirement: Pruebas de arquitectura del aislamiento por compañía

`TimeTracker.ArchitectureTests` DEBE incluir pruebas que fallen cuando: (1) una entidad del modelo de EF Core con propiedad `company_id` no tiene filtro global de consulta (recorrido de `context.Model.GetEntityTypes()`); (2) existe una llamada a `IgnoreQueryFilters` en `src/` fuera de la lista cerrada con motivo (ver `company-isolation`); (3) `docs/api/openapi.json` contiene un parámetro o propiedad `companyId` (sin distinguir mayúsculas); (4) un caso de uso, herramienta del asistente o endpoint declara un parámetro de compañía (`companyId`) en sus firmas públicas. Además, `ICompanyContext` DEBE ser un puerto de Application sin dependencias externas, con su adaptador solo en `TimeTracker.Api`; `Infrastructure` NO DEBE referenciar `HttpContext`. La regla de dependencias vigente (Domain sin paquetes, Application solo Domain) NO cambia.

#### Scenario: Todas las entidades con company_id tienen filtro
- DADO el modelo vigente
- CUANDO se ejecuta la prueba de filtros
- ENTONCES pasa; una entidad temporal con `company_id` sin filtro la hace fallar (comprobado una vez)

#### Scenario: IgnoreQueryFilters en lista cerrada
- DADO el código de `src/`
- CUANDO se ejecuta la prueba de la lista cerrada
- ENTONCES pasa; una llamada temporal fuera de la lista la hace fallar indicando el archivo (comprobado una vez)

#### Scenario: Contrato sin companyId
- DADO el contrato regenerado
- CUANDO se ejecuta la prueba de contrato
- ENTONCES pasa; un parámetro temporal `companyId` la hace fallar (comprobado una vez)

#### Scenario: Sin parámetros de compañía en el código
- DADO los casos de uso, herramientas y endpoints
- CUANDO la prueba revisa sus firmas
- ENTONCES ninguno tiene un parámetro de compañía

#### Scenario: Puerto en Application, adaptador en Api
- DADO `ICompanyContext`
- CUANDO se ejecutan `DependencyRuleTests`
- ENTONCES el puerto está en Application, su implementación solo en Api, Application sigue sin paquetes directos y Infrastructure no referencia `HttpContext`

### Requirement: Contrato OpenAPI generado con el registro habilitado

La generación de `docs/api/openapi.json` durante el build (`GetDocument.Insider`) DEBE forzar `Registration:Enabled` = `true` con un código de invitación ficticio (valor que no sea secreto real), de modo que el contrato documente `POST /api/auth/register-company`, y SIGUE forzando la siembra desactivada (`Seed:DemoUsers` = `false`, para no entrar en conflicto con el registro). Estos valores NO DEBEN estar en `appsettings*.json`, en el código de ejecución normal ni afectar a ella: fuera de la generación, el registro sigue apagado por defecto. El contrato NO DEBE contener el código ficticio.

#### Scenario: Contrato con el registro
- DADO un entorno sin `Registration__*`
- CUANDO se ejecuta `dotnet build` de `TimeTracker.Api`
- ENTONCES el build termina con `0` y `docs/api/openapi.json` contiene `/api/auth/register-company`, `/api/auth/change-password` y `/api/employees`

#### Scenario: Sin efecto en la ejecución normal
- DADO la API arrancada fuera de la generación del contrato, en `Production` y sin `Registration__Enabled`
- CUANDO arranca
- ENTONCES el registro está apagado y la petición a `/api/auth/register-company` se comporta como una ruta inexistente

#### Scenario: Sin código en el contrato
- DADO el contrato regenerado
- CUANDO se busca el código ficticio de generación
- ENTONCES no aparece

### Requirement: Decisiones y límites documentados (ADR 0020 y 0021)

El repositorio DEBE incluir el ADR 0020 (aislamiento multiempresa) y el ADR 0021 (sustituye al 0018 y 0019 solo en lo de «fila única/toda la instancia»; los ADR existentes NO se reescriben), cada uno con contexto, decisión, consecuencias y alternativas. El ADR 0020 DEBE registrar: la base compartida con `company_id` y el aislamiento en tres capas (claves foráneas compuestas, filtro global que falla cerrado, pruebas A/B y de arquitectura); la elección `NO ACTION` frente a `RESTRICT` y su justificación (código SQLSTATE `23503` frente a `23001`); la compañía por defecto de identificador fijo y su doble papel de compañía demo; la clave `cid` y `cname` del token con su límite aceptado (token de 30 minutos: una compañía suspendida o un rol obsoleto conservan el acceso hasta caducar); la RLS de PostgreSQL diferida (el rol de la aplicación es propietario de las tablas: harían falta un rol distinto, `FORCE ROW LEVEL SECURITY` y `app.company_id` por transacción con el pool de Npgsql) como endurecimiento futuro; la enumeración de correos por el `409` del registro como riesgo aceptado; que cualquier Manager de una compañía edita la configuración del asistente como riesgo aceptado; que un rollback de código NO revierte el esquema; y la posposición explícita de la supresión y exportación de datos (RGPD). El ADR 0021 DEBE registrar la configuración del asistente por compañía (una fila por compañía, instantáneas por compañía con sondeo global, auditoría con `company_id`, asistente deshabilitado sin fila, clave de plataforma solo para la demo) y la protección de la clave con AAD v2 como mejora no bloqueante. `AGENTS.md` (§1 y §7), `README.md`, `.env.example` y el runbook DEBEN actualizarse en el mismo cambio, sin valores secretos.

#### Scenario: ADR nuevos presentes
- DADO `docs/adr/`
- CUANDO se listan los archivos
- ENTONCES existen el 0020 y el 0021, cada uno con las cuatro secciones, y el 0021 indica que sustituye al 0018 y 0019 en lo indicado

#### Scenario: Contenido del ADR 0020
- DADO el ADR 0020
- CUANDO se revisan sus consecuencias y alternativas
- ENTONCES menciona `NO ACTION` frente a `RESTRICT`, la RLS diferida y el rol propietario, el límite del token de 30 minutos, la enumeración de correos, el riesgo de que cualquier Manager edite la configuración, que el rollback de código no revierte el esquema y la posposición de la supresión y exportación RGPD

#### Scenario: ADR anteriores intactos
- DADO los ADR 0001 a 0019
- CUANDO se comparan con su versión anterior al cambio
- ENTONCES no han sido reescritos

#### Scenario: AGENTS.md actualizado
- DADO `AGENTS.md`
- CUANDO se revisan §1 y §7
- ENTONCES describen la configuración del asistente por compañía y ya no dicen que sea única para toda la instancia

#### Scenario: README y runbook
- DADO `README.md` y el runbook
- CUANDO se revisan
- ENTONCES el README explica el registro con código de invitación y su bandera, el alta de personas con contraseña temporal y el aviso de que la clave maestra vive solo en Railway, y el runbook describe la copia de seguridad previa, el ensayo con datos copiados y el orden de reversión; ninguno contiene secretos, códigos de invitación ni claves de API reales
