using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tags.Domain.Entities;

namespace Tags.Infrastructure.Persistence.Configuration;

public class ProvisionedBuiltInTagConfiguration : IEntityTypeConfiguration<ProvisionedBuiltInTag>
{
    public void Configure(EntityTypeBuilder<ProvisionedBuiltInTag> builder)
    {
        builder.ToTable("ProvisionedBuiltInTags");
        builder.HasKey(p => new { p.TenantId, p.BuiltInKey });

        builder.Property(p => p.BuiltInKey)
            .HasMaxLength(50);
    }
}
