# ADR 0033: Registro de compañías y datos de demostración en Development

- Estado: Aceptado
- Fecha: 2026-10-24
- Sustituye parcialmente a: ADR 0022 (solo el enlace) y ADR 0032 (Development ya abre el registro)

## Contexto

Quien clona el repositorio y ejecuta la aplicación en local debe poder probar el alta de una compañía desde la Web y, a la vez, conservar los usuarios y datos de demostración. El [ADR 0022](0022-autorregistro-con-codigo-de-invitacion-y-contrasenas.md) deja el registro cerrado por defecto, sin ningún enlace público a `/registro`, y la API se negaba a arrancar con `Registration:Enabled` y `Seed:DemoUsers` activos a la vez. El [ADR 0032](0032-configuracion-de-desarrollo-en-appsettings.md) mantuvo el registro cerrado en `appsettings.Development.json`, de modo que probarlo exigía definir variables y desactivar la demostración.

## Decisión

- `appsettings.Development.json` abre el registro (`Registration:Enabled`) con un código de invitación de desarrollo, constante única `DevelopmentInviteCode`, junto a `DevelopmentSigningKey`. Es público por estar en el repositorio. `appsettings.json` base y Production siguen sin registro abierto.
- La regla «`Registration:Enabled` y `Seed:DemoUsers` no pueden ser true a la vez» solo se aplica fuera de Development. En Development coexisten: la compañía nueva queda aislada por `company_id` y los usuarios de demostración pertenecen a la compañía por defecto. El riesgo real es Production, que mantiene la regla; `DemoUsersStartupGuard` ya solo actúa en Production.
- Salvaguarda: la API no arranca si el entorno no es Development y `Registration:InviteCodes` contiene el código de desarrollo. El mensaje nombra la clave y no imprime el valor. Una prueba impide que el código aparezca en `.env.example`, el `Dockerfile`, `railway.json`, `docker-compose.yml`, el runbook de Railway, el README ni este ADR.
- La Web muestra en el login un botón «Crear una compañía» (hacia `/registro`) solo si su configuración lo activa (`Features:ShowRegisterLink`). Vale true en `wwwroot/appsettings.Development.json` y false en el `appsettings.json` base. `/registro` ofrece «Volver a iniciar sesión».
- En un despliegue el botón sigue oculto por defecto. Puede activarse sin recompilar con la variable `SHOW_REGISTER_LINK=true` del servicio Web, que `40-web-config.sh` escribe en el `appsettings.json` servido; cualquier otro valor, o su ausencia, lo oculta.

## Consecuencias

- `dotnet run` y Compose permiten crear una compañía desde el login sin variables, con los usuarios y datos de demostración presentes.
- Activar el botón en producción exige dos cosas independientes: la variable `SHOW_REGISTER_LINK` del servicio Web y, en la API, `Registration__Enabled=true` con sus códigos de invitación propios. El botón por sí solo no abre el registro: la ruta de la API sigue cerrada y responde como una ruta inexistente. A la inversa, abrir el registro en la API no muestra el botón.
- En Production con registro abierto sigue sin poder haber usuarios de demostración (validación de opciones y guarda de arranque).
- Quien exponga una instancia de Development a una red no confiable asume que cualquiera puede crear compañías con el código público.
- Los ADR 0022 y 0032 no se editan (ADR 0001); la sustitución parcial se declara aquí y en el índice del README.

## Alternativas

- **Mantener el registro cerrado en Development:** obliga a definir variables y apagar la demostración para probarlo; descartada.
- **Permitir la coexistencia en todos los entornos:** los usuarios de demostración tienen una contraseña pública y no deben convivir con un registro abierto en un despliegue real.
- **Mostrar siempre el enlace:** contradice el ADR 0022 en Production, donde la página se entrega por URL directa.
- **Condicionar el botón al entorno de la API:** la Web es estática y no conoce ese entorno; una opción propia de la Web es explícita y no requiere recompilar.
