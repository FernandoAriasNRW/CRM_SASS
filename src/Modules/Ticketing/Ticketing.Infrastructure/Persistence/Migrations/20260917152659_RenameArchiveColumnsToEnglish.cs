using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticketing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameArchiveColumnsToEnglish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BorradoEnUtc",
                table: "Tickets",
                newName: "DeletedAtUtc");

            migrationBuilder.RenameColumn(
                name: "ArchivadoEnUtc",
                table: "Tickets",
                newName: "ArchivedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DeletedAtUtc",
                table: "Tickets",
                newName: "BorradoEnUtc");

            migrationBuilder.RenameColumn(
                name: "ArchivedAtUtc",
                table: "Tickets",
                newName: "ArchivadoEnUtc");
        }
    }
}
