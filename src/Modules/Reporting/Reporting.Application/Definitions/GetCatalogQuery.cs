using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Definitions;

namespace Reporting.Application.Definitions;

/// <summary>El catálogo de la organización: el fijo más sus campos personalizados.</summary>
public sealed record GetCatalogQuery(Guid TenantId) : IQuery<CatalogDto>;
