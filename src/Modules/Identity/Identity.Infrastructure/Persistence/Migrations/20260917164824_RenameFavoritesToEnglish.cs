using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameFavoritesToEnglish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Renombrar, no borrar y crear: es lo que EF propone al cambiar el nombre de la tabla, y
            // se llevaba por delante todos los favoritos guardados.
            migrationBuilder.RenameTable(name: "Favoritos", newName: "Favorites");
            migrationBuilder.RenameColumn(name: "Tipo", table: "Favorites", newName: "EntityType");
            migrationBuilder.RenameColumn(name: "MarcadoUtc", table: "Favorites", newName: "MarkedAtUtc");
            migrationBuilder.RenameIndex(name: "UX_Favoritos_Usuario_Entidad", table: "Favorites", newName: "UX_Favorites_User_Entity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(name: "UX_Favorites_User_Entity", table: "Favorites", newName: "UX_Favoritos_Usuario_Entidad");
            migrationBuilder.RenameColumn(name: "MarkedAtUtc", table: "Favorites", newName: "MarcadoUtc");
            migrationBuilder.RenameColumn(name: "EntityType", table: "Favorites", newName: "Tipo");
            migrationBuilder.RenameTable(name: "Favorites", newName: "Favoritos");
        }
    }
}
