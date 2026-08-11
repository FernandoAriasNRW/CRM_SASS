using BuildingBlocks.Application.Abstractions;

namespace Reporting.Application.Commands;


public sealed record GenerateReportCommand(
    Guid TenantId,
    Guid ReportId,
    string Type,
    string Format
) : ICommand<bool>, IWebhookTriggered
{
    public string WebhookEventName => "report.generated";
}
