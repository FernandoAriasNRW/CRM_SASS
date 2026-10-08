using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotificationKindsAndTypePreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Los avisos guardan de qué tratan y a qué cosa se refieren. Los anteriores se quedan
            // sin esos datos: no hay de dónde sacarlos.
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Notifications",
                type: "varchar(60)",
                maxLength: 60,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EntityType",
                table: "Notifications",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "EntityId",
                table: "Notifications",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            // De once columnas fijas a un ajuste por tipo. Se guarda sólo lo que difiere del
            // catálogo, así que quien nunca tocó nada se queda con «{}» y sigue al catálogo. Cada
            // columna pasa a su tipo con el valor que tenía, para que nadie note el cambio:
            // «TicketCreated» no tiene equivalente —no hay aviso de «ticket creado» para nadie— y se
            // pierde, y «ExportReady» decide también el aviso de exportación fallida.
            migrationBuilder.AddColumn<string>(
                name: "Types",
                table: "NotificationPreferences",
                type: "json",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(@"
                UPDATE `NotificationPreferences`
                SET `Types` = JSON_MERGE_PATCH(JSON_OBJECT(),
                    IF(`TaskAssigned` <> 1, JSON_OBJECT('task.assigned', CAST('false' AS JSON)), JSON_OBJECT()),
                    IF(`TaskCompleted` <> 1, JSON_OBJECT('task.completed', CAST('false' AS JSON)), JSON_OBJECT()),
                    IF(`TaskDueSoon` <> 1, JSON_OBJECT('task.due_soon', CAST('false' AS JSON)), JSON_OBJECT()),
                    IF(`TicketUpdated` <> 0, JSON_OBJECT('ticket.updated', CAST('true' AS JSON)), JSON_OBJECT()),
                    IF(`ProjectUpdated` <> 1, JSON_OBJECT('project.updated', CAST('false' AS JSON)), JSON_OBJECT()),
                    IF(`MentionEnabled` <> 1, JSON_OBJECT('mention', CAST('false' AS JSON)), JSON_OBJECT()),
                    IF(`ExportReady` <> 1, JSON_OBJECT('report.export_ready', CAST('false' AS JSON)), JSON_OBJECT()),
                    IF(`ExportReady` <> 1, JSON_OBJECT('report.export_failed', CAST('false' AS JSON)), JSON_OBJECT()));");

            migrationBuilder.AlterColumn<string>(
                name: "Types",
                table: "NotificationPreferences",
                type: "json",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "json",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.DropColumn(
                name: "ExportReady",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "MentionEnabled",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "ProjectUpdated",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "TaskAssigned",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "TaskCompleted",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "TaskDueSoon",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "TicketCreated",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "TicketUpdated",
                table: "NotificationPreferences");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // De vuelta, cada columna toma el valor del ajuste de su tipo, o el del catálogo si la
            // persona no lo tocó. Lo que no tenía columna se pierde.

            migrationBuilder.AddColumn<bool>(
                name: "ExportReady",
                table: "NotificationPreferences",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "MentionEnabled",
                table: "NotificationPreferences",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ProjectUpdated",
                table: "NotificationPreferences",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "TaskAssigned",
                table: "NotificationPreferences",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "TaskCompleted",
                table: "NotificationPreferences",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TaskDueSoon",
                table: "NotificationPreferences",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "TicketCreated",
                table: "NotificationPreferences",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "TicketUpdated",
                table: "NotificationPreferences",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(@"
                UPDATE `NotificationPreferences`
                SET `TaskAssigned` = IF(JSON_CONTAINS_PATH(`Types`, 'one', '$.""task.assigned""'), JSON_EXTRACT(`Types`, '$.""task.assigned""') = CAST('true' AS JSON), 1),
                    `TaskCompleted` = IF(JSON_CONTAINS_PATH(`Types`, 'one', '$.""task.completed""'), JSON_EXTRACT(`Types`, '$.""task.completed""') = CAST('true' AS JSON), 1),
                    `TaskDueSoon` = IF(JSON_CONTAINS_PATH(`Types`, 'one', '$.""task.due_soon""'), JSON_EXTRACT(`Types`, '$.""task.due_soon""') = CAST('true' AS JSON), 1),
                    `TicketUpdated` = IF(JSON_CONTAINS_PATH(`Types`, 'one', '$.""ticket.updated""'), JSON_EXTRACT(`Types`, '$.""ticket.updated""') = CAST('true' AS JSON), 0),
                    `ProjectUpdated` = IF(JSON_CONTAINS_PATH(`Types`, 'one', '$.""project.updated""'), JSON_EXTRACT(`Types`, '$.""project.updated""') = CAST('true' AS JSON), 1),
                    `MentionEnabled` = IF(JSON_CONTAINS_PATH(`Types`, 'one', '$.""mention""'), JSON_EXTRACT(`Types`, '$.""mention""') = CAST('true' AS JSON), 1),
                    `ExportReady` = IF(JSON_CONTAINS_PATH(`Types`, 'one', '$.""report.export_ready""'), JSON_EXTRACT(`Types`, '$.""report.export_ready""') = CAST('true' AS JSON), 1);");

            migrationBuilder.DropColumn(
                name: "Types",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Notifications");
        }
    }
}
