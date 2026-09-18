using System.Text.RegularExpressions;
using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace Docs.Domain.Mentions;

/// <summary>
/// Saca las menciones del contenido de una página.
///
/// <b>Lee el HTML que el editor guarda, y eso impone un contrato:</b> la extensión de menciones
/// del editor tiene que escribir <c>data-mencion-tipo</c> y <c>data-mencion-id</c> en cada
/// mención. Si el editor dejara de hacerlo, esta función devolvería cero y las menciones
/// desaparecerían <b>sin dar ningún error</b> — por eso hay una prueba de integración que escribe
/// una mención por la API y comprueba que la tarea la ve.
///
/// <b>Por qué una expresión regular y no un analizador de HTML.</b> Se busca un patrón muy
/// concreto —dos atributos en la misma etiqueta— sobre contenido que genera nuestro propio editor,
/// no HTML arbitrario de internet. Un analizador completo traería una dependencia y una superficie
/// de ataque para resolver un problema que aquí no existe. Lo que sí se cuida es que la expresión
/// no dependa del orden de los atributos ni del tipo de comillas, que es donde estas cosas fallan.
/// </summary>
public static partial class MentionReader
{
    /// <summary>
    /// Cuántas menciones se guardan por página como mucho.
    ///
    /// Un documento largo con cien tareas mencionadas es legítimo; uno con diez mil es un pegado
    /// accidental, y sin tope llenaría la tabla y la lista de «mencionado en» de cada tarea.
    /// </summary>
    public const int MaxPerPage = 200;

    [GeneratedRegex(
        """<[^>]*?data-mencion-tipo\s*=\s*["']([^"']+)["'][^>]*?>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TagWithType();

    [GeneratedRegex(
        """data-mencion-id\s*=\s*["']([^"']+)["']""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IdAttribute();

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTags();

    /// <summary>Una mención encontrada en el texto, antes de convertirse en fila.</summary>
    public sealed record FoundMention(string Type, Guid EntityId, string VisibleText);

    public static IReadOnlyList<FoundMention> Read(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return [];

        var found = new List<FoundMention>();

        // Se recorren las etiquetas que llevan el tipo, y de cada una se saca el identificador y
        // el texto. Se descartan las que no traigan los dos: una mención a medias no se puede
        // enlazar, y guardarla dejaría una entrada muerta en «mencionado en».
        foreach (Match tag in TagWithType().Matches(content))
        {
            var type = tag.Groups[1].Value;
            if (!MentionableTypes.Exists(type)) continue;

            var id = IdAttribute().Match(tag.Value);
            if (!id.Success || !Guid.TryParse(id.Groups[1].Value, out var entityId)) continue;
            if (entityId == Guid.Empty) continue;

            found.Add(new FoundMention(type, entityId, TextOf(content, tag)));
        }

        // La misma tarea mencionada tres veces en la misma página es **una** mención: la pregunta
        // que contesta esto es «¿qué documentos hablan de esta tarea?», no «cuántas veces».
        return found
            .GroupBy(m => (m.Type, m.EntityId))
            .Select(g => g.First())
            .Take(MaxPerPage)
            .ToList();
    }

    /// <summary>
    /// El texto que se ve dentro de la mención.
    ///
    /// Se toma lo que hay entre la etiqueta de apertura y la primera de cierre, y se le quitan las
    /// etiquetas de dentro. Si no se encuentra, se devuelve vacío en vez de adivinar: una etiqueta
    /// sin texto es rara pero no es motivo para descartar la mención, que es lo que de verdad
    /// importa.
    /// </summary>
    private static string TextOf(string content, Match tag)
    {
        var start = tag.Index + tag.Length;
        if (start >= content.Length) return string.Empty;

        var end = content.IndexOf('<', start);
        if (end < 0) end = content.Length;

        var text = content[start..end];

        return System.Net.WebUtility.HtmlDecode(HtmlTags().Replace(text, string.Empty)).Trim();
    }
}
