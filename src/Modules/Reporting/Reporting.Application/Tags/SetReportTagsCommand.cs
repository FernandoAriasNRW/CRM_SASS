using BuildingBlocks.Application.Abstractions;

namespace Reporting.Application.Tags;

/// <summary>
/// Cambia todas las etiquetas de un informe. Un comando aparte porque el informe no tiene una
/// edición general: su definición se guarda por su lado (<c>SaveDefinitionCommand</c>).
/// </summary>
public sealed record SetReportTagsCommand(Guid TenantId, Guid ReportId, IReadOnlyList<Guid> TagIds) : ICommand<bool>;
