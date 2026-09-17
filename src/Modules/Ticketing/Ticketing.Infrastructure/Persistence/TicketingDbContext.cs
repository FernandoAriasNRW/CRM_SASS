using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Ticketing.Domain.Entities;

namespace Ticketing.Infrastructure.Persistence;

public sealed class TicketingDbContext(DbContextOptions<TicketingDbContext> options, IUserContext? userContext)
    : TenantDbContext(options, userContext)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<IntakeKey> IntakeKeys => Set<IntakeKey>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TicketingDbContext).Assembly);

        // Ahora **todas** las consultas llevan `ArchivedAtUtc IS NULL` y `IsDeleted = 0`,
        // porque el filtro global los añade. Sin este índice, esa condición se evalúa fila a
        // fila sobre el resultado del filtro de inquilino en cada listado.
        modelBuilder.Entity<Ticket>()
            .HasIndex(t => new { t.TenantId, t.ArchivedAtUtc, t.IsDeleted })
            .HasDatabaseName("IX_Tickets_TenantId_ArchivedAtUtc_IsDeleted");

        modelBuilder.Entity<Ticket>(t =>
        {
            t.Property(x => x.Source).HasMaxLength(20).HasDefaultValue(Ticket.SourceApp);
            t.Property(x => x.RequesterName).HasMaxLength(200);
            t.Property(x => x.RequesterEmail).HasMaxLength(320);
            t.Property(x => x.RequesterPhone).HasMaxLength(40);
            t.Property(x => x.RequesterCompany).HasMaxLength(200);
            t.Property(x => x.Classification).HasMaxLength(100);
            t.Property(x => x.Tags).HasMaxLength(1000).HasDefaultValue(string.Empty);
            t.Ignore(x => x.TagList);
        });

        modelBuilder.Entity<TicketAttachment>(a =>
        {
            a.ToTable("TicketAttachments");
            a.HasKey(x => x.Id);
            a.Property(x => x.Name).HasMaxLength(255).IsRequired();
            a.Property(x => x.Url).HasMaxLength(1000).IsRequired();
            a.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            a.HasIndex(x => new { x.TenantId, x.TicketId });
        });

        modelBuilder.Entity<IntakeKey>(c =>
        {
            c.ToTable("IntakeKeys");
            c.HasKey(x => x.Id);
            c.Ignore(x => x.IsActive);
            c.Property(x => x.Name).HasMaxLength(100).IsRequired();
            c.Property(x => x.Prefix).HasMaxLength(20).IsRequired();
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
