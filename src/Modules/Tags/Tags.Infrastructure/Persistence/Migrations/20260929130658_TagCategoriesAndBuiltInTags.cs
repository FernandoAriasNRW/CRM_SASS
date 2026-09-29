using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tags.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Categorías de etiquetas: las predefinidas pasan a ser Team, Project, Milestone, Business,
    /// Security, WorkType y DevelopmentPhase, y cada organización puede crear las suyas
    /// (<c>CustomTagCategories</c>). Las etiquetas predefinidas llevan su clave (<c>BuiltInKey</c>)
    /// para mostrarse en el idioma de quien las mira.
    ///
    /// Los pasos de datos se escribieron a mano:
    /// <list type="bullet">
    /// <item>Se borran las etiquetas de «Tech» y «Priority». La prioridad ya es un campo de tareas y
    /// tickets, y las de «Tech» se decidió quitarlas. Sólo las tenía la siembra de demostración:
    /// hasta este cambio el alta por la API no guardaba nada.</item>
    /// <item>«⭐ VIP Client» pasa a ser la predefinida «Cliente VIP»; si no, el aprovisionamiento la
    /// crearía al lado y habría dos.</item>
    /// <item>Cualquier otra categoría en uso que no sea predefinida se da de alta como personalizada
    /// de su organización, para que ninguna etiqueta quede apuntando a una categoría que no existe.</item>
    /// </list>
    /// El <c>Down</c> deshace el esquema pero no devuelve las etiquetas borradas.
    /// </summary>
    public partial class TagCategoriesAndBuiltInTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tags_TenantId_Name",
                table: "Tags");

            migrationBuilder.AddColumn<string>(
                name: "BuiltInKey",
                table: "Tags",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CustomTagCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomTagCategories", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("DELETE FROM `Tags` WHERE `Category` IN ('Tech', 'Priority');");

            migrationBuilder.Sql(
                "UPDATE `Tags` SET `BuiltInKey` = 'vip-client', `Name` = 'Cliente VIP' "
                + "WHERE `Category` = 'Business' AND `Name` = '⭐ VIP Client';");

            migrationBuilder.Sql(
                "INSERT INTO `CustomTagCategories` (`Id`, `TenantId`, `Name`) "
                + "SELECT UUID(), `TenantId`, `Category` FROM `Tags` "
                + "WHERE `Category` NOT IN ('Team', 'Project', 'Milestone', 'Business', 'Security', 'WorkType', 'DevelopmentPhase') "
                + "GROUP BY `TenantId`, `Category`;");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_TenantId_Category_Name",
                table: "Tags",
                columns: new[] { "TenantId", "Category", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomTagCategories_TenantId_Name",
                table: "CustomTagCategories",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomTagCategories");

            migrationBuilder.DropIndex(
                name: "IX_Tags_TenantId_Category_Name",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "BuiltInKey",
                table: "Tags");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_TenantId_Name",
                table: "Tags",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }
    }
}
