using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using Docs.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Docs.Infrastructure.Persistence;

public sealed class DocsDbContext(DbContextOptions<DocsDbContext> options, IUserContext? userContext)
    : TenantDbContext(options, userContext)
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<DocumentPermission> DocumentPermissions => Set<DocumentPermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocsDbContext).Assembly);

        // Soft delete query filters

      // Aislamiento por tenant y soft delete, compuestos en un solo filtro.
      ApplyTenantFilters(modelBuilder);
    }
}
