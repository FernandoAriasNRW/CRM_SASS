using System.Text.RegularExpressions;

namespace Comments.Domain.Mentions;

/// <summary>
/// Saca las menciones del texto de un comentario.
///
/// <b>El formato es un contrato con la pantalla:</b> cada mención se escribe
/// <c>@[Nombre](Tipo:id)</c>. Quien escribe ve <c>@Ana Pérez</c> o <c>#Integrar la pasarela</c>; el
/// componente de comentarios lo convierte a este formato al enviar y lo vuelve a convertir al
/// pintar. Si cambiara de un lado y no del otro, las menciones dejarían de reconocerse sin dar
/// ningún error: por eso hay una prueba de integración que escribe una con este formato y la busca
/// desde el otro lado.
///
/// Se descartan las de un tipo desconocido o con un identificador vacío, y la misma cosa
/// mencionada dos veces cuenta una.
/// </summary>
public static partial class CommentMentionReader
{
    /// <summary>Cuántas menciones se guardan por comentario como mucho.</summary>
    public const int MaxPerComment = 50;

    [GeneratedRegex(
        @"@\[(?<label>[^\]\r\n]{1,200})\]\((?<type>[A-Za-z]+):(?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\)",
        RegexOptions.CultureInvariant)]
    private static partial Regex Token();

    public static IReadOnlyList<CommentMention> Read(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        return Token().Matches(text)
            .Select(m => (Type: m.Groups["type"].Value, Id: Guid.Parse(m.Groups["id"].Value), Label: m.Groups["label"].Value.Trim()))
            .Where(m => MentionTypes.Exists(m.Type) && m.Id != Guid.Empty && m.Label.Length > 0)
            .GroupBy(m => (m.Type, m.Id))
            .Select(g => new CommentMention(g.Key.Type, g.Key.Id, g.First().Label))
            .Take(MaxPerComment)
            .ToList();
    }

    /// <summary>
    /// El texto como lo lee una persona: cada mención con su nombre, sin el identificador. Es lo
    /// que se enseña en un extracto o en un aviso.
    /// </summary>
    public static string ToPlainText(string text)
        => Token().Replace(text, m => (m.Groups["type"].Value is MentionTypes.Person or MentionTypes.Team ? "@" : "#")
                                      + m.Groups["label"].Value);
}
