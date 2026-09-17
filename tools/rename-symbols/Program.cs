using System.Diagnostics;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Rename;

// Renombra símbolos de C# con Roslyn, como lo hace el IDE: la declaración y todas sus
// referencias, sin tocar textos ni comentarios.
//
// Uso: dotnet run --project tools/rename-symbols -- <solución.sln> <mapa.tsv> [--dry-run]
//
// Cada línea del mapa lleva tres columnas separadas por tabulador:
//     <ruta del fichero donde se declara>  <nombre viejo>  <nombre nuevo>
// Se renombran TODAS las declaraciones con ese nombre en ese fichero: tipos, miembros,
// parámetros y variables locales. Las líneas vacías y las que empiezan por # se ignoran.
//
// Por qué una herramienta y no buscar y reemplazar: el reemplazo de texto toca cadenas,
// comentarios y nombres que coinciden por casualidad, y deja fuera las referencias que se
// escriben distinto. Roslyn renombra el símbolo.
//
// Ojo: el renombrado se propaga. Cambiar un miembro de una interfaz cambia también el de
// quien la implementa, aunque esté en otro módulo, y eso puede mover una columna de la base
// de datos. Ver las comprobaciones de docs/ESTANDAR-DE-CODIGO.md §6.

MSBuildLocator.RegisterDefaults();
return await SymbolRenamer.RunAsync(args);

static class SymbolRenamer
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Uso: rename-symbols <solución.sln> <mapa.tsv> [--dry-run]");
            return 64;
        }

        var solutionPath = Path.GetFullPath(args[0]);
        var root = Path.GetDirectoryName(solutionPath)!;
        var dryRun = args.Contains("--dry-run");

        var renames = File.ReadAllLines(args[1])
            .Where(line => line.Trim().Length > 0 && !line.TrimStart().StartsWith('#'))
            .Select(line => line.Split('\t'))
            .Select(columns => (File: columns[0].Replace('\\', '/'), OldName: columns[1], NewName: columns[2]))
            .ToList();

        var clock = Stopwatch.StartNew();
        using var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(e =>
        {
            if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                Console.Error.WriteLine("WORKSPACE: " + e.Diagnostic.Message);
        });

        var solution = await workspace.OpenSolutionAsync(solutionPath);
        Console.WriteLine($"Solución abierta en {clock.Elapsed.TotalSeconds:F0}s, {solution.Projects.Count()} proyectos");

        var failures = 0;
        foreach (var (file, oldName, newName) in renames)
        {
            var renamed = 0;

            // Se renombra de una en una hasta que no queda ninguna declaración con ese nombre:
            // cada renombrado devuelve una solución nueva, así que hay que volver a buscar.
            for (var round = 0; round < 200; round++)
            {
                var symbol = await FindFirstDeclarationAsync(solution, root, file, oldName);
                if (symbol is null) break;

                var options = new SymbolRenameOptions(
                    RenameOverloads: true, RenameInStrings: false, RenameInComments: false, RenameFile: false);
                var next = await Renamer.RenameSymbolAsync(solution, symbol, options, newName);

                if (next == solution)
                {
                    Console.Error.WriteLine($"SIN CAMBIOS: {file} {oldName} ({symbol.Kind})");
                    failures++;
                    break;
                }

                solution = next;
                renamed++;
            }

            if (renamed == 0)
            {
                Console.Error.WriteLine($"NO ENCONTRADO: {file} {oldName}");
                failures++;
            }
            else
            {
                Console.WriteLine($"{file}: {oldName} -> {newName} ({renamed})");
            }
        }

        if (dryRun)
        {
            Console.WriteLine("Simulado: no se escribe nada.");
            return failures == 0 ? 0 : 1;
        }

        if (!workspace.TryApplyChanges(solution))
        {
            Console.Error.WriteLine("No se pudieron aplicar los cambios");
            return 2;
        }

        Console.WriteLine($"Aplicado en {clock.Elapsed.TotalSeconds:F0}s. Errores: {failures}");
        return failures == 0 ? 0 : 1;
    }

    private static async Task<ISymbol?> FindFirstDeclarationAsync(
        Solution solution, string root, string file, string name)
    {
        // Un mismo fichero puede estar enlazado en varios proyectos; vale el primero.
        var document = solution.Projects.SelectMany(p => p.Documents)
            .FirstOrDefault(d => d.FilePath is not null &&
                Path.GetRelativePath(root, d.FilePath).Replace('\\', '/')
                    .Equals(file, StringComparison.OrdinalIgnoreCase));
        if (document is null) return null;

        var model = await document.GetSemanticModelAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();
        if (model is null || syntaxRoot is null) return null;

        foreach (var node in syntaxRoot.DescendantNodes())
        {
            if (!IsDeclaration(node)) continue;

            var symbol = model.GetDeclaredSymbol(node);
            if (symbol is null || symbol.Name != name) continue;

            // Los constructores se renombran con su tipo.
            if (symbol is IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor })
                continue;

            return symbol;
        }

        return null;
    }

    private static bool IsDeclaration(SyntaxNode node) => node is
        BaseTypeDeclarationSyntax or DelegateDeclarationSyntax or MethodDeclarationSyntax or
        PropertyDeclarationSyntax or EventDeclarationSyntax or VariableDeclaratorSyntax or
        ParameterSyntax or EnumMemberDeclarationSyntax or SingleVariableDesignationSyntax or
        ForEachStatementSyntax or LocalFunctionStatementSyntax or TypeParameterSyntax or
        CatchDeclarationSyntax or AnonymousObjectMemberDeclaratorSyntax;
}
