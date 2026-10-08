using Docs.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Docs.Infrastructure.Persistence.Configurations;

public sealed class PageConfiguration : IEntityTypeConfiguration<Page>
{
    public void Configure(EntityTypeBuilder<Page> builder)
    {
        builder.HasKey(p => p.Id);

        // Las páginas de un documento, dentro de su inquilino: es la consulta del árbol lateral.
        builder.HasIndex(p => new { p.TenantId, p.DocumentId });

        builder.Property(p => p.Title).HasMaxLength(255).IsRequired();
        builder.Property(p => p.Content).HasColumnType("longtext");

        builder.HasOne<Page>()
            .WithMany(p => p.SubPages)
            .HasForeignKey(p => p.ParentPageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
