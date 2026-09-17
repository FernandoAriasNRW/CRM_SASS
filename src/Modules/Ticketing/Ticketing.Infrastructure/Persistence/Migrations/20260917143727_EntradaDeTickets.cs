using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticketing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EntradaDeTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClaveDeEntradaId",
                table: "Tickets",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "Origen",
                table: "Tickets",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Aplicacion")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SolicitanteEmail",
                table: "Tickets",
                type: "varchar(320)",
                maxLength: 320,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SolicitanteNombre",
                table: "Tickets",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ClavesDeEntrada",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Nombre = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Inicio = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreadaPor = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreadaUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UltimoUsoUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RevocadaUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClavesDeEntrada", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ClavesDeEntrada_Hash",
                table: "ClavesDeEntrada",
                column: "Hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClavesDeEntrada_TenantId",
                table: "ClavesDeEntrada",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClavesDeEntrada");

            migrationBuilder.DropColumn(
                name: "ClaveDeEntradaId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Origen",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SolicitanteEmail",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SolicitanteNombre",
                table: "Tickets");
        }
    }
}
