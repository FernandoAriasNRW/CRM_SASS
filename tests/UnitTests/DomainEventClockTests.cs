using System.Text.Json;
using BuildingBlocks.Domain.Primitives;
using FluentAssertions;
using Xunit;

namespace UnitTests;

/// <summary>
/// La hora de un evento de dominio.
///
/// Antes se fijaba al construir el evento con <c>DateTime.UtcNow</c>, y además se perdía por el
/// camino: el outbox lo guarda como JSON y, al leerlo, una propiedad sin <c>set</c> se volvía a
/// inicializar con la hora de lectura. Ahora la sella el <c>UnitOfWork</c> con el reloj inyectado,
/// copiando el evento con <c>with</c> desde el tipo base.
/// </summary>
public sealed class DomainEventClockTests
{
    private sealed record SampleEvent(Guid TaskId, string Title) : DomainEvent;

    private static readonly DateTime Noon = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Stamping_through_the_base_type_keeps_the_concrete_event()
    {
        DomainEvent original = new SampleEvent(Guid.NewGuid(), "Revisar contrato");

        var stamped = original with { OccurredOnUtc = Noon };

        stamped.Should().BeOfType<SampleEvent>();
        stamped.Should().BeEquivalentTo(original, o => o.Excluding(e => e.OccurredOnUtc));
        stamped.OccurredOnUtc.Should().Be(Noon);
        stamped.EventId.Should().Be(original.EventId, "sellar la hora no lo convierte en otro evento");
    }

    [Fact]
    public void The_time_survives_the_round_trip_through_the_outbox_json()
    {
        DomainEvent stamped = new SampleEvent(Guid.NewGuid(), "Revisar contrato") with { OccurredOnUtc = Noon };

        var payload = JsonSerializer.Serialize(stamped, stamped.GetType());
        var read = (SampleEvent)JsonSerializer.Deserialize(payload, typeof(SampleEvent))!;

        read.OccurredOnUtc.Should().Be(Noon, "es la hora del suceso, no la de quien lo lee");
    }
}
