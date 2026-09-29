using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Exports;

/// <summary>El fichero listo para servir. Sólo lo pide el endpoint de descarga.</summary>
public sealed record DownloadExportQuery(Guid TenantId, Guid ExportId) : IQuery<ExportedFile>;
