using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Definitions;

namespace Reporting.Application.Definitions;

/// <summary>
/// Guarda la definición de un informe a medida.
///
/// La definición se valida en el dominio, y este comando sólo la traslada. La razón de que la
/// validación no viva aquí: un informe con una definición que el motor no sabe traducir se
/// guardaría bien y **fallaría al exportarlo**, cuando quien lo construyó ya no está delante.
/// </summary>
public sealed record SaveDefinitionCommand(
    Guid TenantId,
    Guid ReportId,
    ReportDefinition Definition) : ICommand<bool>;
