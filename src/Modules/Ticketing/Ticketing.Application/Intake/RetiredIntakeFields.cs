namespace Ticketing.Application.Intake;

/// <summary>
/// Los campos que la entrada externa aceptaba y ya no.
///
/// Se quitaron en septiembre de 2026: el formulario del cliente trae sólo quién es y qué le pasa, y
/// lo demás se decide dentro. Una integración antigua puede seguir mandándolos; se rechaza con un
/// 400 que los nombra, porque ignorarlos le haría creer que su prioridad o sus etiquetas se
/// guardaron.
/// </summary>
public static class RetiredIntakeFields
{
    public static readonly IReadOnlyList<string> All = ["priority", "status", "classification", "teamId", "tags"];

    /// <summary>Los de la lista que aparecen entre <paramref name="fieldNames"/>, sin distinguir mayúsculas.</summary>
    public static IReadOnlyList<string> In(IEnumerable<string> fieldNames)
    {
        var sent = fieldNames.Select(n => n.TrimEnd('[', ']')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return All.Where(sent.Contains).ToList();
    }

    public static string Error(IReadOnlyList<string> fields)
        => "Estos campos ya no se aceptan en la entrada externa: " + string.Join(", ", fields)
           + ". Prioridad, estado, clasificación, equipo y etiquetas se asignan dentro de la aplicación.";
}
