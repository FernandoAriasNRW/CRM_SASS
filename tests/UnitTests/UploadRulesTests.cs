using BuildingBlocks.Application.Abstractions;
using FluentAssertions;
using Xunit;

namespace UnitTests;

/// <summary>Qué se puede subir y con qué cabeceras se sirve. Ver <see cref="UploadRules"/>.</summary>
public sealed class UploadRulesTests
{
    [Theory]
    [InlineData("pagina.HTML")]
    [InlineData("dibujo.svg")]
    [InlineData("script.mjs")]
    public void What_a_browser_would_run_is_rejected(string name)
        => UploadRules.RejectionReason(name, 10).Should().NotBeNull();

    [Theory]
    [InlineData("captura.png")]
    [InlineData("informe.pdf")]
    [InlineData("datos.csv")]
    [InlineData("contrato.docx")]
    public void Ordinary_files_are_accepted(string name)
        => UploadRules.RejectionReason(name, 10).Should().BeNull();

    [Fact]
    public void Empty_or_too_big_is_rejected()
    {
        UploadRules.RejectionReason("a.txt", 0).Should().NotBeNull();
        UploadRules.RejectionReason("a.txt", UploadRules.MaxBytes + 1).Should().NotBeNull();
    }

    [Fact]
    public void Everything_is_served_sandboxed_and_without_sniffing()
    {
        var headers = UploadRules.ServingHeaders("captura.png").ToDictionary(h => h.Key, h => h.Value);

        headers["Content-Security-Policy"].Should().StartWith("sandbox");
        headers["X-Content-Type-Options"].Should().Be("nosniff");
        headers.Should().NotContainKey("Content-Disposition");
    }

    [Fact]
    public void An_old_page_is_served_as_a_download()
        => UploadRules.ServingHeaders("antigua.html").Should().Contain(h => h.Key == "Content-Disposition" && h.Value == "attachment");

    /// <summary>Chrome bloquea su visor de PDF bajo sandbox: un PDF adjunto dejaría de abrirse.</summary>
    [Fact]
    public void A_pdf_keeps_opening_in_the_browser()
    {
        var headers = UploadRules.ServingHeaders("informe.pdf").ToDictionary(h => h.Key, h => h.Value);

        headers.Should().NotContainKey("Content-Security-Policy");
        headers["X-Content-Type-Options"].Should().Be("nosniff");
    }
}
