using TimeTracker.Api.Authentication;
using TimeTracker.Api.Configuration;
using TimeTracker.Api.Errors;
using TimeTracker.Api.Health;
using TimeTracker.Api.Security;
using TimeTracker.Api.Seeding;
using TimeTracker.Api.Team;
using TimeTracker.Api.TimeEntries;

var builder = WebApplication.CreateBuilder(DocumentGeneration.CreateOptions(args));
DocumentGeneration.ApplyPlaceholderSettings(builder.Configuration);

// No se anuncia el servidor: la cabecera `Server` revela tecnología y versión sin aportar nada al cliente.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddForwardedHeadersBehindProxy(builder.Configuration);
builder.Services.AddRestrictiveCors();
builder.Services.AddDatabaseConfiguration();
builder.Services.AddJwtAuthentication();
builder.Services.AddDomainProblemDetails();
builder.Services.AddTimeEntries();
builder.Services.AddTeam();
builder.Services.AddDemoSeeding();
builder.Services.AddLoginRateLimiting();
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
// Tras CORS: el 429 conserva las cabeceras del origen permitido (DD-21).
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// El documento solo se sirve en desarrollo; el contrato exportado vive en docs/api/openapi.json.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapTimeEntryEndpoints();
app.MapTeamEndpoints();

app.Run();
