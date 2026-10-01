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
public class BuiltInTemplatesTests
{
    /// <summary>Los tonos que acepta <c>CALLOUT_TONES</c> en el editor.</summary>
    private static readonly string[] EditorTones = ["note", "warning", "danger", "success"];

    public static TheoryData<string, string> Templates()
    {
        var data = new TheoryData<string, string>();
        foreach (var key in BuiltInTemplates.Keys)
            foreach (var language in new[] { "es", "en" })
                data.Add(key, language);
        return data;
    }

    private static string Html(string key, string language)
        => string.Concat(BuiltInTemplates.For(key, language, DateTime.UtcNow)!.Pages.Select(p => p.Html));

    [Theory]
    [MemberData(nameof(Templates))]
    public void Callouts_use_tones_the_editor_knows(string key, string language)
    {
        var tones = Regex.Matches(Html(key, language), @"data-tone=""([^""]*)""")
            .Select(m => m.Groups[1].Value);

        tones.Should().OnlyContain(t => EditorTones.Contains(t));
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void No_attributes_keep_the_old_name(string key, string language)
    {
        Html(key, language).Should()
            .NotContain("data-tipo").And.NotContain("data-tono").And.NotContain("data-mencion");
    }
}
