using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PermisosEnSingular : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Los permisos guardados en plural pasan al singular, que es con lo que preguntan los
            // comandos (ver TiposDePermiso). Hasta ahora esas filas no las consultaba nadie.
            //
            // Si alguien ya tenía las dos —la del plural y la del singular para el mismo rol,
            // persona o equipo—, se queda la del singular: es la única que se ha estado aplicando,
            // así que conservarla no cambia lo que esa persona puede hacer hoy.
            foreach (var (plural, singular) in Renombres)
            {
                migrationBuilder.Sql($"""
                    DELETE plural FROM `EntityPermissions` AS plural
                    JOIN `EntityPermissions` AS singular
                      ON singular.`TenantId` = plural.`TenantId`
                     AND singular.`TargetType` = plural.`TargetType`
                     AND singular.`UserId` <=> plural.`UserId`
                     AND singular.`TeamId` <=> plural.`TeamId`
                     AND singular.`RoleName` <=> plural.`RoleName`
                     AND singular.`EntityId` = plural.`EntityId`
                     AND singular.`EntityType` = '{singular}'
                    WHERE plural.`EntityType` = '{plural}';
                    """);

                migrationBuilder.Sql(
                    $"UPDATE `EntityPermissions` SET `EntityType` = '{singular}' WHERE `EntityType` = '{plural}';");
            }
        }

        /// <summary>
        /// Plural → singular. Escrito aquí y no leído de <c>TiposDePermiso</c>: una migración
        /// tiene que hacer siempre lo mismo, aunque el código de hoy cambie mañana.
        /// </summary>
        private static readonly (string Plural, string Singular)[] Renombres =
        [
            ("Tasks", "Task"),
            ("Projects", "Project"),
            ("Tickets", "Ticket"),
            ("Docs", "Document"),
            ("Documents", "Document"),
            ("Webhooks", "Webhook"),
            ("Teams", "Team"),
            ("Reports", "Report"),
        ];

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin vuelta atrás: volver al plural sólo serviría para que los permisos por rol
            // dejaran otra vez de aplicarse, y no se sabría qué filas eran plurales de origen.
        }
    }
}
