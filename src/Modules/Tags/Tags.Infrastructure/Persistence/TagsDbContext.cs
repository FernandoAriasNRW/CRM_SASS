using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Tags.Domain.Entities;

namespace Tags.Infrastructure.Persistence;

/// <summary>
/// DbContext del modulo Tags. Hereda de TenantDbContext: el aislamiento por
/// tenant y el soft delete se aplican solos a toda entidad marcada.
/// </summary>
public sealed class TagsDbContext(DbContextOptions<TagsDbContext> options, IUserContext? userContext)
    : TenantDbContext(options, userContext)
{
    public DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TagsDbContext).Assembly);

        // Aislamiento por tenant y soft delete, compuestos en un solo filtro.
        ApplyTenantFilters(modelBuilder);
    }
}
