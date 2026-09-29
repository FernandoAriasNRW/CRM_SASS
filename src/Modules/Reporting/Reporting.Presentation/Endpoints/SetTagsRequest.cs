namespace Reporting.Presentation.Endpoints;

/// <summary>Las etiquetas de un elemento, todas: sustituyen a las que tuviera.</summary>
public sealed record SetTagsRequest(List<Guid>? TagIds);
