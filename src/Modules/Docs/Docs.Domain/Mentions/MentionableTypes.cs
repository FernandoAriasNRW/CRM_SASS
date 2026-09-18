using System.Text.RegularExpressions;
using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace Docs.Domain.Mentions;

/// <summary>
/// Qué se puede mencionar dentro de un documento.
///
/// «Persona» está aquí y no en <see cref="EntityTypes"/> porque mencionar a alguien no es
/// mencionar una cosa: no lleva a una pantalla de detalle igual, y quien pregunte «¿qué documentos
/// me mencionan?» está haciendo otra pregunta que «¿qué documentos hablan de esta tarea?».
/// </summary>
public static class MentionableTypes
{
    public const string Person = "Persona";

    public static IReadOnlyList<string> All() =>
        [Person, EntityTypes.Task, EntityTypes.Ticket, EntityTypes.Project, EntityTypes.Document];

    public static bool Exists(string? type) => type is not null && All().Contains(type);
}
