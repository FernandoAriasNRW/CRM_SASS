using BuildingBlocks.Domain.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildingBlocks.Infrastructure.Persistence;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessageEntity>
{
  public void Configure(EntityTypeBuilder<OutboxMessageEntity> builder)
  {
    builder.ToTable("outbox_messages"); // Nombre consistente en todos los módulos

    builder.HasKey(x => x.Id);

    builder.Property(x => x.Type)
        .HasMaxLength(255)
        .IsRequired();

    builder.Property(x => x.Payload)
        .IsRequired();

    // Índice vital para que el Worker de Webhooks no sea lento Buscamos los que NO estén procesados ordenados por fecha
    builder.HasIndex(x => new { x.ProcessedAt, x.CreatedAt });
  }
}