using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Docs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameArchiveColumnsToEnglish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ArchivadoEnUtc",
                table: "Documents",
                newName: "ArchivedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ArchivedAtUtc",
                table: "Documents",
                newName: "ArchivadoEnUtc");
        }
    }
}
