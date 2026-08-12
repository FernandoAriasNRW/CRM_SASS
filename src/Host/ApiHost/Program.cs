using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Infrastructure.Persistence;
using Teams.Presentation.Endpoints;
using BuildingBlocks.Application.Behaviors;
using Calendar.Application.Handlers.Commands;
using Calendar.Infrastructure;
using Calendar.Presentation.Endpoints;
using Communication.Infrastructure;
using Communication.Presentation.Endpoints;
using Docs.Application;
using Docs.Infrastructure;
using Docs.Presentation.Endpoints;
using Identity.Application.Abstractions.Services;
using Identity.Application.Handlers.Commands;
using Identity.Infrastructure;
using Identity.Infrastructure.Services;
using Identity.Presentation.Endpoints;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Notifications.Application.Handlers.Commands;
using Notifications.Infrastructure;
using Notifications.Presentation.Endpoints;
using Projects.Application.Handlers.Commands;
using Projects.Infrastructure;
using Projects.Presentation.Endpoints;
using Reporting.Application.Handlers.Commands;
using Reporting.Presentation.Endpoints;
using Scalar.AspNetCore;
using Serilog;
using Ticketing.Application.Handlers.Commands;
using Ticketing.Infrastructure;
using Ticketing.Presentation.Endpoints;
using Webhook.Application.Handlers;
using Webhook.Infrastructure;
using Webhook.Presentation.Endpoints;
using WorkItems.Application.Handlers.Commands;
using WorkItems.Infrastructure;
using WorkItems.Presentation.Endpoints;
using Tags.Infrastructure;
using Tags.Presentation;
using Tags.Presentation.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════════════════
// VALIDACIÓN DE CONFIGURACIÓN — fail fast
//
// Preferimos que la aplicación no arranque a que arranque con una configuración
// insegura. Sin esto, una clave JWT vacía produce tokens que cualquiera puede
// falsificar, y el fallo aparecería mucho más tarde y de forma confusa.
// ═══════════════════════════════════════════════════════════════════════════════
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "Falta la configuración 'Jwt:Key'. Defínala en appsettings.Development.json, " +
        "en user-secrets (dotnet user-secrets set \"Jwt:Key\" \"<valor>\") " +
        "o en la variable de entorno Jwt__Key.");
}

// HMAC-SHA256 requiere una clave de al menos 256 bits (32 bytes).
if (System.Text.Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "'Jwt:Key' debe tener al menos 32 caracteres para firmar con HMAC-SHA256. " +
        "Genere una con: openssl rand -base64 48");
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Falta la cadena de conexión 'ConnectionStrings:DefaultConnection'. " +
        "Defínala en appsettings.Development.json o en la variable de entorno " +
        "ConnectionStrings__DefaultConnection.");
}

// Serilog
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
// ═══════════════════════════════════════════════════════════════════════════════ INFRAESTRUCTURA CORE (BuildingBlocks) ═══════════════════════════════════════════════════════════════════════════════

// Servicios core: Email, DomainEventDispatcher
builder.Services.AddCoreInfrastructure(builder.Configuration);

// ═══════════════════════════════════════════════════════════════════════════════ PERSISTENCIA DE MÓDULOS - Cada módulo
// con su DbContext independiente ═══════════════════════════════════════════════════════════════════════════════

// ───────────────────────────────────────────────────────────────────────────── IDENTITY MODULE ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddIdentityInfrastructure(builder.Configuration);

// ───────────────────────────────────────────────────────────────────────────── PROJECTS MODULE ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddProjectsInfrastructure(builder.Configuration);

// ───────────────────────────────────────────────────────────────────────────── WORKITEMS MODULE ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddWorkItemsInfrastructure(builder.Configuration);

// ───────────────────────────────────────────────────────────────────────────── TICKETING MODULE ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddTicketingInfrastructure(builder.Configuration);

// ───────────────────────────────────────────────────────────────────────────── NOTIFICATIONS MODULE ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddNotificationsInfrastructure(builder.Configuration);

// ───────────────────────────────────────────────────────────────────────────── CALENDAR MODULE ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddCalendarInfrastructure(builder.Configuration);

// ───────────────────────────────────────────────────────────────────────────── COMMUNICATION MODULE ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddCommunicationInfrastructure(builder.Configuration);

// ───────────────────────────────────────────────────────────────────────────── WEBHOOK MODULE ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddWebhookInfrastructure(builder.Configuration);

