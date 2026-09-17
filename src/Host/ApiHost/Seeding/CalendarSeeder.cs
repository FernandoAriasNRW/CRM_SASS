using Calendar.Domain.Entities;
using Calendar.Domain.ValueObjects;
using Calendar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ApiHost.Seeding;

public sealed class CalendarSeeder(CalendarDbContext calendarDb) : IModuleSeeder
{
    public string Module => "Calendar";
    public int Order => 70;

    public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;
        var organizerId = context.Admin.Id;

        using var _ = calendarDb.AsTenant(tenantId);
        try { await calendarDb.Database.ExecuteSqlAsync($"UPDATE `calendar_events` SET `tenant_id` = {tenantId} WHERE `tenant_id` != {tenantId}", cancellationToken); } catch { }

        if (await calendarDb.CalendarEvents.AnyAsync(e => e.TenantId == tenantId, cancellationToken))
            return;

        var now = DateTime.UtcNow;

        // Con argumentos con nombre. Estaban puestos por posición, y al añadir `ticketId`
        // a `Create` **la descripción pasó a ocupar el hueco del identificador**: sólo se
        // vio porque los tipos no casaban. Con tres `Guid?` seguidos habría compilado
        // igual y los eventos se habrían sembrado enlazados a cualquier cosa.
        var events = new[]
        {
            CalendarEvent.Create(
                tenantId: tenantId, organizerId: organizerId,
                title: "Sprint Planning - CRM SaaS v2.0",
                startTime: now.AddDays(1).Date.AddHours(9),
                endTime: now.AddDays(1).Date.AddHours(10).AddMinutes(30),
                type: CalendarEventType.Meeting,
                description: "Planificación de tareas del sprint con todo el equipo de desarrollo",
                location: "Sala Virtual Meet", isAllDay: false),

            CalendarEvent.Create(
                tenantId: tenantId, organizerId: organizerId,
                title: "Demo de Producto con Cliente VIP - Acme Corp",
                startTime: now.AddDays(2).Date.AddHours(14),
                endTime: now.AddDays(2).Date.AddHours(15),
                type: CalendarEventType.Appointment,
                description: "Presentación de la nueva interfaz ClickUp y permisos granulares",
                location: "Google Meet Link", isAllDay: false),

            CalendarEvent.Create(
                tenantId: tenantId, organizerId: organizerId,
                title: "Revisión de Arquitectura & Webhooks",
                startTime: now.AddDays(3).Date.AddHours(11),
                endTime: now.AddDays(3).Date.AddHours(12),
                type: CalendarEventType.Meeting,
                description: "Auditoría de seguridad y firmado HMAC-SHA256 de webhooks",
                location: "Sala de Reuniones A", isAllDay: false),

            CalendarEvent.Create(
                tenantId: tenantId, organizerId: organizerId,
                title: "Despliegue a Producción v2.1",
                startTime: now.AddDays(5).Date.AddHours(8),
                endTime: now.AddDays(5).Date.AddHours(18),
                type: CalendarEventType.Task,
                description: "Despliegue de contenedores Docker y actualización de base de datos MySQL",
                location: "Servidor Cloud", isAllDay: true)
        };

        foreach (var created in events)
        {
            if (created.IsSuccess && created.Value != null)
                calendarDb.CalendarEvents.Add(created.Value);
        }

        await calendarDb.SaveChangesAsync(cancellationToken);
    }
}
