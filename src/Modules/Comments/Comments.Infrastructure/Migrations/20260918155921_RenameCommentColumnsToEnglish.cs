using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameCommentColumnsToEnglish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Texto",
                table: "Comments",
                newName: "Text");

            migrationBuilder.RenameColumn(
                name: "RespondeAId",
                table: "Comments",
                newName: "ReplyToId");

            migrationBuilder.RenameColumn(
                name: "EntidadDestino",
                table: "Comments",
                newName: "EntityType");

            migrationBuilder.RenameColumn(
                name: "EditadoUtc",
                table: "Comments",
                newName: "EditedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreadoUtc",
                table: "Comments",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "AutorId",
                table: "Comments",
                newName: "AuthorId");

            migrationBuilder.RenameIndex(
                name: "IX_Comments_Tenant_Entidad_Creado",
                table: "Comments",
                newName: "IX_Comments_TenantId_EntityType_EntityId_CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Text",
                table: "Comments",
                newName: "Texto");

            migrationBuilder.RenameColumn(
                name: "ReplyToId",
                table: "Comments",
                newName: "RespondeAId");

            migrationBuilder.RenameColumn(
                name: "EntityType",
                table: "Comments",
                newName: "EntidadDestino");

            migrationBuilder.RenameColumn(
                name: "EditedAtUtc",
                table: "Comments",
                newName: "EditadoUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "Comments",
                newName: "CreadoUtc");

            migrationBuilder.RenameColumn(
                name: "AuthorId",
                table: "Comments",
                newName: "AutorId");

            migrationBuilder.RenameIndex(
                name: "IX_Comments_TenantId_EntityType_EntityId_CreatedAtUtc",
                table: "Comments",
                newName: "IX_Comments_Tenant_Entidad_Creado");
        }
    }
}
