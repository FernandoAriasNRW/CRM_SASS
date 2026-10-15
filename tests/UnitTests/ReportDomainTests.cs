using FluentAssertions;
using Reporting.Domain.Definitions;
using Reporting.Domain.Entities;
using Reporting.Domain.Events;
using Reporting.Domain.ValueObjects;
using Xunit;

namespace UnitTests;

/// <summary>
/// El informe y el panel, en el dominio. Eran lo único del dominio de informes sin una sola
/// prueba unitaria: nada ejercitaba el alta de un informe.
/// </summary>
public sealed class ReportDomainTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Author = Guid.NewGuid();

    private static Report NewReport(string name = "Tareas por estado")
        => Report.Create(Now, Tenant, Author, name, ReportType.TaskSummary, ReportFormat.Pdf).Value!;

    [Fact]
    public void Creating_a_report_keeps_its_data_and_announces_it()
    {
        var report = NewReport();

        report.TenantId.Should().Be(Tenant);
        report.CreatedById.Should().Be(Author);
        report.Type.Should().Be(ReportType.TaskSummary);
        report.Format.Should().Be(ReportFormat.Pdf);
        report.CreatedAt.Should().Be(Now);
        report.DomainEvents.Should().ContainSingle(e => e is ReportCreatedEvent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData("  ab  ")]
    public void A_name_too_short_is_rejected_in_spanish(string name)
    {
        var result = Report.Create(Now, Tenant, Author, name, ReportType.Custom, ReportFormat.Csv);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("nombre");
    }

    [Fact]
    public void A_name_is_trimmed_and_has_a_ceiling()
    {
        NewReport("  Tickets por área  ").Name.Should().Be("Tickets por área");
        ReportName.Create(new string('x', 100)).IsSuccess.Should().BeTrue();
        ReportName.Create(new string('x', 101)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Defining_a_report_makes_it_custom_and_keeps_the_definition()
    {
        var report = NewReport();
        var definition = new ReportDefinition("Tickets", "status", "count", "bar");

        report.Define(definition).IsSuccess.Should().BeTrue();

        report.Type.Should().Be(ReportType.Custom, "con definición el motor tiene que seguirla");
        report.ReadDefinition().Should().BeEquivalentTo(definition);
    }

    [Fact]
    public void An_invalid_definition_is_not_kept()
    {
        var report = NewReport();

        report.Define(new ReportDefinition("Tickets", "no_existe", "count", "bar")).IsFailure.Should().BeTrue();

        report.ReadDefinition().Should().BeNull();
        report.Type.Should().Be(ReportType.TaskSummary);
    }

    [Fact]
    public void Tags_ignore_empty_ids_and_repeats()
    {
        var report = NewReport();
        var tag = Guid.NewGuid();

        report.SetTags([tag, Guid.Empty, tag]);
        report.TagIds.Should().Equal(tag);

        report.AddTag(tag);
        report.TagIds.Should().HaveCount(1);
        report.RemoveTag(tag);
        report.TagIds.Should().BeEmpty();
    }

    [Fact]
    public void Every_report_type_the_screen_offers_exists()
    {
        foreach (var name in new[] { "TaskSummary", "ProjectProgress", "UserActivity", "TicketAnalytics", "Custom", "TaskBreakdown", "KpiSummary" })
            ReportType.FromName<ReportType>(name).Should().NotBeNull(name);

        ReportType.All().Select(t => t.Value).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void A_personal_panel_is_private_and_starts_empty()
    {
        var owner = Guid.NewGuid();

        var panel = Dashboard.CreatePersonal(Tenant, owner);

        panel.IsPublic.Should().BeFalse("lo que alguien coloca para sí no lo ven los demás");
        panel.IsDefault.Should().BeTrue();
        panel.CreatedById.Should().Be(owner);
        // Los recuadros de partida los pone quien lo crea (GetMyDashboardHandler), no el agregado.
        panel.ReadLayout().Placed.Should().BeEmpty();
    }

    [Fact]
    public void A_panel_rejects_a_layout_outside_the_grid_and_keeps_the_old_one()
    {
        var panel = Dashboard.CreatePersonal(Tenant, Guid.NewGuid());
        var before = panel.WidgetsJson;
        var outside = new Reporting.Domain.Dashboards.DashboardLayout(
            [new Reporting.Domain.Dashboards.Widget(Guid.NewGuid(), Guid.NewGuid(), X: 10, Y: 0, Width: 6, Height: 4)]);

        panel.Place(outside).IsFailure.Should().BeTrue();
        panel.WidgetsJson.Should().Be(before);
    }

    [Fact]
    public void A_panel_keeps_its_tags_without_repeats()
    {
        var panel = Dashboard.Create(Tenant, "Ventas", isDefault: false, isPublic: true, Author, "{}");
        var tag = Guid.NewGuid();

        panel.AddTag(tag);
        panel.AddTag(tag);
        panel.TagIds.Should().Equal(tag);

        panel.Update("Ventas Q4", isDefault: true, isPublic: false, "{}");
        panel.Title.Should().Be("Ventas Q4");
        panel.IsPublic.Should().BeFalse();
    }
}
