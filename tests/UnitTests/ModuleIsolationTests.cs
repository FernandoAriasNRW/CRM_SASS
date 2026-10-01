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
public sealed class ModuleIsolationTests
{
    /// <summary>
    /// Sube desde el directorio de ejecución hasta encontrar la solución. El proyecto de
    /// pruebas se ejecuta desde bin/Release/net9.0, y la profundidad cambia según cómo se
    /// lance, así que buscar el ancla es más fiable que contar carpetas hacia arriba.
    /// </summary>
    private static DirectoryInfo RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CrmSaaS.sln")))
            dir = dir.Parent;

        dir.Should().NotBeNull("las pruebas se ejecutan dentro del repositorio, que tiene CrmSaaS.sln en la raíz");
        return dir!;
    }

    private static string? ModuleOf(string absolutePath)
    {
        var parts = absolutePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var i = Array.FindIndex(parts, p => p.Equals("Modules", StringComparison.OrdinalIgnoreCase));

        return i >= 0 && i + 1 < parts.Length ? parts[i + 1] : null;
    }

    [Fact]
    public void No_module_references_another()
    {
        var root = RepositoryRoot();
        var modules = Path.Combine(root.FullName, "src", "Modules");

        Directory.Exists(modules).Should().BeTrue();

        var violations = new List<string>();

        foreach (var csproj in Directory.EnumerateFiles(modules, "*.csproj", SearchOption.AllDirectories))
        {
            var ownModule = ModuleOf(csproj);
            if (ownModule is null) continue;

            var folder = Path.GetDirectoryName(csproj)!;

            foreach (var reference in XDocument.Load(csproj).Descendants("ProjectReference"))
            {
                var include = reference.Attribute("Include")?.Value;
                if (string.IsNullOrWhiteSpace(include)) continue;

                // Los .csproj usan barras invertidas; en Linux hay que normalizarlas o la ruta
                // se toma como un solo nombre de archivo y ningún módulo se detecta jamás.
                var target = Path.GetFullPath(
                    Path.Combine(folder, include.Replace('\\', Path.DirectorySeparatorChar)));

                var targetModule = ModuleOf(target);

                if (targetModule is not null && !targetModule.Equals(ownModule, StringComparison.OrdinalIgnoreCase))
                    violations.Add($"{ownModule} → {targetModule}  ({Path.GetFileName(csproj)} referencia {Path.GetFileName(target)})");
            }
        }

        violations.Should().BeEmpty(
            "ningún módulo puede referenciar a otro; lo que cruza módulos se compone en el host, " +
            "como ApiHost/Reporting/DashboardQueries.cs o PuenteDeAutomatizaciones. Infracciones:\n" +
            string.Join("\n", violations));
    }

    /// <summary>
    /// Un módulo tampoco depende de la aplicación anfitriona. Sería la misma pérdida de
    /// independencia, en la otra dirección y más difícil de deshacer.
    /// </summary>
    [Fact]
    public void No_module_references_the_host()
    {
        var root = RepositoryRoot();
        var modules = Path.Combine(root.FullName, "src", "Modules");

        var violations = new List<string>();

        foreach (var csproj in Directory.EnumerateFiles(modules, "*.csproj", SearchOption.AllDirectories))
        {
            foreach (var reference in XDocument.Load(csproj).Descendants("ProjectReference"))
            {
                var include = reference.Attribute("Include")?.Value ?? string.Empty;

                if (include.Contains("ApiHost", StringComparison.OrdinalIgnoreCase))
                    violations.Add($"{Path.GetFileName(csproj)} → {include}");
            }
        }

        violations.Should().BeEmpty("el host conoce a los módulos, no al revés:\n" + string.Join("\n", violations));
    }
}
