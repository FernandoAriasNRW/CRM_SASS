using FluentAssertions;
using Ticketing.Domain.Entities;
using Ticketing.Domain.ValueObjects;
using Xunit;

namespace UnitTests;

public sealed class IntakeKeyTests
{
    [Fact]
    public void The_hash_is_stored_not_the_key()
    {
        var (key, plainText) = IntakeKey.Generate(Guid.NewGuid(), " Web de soporte ", Guid.NewGuid(), DateTime.UtcNow);

        plainText.Should().StartWith(IntakeKey.KeyPrefix);
        key.Hash.Should().Be(IntakeKey.HashOf(plainText));
        key.Hash.Should().NotContain(plainText);
        plainText.Should().StartWith(key.Prefix);
        key.Prefix.Length.Should().BeLessThan(plainText.Length);
        key.Name.Should().Be("Web de soporte");
    }

    [Fact]
    public void Two_keys_never_match()
    {
        var oneKey = IntakeKey.Generate(Guid.NewGuid(), "a", Guid.NewGuid(), DateTime.UtcNow).PlainText;
        var other = IntakeKey.Generate(Guid.NewGuid(), "a", Guid.NewGuid(), DateTime.UtcNow).PlainText;

        oneKey.Should().NotBe(other);
    }

    /// <summary>Revocar dos veces no cambia cuándo se revocó.</summary>
    [Fact]
    public void Revoking_is_idempotent()
    {
        var (key, _) = IntakeKey.Generate(Guid.NewGuid(), "a", Guid.NewGuid(), DateTime.UtcNow);
        var first = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        key.Revoke(first);
        key.Revoke(first.AddDays(1));

        key.RevokedAtUtc.Should().Be(first);
        key.IsActive.Should().BeFalse();
    }

    [Fact]
    public void An_external_ticket_lands_in_the_key_organisation()
    {
        var tenant = Guid.NewGuid();
        var (key, _) = IntakeKey.Generate(tenant, "a", Guid.NewGuid(), DateTime.UtcNow);

        var ticket = Ticket.CreateFromExternal(DateTime.UtcNow, key, new ExternalTicketRequest(
            "Título suficiente", "Descripción", "  Ana  ", "ana@cliente.com", " ", "Cliente S.L.")).Value!;

        ticket.TenantId.Should().Be(tenant);
        ticket.Source.Should().Be(Ticket.SourceExternal);
        ticket.IntakeKeyId.Should().Be(key.Id);
        ticket.RequesterName.Should().Be("Ana");
        ticket.RequesterPhone.Should().BeNull("un espacio no es un teléfono");
        // Lo demás lo decide quien lo atiende, no quien lo manda.
        ticket.Priority.Should().Be(TicketPriority.Medium);
        ticket.Status.Should().Be(TicketStatus.Open);
        ticket.TeamId.Should().BeNull();
        ticket.Classification.Should().BeNull();
        ticket.TagIds.Should().BeEmpty();
    }

    [Theory]
    [InlineData("captura.png", "image/png", 1024, true)]
    [InlineData("video.mp4", "video/mp4", 1024, true)]
    [InlineData("factura.exe", "image/png", 1024, false)]
    [InlineData("informe.pdf", "application/pdf", 1024, false)]
    [InlineData("vacia.png", "image/png", 0, false)]
    [InlineData("enorme.mov", "video/quicktime", AttachmentRules.MaxBytesPerFile + 1, false)]
    public void Only_reasonably_sized_images_and_videos_are_accepted(string name, string type, long tamano, bool accepted)
        => (AttachmentRules.RejectionReason(name, type, tamano) is null).Should().Be(accepted);
}