// ───────────────────────────────────────────────────────────────────────────── TAGS MODULE ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddTagsInfrastructure(builder.Configuration);

// ───────────────────────────────────────────────────────────────────────────── DOCS MODULE ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddDocsApplication();
builder.Services.AddDocsInfrastructure(builder.Configuration);

// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
      options.TokenValidationParameters = new TokenValidationParameters
      {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
      };

      options.Events = new JwtBearerEvents
      {
          // Un refresh token está firmado con la misma clave y tiene el mismo
          // issuer/audience que un access token, así que pasaría la validación
          // estándar. Sólo el claim token_type los distingue: sin esta
          // comprobación, un refresh token vale como credencial de acceso
          // durante los 7 días de su vigencia.
          OnTokenValidated = context =>
          {
              var tokenType = context.Principal?.FindFirst(
                  Identity.Infrastructure.Services.JwtService.TokenTypeClaim)?.Value;

              // Los tokens de invitado no llevan token_type y sólo sirven para
              // el alta pública de tickets; se siguen aceptando.
              var isGuest = context.Principal?.IsInRole("Guest") == true;

              if (!isGuest && tokenType != Identity.Infrastructure.Services.JwtService.AccessTokenType)
              {
                  context.Fail("Se requiere un access token.");
              }

              return Task.CompletedTask;
          }
      };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<BuildingBlocks.Application.Abstractions.IUserContext, ApiHost.Services.UserContext>();

builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ═══════════════════════════════════════════════════════════════════════════════ PRESENTATION - Endpoints de API ═══════════════════════════════════════════════════════════════════════════════
builder.Services.AddDatabase(builder.Configuration);

builder.Services.AddIdentityPresentation(builder.Configuration);
builder.Services.AddProjectsPresentation(builder.Configuration);
builder.Services.AddWorkItemsPresentation(builder.Configuration);
builder.Services.AddTicketingPresentation(builder.Configuration);
builder.Services.AddNotificationsPresentation(builder.Configuration);
builder.Services.AddCommunicationPresentation(builder.Configuration);
builder.Services.AddCalendarPresentation(builder.Configuration);
builder.Services.AddWebhookPresentation(builder.Configuration);
builder.Services.AddReportingPresentation(builder.Configuration);
builder.Services.AddTeamsPresentation(builder.Configuration);
builder.Services.AddTagsPresentation(builder.Configuration);
builder.Services.AddDocsPresentation(builder.Configuration);

// ═══════════════════════════════════════════════════════════════════════════════ MEDIATR - Commands y Queries ═══════════════════════════════════════════════════════════════════════════════
builder.Services.AddMediatR(cfg =>
{
  cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
  cfg.RegisterServicesFromAssembly(typeof(LoginCommandHandler).Assembly);           // Identity
  cfg.RegisterServicesFromAssembly(typeof(CreateProjectCommandHandler).Assembly);   // Projects
  cfg.RegisterServicesFromAssembly(typeof(CreateTaskCommandHandler).Assembly);      // WorkItems
  cfg.RegisterServicesFromAssembly(typeof(CreateTicketHandler).Assembly);           // Ticketing
  cfg.RegisterServicesFromAssembly(typeof(CreateNotificationHandler).Assembly);    // Notifications
  cfg.RegisterServicesFromAssembly(typeof(CreateCalendarEventHandler).Assembly);   // Calendar
  cfg.RegisterServicesFromAssembly(typeof(Reporting.Application.Handlers.Commands.CreateReportHandler).Assembly);           // Reporting
  cfg.RegisterServicesFromAssembly(typeof(Communication.Application.Handlers.Commands.CreateConversationHandler).Assembly); // Communication
  cfg.RegisterServicesFromAssembly(typeof(WebhookEventNotificationHandler).Assembly); // Webhook
  cfg.RegisterServicesFromAssembly(typeof(Teams.Application.Commands.CreateTeamCommand).Assembly); // Teams

  // Pipeline behavior: valida el request con FluentValidation.
  // Va PRIMERO: no tiene sentido autorizar ni despachar una petición malformada.
  cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));

  // Pipeline behavior: despacha webhook tras commands IWebhookTriggered
  cfg.AddOpenBehavior(typeof(WebhookDispatchBehavior<,>));

  // Pipeline behavior: Authorization
  cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
});

