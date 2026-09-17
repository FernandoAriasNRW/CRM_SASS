using FluentAssertions;
using Ticketing.Domain.Entities;
using Ticketing.Domain.ValueObjects;
using Xunit;

namespace UnitTests;

public sealed class ClaveDeEntradaTests
{
    [Fact]
    public void Se_guarda_el_hash_y_no_la_clave()
    {
        var (clave, enClaro) = ClaveDeEntrada.Generar(Guid.NewGuid(), " Web de soporte ", Guid.NewGuid(), DateTime.UtcNow);

        enClaro.Should().StartWith(ClaveDeEntrada.Prefijo);
        clave.Hash.Should().Be(ClaveDeEntrada.HashDe(enClaro));
        clave.Hash.Should().NotContain(enClaro);
        enClaro.Should().StartWith(clave.Inicio);
        clave.Inicio.Length.Should().BeLessThan(enClaro.Length);
        clave.Nombre.Should().Be("Web de soporte");
    }

    [Fact]
    public void Dos_claves_nunca_coinciden()
    {
        var una = ClaveDeEntrada.Generar(Guid.NewGuid(), "a", Guid.NewGuid(), DateTime.UtcNow).EnClaro;
        var otra = ClaveDeEntrada.Generar(Guid.NewGuid(), "a", Guid.NewGuid(), DateTime.UtcNow).EnClaro;

        una.Should().NotBe(otra);
    }

    /// <summary>Revocar dos veces no cambia cuándo se revocó.</summary>
    [Fact]
    public void Revocar_es_idempotente()
    {
        var (clave, _) = ClaveDeEntrada.Generar(Guid.NewGuid(), "a", Guid.NewGuid(), DateTime.UtcNow);
        var primera = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        clave.Revocar(primera);
        clave.Revocar(primera.AddDays(1));

        clave.RevocadaUtc.Should().Be(primera);
        clave.EstaActiva.Should().BeFalse();
    }

    [Fact]
    public void Un_ticket_de_fuera_cae_en_la_organizacion_de_la_clave()
    {
        var tenant = Guid.NewGuid();
        var (clave, _) = ClaveDeEntrada.Generar(tenant, "a", Guid.NewGuid(), DateTime.UtcNow);

        var ticket = Ticket.CrearDesdeFuera(clave, "Título suficiente", "Descripción",
            TicketPriority.Medium, "  Ana  ", " ").Value!;

        ticket.TenantId.Should().Be(tenant);
        ticket.Origen.Should().Be(Ticket.OrigenExterno);
        ticket.ClaveDeEntradaId.Should().Be(clave.Id);
        ticket.SolicitanteNombre.Should().Be("Ana");
        ticket.SolicitanteEmail.Should().BeNull();
    }
}
