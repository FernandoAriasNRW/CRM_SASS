namespace CustomFields.Domain.ValueObjects;

/// <summary>
/// A qué se le pueden poner campos personalizados.
///
/// Se guarda como texto y no como referencia al módulo dueño: `CustomFields` no debe depender
/// de `WorkItems` ni de `Projects` para saber que existen. La contrapartida es que un tipo mal
/// escrito no lo detecta el compilador, y por eso se valida contra esta lista.
/// </summary>
public static class TipoDeEntidad
{
    public const string Tarea = "Tarea";
    public const string Proyecto = "Proyecto";

    public static IReadOnlyList<string> Todos() => [Tarea, Proyecto];

    public static bool Existe(string tipo) => Todos().Contains(tipo);
}
