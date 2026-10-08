using System.Text.RegularExpressions;
using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace Docs.Domain.Mentions;

/// <summary>
/// Qué se puede mencionar dentro de un documento.
///
/// «Person» está aquí y no en <see cref="EntityTypes"/> porque mencionar a alguien no es
/// mencionar una cosa: no lleva a una pantalla de detalle igual, y quien pregunte «¿qué documentos
/// me mencionan?» está haciendo otra pregunta que «¿qué documentos hablan de esta tarea?».
/// </summary>
public static class MentionableTypes
{
    public const string Person = "Person";

    /// <summary>Un equipo, como en los comentarios. Igual que una persona, no es una cosa con ficha.</summary>
    public const string Team = "Team";

    public static IReadOnlyList<string> All() =>
        [Person, Team, EntityTypes.Task, EntityTypes.Ticket, EntityTypes.Project, EntityTypes.Document];

    public static bool Exists(string? type) => type is not null && All().Contains(type);
}