// Registra todos los IValidator<T> de los ensamblados de módulos, para que
// ValidationBehavior los encuentre. Sin esto los validadores no se ejecutan nunca.
builder.Services.AddValidatorsFromAssemblies(
[
    typeof(LoginCommandHandler).Assembly,           // Identity
    typeof(CreateProjectCommandHandler).Assembly,   // Projects
    typeof(CreateTaskCommandHandler).Assembly,      // WorkItems
    typeof(CreateTicketHandler).Assembly,           // Ticketing
    typeof(CreateNotificationHandler).Assembly,     // Notifications
    typeof(CreateCalendarEventHandler).Assembly,    // Calendar
    typeof(Reporting.Application.Handlers.Commands.CreateReportHandler).Assembly,
    typeof(Communication.Application.Handlers.Commands.CreateConversationHandler).Assembly,
    typeof(Teams.Application.Commands.CreateTeamCommand).Assembly
], includeInternalTypes: true);

// ═══════════════════════════════════════════════════════════════════════════════ RESILIENCIA Y OPERACIÓN ═══════════════════════════════════════════════════════════════════════════════

// Manejo global de errores en formato RFC 7807.
builder.Services.AddExceptionHandler<ApiHost.Infrastructure.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Health checks para orquestadores (K8s liveness/readiness, healthcheck de Docker).
builder.Services.AddHealthChecks();

// Rate limiting. La política global protege toda la API; las políticas nombradas
// blindan los dos puntos abusables sin autenticación previa: el alta pública de
// tickets y el login (fuerza bruta de credenciales).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            // Particionamos por usuario autenticado; si no hay, por IP.
            partitionKey: context.User.Identity?.IsAuthenticated == true
                ? context.User.FindFirst("sub")?.Value ?? context.User.Identity.Name!
                : context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddFixedWindowLimiter("public-tickets", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(5);
        limiterOptions.QueueLimit = 0;
    });
});

// Compresión de respuestas: los payloads JSON de listados son grandes y repetitivos.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

builder.Services.AddScoped<ApiHost.Services.DataSeederService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    
    // Aplicar migraciones para todos los contextos
    var dbContexts = new Microsoft.EntityFrameworkCore.DbContext[] 
    {
        services.GetRequiredService<Identity.Infrastructure.Persistence.IdentityDbContext>(),
        services.GetRequiredService<Teams.Infrastructure.Persistence.TeamsDbContext>(),
        services.GetRequiredService<Projects.Infrastructure.Persistence.ProjectsDbContext>(),
        services.GetRequiredService<WorkItems.Infrastructure.Persistence.WorkItemsDbContext>(),
        services.GetRequiredService<Ticketing.Infrastructure.Persistence.TicketingDbContext>(),
        services.GetRequiredService<Notifications.Infrastructure.Persistence.NotificationsDbContext>(),
        services.GetRequiredService<Calendar.Infrastructure.Persistence.CalendarDbContext>(),
        services.GetRequiredService<Communication.Infrastructure.Persistence.CommunicationsDbContext>(),
        services.GetRequiredService<Webhook.Infrastructure.Persistence.WebhookDbContext>(),
        services.GetRequiredService<Reporting.Infrastructure.Persistence.ReportingDbContext>(),
        services.GetRequiredService<Tags.Infrastructure.Persistence.TagsDbContext>(),
        services.GetRequiredService<Docs.Infrastructure.Persistence.DocsDbContext>(),
        services.GetRequiredService<CrmDbContext>()
    };

    // Antes de tocar la base de datos: comprobar que ninguna entidad se ha quedado
    // fuera del aislamiento por tenant. Una entidad nueva que olvide ITenantEntity, o
    // un DbContext que olvide ApplyTenantFilters, devolverían filas de todos los
    // clientes sin lanzar ningún error. Preferimos no arrancar a servir datos cruzados.
    var isolationViolations = dbContexts
        .SelectMany(BuildingBlocks.Infrastructure.Persistence.TenantIsolationVerifier.FindViolations)
        .ToList();

    if (isolationViolations.Count > 0)
    {
        throw new InvalidOperationException(
            "Aislamiento multi-tenant incompleto. La aplicación no arranca para evitar fuga de datos entre clientes:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, isolationViolations.Select(v => "  - " + v)));
    }

    // Las migraciones son la única vía por la que cambia el esquema. Hasta agosto de 2026
    // aquí se llamaba a EnsureCreated() y a CreateTables() tragándose el error 1050: el
    // esquema se creaba, pero __EFMigrationsHistory quedaba vacía, así que un campo nuevo
    // no llegaba nunca a una base ya existente. Ver docs/CONTINUACION.md §1.
    //
    // Si esto falla, no se sirve nada: arrancar con el esquema a medias es peor que no
    // arrancar. La causa habitual es una base creada por el mecanismo anterior, cuyo
    // historial hay que sellar una vez.
    foreach (var ctx in dbContexts)
    {
        try
        {
            ctx.Database.Migrate();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"No se pudieron aplicar las migraciones de {ctx.GetType().Name}. "
                + "Si esta base se creó con el mecanismo anterior (EnsureCreated), su historial de "
                + "migraciones está vacío y hay que sellarlo una sola vez: ejecutar "
                + "scripts/db/sellar-historial-migraciones.sql. Detalle en docs/CONTINUACION.md §1.",
                ex);
        }
    }

    var identityCtx = services.GetRequiredService<Identity.Infrastructure.Persistence.IdentityDbContext>();

    if (!identityCtx.User.Any())
    {
        var adminRole = Identity.Domain.ValueObjects.UserRole.Admin;
        var email = Identity.Domain.ValueObjects.Email.Create("admin@acme.com").Value!;
        var pass = Identity.Domain.ValueObjects.PasswordHash.Create("admin123");
        var user = Identity.Domain.Entities.User.Create(Guid.NewGuid(), "Admin", email, pass, adminRole).Value!;
        identityCtx.User.Add(user);
        identityCtx.SaveChanges();
    }

    // Ejecutar DataSeederService automáticamente para garantizar datos fake completos
    try
    {
        var seeder = services.GetRequiredService<ApiHost.Services.DataSeederService>();
        seeder.SeedAllAsync().GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();
        logger.LogWarning(ex, "Error durante la siembra automática de datos demo.");
    }
}

