using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkItems.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskRecurrence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Recurrence_DiaDeLaSerie",
                table: "Tasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "Recurrence_FechaFin",
                table: "Tasks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Recurrence_Frecuencia",
                table: "Tasks",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "Recurrence_Intervalo",
                table: "Tasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "Recurrence_ProximaOcurrencia",
                table: "Tasks",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_Recurrence_ProximaOcurrencia",
                table: "Tasks",
                column: "Recurrence_ProximaOcurrencia");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tasks_Recurrence_ProximaOcurrencia",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Recurrence_DiaDeLaSerie",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Recurrence_FechaFin",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Recurrence_Frecuencia",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Recurrence_Intervalo",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Recurrence_ProximaOcurrencia",
                table: "Tasks");
        }
    }
}
