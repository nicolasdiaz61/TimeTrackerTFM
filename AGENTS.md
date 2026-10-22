# AGENTS.md — TimeTracker

Reglas que todo agente (y toda persona) debe seguir al trabajar en este repositorio. Si una regla entra en conflicto con una petición, avisa y propone una alternativa antes de continuar.

## 1. Proyecto

Aplicación de control de tiempos y ausencias para empresas: fichajes, ausencias, saldo de horas y horas extra, con un asistente de IA que responde consultas en lenguaje natural sobre esos datos.

- **Frontend:** Blazor WebAssembly (proyecto independiente). Solo presenta y llama a la API.
- **Backend:** API en .NET con Clean Architecture, desplegada como un único monolito modular.
- **Datos:** PostgreSQL con EF Core.
- **Multicompañía:** una misma instancia sirve a varias compañías, aisladas por `company_id` en una base de datos compartida (ADR 0020). La configuración del asistente es por compañía: cada compañía tiene la suya y ninguna afecta a otra (ADR 0021).
- **IA:** acceso al modelo mediante la abstracción `IChatClient` (Microsoft.Extensions.AI). El proveedor y el modelo se eligen por configuración de entorno o guardada por un Manager de su compañía (ADR 0021), sin cambiar código.
- Todos los datos de ejemplo son inventados. No se incluye código, datos ni nombres de ninguna empresa real.

Idioma: la documentación, los mensajes de commit y los textos de usuario van en español. Los identificadores de código (clases, métodos, variables) van en inglés.

## 2. Forma de trabajo

- **Spec Driven Development (SDD):** antes de implementar, existe una especificación (requisitos, historias de usuario con criterios de aceptación, diseño y tareas). Flujo: explorar → proponer → especificar y diseñar → tareas → implementar → verificar → archivar.
- **Modo Plan antes que Build:** diseña y valida los pasos antes de cambiar código. No hagas cambios grandes sin plan aprobado.
- **Cambios pequeños y verificables:** una unidad de trabajo coherente por commit, con sus tests y su documentación.
- **No inventes:** si falta información, pregunta. No asumas requisitos ni versiones de paquetes; compruébalos.
- **Revisa lo que genera la IA:** nunca aceptes cambios automáticamente. Verifica que se conserva la lógica de negocio y que los tests siguen en verde.
- **Memoria y contexto:** registra decisiones, hallazgos y convenciones relevantes. Mantén `AGENTS.md` breve y mueve las instrucciones especializadas a skills, cargadas bajo demanda.
- **Subagentes:** delega la exploración amplia, las escrituras en varios archivos y las revisiones. Un único agente escribe cada vez; las revisiones se hacen con contexto limpio.

## 3. Arquitectura

- **Monolito modular** como punto de partida. No introduzcas microservicios ni mensajería distribuida salvo necesidad demostrada y registrada en un ADR.
- **Clean Architecture**, con la regla de dependencias apuntando siempre hacia el dominio:
  - `Domain`: entidades, objetos de valor y reglas de negocio. Sin dependencias externas.
  - `Application`: casos de uso e interfaces (puertos). Depende solo de `Domain`.
  - `Infrastructure`: implementaciones (EF Core, adaptador de IA, correo, reloj). Implementa los puertos.
  - `Api`: endpoints, autenticación y autorización, composición. Es el único lugar donde se crean e inyectan las dependencias (composition root).
- **Puertos y adaptadores:** la lógica de negocio depende de abstracciones, nunca de PostgreSQL, del proveedor de IA ni del reloj del sistema.
- **Inyección de dependencias por constructor.** Sin localizadores de servicios ni estáticos con estado.
- **El frontend no contiene reglas de negocio** ni habla con el modelo de IA. Toda la lógica y los permisos viven en la API.
- **Contrato de API con OpenAPI.** Cualquier cambio de endpoint actualiza el contrato y su documentación.

## 4. Principios de diseño

