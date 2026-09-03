using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmailEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PushEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TaskAssigned = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TaskDueSoon = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MentionEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ExportacionLista = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TaskCompleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TicketCreated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TicketUpdated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ProjectUpdated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    QuietHoursEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    QuietHoursStart = table.Column<TimeOnly>(type: "time", nullable: false),
                    QuietHoursEnd = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "UX_NotificationPreferences_Tenant_User",
                table: "NotificationPreferences",
                columns: new[] { "TenantId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationPreferences");
        }
    }
}
