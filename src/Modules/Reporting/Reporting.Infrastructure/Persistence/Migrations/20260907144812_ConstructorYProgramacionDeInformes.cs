using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConstructorYProgramacionDeInformes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefinicionJson",
                table: "Reports",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Programaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ReportId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DestinatarioId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FrecuenciaValue = table.Column<int>(type: "int", nullable: false),
                    FormatoValue = table.Column<int>(type: "int", nullable: false),
                    Hora = table.Column<TimeOnly>(type: "time(6)", nullable: false),
                    Dia = table.Column<int>(type: "int", nullable: true),
                    Activa = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    UltimoDiaGenerado = table.Column<DateOnly>(type: "date", nullable: true),
                    CreadaUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Programaciones", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Programaciones_Activa",
                table: "Programaciones",
                column: "Activa");

            migrationBuilder.CreateIndex(
                name: "IX_Programaciones_TenantId_ReportId",
                table: "Programaciones",
                columns: new[] { "TenantId", "ReportId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Programaciones");

            migrationBuilder.DropColumn(
                name: "DefinicionJson",
                table: "Reports");
        }
    }
}
