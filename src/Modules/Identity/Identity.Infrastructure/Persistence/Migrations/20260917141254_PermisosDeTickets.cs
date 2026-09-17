using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PermisosDeTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Los tickets empiezan a pedir autorización y la pantalla de permisos no tenía fila para
            // ellos. Sin fila, un miembro seguiría pudiendo (es su valor por defecto), pero un
            // invitado dejaría de poder abrir y seguir tickets de golpe, sin que nadie lo decidiera.
            // Se siembra lo que ya podían hacer, en cada organización con permisos por rol.
            foreach (var rol in new[] { "Member", "Guest" })
            {
                migrationBuilder.Sql($"""
                    INSERT INTO `EntityPermissions`
                        (`Id`, `TenantId`, `TargetType`, `UserId`, `TeamId`, `RoleName`, `EntityType`, `EntityId`, `PermissionLevel`)
                    SELECT UUID(), t.`TenantId`, 'Role', NULL, NULL, '{rol}', 'Ticket', '00000000-0000-0000-0000-000000000000', 'Edit'
                    FROM (SELECT DISTINCT `TenantId` FROM `EntityPermissions` WHERE `TargetType` = 'Role') AS t
                    WHERE NOT EXISTS (
                        SELECT 1 FROM `EntityPermissions` AS p
                        WHERE p.`TenantId` = t.`TenantId` AND p.`TargetType` = 'Role'
                          AND p.`RoleName` = '{rol}' AND p.`EntityType` = 'Ticket'
                          AND p.`EntityId` = '00000000-0000-0000-0000-000000000000');
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
