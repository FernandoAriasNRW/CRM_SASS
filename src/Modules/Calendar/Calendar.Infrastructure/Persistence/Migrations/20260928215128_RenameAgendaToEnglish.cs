using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Calendar.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bloque 6a: las columnas de la anulación pasan a inglés. Sólo cambian de nombre —los datos se
    /// quedan— y siguen en snake_case como el resto de la tabla.
    /// </summary>
    public partial class RenameAgendaToEnglish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "motivo_de_cancelacion",
                table: "calendar_events",
                newName: "cancellation_reason");

            migrationBuilder.RenameColumn(
                name: "cancelado_por",
                table: "calendar_events",
                newName: "cancelled_by");

            migrationBuilder.RenameColumn(
                name: "cancelado_en_utc",
                table: "calendar_events",
                newName: "cancelled_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "cancelled_by",
                table: "calendar_events",
                newName: "cancelado_por");

            migrationBuilder.RenameColumn(
                name: "cancelled_at_utc",
                table: "calendar_events",
                newName: "cancelado_en_utc");

            migrationBuilder.RenameColumn(
                name: "cancellation_reason",
                table: "calendar_events",
                newName: "motivo_de_cancelacion");
        }
    }
}
