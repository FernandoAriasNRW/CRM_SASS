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

        // Ahora **todas** las consultas llevan `ArchivadoEnUtc IS NULL` y `IsDeleted = 0`,
        // porque el filtro global los añade. Sin este índice, esa condición se evalúa fila a
        // fila sobre el resultado del filtro de inquilino en cada listado.
        modelBuilder.Entity<Ticket>()
            .HasIndex(t => new { t.TenantId, t.ArchivadoEnUtc, t.IsDeleted })
            .HasDatabaseName("IX_Tickets_TenantId_Archivado_Borrado");

        // Aislamiento por tenant, papelera y archivado, compuestos en un solo filtro.
        ApplyTenantFilters(modelBuilder);
    }
}
