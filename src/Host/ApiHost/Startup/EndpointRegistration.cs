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

        // Sembrar la demostración a mano. No existe salvo que la configuración lo pida
        // (`DemoData:AllowSeedEndpoint`), en cualquier entorno, y exige rol Admin. Antes dependía
        // de no estar en producción: cualquier entorno de pruebas o de preproducción lo tenía
        // abierto sin que nadie lo hubiera decidido.
        if (SeedingSettings.From(app.Configuration).AllowSeedEndpoint)
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
/// El hub de notificaciones no tiene métodos propios: los avisos se empujan desde el servidor a
/// la persona que los recibe (<see cref="ApiHost.Notifications.NotificationPush"/>).
///
/// <b>Exige sesión.</b> No la pedía, y no importaba porque nadie empujaba nada por aquí. Ahora cada
/// aviso va a su destinatario por su identificador, que SignalR saca del token: sin token no hay
/// a quién mandarle nada.
/// </summary>
[Microsoft.AspNetCore.Authorization.Authorize]
public class NotificationsHub : Microsoft.AspNetCore.SignalR.Hub { }
