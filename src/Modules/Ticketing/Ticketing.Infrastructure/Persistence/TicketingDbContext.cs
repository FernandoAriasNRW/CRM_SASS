using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Ticketing.Domain.Entities;

namespace Ticketing.Infrastructure.Persistence;

public sealed class TicketingDbContext(DbContextOptions<TicketingDbContext> options, IUserContext? userContext)
    : TenantDbContext(options, userContext)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<ClaveDeEntrada> ClavesDeEntrada => Set<ClaveDeEntrada>();

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

        modelBuilder.Entity<Ticket>(t =>
        {
            t.Property(x => x.Origen).HasMaxLength(20).HasDefaultValue(Ticket.OrigenAplicacion);
            t.Property(x => x.SolicitanteNombre).HasMaxLength(200);
            t.Property(x => x.SolicitanteEmail).HasMaxLength(320);
        });

        modelBuilder.Entity<ClaveDeEntrada>(c =>
        {
            c.ToTable("ClavesDeEntrada");
            c.HasKey(x => x.Id);
            c.Ignore(x => x.EstaActiva);
            c.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
            c.Property(x => x.Inicio).HasMaxLength(20).IsRequired();
            c.Property(x => x.Hash).HasMaxLength(64).IsRequired();

            // Único: es por lo que se busca en cada ticket que entra, y dos claves con el mismo
            // hash serían la misma clave en dos organizaciones.
            c.HasIndex(x => x.Hash).IsUnique();
            c.HasIndex(x => x.TenantId);
        });

        // Aislamiento por tenant, papelera y archivado, compuestos en un solo filtro.
        ApplyTenantFilters(modelBuilder);
    }
}
