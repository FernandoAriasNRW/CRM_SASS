using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Projects.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProjectCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Projects",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Los proyectos que ya existían no guardaron su hora de alta. La fecha sí: es la de
            // inicio, que se pone al crearlos. Se rellena con ese día a las 00:00, que ordena bien
            // entre días distintos; entre los del mismo día, desempata el identificador como antes.
            migrationBuilder.Sql(
                "UPDATE `Projects` SET `CreatedAtUtc` = CAST(`StartDate` AS DATETIME(6)) WHERE `CreatedAtUtc` = '0001-01-01 00:00:00.000000';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Projects");
        }
    }
}
