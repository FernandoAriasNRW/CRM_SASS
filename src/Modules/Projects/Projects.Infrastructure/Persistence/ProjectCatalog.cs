using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Projects.Infrastructure.Persistence;

/// <summary>
/// Responde el puerto <see cref="IProjectCatalog"/>.
///
/// Con los filtros globales puestos: un proyecto borrado o archivado no cuenta como existente.
/// Y con el inquilino en el <c>Where</c> además del filtro, como en el catálogo de etiquetas: el
/// identificador de un proyecto de otra organización no se tiene que encontrar por ninguna vía.
/// </summary>
internal sealed class ProjectCatalog(ProjectsDbContext context) : IProjectCatalog
{
    public Task<bool> ExistsAsync(Guid tenantId, Guid projectId, CancellationToken ct = default)
        => context.Projects.AsNoTracking().AnyAsync(p => p.TenantId == tenantId && p.Id == projectId, ct);
}