- **SOLID:** una única razón de cambio por clase (SRP); extender sin modificar (OCP); las subclases respetan el contrato de su base (LSP); interfaces pequeñas y específicas (ISP); depender de abstracciones (DIP).
- **DRY, KISS y YAGNI:** elimina duplicación real, prefiere la solución más simple y no construyas nada que aún no se necesite.
- **Patrones con criterio:** úsalos solo cuando resuelven un problema concreto (por ejemplo, Strategy para sustituir un `switch` que crece). Evita la sobreingeniería y los antipatrones.
- **Aprovecha las características del lenguaje** antes de recrear patrones clásicos (records, pattern matching, delegados, LINQ).
- **Estilo:** nombres claros, funciones pequeñas, sin números mágicos, sin código muerto. Analizadores y formateo automáticos obligatorios; las advertencias se tratan como errores.

## 5. Pruebas

- **TDD:** rojo → verde → refactor. Primero el test que falla, después el código mínimo, después la limpieza.
- **Pirámide de pruebas:** muchas pruebas unitarias rápidas (xUnit), pruebas de integración para infraestructura y API, pocas pruebas de extremo a extremo para los flujos críticos.
- **Pruebas de comportamiento:** prueban lo que hace el sistema, no cómo lo implementa. Un test no debe romperse por un refactor que conserva el comportamiento.
- **Cobertura honesta por criticidad:**
  - Dominio y reglas de cálculo de horas, ausencias y permisos: 100 %.
  - Casos de uso y funcionalidades visibles: 80 % o más.
  - Infraestructura y código generado: sin umbral; se valida con pruebas de integración.
  - La cobertura no sustituye a la calidad: cubre primero lo que maneja dinero, tiempo o permisos.
- **Dobles de prueba:** las pruebas unitarias nunca llaman a un modelo de IA real. Usa una implementación falsa de `IChatClient`. Las pruebas con el modelo real son de integración, separadas y opcionales.
- **Pruebas de seguridad del asistente:** cada herramienta de IA tiene tests que comprueban que un empleado no accede a datos ajenos y que un responsable solo ve su equipo.
- **Refactor seguro:** no refactorices sin tests en verde que lo respalden.
- **Control de calidad automático:** los hooks de Git ejecutan formateo, análisis y pruebas rápidas antes del commit, y la suite completa antes del push. La integración continua no se salta nunca.

## 6. Seguridad

- **Secretos y configuración:** nunca en el código ni en el repositorio. Variables de entorno en desarrollo y producción; un `.env.example` documenta las claves sin valores reales. Valida la configuración al arrancar y falla rápido si falta algo.
- **Autenticación y autorización en el servidor.** El identificador del usuario sale siempre del token, nunca de un parámetro que pueda manipular el cliente. Mínimo privilegio y control de acceso por rol y por objeto.
- **Entradas y salidas:** valida y normaliza toda entrada; usa consultas parametrizadas; trata la salida de un modelo de IA como dato no confiable antes de usarla o mostrarla.
- **Contraseñas** con hash seguro; sesiones y tokens con expiración y renovación controladas. Política de contraseñas de 12 a 128 caracteres, sin reglas de composición. La contraseña temporal de un alta es de un solo uso: se muestra una vez, se guarda solo como hash y obliga a cambiarla en el primer inicio de sesión.
- **Aislamiento por compañía** (ADR 0020):
  - `CompanyId` sale siempre del claim `cid` mediante `ICurrentUser.CompanyId`, nunca de la ruta, la consulta, el cuerpo ni una cabecera; ningún caso de uso, herramienta ni endpoint recibe un identificador de compañía.
  - Toda consulta pasa por el filtro global de compañía. `IgnoreQueryFilters` solo en la lista cerrada vigilada por las pruebas de arquitectura; `CreateScope` solo desde su lista cerrada (`ICompanyScopeFactory`; excepción acotada a «sin localizadores de servicios»).
  - Cada herramienta del asistente y cada endpoint nuevo se prueban con dos compañías (A y B): B no ve, no cambia y no distingue nada de A.
- **Cabeceras y transporte:** HTTPS obligatorio, CORS restrictivo, cabeceras de seguridad y límites de tasa (rate limiting).
- **Errores:** nunca expongas trazas ni información sensible al cliente. Registra lo necesario sin datos personales ni secretos.
- **Dependencias:** versiones fijadas, análisis automático de vulnerabilidades y actualizaciones revisadas.
- **Referencia de riesgos:** OWASP Top 10 (web y API) y OWASP Top 10 para aplicaciones con LLM.

