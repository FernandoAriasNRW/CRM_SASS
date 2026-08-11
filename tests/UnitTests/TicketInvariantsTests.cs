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
    private static Ticket NuevoTicket() => Ticket.Create(
        tenantId: Guid.NewGuid(),
        customerId: Guid.NewGuid(),
        title: "No puedo iniciar sesión",
        description: "El botón de acceso no responde",
        priority: TicketPriority.High).Value!;

    [Fact]
    public void Un_ticket_nace_abierto_y_sin_resolver()
    {
        var ticket = NuevoTicket();

        ticket.StatusValue.Should().Be(TicketStatus.Open.Value);
        ticket.ResolvedAt.Should().BeNull();
        ticket.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<TicketCreatedEvent>();
    }

    [Fact]
    public void Un_titulo_vacio_no_produce_ticket()
    {
        var resultado = Ticket.Create(Guid.NewGuid(), Guid.NewGuid(), "", "descripción", TicketPriority.Low);

        resultado.IsSuccess.Should().BeFalse();
        resultado.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Resolver_deja_marca_de_tiempo()
    {
        var ticket = NuevoTicket();

        ticket.ChangeStatus(TicketStatus.Resolved).Should().BeTrue();

        ticket.ResolvedAt.Should().NotBeNull();
        ticket.ResolvedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Cerrado_es_terminal()
    {
        var ticket = NuevoTicket();
        ticket.ChangeStatus(TicketStatus.Closed).Should().BeTrue();

        // Desde Closed no hay ninguna transición definida. Se comprueba explícitamente
        // porque reabrir un ticket cerrado es una petición habitual: si algún día se
        // permite, debe ser una decisión consciente y este test tendrá que cambiar.
        ticket.ChangeStatus(TicketStatus.Open).Should().BeFalse();
        ticket.ChangeStatus(TicketStatus.InProgress).Should().BeFalse();
        ticket.StatusValue.Should().Be(TicketStatus.Closed.Value);
    }

    [Fact]
    public void Una_transicion_rechazada_no_altera_el_estado_ni_emite_evento()
    {
        var ticket = NuevoTicket();
        ticket.ChangeStatus(TicketStatus.Closed);
        ticket.ClearDomainEvents();

        ticket.ChangeStatus(TicketStatus.Resolved).Should().BeFalse();

        ticket.StatusValue.Should().Be(TicketStatus.Closed.Value);
        ticket.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Un_ticket_resuelto_puede_reabrirse_a_en_curso()
    {
        var ticket = NuevoTicket();
        ticket.ChangeStatus(TicketStatus.Resolved);

        ticket.ChangeStatus(TicketStatus.InProgress).Should().BeTrue();

        ticket.StatusValue.Should().Be(TicketStatus.InProgress.Value);
    }

    [Fact]
    public void Asignar_y_desasignar_un_agente()
    {
        var ticket = NuevoTicket();
        var agente = Guid.NewGuid();

        ticket.AssignTo(agente);
        ticket.AssignedAgentId.Should().Be(agente);

        ticket.Unassign();
        ticket.AssignedAgentId.Should().BeNull();
    }

    [Fact]
    public void Cambiar_de_estado_emite_evento_con_el_anterior_y_el_nuevo()
    {
        var ticket = NuevoTicket();
        ticket.ClearDomainEvents();

        ticket.ChangeStatus(TicketStatus.InProgress);

        var evento = ticket.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TicketStatusChangedEvent>().Subject;
        evento.PreviousStatus.Should().Be(TicketStatus.Open.Value);
        evento.NewStatus.Should().Be(TicketStatus.InProgress.Value);
    }

    [Fact]
    public void Añadir_la_misma_etiqueta_dos_veces_no_la_duplica()
    {
        var ticket = NuevoTicket();
        var etiqueta = Guid.NewGuid();

        ticket.AddTag(etiqueta);
        ticket.AddTag(etiqueta);

        ticket.TagIds.Should().ContainSingle();
    }
}
