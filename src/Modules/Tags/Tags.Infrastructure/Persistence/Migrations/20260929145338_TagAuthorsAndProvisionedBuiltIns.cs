using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tags.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Para poder editar y borrar etiquetas: quién creó cada una (<c>Tags.CreatedBy</c>, que puede
    /// gestionarla sin ser administrador) y qué predefinidas se entregaron ya a cada organización
    /// (<c>ProvisionedBuiltInTags</c>), para que una borrada o renombrada no vuelva al arrancar.
    ///
    /// Las etiquetas que ya existían quedan sin autor: todas las creó el sistema (predefinidas,
    /// de equipos y proyectos, de la demostración) o se crearon antes de que se guardara, y sólo
    /// podrán gestionarlas los administradores y quien tenga el permiso.
    ///
    /// El relleno se escribió a mano: da por entregadas las predefinidas que cada organización
    /// tiene hoy.
    /// </summary>
    public partial class TagAuthorsAndProvisionedBuiltIns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "Tags",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "ProvisionedBuiltInTags",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BuiltInKey = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProvisionedBuiltInTags", x => new { x.TenantId, x.BuiltInKey });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(
                "INSERT INTO `ProvisionedBuiltInTags` (`TenantId`, `BuiltInKey`) "
                + "SELECT DISTINCT `TenantId`, `BuiltInKey` FROM `Tags` WHERE `BuiltInKey` IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProvisionedBuiltInTags");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Tags");
        }
    }
}
