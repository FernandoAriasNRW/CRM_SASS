using Calendar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Calendar.Infrastructure.Persistence;

/// <summary>
/// DbContext para el módulo Calendar.
/// Configura el filtro global de soft delete y las configuraciones de entidad.
/// </summary>
public sealed class CalendarDbContext(DbContextOptions<CalendarDbContext> options) : DbContext(options)
{
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplicar configuraciones desde el ensamblado
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CalendarDbContext).Assembly);

        // Filtro global para soft delete
        modelBuilder.Entity<CalendarEvent>().HasQueryFilter(e => !e.IsDeleted);
    }

    /// <summary>
    /// Método para desactivar filtros globales (usado en Queries de auditoría).
    /// </summary>
    public void DisableGlobalFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CalendarEvent>().HasQueryFilter(e => true);
    }

    /// <summary>
    /// Método para reactivar filtros globales.
    /// </summary>
    public void EnableGlobalFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CalendarEvent>().HasQueryFilter(e => !e.IsDeleted);
    }
}