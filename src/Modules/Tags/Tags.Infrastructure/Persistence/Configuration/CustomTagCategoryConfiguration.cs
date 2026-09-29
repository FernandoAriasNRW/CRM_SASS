using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tags.Domain.Entities;

namespace Tags.Infrastructure.Persistence.Configuration;

public class CustomTagCategoryConfiguration : IEntityTypeConfiguration<CustomTagCategory>
{
    public void Configure(EntityTypeBuilder<CustomTagCategory> builder)
    {
        builder.ToTable("CustomTagCategories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(CustomTagCategory.MaxNameLength);

        builder.HasIndex(c => new { c.TenantId, c.Name }).IsUnique();
    }
}
