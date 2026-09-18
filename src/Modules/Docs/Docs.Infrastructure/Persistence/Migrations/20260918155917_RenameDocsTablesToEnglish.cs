using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Docs.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Las tres tablas propias de Docs pasan a inglés con sus columnas y sus índices.
    ///
    /// <b>Escrita a mano.</b> EF proponía <c>DropTable</c> + <c>CreateTable</c> para las tres, lo
    /// que habría borrado las menciones (la lista de «mencionado en» de cada tarea y ticket), los
    /// anclajes de los comentarios en línea —dejando sus hilos de Comments colgando de nada— y los
    /// contadores que ordenan la galería de plantillas. Aquí sólo se renombra: los datos se quedan.
    ///
    /// Cada pareja se ha comprobado a mano contra la propuesta de EF, por lo que pasó en el bloque
    /// 4a (cruzó dos columnas <c>int</c>). En estas tablas no hay dos columnas del mismo tipo que
    /// se puedan confundir, salvo <c>CreadaPor</c>/<c>ResueltaPor</c>, que se emparejan por nombre.
    ///
    /// Los <b>valores</b> guardados (<c>MentionedType</c> = «Tarea», «Persona»…) no cambian aquí:
    /// también están escritos dentro del HTML de las páginas y van en un cambio propio.
    /// </summary>
    public partial class RenameDocsTablesToEnglish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Anotaciones ─────────────────────────────────────────────────────────────────
            migrationBuilder.RenameTable(name: "AnotacionesEnDocumentos", newName: "DocumentAnnotations");
            migrationBuilder.RenameColumn(name: "TextoCitado", table: "DocumentAnnotations", newName: "QuotedText");
            migrationBuilder.RenameColumn(name: "CreadaPor", table: "DocumentAnnotations", newName: "CreatedBy");
            migrationBuilder.RenameColumn(name: "CreadaUtc", table: "DocumentAnnotations", newName: "CreatedAtUtc");
            migrationBuilder.RenameColumn(name: "ResueltaPor", table: "DocumentAnnotations", newName: "ResolvedBy");
            migrationBuilder.RenameColumn(name: "ResueltaUtc", table: "DocumentAnnotations", newName: "ResolvedAtUtc");
            migrationBuilder.RenameIndex(
                name: "IX_Anotaciones_TenantId_PageId",
                table: "DocumentAnnotations",
                newName: "IX_DocumentAnnotations_TenantId_PageId");

            // ── Menciones ───────────────────────────────────────────────────────────────────
            migrationBuilder.RenameTable(name: "MencionesEnDocumentos", newName: "DocumentMentions");
            migrationBuilder.RenameColumn(name: "TipoMencionado", table: "DocumentMentions", newName: "MentionedType");
            migrationBuilder.RenameColumn(name: "EntidadMencionadaId", table: "DocumentMentions", newName: "MentionedEntityId");
            migrationBuilder.RenameColumn(name: "DetectadaUtc", table: "DocumentMentions", newName: "DetectedAtUtc");
            migrationBuilder.RenameIndex(
                name: "IX_Menciones_TenantId_PageId",
                table: "DocumentMentions",
                newName: "IX_DocumentMentions_TenantId_PageId");
            migrationBuilder.RenameIndex(
                name: "IX_Menciones_TenantId_Tipo_Entidad",
                table: "DocumentMentions",
                newName: "IX_DocumentMentions_TenantId_MentionedType_MentionedEntityId");

            // ── Uso de plantillas ───────────────────────────────────────────────────────────
            migrationBuilder.RenameTable(name: "UsosDePlantilla", newName: "TemplateUsages");
            migrationBuilder.RenameColumn(name: "Clave", table: "TemplateUsages", newName: "Key");
            migrationBuilder.RenameColumn(name: "Veces", table: "TemplateUsages", newName: "Count");
            migrationBuilder.RenameColumn(name: "UltimoUsoUtc", table: "TemplateUsages", newName: "LastUsedAtUtc");
            migrationBuilder.RenameIndex(
                name: "IX_UsosDePlantilla_TenantId_Clave",
                table: "TemplateUsages",
                newName: "IX_TemplateUsages_TenantId_Key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_TemplateUsages_TenantId_Key",
                table: "TemplateUsages",
                newName: "IX_UsosDePlantilla_TenantId_Clave");
            migrationBuilder.RenameColumn(name: "LastUsedAtUtc", table: "TemplateUsages", newName: "UltimoUsoUtc");
            migrationBuilder.RenameColumn(name: "Count", table: "TemplateUsages", newName: "Veces");
            migrationBuilder.RenameColumn(name: "Key", table: "TemplateUsages", newName: "Clave");
            migrationBuilder.RenameTable(name: "TemplateUsages", newName: "UsosDePlantilla");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentMentions_TenantId_MentionedType_MentionedEntityId",
                table: "DocumentMentions",
                newName: "IX_Menciones_TenantId_Tipo_Entidad");
            migrationBuilder.RenameIndex(
                name: "IX_DocumentMentions_TenantId_PageId",
                table: "DocumentMentions",
                newName: "IX_Menciones_TenantId_PageId");
            migrationBuilder.RenameColumn(name: "DetectedAtUtc", table: "DocumentMentions", newName: "DetectadaUtc");
            migrationBuilder.RenameColumn(name: "MentionedEntityId", table: "DocumentMentions", newName: "EntidadMencionadaId");
            migrationBuilder.RenameColumn(name: "MentionedType", table: "DocumentMentions", newName: "TipoMencionado");
            migrationBuilder.RenameTable(name: "DocumentMentions", newName: "MencionesEnDocumentos");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentAnnotations_TenantId_PageId",
                table: "DocumentAnnotations",
                newName: "IX_Anotaciones_TenantId_PageId");
            migrationBuilder.RenameColumn(name: "ResolvedAtUtc", table: "DocumentAnnotations", newName: "ResueltaUtc");
            migrationBuilder.RenameColumn(name: "ResolvedBy", table: "DocumentAnnotations", newName: "ResueltaPor");
            migrationBuilder.RenameColumn(name: "CreatedAtUtc", table: "DocumentAnnotations", newName: "CreadaUtc");
            migrationBuilder.RenameColumn(name: "CreatedBy", table: "DocumentAnnotations", newName: "CreadaPor");
            migrationBuilder.RenameColumn(name: "QuotedText", table: "DocumentAnnotations", newName: "TextoCitado");
            migrationBuilder.RenameTable(name: "DocumentAnnotations", newName: "AnotacionesEnDocumentos");
        }
    }
}
