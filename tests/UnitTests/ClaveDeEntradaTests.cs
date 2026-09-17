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

        var ticket = Ticket.CrearDesdeFuera(clave, new SolicitudExterna(
            "Título suficiente", "Descripción", TicketPriority.Medium, TicketStatus.PendingInfo,
            "  Ana  ", "ana@cliente.com", " ", "Cliente S.L.", "Acceso", Guid.Empty, [" Bug ", "bug", "billing"])).Value!;

        ticket.TenantId.Should().Be(tenant);
        ticket.Origen.Should().Be(Ticket.OrigenExterno);
        ticket.ClaveDeEntradaId.Should().Be(clave.Id);
        ticket.SolicitanteNombre.Should().Be("Ana");
        ticket.SolicitanteTelefono.Should().BeNull("un espacio no es un teléfono");
        ticket.Status.Should().Be(TicketStatus.PendingInfo);
        ticket.TeamId.Should().BeNull("Guid.Empty no es un equipo");
        ticket.ListaDeEtiquetas.Should().Equal("bug", "billing");
    }

    [Theory]
    [InlineData("captura.png", "image/png", 1024, true)]
    [InlineData("video.mp4", "video/mp4", 1024, true)]
    [InlineData("factura.exe", "image/png", 1024, false)]
    [InlineData("informe.pdf", "application/pdf", 1024, false)]
    [InlineData("vacia.png", "image/png", 0, false)]
    [InlineData("enorme.mov", "video/quicktime", ReglasDeAdjuntos.MaximoPorFichero + 1, false)]
    public void Solo_se_admiten_imagenes_y_videos_de_tamano_razonable(string nombre, string tipo, long tamano, bool admitido)
        => (ReglasDeAdjuntos.Rechazo(nombre, tipo, tamano) is null).Should().Be(admitido);
}
