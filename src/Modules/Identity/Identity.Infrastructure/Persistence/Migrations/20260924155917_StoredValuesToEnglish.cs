using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Los favoritos guardan el tipo de entidad como texto («Tarea») y pasan al valor en inglés que
    /// ahora escribe <c>EntityTypes</c> («Task»). Sin esto, los favoritos que ya existían dejarían
    /// de salir en el filtro «Favoritos» sin ningún error. Los permisos no se tocan: ya estaban en
    /// inglés (<c>PermisosEnSingular</c>).
    /// </summary>
    public partial class StoredValuesToEnglish : Migration
    {
        /// <summary>
        /// Español → inglés. Escrito aquí y no leído de <c>EntityTypes</c>: una migración tiene que
        /// hacer siempre lo mismo, aunque el código de hoy cambie mañana.
        /// </summary>
        private static readonly (string Spanish, string English)[] Renames =
        [
            ("Tarea", "Task"),
            ("Proyecto", "Project"),
            ("Documento", "Document"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (spanish, english) in Renames)
                migrationBuilder.Sql(
                    $"UPDATE `Favorites` SET `EntityType` = '{english}' WHERE `EntityType` = '{spanish}';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (spanish, english) in Renames)
                migrationBuilder.Sql(
                    $"UPDATE `Favorites` SET `EntityType` = '{spanish}' WHERE `EntityType` = '{english}';");
        }
    }
}
