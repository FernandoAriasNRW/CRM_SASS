using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkItems.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameArchiveColumnsToEnglish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BorradoEnUtc",
                table: "Tasks",
                newName: "DeletedAtUtc");

            migrationBuilder.RenameColumn(
                name: "ArchivadoEnUtc",
                table: "Tasks",
                newName: "ArchivedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DeletedAtUtc",
                table: "Tasks",
                newName: "BorradoEnUtc");

            migrationBuilder.RenameColumn(
                name: "ArchivedAtUtc",
                table: "Tasks",
                newName: "ArchivadoEnUtc");
        }
    }
}
