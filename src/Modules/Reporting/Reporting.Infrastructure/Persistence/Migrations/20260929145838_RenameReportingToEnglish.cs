using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bloque 7a: las tablas de exportaciones y programaciones, y sus columnas, en inglés.
    ///
    /// <b>Escrita a mano.</b> EF proponía <c>DropTable</c> + <c>CreateTable</c> para las tres tablas,
    /// que habría borrado las exportaciones pendientes, los ficheros ya generados y las
    /// programaciones de cada persona. Aquí sólo se renombran: tablas, columnas e índices. Cada
    /// columna se emparejó a mano con la suya —EF también puede cruzar dos del mismo tipo—.
    ///
    /// Los enumerados (estado, formato, frecuencia) se guardan como números y no cambian. El JSON
    /// de <c>DefinitionJson</c> sí lleva claves en español, pero su contenido no se toca aquí: va con
    /// el catálogo, en el bloque 7d.
    /// </summary>
    public partial class RenameReportingToEnglish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "Exportaciones", newName: "Exports");
            migrationBuilder.RenameTable(name: "ContenidosDeExportacion", newName: "ExportContents");
            migrationBuilder.RenameTable(name: "Programaciones", newName: "ReportSchedules");

            migrationBuilder.RenameColumn(name: "SolicitadaPorId", table: "Exports", newName: "RequestedById");
            migrationBuilder.RenameColumn(name: "FormatoValue", table: "Exports", newName: "FormatValue");
            migrationBuilder.RenameColumn(name: "EstadoValue", table: "Exports", newName: "StatusValue");
            migrationBuilder.RenameColumn(name: "SolicitadaUtc", table: "Exports", newName: "RequestedAtUtc");
            migrationBuilder.RenameColumn(name: "ComenzadaUtc", table: "Exports", newName: "StartedAtUtc");
            migrationBuilder.RenameColumn(name: "TerminadaUtc", table: "Exports", newName: "FinishedAtUtc");
            migrationBuilder.RenameColumn(name: "NombreDeFichero", table: "Exports", newName: "FileName");
            migrationBuilder.RenameColumn(name: "TamanoBytes", table: "Exports", newName: "SizeBytes");
            migrationBuilder.RenameColumn(name: "Intentos", table: "Exports", newName: "Attempts");
            migrationBuilder.RenameColumn(name: "ExportacionId", table: "ExportContents", newName: "ExportId");
            migrationBuilder.RenameColumn(name: "TipoDeContenido", table: "ExportContents", newName: "ContentType");
            migrationBuilder.RenameColumn(name: "DestinatarioId", table: "ReportSchedules", newName: "RecipientId");
            migrationBuilder.RenameColumn(name: "FrecuenciaValue", table: "ReportSchedules", newName: "FrequencyValue");
            migrationBuilder.RenameColumn(name: "FormatoValue", table: "ReportSchedules", newName: "FormatValue");
            migrationBuilder.RenameColumn(name: "Hora", table: "ReportSchedules", newName: "Time");
            migrationBuilder.RenameColumn(name: "Dia", table: "ReportSchedules", newName: "Day");
            migrationBuilder.RenameColumn(name: "Activa", table: "ReportSchedules", newName: "IsActive");
            migrationBuilder.RenameColumn(name: "UltimoDiaGenerado", table: "ReportSchedules", newName: "LastGeneratedDay");
            migrationBuilder.RenameColumn(name: "CreadaUtc", table: "ReportSchedules", newName: "CreatedAtUtc");
            migrationBuilder.RenameColumn(name: "DefinicionJson", table: "Reports", newName: "DefinitionJson");

            migrationBuilder.RenameIndex(name: "IX_Exportaciones_Estado_Solicitada", table: "Exports", newName: "IX_Exports_StatusValue_RequestedAtUtc");
            migrationBuilder.RenameIndex(name: "IX_Exportaciones_TenantId_ReportId", table: "Exports", newName: "IX_Exports_TenantId_ReportId");
            migrationBuilder.RenameIndex(name: "UX_ContenidosDeExportacion_ExportacionId", table: "ExportContents", newName: "UX_ExportContents_ExportId");
            migrationBuilder.RenameIndex(name: "IX_Programaciones_Activa", table: "ReportSchedules", newName: "IX_ReportSchedules_IsActive");
            migrationBuilder.RenameIndex(name: "IX_Programaciones_TenantId_ReportId", table: "ReportSchedules", newName: "IX_ReportSchedules_TenantId_ReportId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(name: "IX_Exports_StatusValue_RequestedAtUtc", table: "Exports", newName: "IX_Exportaciones_Estado_Solicitada");
            migrationBuilder.RenameIndex(name: "IX_Exports_TenantId_ReportId", table: "Exports", newName: "IX_Exportaciones_TenantId_ReportId");
            migrationBuilder.RenameIndex(name: "UX_ExportContents_ExportId", table: "ExportContents", newName: "UX_ContenidosDeExportacion_ExportacionId");
            migrationBuilder.RenameIndex(name: "IX_ReportSchedules_IsActive", table: "ReportSchedules", newName: "IX_Programaciones_Activa");
            migrationBuilder.RenameIndex(name: "IX_ReportSchedules_TenantId_ReportId", table: "ReportSchedules", newName: "IX_Programaciones_TenantId_ReportId");

            migrationBuilder.RenameColumn(name: "RequestedById", table: "Exports", newName: "SolicitadaPorId");
            migrationBuilder.RenameColumn(name: "FormatValue", table: "Exports", newName: "FormatoValue");
            migrationBuilder.RenameColumn(name: "StatusValue", table: "Exports", newName: "EstadoValue");
            migrationBuilder.RenameColumn(name: "RequestedAtUtc", table: "Exports", newName: "SolicitadaUtc");
            migrationBuilder.RenameColumn(name: "StartedAtUtc", table: "Exports", newName: "ComenzadaUtc");
            migrationBuilder.RenameColumn(name: "FinishedAtUtc", table: "Exports", newName: "TerminadaUtc");
            migrationBuilder.RenameColumn(name: "FileName", table: "Exports", newName: "NombreDeFichero");
            migrationBuilder.RenameColumn(name: "SizeBytes", table: "Exports", newName: "TamanoBytes");
            migrationBuilder.RenameColumn(name: "Attempts", table: "Exports", newName: "Intentos");
            migrationBuilder.RenameColumn(name: "ExportId", table: "ExportContents", newName: "ExportacionId");
            migrationBuilder.RenameColumn(name: "ContentType", table: "ExportContents", newName: "TipoDeContenido");
            migrationBuilder.RenameColumn(name: "RecipientId", table: "ReportSchedules", newName: "DestinatarioId");
            migrationBuilder.RenameColumn(name: "FrequencyValue", table: "ReportSchedules", newName: "FrecuenciaValue");
            migrationBuilder.RenameColumn(name: "FormatValue", table: "ReportSchedules", newName: "FormatoValue");
            migrationBuilder.RenameColumn(name: "Time", table: "ReportSchedules", newName: "Hora");
            migrationBuilder.RenameColumn(name: "Day", table: "ReportSchedules", newName: "Dia");
            migrationBuilder.RenameColumn(name: "IsActive", table: "ReportSchedules", newName: "Activa");
            migrationBuilder.RenameColumn(name: "LastGeneratedDay", table: "ReportSchedules", newName: "UltimoDiaGenerado");
            migrationBuilder.RenameColumn(name: "CreatedAtUtc", table: "ReportSchedules", newName: "CreadaUtc");
            migrationBuilder.RenameColumn(name: "DefinitionJson", table: "Reports", newName: "DefinicionJson");

            migrationBuilder.RenameTable(name: "Exports", newName: "Exportaciones");
            migrationBuilder.RenameTable(name: "ExportContents", newName: "ContenidosDeExportacion");
            migrationBuilder.RenameTable(name: "ReportSchedules", newName: "Programaciones");
        }
    }
}
