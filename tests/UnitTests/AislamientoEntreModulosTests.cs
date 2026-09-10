using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace UnitTests;

/// <summary>
/// Ningún módulo referencia a otro.
///
/// Es la regla que sostiene el monolito modular: si un módulo puede alcanzar las entrañas de
/// otro, la separación es decorativa y el día que haya que extraer uno no se puede. Lo que
/// necesita datos de varios se compone en el host, que sí los conoce a todos.
///
/// Existe esta prueba porque la regla se había roto sin que nadie lo notara.
/// `Reporting.Infrastructure` referenciaba seis proyectos de Projects, WorkItems y Ticketing
/// —capas de infraestructura incluidas, o sea sus `DbContext`— para que el repositorio del panel
/// consultara sus tablas directamente. Compilaba, pasaba las pruebas y nadie lo veía: una regla
/// de arquitectura que sólo vive en la cabeza de quien la escribió se rompe en cuanto entra
/// alguien nuevo, o en cuanto han pasado unos meses.
///
/// Se comprueba sobre los `.csproj` en disco y no sobre los ensamblados cargados: así ve
/// **todos** los módulos, incluidos los que este proyecto de pruebas no referencia, que son
/// justo donde nadie está mirando.
/// </summary>
public sealed class AislamientoEntreModulosTests
{
    /// <summary>
    /// Sube desde el directorio de ejecución hasta encontrar la solución. El proyecto de
    /// pruebas se ejecuta desde bin/Release/net9.0, y la profundidad cambia según cómo se
    /// lance, así que buscar el ancla es más fiable que contar carpetas hacia arriba.
    /// </summary>
    private static DirectoryInfo RaizDelRepositorio()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CrmSaaS.sln")))
            dir = dir.Parent;

        dir.Should().NotBeNull("las pruebas se ejecutan dentro del repositorio, que tiene CrmSaaS.sln en la raíz");
        return dir!;
    }

    private static string? ModuloDe(string rutaAbsoluta)
    {
        var partes = rutaAbsoluta.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var i = Array.FindIndex(partes, p => p.Equals("Modules", StringComparison.OrdinalIgnoreCase));

        return i >= 0 && i + 1 < partes.Length ? partes[i + 1] : null;
    }

    [Fact]
    public void Ningun_modulo_referencia_a_otro()
    {
        var raiz = RaizDelRepositorio();
        var modulos = Path.Combine(raiz.FullName, "src", "Modules");

        Directory.Exists(modulos).Should().BeTrue();

        var infracciones = new List<string>();

        foreach (var csproj in Directory.EnumerateFiles(modulos, "*.csproj", SearchOption.AllDirectories))
        {
            var moduloPropio = ModuloDe(csproj);
            if (moduloPropio is null) continue;

            var carpeta = Path.GetDirectoryName(csproj)!;

            foreach (var referencia in XDocument.Load(csproj).Descendants("ProjectReference"))
            {
                var incluye = referencia.Attribute("Include")?.Value;
                if (string.IsNullOrWhiteSpace(incluye)) continue;

                // Los .csproj usan barras invertidas; en Linux hay que normalizarlas o la ruta
                // se toma como un solo nombre de archivo y ningún módulo se detecta jamás.
                var destino = Path.GetFullPath(
                    Path.Combine(carpeta, incluye.Replace('\\', Path.DirectorySeparatorChar)));

                var moduloDestino = ModuloDe(destino);

                if (moduloDestino is not null && !moduloDestino.Equals(moduloPropio, StringComparison.OrdinalIgnoreCase))
                    infracciones.Add($"{moduloPropio} → {moduloDestino}  ({Path.GetFileName(csproj)} referencia {Path.GetFileName(destino)})");
            }
        }

        infracciones.Should().BeEmpty(
            "ningún módulo puede referenciar a otro; lo que cruza módulos se compone en el host, " +
            "como ApiHost/Reporting/ConsultasDelPanel.cs o PuenteDeAutomatizaciones. Infracciones:\n" +
            string.Join("\n", infracciones));
    }

    /// <summary>
    /// Un módulo tampoco depende de la aplicación anfitriona. Sería la misma pérdida de
    /// independencia, en la otra dirección y más difícil de deshacer.
    /// </summary>
    [Fact]
    public void Ningun_modulo_referencia_al_host()
    {
        var raiz = RaizDelRepositorio();
        var modulos = Path.Combine(raiz.FullName, "src", "Modules");

        var infracciones = new List<string>();

        foreach (var csproj in Directory.EnumerateFiles(modulos, "*.csproj", SearchOption.AllDirectories))
        {
            foreach (var referencia in XDocument.Load(csproj).Descendants("ProjectReference"))
            {
                var incluye = referencia.Attribute("Include")?.Value ?? string.Empty;

                if (incluye.Contains("ApiHost", StringComparison.OrdinalIgnoreCase))
                    infracciones.Add($"{Path.GetFileName(csproj)} → {incluye}");
            }
        }

        infracciones.Should().BeEmpty("el host conoce a los módulos, no al revés:\n" + string.Join("\n", infracciones));
    }
}
