using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace WorkItems.Infrastructure.Persistence;

/// <summary>
/// Suelta una etiqueta borrada de las tareas que la llevan. Ver <see cref="ITagReferences"/>.
///
/// En SQL y no en LINQ: EF no traduce <c>TagIds.Contains</c> sin activar las colecciones primitivas
/// de Pomelo, que cambiaría cómo se traducen otras consultas de toda la aplicación. Sin filtros globales y con el inquilino en el Where: también las tareas archivadas o en la
/// papelera, que pueden volver, tienen que quedarse sin el id.
/// </summary>
internal sealed class TaskTagReferences(WorkItemsDbContext context) : ITagReferences
{
    public async Task<int> RemoveTagAsync(Guid tenantId, Guid tagId, CancellationToken ct = default)
    {
        var tasks = await context.Tasks
            .FromSqlInterpolated(
                $"SELECT * FROM `Tasks` WHERE `TenantId` = {tenantId} AND JSON_CONTAINS(`TagIds`, JSON_QUOTE({tagId.ToString()}))")
            .IgnoreQueryFilters()
            .ToListAsync(ct);

        foreach (var task in tasks)
            task.RemoveTag(tagId);

        await context.SaveChangesAsync(ct);
        return tasks.Count;
    }
}
