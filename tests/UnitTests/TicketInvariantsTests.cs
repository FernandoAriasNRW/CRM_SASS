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
    public void Un_ticket_cerrado_puede_reabrirse()
    {
        var ticket = NuevoTicket();
        ticket.ChangeStatus(TicketStatus.Closed).Should().BeTrue();

        // Closed fue terminal y dejó de serlo con la retirada de la máquina de estados:
        // en un tablero, esa regla se traducía en una tarjeta que no se dejaba arrastrar
        // sin explicar por qué. Si algún día vuelve a bloquearse, será una decisión
        // consciente y este test tendrá que cambiar.
        ticket.ChangeStatus(TicketStatus.Open).Should().BeTrue();

        ticket.StatusValue.Should().Be(TicketStatus.Open.Value);
    }

    [Theory]
    [MemberData(nameof(TodasLasCombinaciones))]
    public void Cualquier_estado_es_alcanzable_desde_cualquier_otro(int desde, int hasta)
    {
        var origen = TicketStatus.All().Single(s => s.Value == desde);
        var destino = TicketStatus.All().Single(s => s.Value == hasta);
        var ticket = NuevoTicket();
        ticket.ChangeStatus(origen);

        ticket.ChangeStatus(destino).Should().BeTrue();

        ticket.StatusValue.Should().Be(destino.Value);
    }

    public static TheoryData<int, int> TodasLasCombinaciones()
    {
        var datos = new TheoryData<int, int>();
        foreach (var desde in TicketStatus.All())
            foreach (var hasta in TicketStatus.All())
                datos.Add(desde.Value, hasta.Value);
        return datos;
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
