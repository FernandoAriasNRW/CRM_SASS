using BuildingBlocks.Application.Abstractions;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Application.Intake;
using Ticketing.Domain.Entities;
using Xunit;

namespace UnitTests;

public sealed class TicketIntakeHandlerTests
{
    private readonly IIntakeKeyRepository _keys = Substitute.For<IIntakeKeyRepository>();
    private readonly ITicketRepository _tickets = Substitute.For<ITicketRepository>();
    private readonly ITicketAttachmentRepository _attachments = Substitute.For<ITicketAttachmentRepository>();
    private readonly IStorageService _storage = Substitute.For<IStorageService>();
    private readonly ITicketingUnitOfWork _unitOfWork = Substitute.For<ITicketingUnitOfWork>();

    private CreateExternalTicketHandler Handler() => new(TimeProvider.System, _keys, _tickets, _attachments, _storage, _unitOfWork);

    private static IncomingFile File(string name, string type)
        => new(name, type, 128, () => new MemoryStream(new byte[128]));

    private CreateExternalTicketCommand Request(string key, params IncomingFile[] attachments)
        => new(key, "Asunto suficiente", "Mensaje", "Marta", "marta@cliente.com", "600000000", "Cliente S.L.", attachments);

    /// <summary>
    /// Una integración de antes puede seguir mandando prioridad o etiquetas. Se rechaza nombrando
    /// los campos, sin crear nada: ignorarlos le haría creer que se aplicaron.
    /// </summary>
    [Fact]
    public async Task Retired_fields_are_rejected_by_name_and_nothing_is_created()
    {
        var (key, plainText) = IntakeKey.Generate(Guid.NewGuid(), "Web", Guid.NewGuid(), DateTime.UtcNow);
        _keys.FindActiveByHashAsync(IntakeKey.HashOf(plainText), Arg.Any<CancellationToken>()).Returns(key);

        var result = await Handler().Handle(
            Request(plainText) with { RetiredFields = RetiredIntakeFields.In(["Tags", "priority", "title"]) },
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("priority").And.Contain("tags").And.NotContain("title");
        await _tickets.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    /// <summary>
    /// Un fichero que el almacenamiento rechaza es culpa del fichero: se contesta con su nombre, se
    /// borra lo que ya se subió y no se crea el ticket. Probando desde el navegador respondía 500.
    /// </summary>
    [Fact]
    public async Task If_storage_rejects_an_attachment_nothing_is_created_and_it_says_which()
    {
        var (key, plainText) = IntakeKey.Generate(Guid.NewGuid(), "Web", Guid.NewGuid(), DateTime.UtcNow);
        _keys.FindActiveByHashAsync(IntakeKey.HashOf(plainText), Arg.Any<CancellationToken>()).Returns(key);

        _storage.UploadFileAsync(Arg.Any<Stream>(), "captura.png", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://almacen/captura.png");
        _storage.UploadFileAsync(Arg.Any<Stream>(), "roto.mp4", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Unsupported video format or file"));

        var result = await Handler().Handle(
            Request(plainText, File("captura.png", "image/png"), File("roto.mp4", "video/mp4")), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("roto.mp4").And.NotContain("Unsupported", "el detalle interno no se enseña fuera");
        await _storage.Received(1).DeleteFileAsync("https://almacen/captura.png");
        await _tickets.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    /// <summary>Con una clave que no existe no se mira nada más, ni se dice qué falta.</summary>
    [Fact]
    public async Task With_an_unknown_key_nothing_is_validated_or_uploaded()
    {
        var result = await Handler().Handle(
            Request("tke_desconocida", File("captura.png", "image/png")) with { RequesterPhone = null },
            CancellationToken.None);

        result.Error.Should().Be(IntakeErrors.InvalidKey);
        await _storage.DidNotReceiveWithAnyArgs().UploadFileAsync(default!, default!, default!, default);
    }
}
