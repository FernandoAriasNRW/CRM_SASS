using System.Text.RegularExpressions;
using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace Docs.Domain.Menciones;

/// <summary>
/// Una mención dentro de un documento: «esta página nombra esta tarea».
///
/// <b>Es el diferencial del producto, y por eso existe esta tabla en vez de dejarlo en el HTML.</b>
/// El plan lo dice: «mencionar un ticket dentro de un documento y que el ticket muestre el
/// documento es algo que ClickUp hace a medias». La primera mitad —escribir <c>#tarea</c> y que
/// quede un enlace— se resuelve con una extensión del editor. La segunda —que la tarea sepa qué
/// documentos hablan de ella— <b>no se puede resolver mirando el documento</b>: habría que abrir
/// todos los documentos del inquilino y buscar dentro. Por eso la relación se guarda al derecho,
/// desde la página, y se consulta al revés.
///
/// <b>Las menciones se derivan del contenido, no se mandan aparte.</b> Al guardar una página se
/// vuelven a extraer de lo que se ha guardado. Aceptar una lista de menciones del cliente
/// permitiría que el documento dijera una cosa y la tabla otra: alguien borra la mención del texto
/// y la tarea sigue enseñando el documento para siempre.
/// </summary>
public sealed class MencionEnDocumento : Entity, ITenantEntity
{
    public Guid TenantId { get; private set; }

    /// <summary>La página que menciona. Es el grano del que se borra y se vuelve a escribir.</summary>
    public Guid PageId { get; private set; }

    /// <summary>El documento al que pertenece la página, para poder enlazarlo sin otra consulta.</summary>
    public Guid DocumentId { get; private set; }

    /// <summary>Qué se menciona. Uno de <see cref="TiposDeEntidad"/>, más «Persona».</summary>
    public string TipoMencionado { get; private set; } = string.Empty;

    public Guid EntidadMencionadaId { get; private set; }

    /// <summary>
    /// Cómo se ve la mención en el texto, tal como estaba al guardar.
    ///
    /// Se guarda una copia a propósito, aunque el nombre real esté en el otro módulo: sirve para
    /// pintar la lista de «mencionado en» sin ir a buscar cada nombre, y para que una mención a
    /// algo que luego se borró siga diciendo a qué se refería en vez de quedar como un hueco.
    /// </summary>
    public string TextoVisible { get; private set; } = string.Empty;

    public DateTime DetectadaUtc { get; private set; }

    private MencionEnDocumento() { }

    public static MencionEnDocumento Crear(
        Guid tenantId, Guid documentId, Guid pageId, string tipo, Guid entidadId, string textoVisible)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentId = documentId,
            PageId = pageId,
            TipoMencionado = tipo,
            EntidadMencionadaId = entidadId,
            TextoVisible = Recortar(textoVisible),
            DetectadaUtc = DateTime.UtcNow
        };

    /// <summary>El texto visible se recorta: es una etiqueta, no el contenido de la página.</summary>
    private static string Recortar(string texto)
    {
        var limpio = (texto ?? string.Empty).Trim();
        return limpio.Length > 200 ? limpio[..200] : limpio;
    }
}

/// <summary>
/// Qué se puede mencionar dentro de un documento.
///
/// «Persona» está aquí y no en <see cref="TiposDeEntidad"/> porque mencionar a alguien no es
/// mencionar una cosa: no lleva a una pantalla de detalle igual, y quien pregunte «¿qué documentos
/// me mencionan?» está haciendo otra pregunta que «¿qué documentos hablan de esta tarea?».
/// </summary>
public static class TiposMencionables
{
    public const string Persona = "Persona";

    public static IReadOnlyList<string> Todos() =>
        [Persona, TiposDeEntidad.Tarea, TiposDeEntidad.Ticket, TiposDeEntidad.Proyecto, TiposDeEntidad.Documento];

    public static bool Existe(string? tipo) => tipo is not null && Todos().Contains(tipo);
}

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
public static partial class LectorDeMenciones
{
    /// <summary>
    /// Cuántas menciones se guardan por página como mucho.
    ///
    /// Un documento largo con cien tareas mencionadas es legítimo; uno con diez mil es un pegado
    /// accidental, y sin tope llenaría la tabla y la lista de «mencionado en» de cada tarea.
    /// </summary>
    public const int MaximoPorPagina = 200;

    [GeneratedRegex(
        """<[^>]*?data-mencion-tipo\s*=\s*["']([^"']+)["'][^>]*?>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EtiquetaConTipo();

    [GeneratedRegex(
        """data-mencion-id\s*=\s*["']([^"']+)["']""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AtributoId();

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex Etiquetas();

    /// <summary>Una mención encontrada en el texto, antes de convertirse en fila.</summary>
    public sealed record Encontrada(string Tipo, Guid EntidadId, string TextoVisible);

    public static IReadOnlyList<Encontrada> Leer(string? contenido)
    {
        if (string.IsNullOrWhiteSpace(contenido)) return [];

        var encontradas = new List<Encontrada>();

        // Se recorren las etiquetas que llevan el tipo, y de cada una se saca el identificador y
        // el texto. Se descartan las que no traigan los dos: una mención a medias no se puede
        // enlazar, y guardarla dejaría una entrada muerta en «mencionado en».
        foreach (Match etiqueta in EtiquetaConTipo().Matches(contenido))
        {
            var tipo = etiqueta.Groups[1].Value;
            if (!TiposMencionables.Existe(tipo)) continue;

            var id = AtributoId().Match(etiqueta.Value);
            if (!id.Success || !Guid.TryParse(id.Groups[1].Value, out var entidadId)) continue;
            if (entidadId == Guid.Empty) continue;

            encontradas.Add(new Encontrada(tipo, entidadId, TextoDe(contenido, etiqueta)));
        }

        // La misma tarea mencionada tres veces en la misma página es **una** mención: la pregunta
        // que contesta esto es «¿qué documentos hablan de esta tarea?», no «cuántas veces».
        return encontradas
            .GroupBy(m => (m.Tipo, m.EntidadId))
            .Select(g => g.First())
            .Take(MaximoPorPagina)
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
    private static string TextoDe(string contenido, Match etiqueta)
    {
        var desde = etiqueta.Index + etiqueta.Length;
        if (desde >= contenido.Length) return string.Empty;

        var cierre = contenido.IndexOf('<', desde);
        if (cierre < 0) cierre = contenido.Length;

        var texto = contenido[desde..cierre];

        return System.Net.WebUtility.HtmlDecode(Etiquetas().Replace(texto, string.Empty)).Trim();
    }
}
