using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using Calendar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Calendar.Infrastructure.Persistence;

/// <summary>
/// DbContext para el módulo Calendar.
/// </summary>
public sealed class CalendarDbContext(DbContextOptions<CalendarDbContext> options, IUserContext? userContext)
    : TenantDbContext(options, userContext)
{
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplicar configuraciones desde el ensamblado
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CalendarDbContext).Assembly);

      // Aislamiento por tenant y soft delete, compuestos en un solo filtro.
      ApplyTenantFilters(modelBuilder);
    }

}
