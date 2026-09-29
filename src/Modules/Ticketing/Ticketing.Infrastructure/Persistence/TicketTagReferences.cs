using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Ticketing.Infrastructure.Persistence;

/// <summary>
/// Suelta una etiqueta borrada de los tickets que la llevan. Ver <see cref="ITagReferences"/>.
/// En SQL y no en LINQ: EF no traduce <c>TagIds.Contains</c> sin activar las colecciones primitivas
/// de Pomelo, que cambiaría cómo se traducen otras consultas de toda la aplicación. Sin filtros globales: también los archivados y los de la papelera.
/// </summary>
internal sealed class TicketTagReferences(TicketingDbContext context) : ITagReferences
{
    public async Task<int> RemoveTagAsync(Guid tenantId, Guid tagId, CancellationToken ct = default)
    {
        var tickets = await context.Tickets
            .FromSqlInterpolated(
                $"SELECT * FROM `Tickets` WHERE `TenantId` = {tenantId} AND JSON_CONTAINS(`TagIds`, JSON_QUOTE({tagId.ToString()}))")
            .IgnoreQueryFilters()
            .ToListAsync(ct);

        foreach (var ticket in tickets)
            ticket.RemoveTag(tagId);

        await context.SaveChangesAsync(ct);
        return tickets.Count;
    }
}
