using Docs.Domain.ValueObjects;

namespace Docs.Application.Templates;

/// <summary>Lo que sale al crear un documento desde una plantilla del sistema.</summary>
public sealed record TemplateContent(
    string Title,
    string Description,
    DocumentType Type,
    IReadOnlyList<(string Title, string Html)> Pages);
