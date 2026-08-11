using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Calendar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "calendar_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    tenant_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    organizer_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    project_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    task_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    title = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    type_value = table.Column<int>(type: "int", nullable: false),
                    start_time = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    end_time = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    location = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_all_day = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    recurrence_value = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    recurrence_interval = table.Column<int>(type: "int", nullable: true),
                    recurrence_end_date = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    is_deleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    deleted_by = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_events", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_is_deleted",
                table: "calendar_events",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_tenant_id",
                table: "calendar_events",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_tenant_start",
                table: "calendar_events",
                columns: new[] { "tenant_id", "start_time" });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_tenant_type",
                table: "calendar_events",
                columns: new[] { "tenant_id", "type_value" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calendar_events");
        }
    }
}