### Reglas específicas del asistente de IA

- El modelo **no accede a la base de datos**: solo invoca herramientas definidas en la API.
- Cada herramienta aplica la autorización del usuario autenticado; el modelo nunca decide ni recibe su identidad como parámetro.
- Conjunto reducido de herramientas, **solo lectura** por defecto. Cualquier acción que modifique datos exige confirmación explícita de una persona.
- Defensa frente a inyección de instrucciones: separa las instrucciones del sistema de los datos del usuario, valida los parámetros de cada llamada y no pongas secretos ni reglas de seguridad en el prompt.
- Consumo acotado: límite de peticiones, de tokens y de tiempo por usuario; sin bucles de herramientas sin tope.
- Registra las llamadas a herramientas (qué, quién, cuándo) sin datos sensibles, para poder auditarlas.

## 7. Infraestructura y entrega

- **Contenedores:** la API (que puede servir también el frontend) se empaqueta con Docker. Imágenes reproducibles, sin secretos dentro.
- **Integración y despliegue continuos con GitHub Actions:** en cada cambio compila, formatea, analiza, ejecuta las pruebas y publica. El despliegue automático desde la rama principal solo se admite si todo está en verde; hoy el despliegue en Railway es manual (véase más abajo).
- **Entornos separados** (desarrollo, producción) con configuración distinta e idéntico código.
- **Infraestructura como código** siempre que sea posible; nada se configura a mano sin quedar documentado.
- **Base de datos:** cambios mediante migraciones versionadas. Datos de prueba generados por un script y reproducibles.
  - Migraciones en dos pasos (expandir/contraer), en despliegues separados. **Atención: un rollback de código no revierte el esquema;** ejecute `Down` antes de redesplegar el código anterior (runbook en `docs/runbook-railway.md`); tras abrir el registro no hay `Down`, se corrige hacia delante.
  - Toda tabla de negocio nueva lleva `company_id` con clave foránea compuesta y filtro global.
- **Despliegue en Railway:** hoy es manual con `railway up` porque los servicios no tienen repositorio conectado (sin repositorio conectado, fusionar una PR no despliega). Si se conecta el autodespliegue, el plan de despliegue por fases debe rehacerse antes de fusionar. Los pasos y las puertas previas de cada despliegue están en `docs/runbook-railway.md`.
- **Observabilidad:** registros estructurados, comprobaciones de salud y seguimiento de errores en producción. Mide con umbrales concretos y alerta de forma accionable.
- **Costes:** el modelo de IA de producción tiene presupuesto y límite de uso definidos.
- **Cambio de proveedor de IA:** solo mediante configuración: de entorno (`Ai:Provider`, `Ai:Model`, `Ai:Endpoint`) o configuración guardada por un Manager de su compañía (ADR 0021). En desarrollo se usa un modelo pequeño local (Ollama).

## 8. Documentación

- **Documentación como código:** vive en el repositorio, se versiona con Git, se revisa en las pull requests y se actualiza junto con el código que describe. Una documentación obsoleta es un defecto.
- **`README.md`** completo: descripción del proyecto, stack, instalación y ejecución, estructura, funcionalidades principales, enlaces de despliegue y usuario de prueba.
- **ADR (registros de decisiones de arquitectura):** una decisión relevante, un archivo en `docs/adr/` con contexto, decisión, consecuencias y alternativas consideradas. Los ADR no se reescriben; se sustituyen por otro nuevo.
- **API:** el contrato OpenAPI es la fuente de verdad y se genera a partir del código.
- **Especificaciones:** requisitos, diseño y tareas del flujo SDD en `docs/` o en la carpeta de cambios correspondiente.
- **Comentarios en el código:** explican el porqué, no el qué. El código se explica por sí mismo con buenos nombres.
- **Resúmenes ejecutivos:** los cambios importantes se pueden explicar en pocas líneas, sin jerga, para quien no es técnico.

## 9. Control de versiones

- Rama principal siempre desplegable y protegida. Los cambios entran mediante pull request con la integración en verde.
- Commits pequeños, descriptivos y en español. Tests y documentación van en el mismo commit que el código que acompañan.
- No se sube nada que contenga secretos, datos reales o archivos generados.
