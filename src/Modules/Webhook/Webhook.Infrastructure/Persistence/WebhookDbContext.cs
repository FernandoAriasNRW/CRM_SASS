using System.Text.Json;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Webhook.Domain.Entities;

namespace Webhook.Infrastructure.Persistence;

public sealed class WebhookDbContext(DbContextOptions<WebhookDbContext> options, IUserContext? userContext)
    : TenantDbContext(options, userContext)
{
    public DbSet<WebhookSubscription> Subscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDelivery> Deliveries => Set<WebhookDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new WebhookSubscriptionConfiguration());
        modelBuilder.ApplyConfiguration(new WebhookDeliveryConfiguration());

        // Aislamiento por tenant y soft delete, compuestos en un solo filtro.
        ApplyTenantFilters(modelBuilder);
    }
}

internal sealed class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.ToTable("WebhookSubscriptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(WebhookSubscription.NameMaxLength).IsRequired();
        builder.Property(x => x.TargetUrl).HasMaxLength(WebhookSubscription.UrlMaxLength).IsRequired();
        builder.Property(x => x.Secret).HasMaxLength(500).IsRequired();

        // La lista de eventos, como JSON en una columna: se lee entera con la suscripción y nunca
        // se busca dentro desde SQL —una organización tiene pocas suscripciones y se filtran en
        // memoria—, así que una tabla aparte sólo añadiría una unión.
        builder.Ignore(x => x.EventTypes);
        builder.Property<List<string>>("_eventTypes")
            .HasColumnName("EventTypes")
            .HasColumnType("json")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
                v => v.Aggregate(0, (hash, e) => HashCode.Combine(hash, e.GetHashCode())),
                v => v.ToList()));

        builder.HasIndex(x => x.TenantId);
    }
}

internal sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("WebhookDeliveries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Payload).HasColumnType("longtext").IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(WebhookDelivery.ErrorMaxLength);

        // La consulta del trabajo de entrega: lo pendiente cuyo turno ha llegado.
        builder.HasIndex(x => new { x.Status, x.NextAttemptAtUtc }).HasDatabaseName("IX_WebhookDeliveries_Status_NextAttemptAtUtc");

        // Y la de la administración: los últimos envíos de una suscripción.
        builder.HasIndex(x => new { x.TenantId, x.SubscriptionId, x.CreatedAtUtc }).HasDatabaseName("IX_WebhookDeliveries_Tenant_Subscription_CreatedAtUtc");
    }
}
