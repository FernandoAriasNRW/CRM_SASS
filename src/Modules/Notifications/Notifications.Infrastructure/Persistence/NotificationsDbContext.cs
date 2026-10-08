using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Notifications.Domain.Entities;

namespace Notifications.Infrastructure.Persistence;

/// <summary>
/// DbContext del modulo Notifications. Hereda de TenantDbContext: el aislamiento por
/// tenant y el soft delete se aplican solos a toda entidad marcada.
/// </summary>
public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options, IUserContext? userContext)
    : TenantDbContext(options, userContext)
{
  public DbSet<Notification> Notifications => Set<Notification>();
  public DbSet<NotificationPreferences> NotificationPreferences => Set<NotificationPreferences>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    // Aplicar configuraciones desde el ensamblado
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);

    // De qué trata el aviso y a qué se refiere, con longitud: sin ella serían columnas TEXT.
    modelBuilder.Entity<Notification>(n =>
    {
      n.Property(x => x.Kind).HasMaxLength(60);
      n.Property(x => x.EntityType).HasMaxLength(30);
    });

    modelBuilder.Entity<NotificationPreferences>(p =>
    {
      p.ToTable("NotificationPreferences");

      // Una sola fila por persona y organización. La restricción va en la base y no sólo en el
      // código porque dos pestañas guardando a la vez pueden intentar crear dos filas, y
      // entonces cuál de las dos se lee al día siguiente es cuestión de suerte.
      p.HasIndex(x => new { x.TenantId, x.UserId })
       .IsUnique()
       .HasDatabaseName("UX_NotificationPreferences_Tenant_User");

      // TimeOnly va a una columna `time`, no a texto: comparar horas es lo único que se hace
      // con esto, y sobre texto la comparación sería alfabética.
      // Lo que cada persona cambió respecto al catálogo, por tipo de aviso. En JSON: se lee
      // entero con la fila y nunca se busca dentro.
      p.Ignore(x => x.Types);
      p.Property<Dictionary<string, bool>>("_types")
        .HasColumnName("Types")
        .HasColumnType("json")
        .HasConversion(
            v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
            v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, bool>())
        .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<Dictionary<string, bool>>(
            (a, b) => (a ?? new Dictionary<string, bool>()).OrderBy(x => x.Key).SequenceEqual((b ?? new Dictionary<string, bool>()).OrderBy(x => x.Key)),
            v => v.Aggregate(0, (hash, e) => HashCode.Combine(hash, e.Key.GetHashCode(), e.Value.GetHashCode())),
            v => v.ToDictionary(x => x.Key, x => x.Value)));

      p.Property(x => x.QuietHoursStart).HasColumnType("time");
      p.Property(x => x.QuietHoursEnd).HasColumnType("time");
    });

    // Aislamiento por tenant y soft delete, compuestos en un solo filtro.
    ApplyTenantFilters(modelBuilder);
  }

}
