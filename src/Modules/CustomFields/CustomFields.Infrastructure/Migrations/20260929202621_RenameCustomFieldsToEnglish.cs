using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CustomFields.Infrastructure.Migrations
{
    /// <summary>
    /// CustomFields en inglés: las columnas y los índices, y los tipos de campo guardados.
    ///
    /// <c>Type</c> guardaba el tipo en español («Texto», «Numero», «SeleccionMultiple»…) y viaja tal
    /// cual por la API, así que se reescribe en la misma migración que renombra la columna.
    /// «Formula» se escribe igual en los dos idiomas y no cambia.
    /// </summary>
    public partial class RenameCustomFieldsToEnglish : Migration
    {
        /// <summary>Español → inglés, escrito aquí para que la migración haga siempre lo mismo.</summary>
        private static readonly (string Spanish, string English)[] FieldTypes =
        [
            ("Texto", "Text"),
            ("Numero", "Number"),
            ("Fecha", "Date"),
            ("Seleccion", "Select"),
            ("SeleccionMultiple", "MultiSelect"),
            ("Usuario", "User"),
        ];

        private static void ChangeFieldTypes(MigrationBuilder migrationBuilder, bool toEnglish)
        {
            foreach (var (spanish, english) in FieldTypes)
            {
                var (from, to) = toEnglish ? (spanish, english) : (english, spanish);
                migrationBuilder.Sql(
                    $"UPDATE `CustomFieldDefinitions` SET `Type` = '{to}' WHERE `Type` = '{from}';");
            }
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Valor",
                table: "CustomFieldValues",
                newName: "Value");

            migrationBuilder.RenameIndex(
                name: "UX_CustomFieldValues_Tenant_Definicion_Entidad",
                table: "CustomFieldValues",
                newName: "UX_CustomFieldValues_Tenant_Definition_Entity");

            migrationBuilder.RenameIndex(
                name: "IX_CustomFieldValues_Tenant_Entidad",
                table: "CustomFieldValues",
                newName: "IX_CustomFieldValues_Tenant_Entity");

            migrationBuilder.RenameColumn(
                name: "Tipo",
                table: "CustomFieldDefinitions",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "Posicion",
                table: "CustomFieldDefinitions",
                newName: "Position");

            migrationBuilder.RenameColumn(
                name: "Opciones",
                table: "CustomFieldDefinitions",
                newName: "Options");

            migrationBuilder.RenameColumn(
                name: "Obligatorio",
                table: "CustomFieldDefinitions",
                newName: "IsRequired");

            migrationBuilder.RenameColumn(
                name: "Nombre",
                table: "CustomFieldDefinitions",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "EntidadDestino",
                table: "CustomFieldDefinitions",
                newName: "TargetEntity");

            migrationBuilder.RenameIndex(
                name: "UX_CustomFieldDefinitions_Tenant_Entidad_Nombre",
                table: "CustomFieldDefinitions",
                newName: "UX_CustomFieldDefinitions_Tenant_TargetEntity_Name");

            ChangeFieldTypes(migrationBuilder, toEnglish: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ChangeFieldTypes(migrationBuilder, toEnglish: false);

            migrationBuilder.RenameColumn(
                name: "Value",
                table: "CustomFieldValues",
                newName: "Valor");

            migrationBuilder.RenameIndex(
                name: "UX_CustomFieldValues_Tenant_Definition_Entity",
                table: "CustomFieldValues",
                newName: "UX_CustomFieldValues_Tenant_Definicion_Entidad");

            migrationBuilder.RenameIndex(
                name: "IX_CustomFieldValues_Tenant_Entity",
                table: "CustomFieldValues",
                newName: "IX_CustomFieldValues_Tenant_Entidad");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "CustomFieldDefinitions",
                newName: "Tipo");

            migrationBuilder.RenameColumn(
                name: "TargetEntity",
                table: "CustomFieldDefinitions",
                newName: "EntidadDestino");

            migrationBuilder.RenameColumn(
                name: "Position",
                table: "CustomFieldDefinitions",
                newName: "Posicion");

            migrationBuilder.RenameColumn(
                name: "Options",
                table: "CustomFieldDefinitions",
                newName: "Opciones");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "CustomFieldDefinitions",
                newName: "Nombre");

            migrationBuilder.RenameColumn(
                name: "IsRequired",
                table: "CustomFieldDefinitions",
                newName: "Obligatorio");

            migrationBuilder.RenameIndex(
                name: "UX_CustomFieldDefinitions_Tenant_TargetEntity_Name",
                table: "CustomFieldDefinitions",
                newName: "UX_CustomFieldDefinitions_Tenant_Entidad_Nombre");
        }
    }
}
