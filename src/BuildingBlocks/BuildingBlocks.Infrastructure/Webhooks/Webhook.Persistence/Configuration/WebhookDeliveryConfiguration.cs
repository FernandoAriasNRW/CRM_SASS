using BuildingBlocks.Infrastructure.Webhooks.Webhook.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildingBlocks.Infrastructure.Webhooks.Webhook.Persistence.Configuration;

public class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
  public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
  {
    builder.ToTable("webhook_deliveries");

    builder.HasKey(x => x.Id);

    builder.Property(x => x.Payload).IsRequired();
    builder.Property(x => x.Status).IsRequired();
  }
}