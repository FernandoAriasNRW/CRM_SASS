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

/// <summary>Guarda dónde está cada recuadro. Lo manda la pantalla al mover o redimensionar.</summary>
public sealed record SaveLayoutCommand(
    Guid TenantId, Guid UserId, Guid PanelId, IReadOnlyList<Widget> Widgets) : ICommand<bool>;
