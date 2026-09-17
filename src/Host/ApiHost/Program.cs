using ApiHost.Startup;
using Scalar.AspNetCore;
using Serilog;

// Cada paso del arranque vive en su propio fichero de Startup/. Aquí sólo queda el orden, que es
// lo que importa leer de un vistazo: sobre todo el del pipeline, donde mover una línea cambia qué
// peticiones llegan autenticadas, comprimidas o limitadas.

if (args.Contains(HealthCheckProbe.Argument))
    return await HealthCheckProbe.RunAsync();

// La licencia comunitaria de QuestPDF hay que declararla antes de generar el primer PDF, o
// lanza al hacerlo. Se declara aquí, al arrancar, y no dentro del escritor: es una decisión de
// la aplicación —bajo qué licencia se usa la librería— y no del código que dibuja una tabla.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);
var jwtKey = RequiredSettings.EnsureValid(builder.Configuration);

builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

builder.Services.AddModules(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration, jwtKey);
builder.Services.AddCorsPolicies(builder.Configuration);
builder.Services.AddApiRateLimiting(builder.Configuration);

// Manejo global de errores en formato RFC 7807.
builder.Services.AddExceptionHandler<ApiHost.Infrastructure.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Health checks para orquestadores (K8s liveness/readiness, healthcheck de Docker).
builder.Services.AddHealthChecks();

// Compresión de respuestas: los payloads JSON de listados son grandes y repetitivos.
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

var app = builder.Build();

app.InitializeDatabase();

if (app.Environment.IsDevelopment())
{
    // Genera el endpoint del JSON de OpenAPI (/openapi/v1.json) y la interfaz de Scalar.
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("CRM API Documentation")
            .WithTheme(ScalarTheme.Moon)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

// Debe ir lo primero del pipeline para capturar cualquier excepción posterior.
app.UseExceptionHandler();

app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseCors(CorsSetup.DefaultPolicy);

// Detrás de CORS y delante de la autenticación: ver DiskStorageFiles.
app.UseDiskStorageFiles();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapApplicationEndpoints();

await app.RunAsync();

// El código de salida del proceso. Lo exige el compilador desde que la sonda de salud de
// arriba devuelve 0 o 1: en cuanto una rama devuelve un valor, todas tienen que hacerlo.
return 0;

/// <summary>
/// Program es implícito al usar instrucciones de nivel superior y queda como internal,
/// fuera del alcance de WebApplicationFactory. Declararlo parcial y público es lo que
/// permite a las pruebas de integración arrancar la API real.
/// </summary>
public partial class Program { }
