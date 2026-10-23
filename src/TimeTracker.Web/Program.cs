using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using TimeTracker.Web;
using TimeTracker.Web.Account;
using TimeTracker.Web.Assistant;
using TimeTracker.Web.Auth;
using TimeTracker.Web.Registration;
using TimeTracker.Web.Shared;
using TimeTracker.Web.Team;
using TimeTracker.Web.Time;
using TimeTracker.Web.TimeEntries;
using TimeTracker.Web.WorkSchedules;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// La URL de la API sale solo de configuración (wwwroot/appsettings.json): falla rápido si falta o es inválida (UI-10).
var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiBaseUri) || apiBaseUri.Scheme is not ("http" or "https"))
{
    throw new InvalidOperationException(
        "Falta la configuración «ApiBaseUrl» o no es una URL absoluta http/https. Defínala en wwwroot/appsettings.json.");
}

builder.Services.AddSingleton(TimeProvider.System);
// Zona horaria del navegador (js/local-time.js): WebAssembly se ejecuta en el hilo del navegador, así que la llamada es síncrona.
builder.Services.AddScoped<ILocalTimeZone>(sp => new BrowserTimeZone((IJSInProcessRuntime)sp.GetRequiredService<IJSRuntime>()));
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<ChatViewport>();
builder.Services.AddScoped<ISessionStorage, BrowserSessionStorage>();
builder.Services.AddScoped<DialogFocus>();
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<SessionSynchronizer>();
builder.Services.AddScoped<SessionNotice>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());

// HttpClient propio de cada cliente tipado (DD-17): no se añade Microsoft.Extensions.Http al WebAssembly.
// El login y el registro no llevan handler: el 401 del login («credenciales incorrectas») no debe redirigir ni cerrar sesión
// y el registro nunca envía «Authorization».
builder.Services.AddScoped(sp => new AuthApiClient(new HttpClient { BaseAddress = apiBaseUri }));
builder.Services.AddScoped<ILoginApi>(sp => sp.GetRequiredService<AuthApiClient>());
builder.Services.AddScoped<IRegistrationApi>(sp => sp.GetRequiredService<AuthApiClient>());
builder.Services.AddScoped<ISessionSignIn>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddScoped(sp => new AccountApiClient(CreateAuthorizedClient(sp, apiBaseUri)));
builder.Services.AddScoped<IPasswordChangeApi>(sp => sp.GetRequiredService<AccountApiClient>());
builder.Services.AddScoped(sp => new EmployeesApiClient(CreateAuthorizedClient(sp, apiBaseUri)));
builder.Services.AddScoped<IEmployeesApi>(sp => sp.GetRequiredService<EmployeesApiClient>());
builder.Services.AddScoped(sp => new TimeEntriesApiClient(CreateAuthorizedClient(sp, apiBaseUri)));
builder.Services.AddScoped(sp => new TeamApiClient(CreateAuthorizedClient(sp, apiBaseUri)));
builder.Services.AddScoped(sp => new WorkSchedulesApiClient(CreateAuthorizedClient(sp, apiBaseUri)));
builder.Services.AddScoped<IWorkSchedulesApi>(sp => sp.GetRequiredService<WorkSchedulesApiClient>());
builder.Services.AddScoped(sp => new AssistantApiClient(CreateAuthorizedClient(sp, apiBaseUri)));
builder.Services.AddScoped(sp => new AssistantConfigurationApiClient(CreateAuthorizedClient(sp, apiBaseUri)));

await builder.Build().RunAsync();

static HttpClient CreateAuthorizedClient(IServiceProvider services, Uri apiBaseUri)
{
    var handler = new BearerTokenHandler(
        apiBaseUri,
        services.GetRequiredService<TokenStore>(),
        services.GetRequiredService<JwtAuthenticationStateProvider>(),
        services.GetRequiredService<SessionNotice>(),
        services.GetRequiredService<NavigationManager>())
    {
        InnerHandler = new HttpClientHandler(),
    };

    return new HttpClient(handler) { BaseAddress = apiBaseUri };
}
