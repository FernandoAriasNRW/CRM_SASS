using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Definitions;

namespace Reporting.Application.Definitions;

/// <summary>
/// El catálogo completo, tal como se sirve a la pantalla.
///
/// <b>La pantalla no escribe ninguna de estas listas.</b> Es la razón de que este endpoint
/// exista: dos veces en este módulo el desplegable ofreció opciones que el servidor no conocía
/// —los tipos de informe, y antes los filtros del menú— y en los dos casos el fallo no dio error,
/// sólo un 400 con un mensaje que no decía qué campo estaba mal. Con el catálogo servido, ofrecer
/// algo que no existe deja de ser posible.
/// </summary>
public sealed record CatalogDto(
    IReadOnlyList<DataSourceDto> DataSources,
    IReadOnlyList<OperatorDto> Operators,
    IReadOnlyList<OptionDto> Visualizations,
    IReadOnlyList<OptionDto> Granularities);
