using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Docs.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Los valores en español que viven dentro de los datos de Docs, no en su esquema.
    ///
    /// <b>Las menciones</b> guardan el tipo mencionado como texto («Tarea», «Persona»). <b>Las
    /// páginas</b> guardan HTML, y el editor escribía en él atributos y valores en español
    /// (<c>data-mencion-tipo="Tarea"</c>, <c>data-tipo="aviso"</c>, <c>data-tono="ojo"</c>…) que
    /// ahora lee en inglés. Sin reescribirlos, al abrir una página vieja las menciones se
    /// convertirían en texto suelto, los avisos en párrafos y las columnas en bloques seguidos: el
    /// editor no reconocería nada, y al guardar lo perdería del todo.
    ///
    /// <b>Sólo dentro de las etiquetas.</b> Cada cambio exige que el atributo esté entre un
    /// <c>&lt;</c> y su <c>&gt;</c>, así que si alguien escribió literalmente
    /// <c>data-tono="bien"</c> en el texto de una página, se queda como estaba.
    /// </summary>
    public partial class StoredValuesToEnglish : Migration
    {
        /// <summary>
        /// Español → inglés. Escrito aquí y no leído de <c>EntityTypes</c>: una migración tiene que
        /// hacer siempre lo mismo, aunque el código de hoy cambie mañana.
        /// </summary>
        private static readonly (string Spanish, string English)[] MentionedTypes =
        [
            ("Tarea", "Task"),
            ("Proyecto", "Project"),
            ("Documento", "Document"),
            ("Persona", "Person"),
        ];

        /// <summary>
        /// Lo que cambia en el HTML de las páginas, en orden: primero los atributos con su valor y
        /// después los que sólo cambian de nombre, para que el genérico no se adelante al concreto.
        /// </summary>
        private static readonly (string Spanish, string English)[] PageMarkup =
        [
            // Menciones: el nombre del atributo y el tipo mencionado.
            ("data-mencion-tipo=\"Tarea\"", "data-mention-type=\"Task\""),
            ("data-mencion-tipo=\"Proyecto\"", "data-mention-type=\"Project\""),
            ("data-mencion-tipo=\"Documento\"", "data-mention-type=\"Document\""),
            ("data-mencion-tipo=\"Persona\"", "data-mention-type=\"Person\""),
            ("data-mencion-tipo=\"", "data-mention-type=\""),
            ("data-mencion-id=\"", "data-mention-id=\""),

            // Avisos: el bloque y sus cuatro tonos.
            ("data-tipo=\"aviso\"", "data-type=\"callout\""),
            ("data-tono=\"nota\"", "data-tone=\"note\""),
            ("data-tono=\"ojo\"", "data-tone=\"warning\""),
            ("data-tono=\"peligro\"", "data-tone=\"danger\""),
            ("data-tono=\"bien\"", "data-tone=\"success\""),
            ("data-tono=\"", "data-tone=\""),

            // Columnas.
            ("data-tipo=\"columnas\"", "data-type=\"columns\""),
            ("data-tipo=\"columna\"", "data-type=\"column\""),
            ("data-cantidad=\"", "data-count=\""),

            // Comentarios en línea.
            ("data-anotacion=\"", "data-annotation=\""),

            // Las clases. El editor las vuelve a poner al guardar, pero una página que nadie abra
            // se quedaría con las viejas, y la hoja de estilos ya no las pinta.
            ("class=\"aviso\"", "class=\"callout\""),
            ("class=\"columnas\"", "class=\"columns\""),
            ("class=\"columna\"", "class=\"column\""),
            ("class=\"comentado\"", "class=\"commented\""),
            ("class=\"desplegable\"", "class=\"toggle\""),
            ("class=\"mencion\"", "class=\"mention\""),
            ("class=\"mencion ", "class=\"mention "),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (spanish, english) in MentionedTypes)
                migrationBuilder.Sql(
                    $"UPDATE `DocumentMentions` SET `MentionedType` = '{english}' WHERE `MentionedType` = '{spanish}';");

            foreach (var (spanish, english) in PageMarkup)
                migrationBuilder.Sql(RewriteInsideTags(spanish, english));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (spanish, english) in MentionedTypes)
                migrationBuilder.Sql(
                    $"UPDATE `DocumentMentions` SET `MentionedType` = '{spanish}' WHERE `MentionedType` = '{english}';");

            // En el mismo orden que la subida, no al revés: también de vuelta el concreto
            // («data-tone="success"») tiene que ir antes que el genérico («data-tone="»), o éste
            // se llevaría el nombre del atributo y dejaría el valor en inglés.
            foreach (var (spanish, english) in PageMarkup)
                migrationBuilder.Sql(RewriteInsideTags(english, spanish));
        }

        /// <summary>
        /// Cambia <paramref name="from"/> por <paramref name="to"/> sólo cuando está dentro de una
        /// etiqueta: <c>(&lt;[^&gt;]*)</c> captura el principio de la etiqueta y se devuelve tal
        /// cual con <c>$1</c>. Ninguno de los textos lleva caracteres especiales de expresión
        /// regular, comodines de <c>LIKE</c> ni comillas simples, así que van sin escapar.
        /// </summary>
        private static string RewriteInsideTags(string from, string to)
            => $"UPDATE `Pages` SET `Content` = REGEXP_REPLACE(`Content`, '(<[^>]*){from}', '$1{to}') "
               + $"WHERE `Content` LIKE '%{from}%';";
    }
}
