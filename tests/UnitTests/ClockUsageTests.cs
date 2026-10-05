using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace UnitTests;

/// <summary>
/// Nadie lee el reloj del sistema directamente en <c>src/</c>.
///
/// La hora entra por un sitio: <c>TimeProvider</c>, inyectado en aplicación, infraestructura y Host,
/// y como parámetro <c>nowUtc</c> en las entidades de dominio. Así una prueba puede fijarla, y no
/// conviven dos formas de dar la hora —que es como un día una fecha de creación y una de
/// vencimiento acaban comparándose con relojes distintos—.
///
/// Esta prueba es la que impide que el cambio se deshaga sin que nadie lo note: un
/// <c>DateTime.UtcNow</c> nuevo compila sin problemas.
/// </summary>
public sealed class ClockUsageTests
{
    private static readonly Regex DirectClock = new(@"\bDateTime(Offset)?\.(UtcNow|Now|Today)\b");

    private static DirectoryInfo RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CrmSaaS.sln")))
            dir = dir.Parent;

        dir.Should().NotBeNull("las pruebas se ejecutan dentro del repositorio, que tiene CrmSaaS.sln en la raíz");
        return dir!;
    }

    [Fact]
    public void No_production_code_reads_the_system_clock_directly()
    {
        var src = Path.Combine(RepositoryRoot().FullName, "src");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            // Las migraciones son historia generada: no se reescriben.
            if (file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                continue;

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var code = lines[i];
                var comment = code.IndexOf("//", StringComparison.Ordinal);
                if (comment >= 0) code = code[..comment];

                if (DirectClock.IsMatch(code))
                    violations.Add($"{Path.GetRelativePath(src, file)}:{i + 1}");
            }
        }

        violations.Should().BeEmpty(
            "la hora se pide a TimeProvider (o llega como nowUtc al dominio), no al reloj del sistema");
    }
}
