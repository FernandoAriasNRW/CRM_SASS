using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Webhook.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WebhooksByEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Era una fila por evento, en una tabla con nombre en minúsculas. Pasa a una fila por
            // destino con la lista de eventos, y los nombres de evento pasan a los del catálogo
            // (task.*, user.*, chat.*). Se copia antes de quitar la columna vieja: borrarla primero
            // dejaría las suscripciones sin evento.
            migrationBuilder.DropIndex(
                name: "IX_webhook_subscriptions_TenantId_EventName",
                table: "webhook_subscriptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_webhook_subscriptions",
                table: "webhook_subscriptions");

            migrationBuilder.RenameTable(
                name: "webhook_subscriptions",
                newName: "WebhookSubscriptions");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WebhookSubscriptions",
                table: "WebhookSubscriptions",
                column: "Id");

            // Opcional al principio: las filas que ya hay no tienen valor, y una columna JSON de
            // MySQL no admite valor por defecto.
            migrationBuilder.AddColumn<string>(
                name: "EventTypes",
                table: "WebhookSubscriptions",
                type: "json",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "WebhookSubscriptions",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(@"
                UPDATE `WebhookSubscriptions`
                SET `EventTypes` = JSON_ARRAY(CASE `EventName`
                    WHEN 'identity.user.registered' THEN 'user.created'
                    WHEN 'identity.user.updated' THEN 'user.updated'
                    WHEN 'identity.user.deleted' THEN 'user.deleted'
                    WHEN 'workitem.created' THEN 'task.created'
                    WHEN 'workitem.moved' THEN 'task.status_changed'
                    WHEN 'workitem.patched' THEN 'task.updated'
                    WHEN 'workitem.deleted' THEN 'task.deleted'
                    WHEN 'workitem.checklist.added' THEN 'task.checklist.added'
                    WHEN 'workitem.checklist.updated' THEN 'task.checklist.updated'
                    WHEN 'workitem.checklist.removed' THEN 'task.checklist.removed'
                    WHEN 'workitem.recurrence.set' THEN 'task.recurrence.set'
                    WHEN 'workitem.recurrence.cleared' THEN 'task.recurrence.cleared'
                    WHEN 'workitem.reparented' THEN 'task.reparented'
                    WHEN 'workitem.assignee.added' THEN 'task.assignee.added'
                    WHEN 'workitem.assignee.removed' THEN 'task.assignee.removed'
                    WHEN 'workitem.dependency.added' THEN 'task.dependency.added'
                    WHEN 'workitem.dependency.removed' THEN 'task.dependency.removed'
                    WHEN 'communication.conversation.created' THEN 'chat.conversation.created'
                    WHEN 'communication.conversation.deleted' THEN 'chat.conversation.deleted'
                    WHEN 'communication.message.sent' THEN 'chat.message.sent'
                    WHEN 'communication.message.edited' THEN 'chat.message.edited'
                    WHEN 'communication.message.deleted' THEN 'chat.message.deleted'
                    ELSE `EventName` END),
                    `Name` = LEFT(CONCAT('Webhook ', `EventName`), 100);");

            migrationBuilder.AlterColumn<string>(
                name: "EventTypes",
                table: "WebhookSubscriptions",
                type: "json",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "json",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.DropColumn(
                name: "EventName",
                table: "WebhookSubscriptions");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookSubscriptions_TenantId",
                table: "WebhookSubscriptions",
                column: "TenantId");

            migrationBuilder.CreateTable(
                name: "WebhookDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SubscriptionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EventName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Payload = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastStatusCode = table.Column<int>(type: "int", nullable: true),
                    LastError = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookDeliveries", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_Status_NextAttemptAtUtc",
                table: "WebhookDeliveries",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_Tenant_Subscription_CreatedAtUtc",
                table: "WebhookDeliveries",
                columns: new[] { "TenantId", "SubscriptionId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // De vuelta, cada suscripción se queda con su primer evento y su nombre antiguo: la
            // forma vieja no sabía guardar más de uno.
            migrationBuilder.DropTable(
                name: "WebhookDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_WebhookSubscriptions_TenantId",
                table: "WebhookSubscriptions");

            migrationBuilder.AddColumn<string>(
                name: "EventName",
                table: "WebhookSubscriptions",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(@"
                UPDATE `WebhookSubscriptions`
                SET `EventName` = CASE JSON_UNQUOTE(JSON_EXTRACT(`EventTypes`, '$[0]'))
                    WHEN 'user.created' THEN 'identity.user.registered'
                    WHEN 'user.updated' THEN 'identity.user.updated'
                    WHEN 'user.deleted' THEN 'identity.user.deleted'
                    WHEN 'task.created' THEN 'workitem.created'
                    WHEN 'task.status_changed' THEN 'workitem.moved'
                    WHEN 'task.updated' THEN 'workitem.patched'
                    WHEN 'task.deleted' THEN 'workitem.deleted'
                    WHEN 'task.checklist.added' THEN 'workitem.checklist.added'
                    WHEN 'task.checklist.updated' THEN 'workitem.checklist.updated'
                    WHEN 'task.checklist.removed' THEN 'workitem.checklist.removed'
                    WHEN 'task.recurrence.set' THEN 'workitem.recurrence.set'
                    WHEN 'task.recurrence.cleared' THEN 'workitem.recurrence.cleared'
                    WHEN 'task.reparented' THEN 'workitem.reparented'
                    WHEN 'task.assignee.added' THEN 'workitem.assignee.added'
                    WHEN 'task.assignee.removed' THEN 'workitem.assignee.removed'
                    WHEN 'task.dependency.added' THEN 'workitem.dependency.added'
                    WHEN 'task.dependency.removed' THEN 'workitem.dependency.removed'
                    WHEN 'chat.conversation.created' THEN 'communication.conversation.created'
                    WHEN 'chat.conversation.deleted' THEN 'communication.conversation.deleted'
                    WHEN 'chat.message.sent' THEN 'communication.message.sent'
                    WHEN 'chat.message.edited' THEN 'communication.message.edited'
                    WHEN 'chat.message.deleted' THEN 'communication.message.deleted'
                    ELSE JSON_UNQUOTE(JSON_EXTRACT(`EventTypes`, '$[0]')) END;");

            migrationBuilder.DropColumn(
                name: "EventTypes",
                table: "WebhookSubscriptions");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "WebhookSubscriptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WebhookSubscriptions",
                table: "WebhookSubscriptions");

            migrationBuilder.RenameTable(
                name: "WebhookSubscriptions",
                newName: "webhook_subscriptions");

            migrationBuilder.AddPrimaryKey(
                name: "PK_webhook_subscriptions",
                table: "webhook_subscriptions",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_subscriptions_TenantId_EventName",
                table: "webhook_subscriptions",
                columns: new[] { "TenantId", "EventName" });
        }
    }
}
