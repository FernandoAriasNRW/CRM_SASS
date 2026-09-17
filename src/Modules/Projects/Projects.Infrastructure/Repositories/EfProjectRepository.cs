using Microsoft.EntityFrameworkCore;
using Projects.Application.Abstractions.Repositories;
using Projects.Domain.Entities;
using Projects.Infrastructure.Persistence;

namespace Projects.Infrastructure.Repositories;

public sealed class EfProjectRepository(ProjectsDbContext context) : IProjectRepository
{
  public async Task<Project?> GetByIdAsync(Guid tenantId, Guid id, bool includeDeleted = false, CancellationToken ct = default)
  {
    if (includeDeleted)
    {
      // Antes esto era `IgnoreQueryFilters()`, que apaga **todos** los filtros —el de inquilino
      // incluido— y sólo se salvaba porque la condición repetía el TenantId a mano. El ámbito
      // abre lo justo: la papelera y el archivo, con el aislamiento intacto.
      using var _ = context.IncludeHidden(deleted: true, archived: true);

      return await context.Projects.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id, ct);
    }

    return await context.Projects.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id, ct);
  }

  public async Task AddAsync(Project project, CancellationToken ct = default)
      => await context.Projects.AddAsync(project, ct);

  public Task UpdateAsync(Project project, CancellationToken ct = default)
  {
    context.Projects.Update(project);
    return Task.CompletedTask;
  }

  public async Task<bool> ExistsAsync(Guid tenantId, Guid id, CancellationToken ct = default)
      => await context.Projects.AnyAsync(p => p.TenantId == tenantId && p.Id == id, ct);
}