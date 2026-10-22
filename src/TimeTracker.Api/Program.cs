using TimeTracker.Api.Assistant;
using TimeTracker.Api.Authentication;
using TimeTracker.Api.Companies;
using TimeTracker.Api.Configuration;
using TimeTracker.Api.Errors;
using TimeTracker.Api.Health;
using TimeTracker.Api.Registration;
using TimeTracker.Api.Security;
using TimeTracker.Api.Seeding;
using TimeTracker.Api.Team;
using TimeTracker.Api.TimeEntries;
using TimeTracker.Infrastructure.Assistant;

var builder = WebApplication.CreateBuilder(DocumentGeneration.CreateOptions(args));
DocumentGeneration.ApplyPlaceholderSettings(builder.Configuration);

// No se anuncia el servidor: la cabecera `Server` revela tecnología y versión sin aportar nada al cliente.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddForwardedHeadersBehindProxy(builder.Configuration);
builder.Services.AddRestrictiveCors();
builder.Services.AddDatabaseConfiguration();
builder.Services.AddJwtAuthentication();
builder.Services.AddCompanyContext();
builder.Services.AddDomainProblemDetails();
builder.Services.AddTimeEntries();
builder.Services.AddAuthorizationPolicies();
builder.Services.AddTeam();
builder.Services.AddDemoSeeding();
builder.Services.AddRegistration();
builder.Services.AddCompanyLimits();
builder.Services.AddAssistant();
builder.Services.AddAssistantConfiguration();
// Va después de la migración (DatabaseMigrationHostedService corre en StartingAsync) y antes de que Kestrel acepte peticiones:
// Kestrel arranca como último servicio alojado, así que la primera carga acotada queda completa antes del primer chat (V-6).
// La exportación del contrato OpenAPI nunca debe tocar la base de datos: allí no se registra el sondeo.
if (!DocumentGeneration.IsRunningUnderTool)
{
    builder.Services.AddAssistantSettingsPoller();
}
builder.Services.AddHostedService<ProtectionStatusLogger>();
builder.Services.AddRateLimiting();
builder.Services.AddOpenApi(options => options.AddBearerSecurity());

var app = builder.Build();

// Tras un proxy de confianza (opt-in): IP y esquema reales antes que nada, para el limitador por IP (DD-19).
app.UseForwardedHeadersBehindProxy();
// Después: las cabeceras de seguridad llegan también a los errores (DD-20).
app.UseSecurityHeaders();
// Antes de CORS: así los errores también pasan por la política de la API.
app.UseExceptionHandler();
// Da cuerpo problem+json a los 401/403/404/405/429 que se generan sin cuerpo (DD-21).
app.UseStatusCodePages();
app.UseCors();
// Autenticación y autorización antes del limitador: la política del asistente particiona por usuario (claim sub) y
// un anónimo recibe 401 sin consumir permisos de nadie. CORS sigue antes: el 429 conserva las cabeceras del origen permitido.
app.UseAuthentication();
// La compañía sale solo del token ya validado y se fija antes de autorizar (V-10).
app.UseCompanyContext();
// El cambio de contraseña pendiente (claim mcp) se decide solo por el claim, antes de autorizar y de ejecutar ningún caso de uso (V-10).
app.UsePasswordChangePending();
app.UseAuthorization();
app.UseRateLimiter();

// El documento solo se sirve en desarrollo; el contrato exportado vive en docs/api/openapi.json.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapTimeEntryEndpoints();
app.MapTeamEndpoints();
app.MapAssistantEndpoints();
app.MapAssistantConfigurationEndpoints();
app.MapRegistrationEndpoint();

app.Run();
