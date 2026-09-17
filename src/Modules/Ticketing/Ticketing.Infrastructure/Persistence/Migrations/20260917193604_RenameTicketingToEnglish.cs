using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticketing.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Ticketing en inglés: las dos tablas de la entrada y las columnas del solicitante.
    ///
    /// <b>Escrita a mano.</b> EF propuso <c>DropTable</c> + <c>CreateTable</c> para las dos tablas
    /// renombradas y <c>DropColumn</c> + <c>AddColumn</c> para <c>Origen</c>: eso habría borrado
    /// las claves de entrada, los adjuntos de todos los tickets y el origen de cada uno.
    ///
    /// El valor guardado también cambia: <c>Origen</c> valía «Aplicacion» o «Externo», y
    /// <c>Source</c> vale «App» o «External». Se traduce con un <c>UPDATE</c>, porque una fila con
    /// el valor viejo dejaría de reconocerse como externa y la ficha no enseñaría al solicitante.
    /// </summary>
    public partial class RenameTicketingToEnglish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- Tickets: el solicitante, la clasificación, las etiquetas y la clave de entrada.
            migrationBuilder.RenameColumn(name: "SolicitanteNombre", table: "Tickets", newName: "RequesterName");
            migrationBuilder.RenameColumn(name: "SolicitanteEmail", table: "Tickets", newName: "RequesterEmail");
            migrationBuilder.RenameColumn(name: "SolicitanteTelefono", table: "Tickets", newName: "RequesterPhone");
            migrationBuilder.RenameColumn(name: "SolicitanteEmpresa", table: "Tickets", newName: "RequesterCompany");
            migrationBuilder.RenameColumn(name: "Clasificacion", table: "Tickets", newName: "Classification");
            migrationBuilder.RenameColumn(name: "Etiquetas", table: "Tickets", newName: "Tags");
            migrationBuilder.RenameColumn(name: "ClaveDeEntradaId", table: "Tickets", newName: "IntakeKeyId");
            migrationBuilder.RenameColumn(name: "Origen", table: "Tickets", newName: "Source");

            migrationBuilder.RenameIndex(
                name: "IX_Tickets_TenantId_Archivado_Borrado",
                table: "Tickets",
                newName: "IX_Tickets_TenantId_ArchivedAtUtc_IsDeleted");

            // El valor por defecto va en la definición de la columna, así que el renombrado se lo
            // lleva tal cual: sin esto, un ticket nuevo sin origen entraría como «Aplicacion».
            migrationBuilder.AlterColumn<string>(
                name: "Source",
                table: "Tickets",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "App",
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldNullable: false,
                oldDefaultValue: "Aplicacion")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("UPDATE Tickets SET Source = 'App' WHERE Source = 'Aplicacion'");
            migrationBuilder.Sql("UPDATE Tickets SET Source = 'External' WHERE Source = 'Externo'");

            // --- Claves de entrada.
            migrationBuilder.RenameTable(name: "ClavesDeEntrada", newName: "IntakeKeys");
            migrationBuilder.RenameColumn(name: "Nombre", table: "IntakeKeys", newName: "Name");
            migrationBuilder.RenameColumn(name: "Inicio", table: "IntakeKeys", newName: "Prefix");
            migrationBuilder.RenameColumn(name: "CreadaPor", table: "IntakeKeys", newName: "CreatedBy");
            migrationBuilder.RenameColumn(name: "CreadaUtc", table: "IntakeKeys", newName: "CreatedAtUtc");
            migrationBuilder.RenameColumn(name: "UltimoUsoUtc", table: "IntakeKeys", newName: "LastUsedAtUtc");
            migrationBuilder.RenameColumn(name: "RevocadaUtc", table: "IntakeKeys", newName: "RevokedAtUtc");
            migrationBuilder.RenameIndex(name: "IX_ClavesDeEntrada_Hash", table: "IntakeKeys", newName: "IX_IntakeKeys_Hash");
            migrationBuilder.RenameIndex(name: "IX_ClavesDeEntrada_TenantId", table: "IntakeKeys", newName: "IX_IntakeKeys_TenantId");

            // --- Adjuntos.
            migrationBuilder.RenameTable(name: "AdjuntosDeTicket", newName: "TicketAttachments");
            migrationBuilder.RenameColumn(name: "Nombre", table: "TicketAttachments", newName: "Name");
            migrationBuilder.RenameColumn(name: "TipoDeContenido", table: "TicketAttachments", newName: "ContentType");
            migrationBuilder.RenameColumn(name: "Tamano", table: "TicketAttachments", newName: "Size");
            migrationBuilder.RenameColumn(name: "SubidoPor", table: "TicketAttachments", newName: "UploadedBy");
            migrationBuilder.RenameColumn(name: "SubidoUtc", table: "TicketAttachments", newName: "UploadedAtUtc");
            migrationBuilder.RenameIndex(
                name: "IX_AdjuntosDeTicket_TenantId_TicketId",
                table: "TicketAttachments",
                newName: "IX_TicketAttachments_TenantId_TicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_TicketAttachments_TenantId_TicketId",
                table: "TicketAttachments",
                newName: "IX_AdjuntosDeTicket_TenantId_TicketId");
            migrationBuilder.RenameColumn(name: "UploadedAtUtc", table: "TicketAttachments", newName: "SubidoUtc");
            migrationBuilder.RenameColumn(name: "UploadedBy", table: "TicketAttachments", newName: "SubidoPor");
            migrationBuilder.RenameColumn(name: "Size", table: "TicketAttachments", newName: "Tamano");
            migrationBuilder.RenameColumn(name: "ContentType", table: "TicketAttachments", newName: "TipoDeContenido");
            migrationBuilder.RenameColumn(name: "Name", table: "TicketAttachments", newName: "Nombre");
            migrationBuilder.RenameTable(name: "TicketAttachments", newName: "AdjuntosDeTicket");

            migrationBuilder.RenameIndex(name: "IX_IntakeKeys_TenantId", table: "IntakeKeys", newName: "IX_ClavesDeEntrada_TenantId");
            migrationBuilder.RenameIndex(name: "IX_IntakeKeys_Hash", table: "IntakeKeys", newName: "IX_ClavesDeEntrada_Hash");
            migrationBuilder.RenameColumn(name: "RevokedAtUtc", table: "IntakeKeys", newName: "RevocadaUtc");
            migrationBuilder.RenameColumn(name: "LastUsedAtUtc", table: "IntakeKeys", newName: "UltimoUsoUtc");
            migrationBuilder.RenameColumn(name: "CreatedAtUtc", table: "IntakeKeys", newName: "CreadaUtc");
            migrationBuilder.RenameColumn(name: "CreatedBy", table: "IntakeKeys", newName: "CreadaPor");
            migrationBuilder.RenameColumn(name: "Prefix", table: "IntakeKeys", newName: "Inicio");
            migrationBuilder.RenameColumn(name: "Name", table: "IntakeKeys", newName: "Nombre");
            migrationBuilder.RenameTable(name: "IntakeKeys", newName: "ClavesDeEntrada");

            migrationBuilder.Sql("UPDATE Tickets SET Source = 'Aplicacion' WHERE Source = 'App'");
            migrationBuilder.Sql("UPDATE Tickets SET Source = 'Externo' WHERE Source = 'External'");

            migrationBuilder.AlterColumn<string>(
                name: "Source",
                table: "Tickets",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Aplicacion",
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldNullable: false,
                oldDefaultValue: "App")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.RenameIndex(
                name: "IX_Tickets_TenantId_ArchivedAtUtc_IsDeleted",
                table: "Tickets",
                newName: "IX_Tickets_TenantId_Archivado_Borrado");

            migrationBuilder.RenameColumn(name: "Source", table: "Tickets", newName: "Origen");
            migrationBuilder.RenameColumn(name: "IntakeKeyId", table: "Tickets", newName: "ClaveDeEntradaId");
            migrationBuilder.RenameColumn(name: "Tags", table: "Tickets", newName: "Etiquetas");
            migrationBuilder.RenameColumn(name: "Classification", table: "Tickets", newName: "Clasificacion");
            migrationBuilder.RenameColumn(name: "RequesterCompany", table: "Tickets", newName: "SolicitanteEmpresa");
            migrationBuilder.RenameColumn(name: "RequesterPhone", table: "Tickets", newName: "SolicitanteTelefono");
            migrationBuilder.RenameColumn(name: "RequesterEmail", table: "Tickets", newName: "SolicitanteEmail");
            migrationBuilder.RenameColumn(name: "RequesterName", table: "Tickets", newName: "SolicitanteNombre");
        }
    }
}
