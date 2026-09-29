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

/// <summary>Quita un recuadro del panel. El informe al que apunta no se toca.</summary>
public sealed record RemoveWidgetCommand(Guid TenantId, Guid UserId, Guid PanelId, Guid WidgetId) : ICommand<bool>;
