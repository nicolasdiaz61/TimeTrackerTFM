# ADR 0023: Proveedor Groq para el asistente

- Estado: Aceptado
- Fecha: 2026-10-23

## Contexto

El catálogo cerrado del asistente ([ADR 0018](0018-configuracion-del-asistente-por-el-responsable.md)) ofrece `Anthropic`, `OpenAI`, `Gemini` y `Ollama`. Se quiere añadir Groq, que expone una API compatible con el protocolo de OpenAI. Rige la misma regla de seguridad que para el resto: ninguna URL sale de la configuración ni de un Manager ([ADR 0019](0019-auditoria-inmutable-y-despliegue-de-la-configuracion-del-asistente.md) y [ADR 0021](0021-configuracion-del-asistente-por-compania.md)), porque elegirla permitiría exfiltrar la clave o provocar SSRF.

## Decisión

- **Nuevo miembro `Groq`** en `AiProvider` (Infrastructure) con el valor `5`, el siguiente libre: los valores existentes no se renumeran. `AssistantProvider` (Application) añade `Groq` al final con el mismo nombre (una prueba fija la correspondencia).
- **URL fija en el servidor.** `ProviderEndpoints.GroqOpenAi` es la constante `https://api.groq.com/openai/v1`. Se construye con el cliente del SDK de OpenAI, igual que Gemini; `Ai__Endpoint` se ignora para Groq (solo lo usa Ollama).
- **Mismo comportamiento que Gemini.** Exige `Ai__ApiKey` y `Ai__Model` (texto libre), admite esfuerzo de razonamiento, se acepta en `Ai__AllowedProviders` (`Groq`, sin distinguir mayúsculas) y su nombre se muestra tal cual en el desplegable de la Web, que se construye con la lista del servidor. Los errores 401/403, 404 y 429 se clasifican por el estado HTTP del SDK, sin mirar el mensaje.
- **Persistencia.** El proveedor se guarda como texto; la migración `AddGroqProvider` amplía la restricción `ck_assistant_settings_provider` para admitir `'Groq'`. No cambia ninguna columna ni el cifrado de la clave. El contrato OpenAPI no cambia (el proveedor es una cadena).

## Consecuencias

- Operar con Groq requiere una clave propia de Groq, igual que con el resto de proveedores en la nube; la clave nunca entra en el repositorio.
- **Reversión.** El `Down` de la migración restaura la restricción anterior y falla si existen filas con `Groq`: antes hay que cambiar o restablecer la configuración de esas compañías.
- La compatibilidad del campo `reasoning_effort` con cada modelo de Groq depende del modelo elegido; si un modelo lo rechaza, la prueba de conexión lo informa como rechazo y el Manager puede dejar el esfuerzo por defecto.

## Alternativas

- **URL configurable** (`Ai__Endpoint` para Groq): descartada por la regla de seguridad anterior.
- **Tratar Groq como `OpenAI` con otra URL elegida por el usuario:** descartada por la misma razón.
- **Reutilizar un valor numérico libre en lugar de `5`:** descartada; se respeta la regla de no renumerar.
