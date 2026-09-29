using Tags.Domain.ValueObjects;

namespace Tags.Application.BuiltIn;

/// <summary>
/// Las etiquetas y los nombres de categoría que trae el producto, en español y en inglés.
///
/// Se guardan en la base con su clave (<c>Tags.BuiltInKey</c>) y el nombre en español; al leerlas
/// se muestra el del idioma que pida la pantalla. Así una organización con gente en los dos
/// idiomas comparte las mismas etiquetas y cada uno las lee en el suyo.
///
/// <b>Las claves son valores guardados.</b> Cambiar una deja huérfanas las filas que la llevan: el
/// aprovisionamiento crearía otra etiqueta con la clave nueva al lado de la vieja.
/// </summary>
public static class BuiltInTags
{
    public static readonly IReadOnlyList<BuiltInTag> All =
    [
        // Hitos
        new("poc", TagCategory.Milestone, "Prueba de concepto", "Proof of concept", "#A855F7"),
        new("mvp", TagCategory.Milestone, "MVP", "MVP", "#8B5CF6"),
        new("beta", TagCategory.Milestone, "Beta", "Beta", "#6366F1"),
        new("release-candidate", TagCategory.Milestone, "Versión candidata", "Release candidate", "#3B82F6"),
        new("release", TagCategory.Milestone, "Lanzamiento", "Release", "#EC4899"),

        // Negocio
        new("vip-client", TagCategory.Business, "Cliente VIP", "VIP client", "#F59E0B"),
        new("enterprise-client", TagCategory.Business, "Cliente empresa", "Enterprise client", "#D97706"),
        new("api-client", TagCategory.Business, "Cliente API", "API client", "#0EA5E9"),
        new("web-client", TagCategory.Business, "Cliente web", "Web client", "#06B6D4"),
        new("trial-client", TagCategory.Business, "Cliente en prueba", "Trial client", "#84CC16"),
        new("prospect", TagCategory.Business, "Cliente potencial", "Prospect", "#A3A3A3"),
        new("partner", TagCategory.Business, "Socio", "Partner", "#14B8A6"),

        // Seguridad
        new("vulnerability", TagCategory.Security, "Vulnerabilidad", "Vulnerability", "#DC2626"),
        new("security-incident", TagCategory.Security, "Incidente de seguridad", "Security incident", "#B91C1C"),
        new("access-control", TagCategory.Security, "Control de acceso", "Access control", "#10B981"),
        new("personal-data", TagCategory.Security, "Datos personales", "Personal data", "#059669"),
        new("compliance", TagCategory.Security, "Cumplimiento normativo", "Compliance", "#047857"),

        // Tipo de trabajo. «Feature», «Bug», «Hotfix» y «Refactor» se dejan así en español porque es
        // como se dicen en un equipo de desarrollo, y como ya aparecen en las fichas de tarea.
        new("feature", TagCategory.WorkType, "Feature", "Feature", "#2563EB"),
        new("bug", TagCategory.WorkType, "Bug", "Bug", "#EF4444"),
        new("hotfix", TagCategory.WorkType, "Hotfix", "Hotfix", "#F97316"),
        new("improvement", TagCategory.WorkType, "Mejora", "Improvement", "#4F46E5"),
        new("refactor", TagCategory.WorkType, "Refactor", "Refactor", "#9333EA"),
        new("documentation", TagCategory.WorkType, "Documentación", "Documentation", "#6B7280"),
        new("maintenance", TagCategory.WorkType, "Mantenimiento", "Maintenance", "#64748B"),

        // Fase de desarrollo
        new("requirements", TagCategory.DevelopmentPhase, "Requisitos", "Requirements", "#0891B2"),
        new("design", TagCategory.DevelopmentPhase, "Diseño", "Design", "#DB2777"),
        new("implementation", TagCategory.DevelopmentPhase, "Implementación", "Implementation", "#16A34A"),
        new("testing", TagCategory.DevelopmentPhase, "Pruebas", "Testing", "#CA8A04"),
        new("deployment", TagCategory.DevelopmentPhase, "Despliegue", "Deployment", "#7C3AED"),
    ];

    private static readonly Dictionary<string, BuiltInTag> ByKey = All.ToDictionary(t => t.Key);

    private static readonly Dictionary<string, (string Spanish, string English)> CategoryLabels = new()
    {
        [TagCategory.Team] = ("Equipo", "Team"),
        [TagCategory.Project] = ("Proyecto", "Project"),
        [TagCategory.Milestone] = ("Hito", "Milestone"),
        [TagCategory.Business] = ("Negocio", "Business"),
        [TagCategory.Security] = ("Seguridad", "Security"),
        [TagCategory.WorkType] = ("Tipo de trabajo", "Work type"),
        [TagCategory.DevelopmentPhase] = ("Fase de desarrollo", "Development phase"),
    };

    public static BuiltInTag? Find(string? key) => key is not null && ByKey.TryGetValue(key, out var tag) ? tag : null;

    /// <summary>El nombre visible de una categoría predefinida; el de una personalizada es su propio nombre.</summary>
    public static string CategoryLabel(string category, string? language)
        => CategoryLabels.TryGetValue(category, out var label)
            ? (Languages.IsEnglish(language) ? label.English : label.Spanish)
            : category;

    /// <summary>
    /// Si un nombre de categoría personalizada chocaría con una predefinida, por su valor
    /// («Milestone») o por cómo se ve en cualquiera de los dos idiomas («Hito»). Una categoría
    /// personalizada «Hito» al lado de la predefinida sería la misma cosa con dos nombres.
    /// </summary>
    public static bool CollidesWithBuiltInCategory(string name)
        => CategoryLabels.Any(c =>
            string.Equals(c.Key, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.Value.Spanish, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.Value.English, name, StringComparison.OrdinalIgnoreCase));
}
