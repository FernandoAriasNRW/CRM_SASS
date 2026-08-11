using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configuration;

public sealed class SavedViewConfiguration : IEntityTypeConfiguration<SavedView>
{
    public void Configure(EntityTypeBuilder<SavedView> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.TenantId).IsRequired();
        
        builder.Property(x => x.ModuleName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.ViewName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.StateJson)
            .IsRequired();
    }
}
