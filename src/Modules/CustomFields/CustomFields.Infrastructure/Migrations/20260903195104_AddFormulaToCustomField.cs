using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CustomFields.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFormulaToCustomField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Formula",
                table: "CustomFieldDefinitions",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Formula",
                table: "CustomFieldDefinitions");
        }
    }
}
