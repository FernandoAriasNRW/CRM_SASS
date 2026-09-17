using Microsoft.EntityFrameworkCore;
using Ticketing.Domain.Entities;
using Ticketing.Domain.ValueObjects;
using Ticketing.Infrastructure.Persistence;

namespace ApiHost.Seeding;

public sealed class TicketingSeeder(TicketingDbContext ticketsDb) : IModuleSeeder
{
    public string Module => "Ticketing";
    public int Order => 60;

    private static readonly (string Title, string Description, TicketPriority Priority)[] SampleTickets =
    [
        ("Error 500 al consultar permisos por rol en el módulo Admin", "Al consultar /api/v1/permissions?targetType=Role se genera una excepción de columna no encontrada en MySQL.", TicketPriority.High),
        ("Solicitud de integración de Webhooks con canal de Slack", "Requerimos enviar alertas automáticas cuando un ticket pase a estado Resuelto.", TicketPriority.Medium),
        ("Duda sobre exportación de reportes de tareas a formato PDF", "¿Existe opción para descargar el reporte de rendimiento en PDF o Excel?", TicketPriority.Low),
        ("Problema al subir imagen de perfil/avatar en configuración", "El sistema muestra error de tipo de archivo al intentar subir una imagen PNG.", TicketPriority.Medium),
        ("Consulta sobre límite de miembros por equipo", "Necesitamos agregar 15 usuarios a un solo equipo de desarrollo.", TicketPriority.Low)
    ];

    public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;

        using var _ = ticketsDb.AsTenant(tenantId);
        try { await ticketsDb.Database.ExecuteSqlAsync($"UPDATE `Tickets` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken); } catch { }

        if (await ticketsDb.Tickets.CountAsync(t => t.TenantId == tenantId, cancellationToken) >= 5)
            return;

        foreach (var sample in SampleTickets)
        {
            var customerId = context.Members.Count > 0
                ? context.Members[Random.Shared.Next(context.Members.Count)].Id
                : context.Admin.Id;

            var created = Ticket.Create(tenantId, customerId, sample.Title, sample.Description, sample.Priority);
            if (created.IsFailure || created.Value is null)
                continue;

            created.Value.AssignTo(context.Admin.Id);
            ticketsDb.Tickets.Add(created.Value);
        }

        await ticketsDb.SaveChangesAsync(cancellationToken);
    }
}
