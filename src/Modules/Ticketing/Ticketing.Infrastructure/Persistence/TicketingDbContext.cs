using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Ticketing.Domain.Entities;

namespace Ticketing.Infrastructure.Persistence;

public sealed class TicketingDbContext(DbContextOptions<TicketingDbContext> options, IUserContext? userContext)
    : TenantDbContext(options, userContext)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TicketingDbContext).Assembly);

      // Aislamiento por tenant y soft delete, compuestos en un solo filtro.
      ApplyTenantFilters(modelBuilder);
    }
}
