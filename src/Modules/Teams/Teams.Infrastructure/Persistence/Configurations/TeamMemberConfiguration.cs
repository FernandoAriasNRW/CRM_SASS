using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Teams.Domain.Entities;

namespace Teams.Infrastructure.Persistence.Configurations;

internal sealed class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.ToTable("TeamMembers");
        builder.HasKey(m => m.Id);
        
        builder.HasIndex(m => m.UserId);

        builder.OwnsOne(m => m.Role, role =>
        {
            role.Property(r => r.Name).HasColumnName("RoleName").HasMaxLength(50).IsRequired();
            role.Property(r => r.CanManageProjects).HasColumnName("CanManageProjects");
            role.Property(r => r.CanManageTasks).HasColumnName("CanManageTasks");
            role.Property(r => r.CanManageTickets).HasColumnName("CanManageTickets");
            role.Property(r => r.CanManageMembers).HasColumnName("CanManageMembers");
        });
    }
}
