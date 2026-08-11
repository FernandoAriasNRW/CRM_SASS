using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BuildingBlocks.Infrastructure.Webhooks.Webhook.Domain;

namespace BuildingBlocks.Infrastructure.Webhooks.Webhook.Persistence.Configuration;

public class WebhookConfiguration : IEntityTypeConfiguration<WebhookEntity>
{
    public void Configure(EntityTypeBuilder<WebhookEntity> builder)
    {
        builder.ToTable("webhooks");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Url).IsRequired();
        builder.Property(x => x.Event).IsRequired();
        builder.Property(x => x.Secret).IsRequired();
    }
}