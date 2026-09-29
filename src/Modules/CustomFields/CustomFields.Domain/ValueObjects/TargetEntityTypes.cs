using BuildingBlocks.Domain;

namespace CustomFields.Domain.ValueObjects;

/// <summary>
/// A qué se le pueden poner campos personalizados.
///
/// Se guarda como texto y no como referencia al módulo dueño: `CustomFields` no debe depender
/// de `WorkItems` ni de `Projects` para saber que existen. La contrapartida es que un tipo mal
/// escrito no lo detecta el compilador, y por eso se valida contra esta lista.
///
/// Los valores salen de <see cref="EntityTypes"/>: escritos aquí a mano, el día que cambiaran allí
/// los campos de esa entidad dejarían de encontrarse sin ningún error.
/// </summary>
public static class TargetEntityTypes
{
    public const string Task = EntityTypes.Task;
    public const string Project = EntityTypes.Project;

    public static IReadOnlyList<string> All() => [Task, Project];

    public static bool Exists(string type) => All().Contains(type);
}
