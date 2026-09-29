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

/// <summary>
/// El panel propio de quien pregunta, creándolo si es su primera vez.
///
/// <b>Crearlo aquí y no en un proceso de alta</b> porque una persona puede existir desde antes de
/// que el panel existiera: un alta que rellene paneles sólo cubre a quien se dé de alta a partir
/// de mañana, y todos los demás verían una pantalla vacía sin saber por qué.
/// </summary>
public sealed record GetMyDashboardQuery(Guid TenantId, Guid UserId) : IQuery<MyDashboardDto>;
