# Propuesta: asistente-ia

Fecha: 2026-10-18 · Fase: sdd-propose · Base: `exploration.md` (alternativa A) y valores por defecto de las preguntas abiertas 1-8, aceptados por delegación del usuario.

## Intención

La aplicación de fichajes está terminada y desplegada, pero falta la pieza central del TFM: el asistente de IA. Hoy, para saber «¿cuántas horas trabajé la semana pasada?» o «¿quién de mi equipo no ha fichado hoy?», hay que recorrer `/fichajes` o `/equipo` y sumar a mano. Este cambio añade un asistente en lenguaje natural, de solo lectura, que responde en español sobre los fichajes del usuario autenticado y, si es responsable, sobre los de sus reportes directos, respetando las reglas de rol y ownership existentes.

## Alcance

### Dentro
- **Application:** consultas de resumen por día en hora local (Europe/Madrid vía `SpainTime`), totales por tipo, `Status` y `openSince`; rol disponible en el contexto del asistente.
- **Infrastructure:** `AiOptions` y validador; tres `AIFunction` de solo lectura (`get_my_time_entries`, `get_team_members`, `get_team_member_time_entries`); bucle con `FunctionInvokingChatClient` acotado; system prompt con «hoy» y respuesta en español; factory `IChatClient` Anthropic/Ollama.
- **Api:** `POST /api/asistente` sin estado ni streaming; 400/429/503; límite por usuario y tope diario; registro solo de metadatos; OpenAPI.
- **Web:** página `/asistente`, enlace en `NavMenu`, cliente tipado, respuesta en texto plano.
- **Pruebas:** nuevo `tests/TimeTracker.Infrastructure.Tests` con `FakeChatClient`; seguridad por herramienta; integración del endpoint.
- **Documentación:** ADR 0017, README, `.env.example`.

### Fuera
Acciones de escritura · streaming · conversaciones persistidas · ausencias · zona horaria por usuario · pruebas automáticas con modelo real (opcionales, desactivadas).

## Capacidades

### Nuevas
- `assistant-tools`: herramientas de solo lectura, autorización por rol/objeto, resumen compacto, mensajes genéricos ante ajeno o inexistente.
- `assistant-endpoint`: contrato de `POST /api/asistente`, historial no confiable, límites de tasa, coste y tiempo, errores sin detalle del proveedor, registro sin datos.
- `assistant-web`: página `/asistente`, enlace, estados de carga y errores 429/503 en español.
- `assistant-configuration`: claves `Ai:*`, selección de proveedor, validación al arrancar, deshabilitado sin `Ai:Provider`.

### Modificadas
- `startup-configuration`: validación de `Ai:*`; ausencia de proveedor no impide arrancar.
- `solution-architecture`: paquetes de IA solo en Infrastructure, nuevo proyecto de pruebas, 17 ADR.
- `authentication`: solo si el diseño mueve `UseRateLimiter` tras la autenticación (cambia el orden del pipeline).

## Enfoque

Alternativa A: Application sin SDK de IA; adaptador en Infrastructure; Api como composition root. Las herramientas llaman a los casos de uso existentes, que ya aplican el ownership; las de equipo solo se registran para `Manager`. La identidad nunca es parámetro.

### Decisiones asumidas
| N.º | Decisión |
|---|---|
| 1 | Alternativa A con `Infrastructure.Tests` |
| 2 | Europe/Madrid fija con `SpainTime` (reubicar si hace falta) |
| 3 | Rate limiter: mover `UseRateLimiter` o filtro propio, lo decide el diseño |
| 4 | Modelos solo por configuración; tope diario configurable con valor conservador (lo fija el diseño) |
| 5 | Sin `Ai:Provider`: la API arranca y el endpoint devuelve 503 |
| 6 | Rol en `ICurrentUser` o desde el endpoint: lo decide el diseño |
| 7 | `Microsoft.Extensions.AI` 10.10.0 salvo motivo documentado |
| 8 | El system prompt exige respuestas en español |

## Áreas afectadas

| Área | Impacto | Descripción |
|---|---|---|
| `src/TimeTracker.Application` | Modificado | Resumen por día, rol, `SpainTime` |
| `src/TimeTracker.Infrastructure` | Modificado | Adaptador de IA, opciones, factory |
| `src/TimeTracker.Api` | Modificado | Endpoint, rate limiting, DI |
| `src/TimeTracker.Web` | Modificado | `/asistente`, cliente, `NavMenu` |
| `tests/TimeTracker.Infrastructure.Tests` | Nuevo | Herramientas y bucle con fake |
| `tests/*` | Modificado | Integración, contrato, cobertura de autorización |
| `Directory.Packages.props`, `docs/`, `.env.example`, `README.md` | Modificado/Nuevo | Paquetes, ADR 0017, `openapi.json` |

## Riesgos

| Riesgo | Prob. | Mitigación |
|---|---|---|
| Regla de dependencias rota | Baja | Alternativa A; `DependencyRuleTests` |
| Orden del middleware para limitar por usuario | Media | Decisión en diseño |
| `ICurrentUser` sin rol | Media | Decisión 6 en diseño |
| Modelos Ollama pequeños fallan con herramientas o español | Media | Modelo con soporte de herramientas; pruebas con fake |
| NU1903 con `-warnaserror` | Media | Verificar al restaurar; subir versión documentada |
| Tiempos del borde de Railway | Media | Timeout < 30 s |
| Datos personales al proveedor cloud (RGPD) | Media | Datos mínimos, demo inventada, ADR 0017 |

## Plan de reversión

Sin cambio de esquema ni migración. Desactivación inmediata: quitar `Ai__Provider` en Railway (endpoint 503, resto intacto). Reversión completa: revertir los PR en orden inverso (4 → 1).

## Dependencias

`Microsoft.Extensions.AI` 10.10.0, `Anthropic` 12.58.0, `OllamaSharp` 5.5.0; clave de Anthropic en Railway; Ollama local opcional.

## Criterios de éxito

- [ ] Build `-warnaserror`, `dotnet format` y `dotnet test` en verde; Application ≥ 80 %.
- [ ] Empleado solo obtiene sus datos; herramientas de equipo no registradas para él.
- [ ] Responsable solo ve reportes directos; ajeno, inexistente y propio dan la misma respuesta.
- [ ] Historial con rol `system`/`tool` → 400; límite superado → 429; sin proveedor o fallo → 503 genérico.
- [ ] Los logs no contienen prompt ni respuesta.
- [ ] Demo en Railway con Anthropic y en local con Ollama, cambiando solo configuración.

## Previsión de carga de revisión

- Estimación: 1.300-1.700 líneas.
- PR encadenados: 1 consultas y seguridad (~350-400) → 2 orquestación (~400) → 3 endpoint y proveedores (~400) → 4 Web (~300).
- Decision needed before apply: Yes
- Chained PRs recommended: Yes
- 400-line budget risk: High

## Ronda de preguntas de propuesta

Resuelta por delegación (modo automático). Para revisión del usuario:
- ¿Debe el responsable poder preguntar por el equipo completo en una sola consulta (agregados), o basta miembro a miembro?
- ¿Mensaje visible cuando el asistente está deshabilitado: ocultar el enlace o mostrar aviso?
