using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Docs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PagesCarryTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Pages",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            // Cada página existente toma el inquilino de su documento. Sin esto quedarían todas con
            // Guid.Empty, el filtro no las dejaría ver y los documentos se abrirían vacíos.
            migrationBuilder.Sql(
                "UPDATE `Pages` p JOIN `Documents` d ON d.`Id` = p.`DocumentId` SET p.`TenantId` = d.`TenantId`;");

            migrationBuilder.CreateIndex(
                name: "IX_Pages_TenantId_DocumentId",
                table: "Pages",
                columns: new[] { "TenantId", "DocumentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pages_TenantId_DocumentId",
                table: "Pages");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Pages");
        }
    }
}
