using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comments.Infrastructure.Migrations
{
    /// <summary>
    /// Cada comentario guarda de qué cuelga como texto («Tarea», «Anotacion»). Pasan a los valores
    /// en inglés de <c>EntityTypes</c> y <c>CommentableEntityTypes</c>; sin esto, los hilos que ya
    /// existían dejarían de encontrarse: la pantalla pediría «Task» y en la tabla diría «Tarea».
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
            ("Anotacion", "Annotation"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (spanish, english) in Renames)
                migrationBuilder.Sql(
                    $"UPDATE `Comments` SET `EntityType` = '{english}' WHERE `EntityType` = '{spanish}';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (spanish, english) in Renames)
                migrationBuilder.Sql(
                    $"UPDATE `Comments` SET `EntityType` = '{spanish}' WHERE `EntityType` = '{english}';");
        }
    }
}
