namespace BuildingBlocks.Application.Abstractions;

/// <summary>
/// Un documento que menciona algo, con lo justo para enlazarlo y pintarlo.
/// </summary>
/// <param name="VisibleText">
/// Cómo estaba escrita la mención. Se enseña para dar contexto: «Reunión de diseño» dice más que
/// un identificador, y si el documento cambia de nombre esto sigue diciendo cómo se la llamó.
/// </param>
public sealed record MentioningDocument(
    Guid DocumentId,
    Guid PageId,
    string DocumentTitle,
    string PageTitle,
    string VisibleText,
    DateTime MentionedAtUtc);
