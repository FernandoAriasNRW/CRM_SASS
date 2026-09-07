namespace BuildingBlocks.Application.Abstractions;

/// <summary>
/// Qué documentos mencionan una cosa.
///
/// <b>Es un puerto, como los favoritos y la visibilidad, y por la misma razón:</b> las menciones
/// las guarda Docs —es quien tiene el texto— y la pregunta la hace la pantalla de una tarea o de
/// un ticket, que están en otros módulos. Ninguno referencia a otro; el contrato se declara aquí.
///
/// <b>Y es la mitad que hace que esto valga algo.</b> Escribir <c>#tarea</c> en un documento y que
/// quede un enlace lo hace cualquiera. Que la tarea sepa qué documentos hablan de ella es lo que
/// el plan señalaba como el diferencial, y no se puede resolver leyendo el documento: habría que
/// abrir todos los del inquilino y buscar dentro.
/// </summary>
public interface IMencionesEnDocumentos
{
    /// <summary>
    /// Los documentos que mencionan esta entidad, del más reciente al más antiguo.
    ///
    /// Lista vacía si nadie la menciona. Quien la use debe enseñarla como tal —«ningún documento
    /// habla de esto»— y no esconder la sección: un apartado que aparece y desaparece según los
    /// datos hace pensar que la aplicación se comporta distinto cada día.
    /// </summary>
    Task<IReadOnlyList<DocumentoQueMenciona>> QuienMencionaAsync(
        string tipo, Guid entidadId, CancellationToken ct = default);
}

/// <summary>
/// Un documento que menciona algo, con lo justo para enlazarlo y pintarlo.
/// </summary>
/// <param name="TextoVisible">
/// Cómo estaba escrita la mención. Se enseña para dar contexto: «Reunión de diseño» dice más que
/// un identificador, y si el documento cambia de nombre esto sigue diciendo cómo se la llamó.
/// </param>
public sealed record DocumentoQueMenciona(
    Guid DocumentId,
    Guid PageId,
    string TituloDelDocumento,
    string TituloDeLaPagina,
    string TextoVisible,
    DateTime MencionadaUtc);
