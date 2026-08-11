using BuildingBlocks.Application.Abstractions;
using Reporting.Domain.Entities;

namespace Reporting.Application.Commands;

public sealed record CreateReportCommand(
    Guid TenantId,
    Guid CreatedById,
    string Name,
    string Type,
    string Format,
    string? Parameters = null
) : ICommand<Report>, IWebhookTriggered
{
    public string WebhookEventName => "report.created";
}
