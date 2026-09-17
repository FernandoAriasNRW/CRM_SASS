using ApiHost.Calendar;
using Automations.Presentation.Endpoints;
using Calendar.Presentation.Endpoints;
using Comments.Presentation.Endpoints;
using Communication.Presentation.Endpoints;
using CustomFields.Presentation.Endpoints;
using Docs.Presentation.Endpoints;
using Identity.Presentation.Endpoints;
using Notifications.Presentation.Endpoints;
using Projects.Presentation.Endpoints;
using Reporting.Presentation.Endpoints;
using Tags.Presentation.Endpoints;
using Teams.Presentation.Endpoints;
using Ticketing.Presentation.Endpoints;
using Webhook.Presentation.Endpoints;
using WorkItems.Presentation.Endpoints;

namespace ApiHost.Startup;

public static class EndpointRegistration
{
    public static void MapApplicationEndpoints(this WebApplication app)
    {
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
        app.MapAgendaEndpoints();
        app.MapReportingEndpoints();
        app.MapWebhookEndpoints();
        app.MapTeamsEndpoints();
        app.MapTagsEndpoints();
        app.MapCustomFieldsEndpoints();
        app.MapAutomationsEndpoints();
        app.MapCommentsEndpoints();
        app.MapDocsEndpoints();

        // Seed de datos de demostración.
        //
        // Sólo existe fuera de producción: reinicializar datos es destructivo y no debe
        // ser alcanzable en un entorno real ni siquiera por un administrador despistado.
        // Adicionalmente exige rol Admin autenticado.
        if (!app.Environment.IsProduction())
        {
            app.MapPost("/api/v1/admin/seed-database", async (Services.DataSeederService seeder, CancellationToken ct) =>
            {
                await seeder.SeedAllAsync(ct);
                return Results.Ok(new { Message = "Database seeded successfully" });
            })
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .WithName("SeedDatabase")
            .WithOpenApi();
        }

        app.MapHub<NotificationsHub>("/hubs/notifications");
        app.MapHub<WorkItems.Presentation.Hubs.BoardHub>("/hubs/board");
        app.MapHub<Ticketing.Presentation.Hubs.TicketsHub>("/hubs/tickets");
    }
}

/// <summary>
/// El hub de notificaciones no tiene métodos propios: los mensajes se empujan desde el servidor.
/// Se llamaba <c>DummyNotificationsHub</c>, pero no es de pruebas, es el que usa la aplicación.
/// </summary>
public class NotificationsHub : Microsoft.AspNetCore.SignalR.Hub { }
