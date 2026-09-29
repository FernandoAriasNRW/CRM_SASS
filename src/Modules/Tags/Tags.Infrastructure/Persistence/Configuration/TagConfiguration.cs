using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tags.Domain.Entities;

namespace Tags.Infrastructure.Persistence.Configuration;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.ColorHex)
            .HasMaxLength(10);

        builder.Property(t => t.Category)
            .IsRequired()
            .HasMaxLength(50);
            
        builder.Property(t => t.BuiltInKey)
            .HasMaxLength(50);

        // Único por categoría, no en toda la organización: un proyecto llamado «Beta» y el hito
        // predefinido «Beta» son dos etiquetas distintas. Con el índice anterior (TenantId, Name) el
        // alta automática de la etiqueta del proyecto chocaba contra él.
        builder.HasIndex(t => new { t.TenantId, t.Category, t.Name }).IsUnique();
    }
}
