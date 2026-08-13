using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkItems.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskAssignees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskAssignees",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    WorkTaskId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskAssignees", x => new { x.WorkTaskId, x.UserId });
                    table.ForeignKey(
                        name: "FK_TaskAssignees_Tasks_WorkTaskId",
                        column: x => x.WorkTaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Traspaso de los datos que ya existen: cada tarea asignada pasa a tener una fila de
            // responsable con quien ya tenía. Sin esto, las tareas de siempre aparecerían con
            // cero responsables mientras su AssigneeId sigue apuntando a alguien —el principal no
            // figuraría entre los responsables— y los filtros nuevos no las encontrarían. No
            // daría ningún error: simplemente dejarían de salir.
            //
            // Se excluye el Guid vacío porque ahí significa «sin asignar», no una persona.
            migrationBuilder.Sql("""
                INSERT INTO `TaskAssignees` (`WorkTaskId`, `UserId`)
                SELECT `Id`, `AssigneeId`
                FROM `Tasks`
                WHERE `AssigneeId` <> '00000000-0000-0000-0000-000000000000'
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskAssignees");
        }
    }
}
