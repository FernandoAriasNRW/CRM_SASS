using Docs.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Docs.Infrastructure.Persistence.Configurations;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Title).HasMaxLength(255).IsRequired();
        builder.Property(d => d.Description).HasMaxLength(1000);

        builder.HasMany(d => d.Pages)
            .WithOne()
            .HasForeignKey(p => p.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Metadata.FindNavigation(nameof(Document.Pages))?.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(d => d.Permissions)
            .WithOne()
            .HasForeignKey(p => p.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.Metadata.FindNavigation(nameof(Document.Permissions))?.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
