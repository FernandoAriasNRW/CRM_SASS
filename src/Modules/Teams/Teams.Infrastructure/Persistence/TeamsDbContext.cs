using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Teams.Domain.Entities;

namespace Teams.Infrastructure.Persistence;

public sealed class TeamsDbContext(DbContextOptions<TeamsDbContext> options, IUserContext? userContext)
    : TenantDbContext(options, userContext)
{
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TeamsDbContext).Assembly);

        // Soft delete global filters

      // Aislamiento por tenant y soft delete, compuestos en un solo filtro.
      ApplyTenantFilters(modelBuilder);
    }
}
