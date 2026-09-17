using FluentAssertions;
using Ticketing.Domain.Entities;
using Ticketing.Domain.ValueObjects;
using Xunit;

namespace UnitTests;

public sealed class IntakeKeyTests
{
    [Fact]
    public void Se_guarda_el_hash_y_no_la_clave()
    {
        var (clave, enClaro) = IntakeKey.Generate(Guid.NewGuid(), " Web de soporte ", Guid.NewGuid(), DateTime.UtcNow);

        enClaro.Should().StartWith(IntakeKey.KeyPrefix);
        clave.Hash.Should().Be(IntakeKey.HashOf(enClaro));
        clave.Hash.Should().NotContain(enClaro);
        enClaro.Should().StartWith(clave.Prefix);
        clave.Prefix.Length.Should().BeLessThan(enClaro.Length);
        clave.Name.Should().Be("Web de soporte");
    }

    [Fact]
    public void Dos_claves_nunca_coinciden()
    {
        var una = IntakeKey.Generate(Guid.NewGuid(), "a", Guid.NewGuid(), DateTime.UtcNow).PlainText;
        var otra = IntakeKey.Generate(Guid.NewGuid(), "a", Guid.NewGuid(), DateTime.UtcNow).PlainText;

        una.Should().NotBe(otra);
    }

    /// <summary>Revocar dos veces no cambia cuándo se revocó.</summary>
    [Fact]
    public void Revocar_es_idempotente()
    {
        var (clave, _) = IntakeKey.Generate(Guid.NewGuid(), "a", Guid.NewGuid(), DateTime.UtcNow);
        var primera = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        clave.Revoke(primera);
        clave.Revoke(primera.AddDays(1));

        clave.RevokedAtUtc.Should().Be(primera);
        clave.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Un_ticket_de_fuera_cae_en_la_organizacion_de_la_clave()
    {
        var tenant = Guid.NewGuid();
        var (clave, _) = IntakeKey.Generate(tenant, "a", Guid.NewGuid(), DateTime.UtcNow);

        var ticket = Ticket.CreateFromExternal(clave, new ExternalTicketRequest(
            "Título suficiente", "Descripción", TicketPriority.Medium, TicketStatus.PendingInfo,
            "  Ana  ", "ana@cliente.com", " ", "Cliente S.L.", "Acceso", Guid.Empty, [" Bug ", "bug", "billing"])).Value!;

        ticket.TenantId.Should().Be(tenant);
        ticket.Source.Should().Be(Ticket.SourceExternal);
        ticket.IntakeKeyId.Should().Be(clave.Id);
        ticket.RequesterName.Should().Be("Ana");
        ticket.RequesterPhone.Should().BeNull("un espacio no es un teléfono");
        ticket.Status.Should().Be(TicketStatus.PendingInfo);
        ticket.TeamId.Should().BeNull("Guid.Empty no es un equipo");
        ticket.TagList.Should().Equal("bug", "billing");
    }

    [Theory]
    [InlineData("captura.png", "image/png", 1024, true)]
    [InlineData("video.mp4", "video/mp4", 1024, true)]
    [InlineData("factura.exe", "image/png", 1024, false)]
    [InlineData("informe.pdf", "application/pdf", 1024, false)]
    [InlineData("vacia.png", "image/png", 0, false)]
    [InlineData("enorme.mov", "video/quicktime", AttachmentRules.MaxBytesPerFile + 1, false)]
    public void Solo_se_admiten_imagenes_y_videos_de_tamano_razonable(string nombre, string tipo, long tamano, bool admitido)
        => (AttachmentRules.RejectionReason(nombre, tipo, tamano) is null).Should().Be(admitido);
}
