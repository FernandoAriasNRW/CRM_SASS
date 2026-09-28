using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CustomFields.Infrastructure.Migrations
{
    /// <summary>
    /// A qué entidad pertenece cada campo personalizado, que se guarda como texto («Tarea»). Pasa al
    /// valor en inglés de <c>EntityTypes</c>. La columna sigue llamándose <c>EntidadDestino</c>:
    /// renombrarla le toca al bloque de CustomFields, no a este.
    /// </summary>
    public partial class StoredValuesToEnglish : Migration
    {
        /// <summary>
        /// Español → inglés. Escrito aquí y no leído de <c>EntityTypes</c>: una migración tiene que
        /// hacer siempre lo mismo, aunque el código de hoy cambie mañana.
        /// </summary>
        private static readonly (string Spanish, string English)[] Renames =
        [
            ("Tarea", "Task"),
            ("Proyecto", "Project"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (spanish, english) in Renames)
                migrationBuilder.Sql(
                    $"UPDATE `CustomFieldDefinitions` SET `EntidadDestino` = '{english}' WHERE `EntidadDestino` = '{spanish}';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (spanish, english) in Renames)
                migrationBuilder.Sql(
                    $"UPDATE `CustomFieldDefinitions` SET `EntidadDestino` = '{spanish}' WHERE `EntidadDestino` = '{english}';");
        }
    }
}
