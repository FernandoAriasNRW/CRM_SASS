using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public sealed class EntityPermissionConfiguration : IEntityTypeConfiguration<EntityPermission>
{
    public void Configure(EntityTypeBuilder<EntityPermission> builder)
    {
        builder.ToTable("EntityPermissions");

        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.TargetType).HasMaxLength(20).HasDefaultValue("User");
        builder.Property(e => e.EntityType).HasMaxLength(50);
        builder.Property(e => e.PermissionLevel).HasMaxLength(20);
        builder.Property(e => e.RoleName).HasMaxLength(50);

        builder.HasIndex(e => new { e.TenantId, e.TargetType, e.UserId, e.TeamId, e.RoleName, e.EntityType, e.EntityId });
    }
}