// ═══════════════════════════════════════════════════════════════════════════════ PIPELINE DE LA APLICACIÓN ═══════════════════════════════════════════════════════════════════════════════
if (app.Environment.IsDevelopment())
{
  // Genera el endpoint del JSON de OpenAPI (/openapi/v1.json)
  app.MapOpenApi();

  // Configura la interfaz de Scalar
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
app.UseCors("AllowSpecificOrigins");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health checks: /health/live responde si el proceso está vivo;
// /health/ready sólo si además las dependencias (BD) responden.
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

app.MapHealthChecks("/health/ready").AllowAnonymous();

app.MapIdentityEndpoints();
app.MapProjectsEndpoints();
app.MapWorkItemsEndpoints();
app.MapTicketingEndpoints();
app.MapNotificationsEndpoints();
app.MapCommunicationEndpoints();
app.MapCalendarEndpoints();
app.MapReportingEndpoints();
app.MapWebhookEndpoints();
app.MapTeamsEndpoints();
app.MapTagsEndpoints();
app.MapDocsEndpoints();

// Seed de datos de demostración.
//
// Sólo existe fuera de producción: reinicializar datos es destructivo y no debe
// ser alcanzable en un entorno real ni siquiera por un administrador despistado.
// Adicionalmente exige rol Admin autenticado.
if (!app.Environment.IsProduction())
{
    app.MapPost("/api/v1/admin/seed-database", async (ApiHost.Services.DataSeederService seeder, CancellationToken ct) =>
    {
        await seeder.SeedAllAsync(ct);
        return Results.Ok(new { Message = "Database seeded successfully" });
    })
    .RequireAuthorization(policy => policy.RequireRole("Admin"))
    .WithName("SeedDatabase")
    .WithOpenApi();
}

app.MapHub<DummyNotificationsHub>("/hubs/notifications");
app.MapHub<WorkItems.Presentation.Hubs.BoardHub>("/hubs/board");
app.MapHub<Ticketing.Presentation.Hubs.TicketsHub>("/hubs/tickets");

await app.RunAsync();

public class DummyNotificationsHub : Microsoft.AspNetCore.SignalR.Hub { }

/// <summary>
/// Program es implícito al usar instrucciones de nivel superior y queda como internal,
/// fuera del alcance de WebApplicationFactory. Declararlo parcial y público es lo que
/// permite a las pruebas de integración arrancar la API real.
/// </summary>
public partial class Program { }
