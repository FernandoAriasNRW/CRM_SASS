using System.Reflection;
using System.Runtime.Loader;

// Lista los campos que la API puede sacar en el JSON: las propiedades públicas de los tipos
// públicos de los ensamblados del proyecto, en camelCase como las serializa ASP.NET.
//
// Uso: dotnet run --project tools/contract-snapshot -- <carpeta con los .dll> > contratos.txt
//
// Para qué: comparando la salida de dos ramas se ven los campos renombrados. El compilador no
// avisa de esto —el frontend no está tipado contra el backend—, así que un renombrado en cascada
// puede cambiar un contrato sin que nada falle hasta que alguien abre la pantalla.
//
// Cómo se usa en la práctica (ver docs/ESTANDAR-DE-CODIGO.md §6):
//   1. Compilar main y la rama en dos carpetas distintas.
//   2. Ejecutar esto sobre cada una y hacer diff.
//   3. Cada campo que cambie, buscarlo en web/ por su nombre viejo.

if (args.Length < 1)
{
    Console.Error.WriteLine("Uso: contract-snapshot <carpeta con los .dll>");
    return 64;
}

var folder = Path.GetFullPath(args[0]);

// Los ensamblados del proyecto; el resto son dependencias.
static bool IsProjectAssembly(string name) =>
    name.EndsWith(".Application") || name.EndsWith(".Presentation") || name.EndsWith(".Domain")
    || name == "ApiHost" || name.StartsWith("BuildingBlocks");

var context = new AssemblyLoadContext("contract-snapshot", isCollectible: false);
context.Resolving += (ctx, name) =>
{
    var path = Path.Combine(folder, name.Name + ".dll");
    return File.Exists(path) ? ctx.LoadFromAssemblyPath(path) : null;
};

var lines = new SortedSet<string>(StringComparer.Ordinal);
foreach (var dll in Directory.GetFiles(folder, "*.dll"))
{
    if (!IsProjectAssembly(Path.GetFileNameWithoutExtension(dll))) continue;

    Type[] types;
    try
    {
        types = context.LoadFromAssemblyPath(dll).GetExportedTypes();
    }
    catch (ReflectionTypeLoadException ex)
    {
        // Un tipo que no carga no invalida el resto del ensamblado.
        types = ex.Types.Where(t => t is not null).ToArray()!;
    }

    foreach (var type in types)
    {
        if (!type.IsClass) continue;

        foreach (var property in type.GetProperties(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (property.GetIndexParameters().Length > 0) continue;

            var jsonName = char.ToLowerInvariant(property.Name[0]) + property.Name[1..];
            lines.Add($"{type.FullName}\t{jsonName}");
        }
    }
}

foreach (var line in lines) Console.WriteLine(line);
return 0;
