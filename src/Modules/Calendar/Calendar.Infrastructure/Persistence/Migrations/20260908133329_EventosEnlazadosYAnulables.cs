using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Calendar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EventosEnlazadosYAnulables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "cancelado_en_utc",
                table: "calendar_events",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cancelado_por",
                table: "calendar_events",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "motivo_de_cancelacion",
                table: "calendar_events",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "ticket_id",
                table: "calendar_events",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cancelado_en_utc",
                table: "calendar_events");

            migrationBuilder.DropColumn(
                name: "cancelado_por",
                table: "calendar_events");

            migrationBuilder.DropColumn(
                name: "motivo_de_cancelacion",
                table: "calendar_events");

            migrationBuilder.DropColumn(
                name: "ticket_id",
                table: "calendar_events");
        }
    }
}
