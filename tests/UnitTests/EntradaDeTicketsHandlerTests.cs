using BuildingBlocks.Application.Abstractions;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Application.Entrada;
using Ticketing.Domain.Entities;
using Xunit;

namespace UnitTests;

public sealed class EntradaDeTicketsHandlerTests
{
    private readonly IClavesDeEntradaRepository _claves = Substitute.For<IClavesDeEntradaRepository>();
    private readonly ITicketRepository _tickets = Substitute.For<ITicketRepository>();
    private readonly IAdjuntosDeTicketRepository _adjuntos = Substitute.For<IAdjuntosDeTicketRepository>();
    private readonly IStorageService _almacen = Substitute.For<IStorageService>();
    private readonly ITicketingUnitOfWork _unidad = Substitute.For<ITicketingUnitOfWork>();

    private CrearTicketExternoHandler Handler() => new(_claves, _tickets, _adjuntos, _almacen, _unidad);

    private static FicheroRecibido Fichero(string nombre, string tipo)
        => new(nombre, tipo, 128, () => new MemoryStream(new byte[128]));

    private CrearTicketExternoCommand Peticion(string clave, params FicheroRecibido[] adjuntos)
        => new(clave, "Asunto suficiente", "Mensaje", "Marta", "marta@cliente.com", "600000000", "Cliente S.L.",
            null, null, null, null, [], adjuntos);

    /// <summary>
    /// Un fichero que el almacenamiento rechaza es culpa del fichero: se contesta con su nombre, se
    /// borra lo que ya se subió y no se crea el ticket. Probando desde el navegador respondía 500.
    /// </summary>
    [Fact]
    public async Task Si_el_almacenamiento_rechaza_un_adjunto_no_se_crea_nada_y_se_dice_cual()
    {
        var (clave, enClaro) = ClaveDeEntrada.Generar(Guid.NewGuid(), "Web", Guid.NewGuid(), DateTime.UtcNow);
        _claves.BuscarActivaPorHashAsync(ClaveDeEntrada.HashDe(enClaro), Arg.Any<CancellationToken>()).Returns(clave);

        _almacen.UploadFileAsync(Arg.Any<Stream>(), "captura.png", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("https://almacen/captura.png");
        _almacen.UploadFileAsync(Arg.Any<Stream>(), "roto.mp4", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Unsupported video format or file"));

        var resultado = await Handler().Handle(
            Peticion(enClaro, Fichero("captura.png", "image/png"), Fichero("roto.mp4", "video/mp4")), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Contain("roto.mp4").And.NotContain("Unsupported", "el detalle interno no se enseña fuera");
        await _almacen.Received(1).DeleteFileAsync("https://almacen/captura.png");
        await _tickets.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _unidad.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    /// <summary>Con una clave que no existe no se mira nada más, ni se dice qué falta.</summary>
    [Fact]
    public async Task Con_una_clave_desconocida_no_se_valida_ni_se_sube_nada()
    {
        var resultado = await Handler().Handle(
            Peticion("tke_desconocida", Fichero("captura.png", "image/png")) with { RequesterPhone = null },
            CancellationToken.None);

        resultado.Error.Should().Be(ErroresDeEntrada.ClaveNoValida);
        await _almacen.DidNotReceiveWithAnyArgs().UploadFileAsync(default!, default!, default!, default);
    }
}
