using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Docs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AnotacionesEnDocumentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnotacionesEnDocumentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DocumentId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PageId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TextoCitado = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreadaPor = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreadaUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ResueltaUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ResueltaPor = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnotacionesEnDocumentos", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Anotaciones_TenantId_PageId",
                table: "AnotacionesEnDocumentos",
                columns: new[] { "TenantId", "PageId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnotacionesEnDocumentos");
        }
    }
}
