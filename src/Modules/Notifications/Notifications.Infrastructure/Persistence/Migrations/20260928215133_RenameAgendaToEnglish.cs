using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bloque 6a: la preferencia de aviso de exportación terminada pasa a llamarse como su tipo de
    /// aviso, «ExportReady», que ya era el nombre del campo en la API.
    /// </summary>
    public partial class RenameAgendaToEnglish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ExportacionLista",
                table: "NotificationPreferences",
                newName: "ExportReady");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ExportReady",
                table: "NotificationPreferences",
                newName: "ExportacionLista");
        }
    }
}
