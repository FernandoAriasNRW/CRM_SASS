using Microsoft.EntityFrameworkCore;
using Teams.Domain.Entities;

namespace Teams.Infrastructure.Persistence;

public sealed class TeamsDbContext(DbContextOptions<TeamsDbContext> options) : DbContext(options)
{
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TeamsDbContext).Assembly);

        // Soft delete global filters
        modelBuilder.Entity<Team>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<TeamMember>().HasQueryFilter(e => !e.IsDeleted);
    }
}
