# ADR 0007: Configuración por entorno y validación al arrancar

- Estado: Aceptado
- Fecha: 2026-10-11

## Contexto

`AGENTS.md` prohíbe secretos y configuración en el repositorio, exige variables de entorno y pide validar la configuración al arrancar y fallar rápido. La API necesita, como mínimo, la lista de orígenes CORS permitidos, y debe ejecutarse igual en local, en Docker y en producción.

## Decisión

- La configuración llega por variables de entorno (por ejemplo, `Cors__AllowedOrigins__0`). `appsettings*.json` no contiene orígenes ni secretos. `.env.example` documenta las claves sin valores reales; `.env` está en `.gitignore` y `.dockerignore`. Las claves de IA (`Ai__*`) solo aparecen comentadas, porque la API aún no las usa.
- Las opciones se enlazan a `ApiCorsOptions` con `AddOptions<ApiCorsOptions>().BindConfiguration("Cors").ValidateOnStart()` y un `IValidateOptions<ApiCorsOptions>` que rechaza la lista ausente o vacía, los valores en blanco, `*`, lo que no sea una URL absoluta `http`/`https` sin ruta, consulta, fragmento ni usuario, y la barra final. El mensaje nombra la clave con su índice (`Cors:AllowedOrigins:0`) y la razón, nunca el valor.
- Si la validación falla, el proceso termina antes de abrir ningún puerto.
- El puerto se controla con `ASPNETCORE_HTTP_PORTS`. La imagen Docker lo fija en `8080`; `.env.example` lo deja comentado porque un valor vacío lo anularía. `ASPNETCORE_URLS` y `launchSettings.json` tienen prioridad sobre él (se comprueba con `dotnet run --no-launch-profile`).
- Dentro del contenedor la API escucha en **HTTP**. El TLS se termina en el ingreso (proxy inverso o plataforma de despliegue), que se decide en el cambio de despliegue.

## Consecuencias

- Una configuración errónea se detecta al arrancar, con un mensaje accionable y sin filtrar valores.
- En Docker hay que proporcionar `Cors__AllowedOrigins__0` (con `--env-file` o `-e`); sin ella el contenedor termina con un código distinto de 0.
- `HEALTHCHECK` admite un solo puerto y usa `ASPNETCORE_HTTP_PORTS` con 8080 por defecto.
- HTTPS queda diferido y explícito: `AGENTS.md` exige HTTPS obligatorio, por lo que el despliegue deberá garantizarlo antes de producción.
- La exportación de OpenAPI en el build aporta un origen ficticio solo en ese proceso (ADR 0004).

## Alternativas

- **DataAnnotations:** no validan los elementos de una lista.
- **Validación manual antes de `Build()`:** rompería la exportación de OpenAPI y las sustituciones de `WebApplicationFactory`.
- **`ASPNETCORE_URLS` o `--urls`:** no es la variable que usan las imágenes oficiales para el puerto.
- **HTTPS en Kestrel dentro del contenedor:** obliga a gestionar certificados en la imagen; se resuelve en el ingreso.
