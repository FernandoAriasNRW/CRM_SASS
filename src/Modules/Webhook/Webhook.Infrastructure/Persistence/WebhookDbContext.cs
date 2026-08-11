using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Webhook.Domain.Entities;

namespace Webhook.Infrastructure.Persistence;

public sealed class WebhookDbContext(DbContextOptions<WebhookDbContext> options, IUserContext? userContext)
    : TenantDbContext(options, userContext)
{
    public DbSet<WebhookSubscription> Subscriptions => Set<WebhookSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new WebhookSubscriptionConfiguration());

      // Aislamiento por tenant y soft delete, compuestos en un solo filtro.
      ApplyTenantFilters(modelBuilder);
    }
}

internal sealed class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.ToTable("webhook_subscriptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.EventName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TargetUrl).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.Secret).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.EventName });
    }
}
