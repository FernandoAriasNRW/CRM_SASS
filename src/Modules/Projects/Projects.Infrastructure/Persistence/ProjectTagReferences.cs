using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Projects.Infrastructure.Persistence;

/// <summary>
/// Suelta una etiqueta borrada de los proyectos que la llevan. Ver <see cref="ITagReferences"/>.
/// En SQL y no en LINQ: EF no traduce <c>TagIds.Contains</c> sin activar las colecciones primitivas
/// de Pomelo, que cambiaría cómo se traducen otras consultas de toda la aplicación. Sin filtros globales: también los archivados y los de la papelera.
/// </summary>
internal sealed class ProjectTagReferences(ProjectsDbContext context) : ITagReferences
{
    public async Task<int> RemoveTagAsync(Guid tenantId, Guid tagId, CancellationToken ct = default)
    {
        var projects = await context.Projects
            .FromSqlInterpolated(
                $"SELECT * FROM `Projects` WHERE `TenantId` = {tenantId} AND JSON_CONTAINS(`TagIds`, JSON_QUOTE({tagId.ToString()}))")
            .IgnoreQueryFilters()
            .ToListAsync(ct);

        foreach (var project in projects)
            project.RemoveTag(tagId);

        await context.SaveChangesAsync(ct);
        return projects.Count;
    }
}
