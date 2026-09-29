using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Application.Dashboards;
using Reporting.Application.Definitions;
using Reporting.Domain.Entities;
using Reporting.Domain.Dashboards;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Dashboards;

/// <summary>Pone un informe existente en el panel, abajo del todo.</summary>
public sealed record AddWidgetCommand(
    Guid TenantId, Guid UserId, Guid PanelId, Guid ReportId, string? Visualization) : ICommand<Widget>;
