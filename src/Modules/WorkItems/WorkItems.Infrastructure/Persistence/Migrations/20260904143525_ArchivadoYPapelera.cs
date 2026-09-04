using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkItems.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ArchivadoYPapelera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivadoEnUtc",
                table: "Tasks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BorradoEnUtc",
                table: "Tasks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Tasks",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_TenantId_Archivado_Borrado",
                table: "Tasks",
                columns: new[] { "TenantId", "ArchivadoEnUtc", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tasks_TenantId_Archivado_Borrado",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ArchivadoEnUtc",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "BorradoEnUtc",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Tasks");
        }
    }
}
