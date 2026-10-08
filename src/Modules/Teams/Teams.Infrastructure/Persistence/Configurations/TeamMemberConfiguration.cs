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

        // La clave la pone el dominio al crear el miembro, no la base. Dicho así, EF da por nuevo
        // al miembro que encuentra en la colección de un equipo ya cargado. Como clave
        // «generada», al tener valor lo daba por existente y mandaba un UPDATE que no tocaba
        // ninguna fila: añadir a alguien a un equipo acababa en DbUpdateConcurrencyException.
        builder.Property(m => m.Id).ValueGeneratedNever();
        
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
