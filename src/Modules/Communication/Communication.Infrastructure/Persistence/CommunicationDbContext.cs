using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using Communication.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Communication.Infrastructure.Persistence;

/// <summary>
/// DbContext del modulo Communication. Hereda de TenantDbContext: el aislamiento por
/// tenant y el soft delete se aplican solos a toda entidad marcada.
/// </summary>
public sealed class CommunicationsDbContext(DbContextOptions<CommunicationsDbContext> options, IUserContext? userContext)
    : TenantDbContext(options, userContext)
{
  public DbSet<Conversation> Conversations => Set<Conversation>();
  public DbSet<Message> Messages => Set<Message>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    // Aplicar configuraciones desde el ensamblado
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommunicationsDbContext).Assembly);

    // Filtros globales para soft delete

    // Aislamiento por tenant y soft delete, compuestos en un solo filtro.
    ApplyTenantFilters(modelBuilder);
  }

}
