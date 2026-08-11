using Communication.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Communication.Infrastructure.Persistence;

/// <summary>
/// DbContext para el módulo Communication. Configura el filtro global de soft delete y las configuraciones de entidad.
/// </summary>
public sealed class CommunicationsDbContext(DbContextOptions<CommunicationsDbContext> options) : DbContext(options)
{
  public DbSet<Conversation> Conversations => Set<Conversation>();
  public DbSet<Message> Messages => Set<Message>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    // Aplicar configuraciones desde el ensamblado
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommunicationsDbContext).Assembly);

    // Filtros globales para soft delete
    modelBuilder.Entity<Conversation>().HasQueryFilter(e => !e.IsDeleted);
    modelBuilder.Entity<Message>().HasQueryFilter(e => !e.IsDeleted);
  }

  /// <summary>
  /// Desactiva los filtros globales para consultas de auditoría.
  /// </summary>
  public void DisableGlobalFilters(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<Conversation>().HasQueryFilter(e => true);
    modelBuilder.Entity<Message>().HasQueryFilter(e => true);
  }

  /// <summary>
  /// Reactiva los filtros globales.
  /// </summary>
  public void EnableGlobalFilters(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<Conversation>().HasQueryFilter(e => !e.IsDeleted);
    modelBuilder.Entity<Message>().HasQueryFilter(e => !e.IsDeleted);
  }
}