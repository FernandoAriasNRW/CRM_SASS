using FluentAssertions;
using Ticketing.Domain.Entities;
using Ticketing.Domain.Events;
using Ticketing.Domain.ValueObjects;
using Xunit;

namespace UnitTests;

/// <summary>
/// Invariantes de Ticket. Ticketing es el módulo con más recorrido comercial —ni ClickUp
/// ni Monday traen helpdesk de serie— así que su máquina de estados conviene tenerla
/// clavada antes de construir encima.
/// </summary>
public sealed class TicketInvariantsTests
{
    private static Ticket NewTicket() => Ticket.Create(
        DateTime.UtcNow,
        tenantId: Guid.NewGuid(),
        customerId: Guid.NewGuid(),
        title: "No puedo iniciar sesión",
        description: "El botón de acceso no responde",
        priority: TicketPriority.High).Value!;

    [Fact]
    public void A_ticket_starts_open_and_unresolved()
    {
        var ticket = NewTicket();

        ticket.StatusValue.Should().Be(TicketStatus.Open.Value);
        ticket.ResolvedAt.Should().BeNull();
        ticket.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<TicketCreatedEvent>();
    }

    [Fact]
    public void An_empty_title_produces_no_ticket()
    {
        var result = Ticket.Create(DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid(), "", "descripción", TicketPriority.Low);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Resolving_leaves_a_timestamp()
    {
        var ticket = NewTicket();

        ticket.ChangeStatus(DateTime.UtcNow, TicketStatus.Resolved).Should().BeTrue();

        ticket.ResolvedAt.Should().NotBeNull();
        ticket.ResolvedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void A_closed_ticket_can_be_reopened()
    {
        var ticket = NewTicket();
        ticket.ChangeStatus(DateTime.UtcNow, TicketStatus.Closed).Should().BeTrue();

        // Closed fue terminal y dejó de serlo con la retirada de la máquina de estados:
        // en un tablero, esa regla se traducía en una tarjeta que no se dejaba arrastrar
        // sin explicar por qué. Si algún día vuelve a bloquearse, será una decisión
        // consciente y este test tendrá que cambiar.
        ticket.ChangeStatus(DateTime.UtcNow, TicketStatus.Open).Should().BeTrue();

        ticket.StatusValue.Should().Be(TicketStatus.Open.Value);
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void Any_status_is_reachable_from_any_other(int from, int to)
    {
        var source = TicketStatus.All().Single(s => s.Value == from);
        var target = TicketStatus.All().Single(s => s.Value == to);
        var ticket = NewTicket();
        ticket.ChangeStatus(DateTime.UtcNow, source);

        ticket.ChangeStatus(DateTime.UtcNow, target).Should().BeTrue();

        ticket.StatusValue.Should().Be(target.Value);
    }

    public static TheoryData<int, int> AllCombinations()
    {
        var data = new TheoryData<int, int>();
        foreach (var from in TicketStatus.All())
            foreach (var to in TicketStatus.All())
                data.Add(from.Value, to.Value);
        return data;
    }

    [Fact]
    public void A_resolved_ticket_can_be_reopened_to_in_progress()
    {
        var ticket = NewTicket();
        ticket.ChangeStatus(DateTime.UtcNow, TicketStatus.Resolved);

        ticket.ChangeStatus(DateTime.UtcNow, TicketStatus.InProgress).Should().BeTrue();

        ticket.StatusValue.Should().Be(TicketStatus.InProgress.Value);
    }

    [Fact]
    public void Assign_and_unassign_an_agent()
    {
        var ticket = NewTicket();
        var agent = Guid.NewGuid();

        ticket.AssignTo(agent);
        ticket.AssignedAgentId.Should().Be(agent);

        ticket.Unassign();
        ticket.AssignedAgentId.Should().BeNull();
    }

    [Fact]
    public void Changing_status_raises_an_event_with_old_and_new()
    {
        var ticket = NewTicket();
        ticket.ClearDomainEvents();

        ticket.ChangeStatus(DateTime.UtcNow, TicketStatus.InProgress);

        var domainEvent = ticket.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TicketStatusChangedEvent>().Subject;
        domainEvent.PreviousStatus.Should().Be(TicketStatus.Open.Value);
        domainEvent.NewStatus.Should().Be(TicketStatus.InProgress.Value);
    }

    [Fact]
    public void Adding_the_same_tag_twice_does_not_duplicate_it()
    {
        var ticket = NewTicket();
        var tag = Guid.NewGuid();

        ticket.AddTag(tag);
        ticket.AddTag(tag);

        ticket.TagIds.Should().ContainSingle();
    }
}
