using System.Text.RegularExpressions;
using Docs.Application.Templates;
using FluentAssertions;
using Xunit;

namespace UnitTests;

/// <summary>
/// El HTML de las plantillas predefinidas, que se escribe a mano en C#.
///
/// <b>Es la única parte del servidor que escribe bloques del editor</b>, así que tiene que usar
/// los mismos nombres que la extensión los lee (<c>callout.ts</c>). Un tono que el editor no conoce
/// no da error: el aviso se pinta como nota y nadie lo nota. Y un atributo con el nombre viejo
/// (<c>data-tono</c>) haría que el aviso entero se leyera como un párrafo suelto.
/// </summary>
public class PlantillasPredefinidasTests
{
    /// <summary>Los tonos que acepta <c>CALLOUT_TONES</c> en el editor.</summary>
    private static readonly string[] TonosDelEditor = ["note", "warning", "danger", "success"];

    public static TheoryData<string, string> Plantillas()
    {
        var datos = new TheoryData<string, string>();
        foreach (var clave in BuiltInTemplates.Keys)
            foreach (var idioma in new[] { "es", "en" })
                datos.Add(clave, idioma);
        return datos;
    }

    private static string Html(string clave, string idioma)
        => string.Concat(BuiltInTemplates.For(clave, idioma, DateTime.UtcNow)!.Pages.Select(p => p.Html));

    [Theory]
    [MemberData(nameof(Plantillas))]
    public void Los_avisos_usan_tonos_que_el_editor_conoce(string clave, string idioma)
    {
        var tonos = Regex.Matches(Html(clave, idioma), @"data-tone=""([^""]*)""")
            .Select(m => m.Groups[1].Value);

        tonos.Should().OnlyContain(t => TonosDelEditor.Contains(t));
    }

    [Theory]
    [MemberData(nameof(Plantillas))]
    public void No_quedan_atributos_con_el_nombre_viejo(string clave, string idioma)
    {
        Html(clave, idioma).Should()
            .NotContain("data-tipo").And.NotContain("data-tono").And.NotContain("data-mencion");
    }
}
