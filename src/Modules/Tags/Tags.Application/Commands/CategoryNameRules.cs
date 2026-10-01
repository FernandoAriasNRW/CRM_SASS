using FluentValidation;
using Tags.Application.BuiltIn;
using Tags.Domain.Entities;

namespace Tags.Application.Commands;

/// <summary>Las reglas del nombre de una categoría propia, iguales al crearla y al renombrarla.</summary>
public static class CategoryNameRules
{
    public static IRuleBuilderOptions<T, string> ValidCategoryName<T>(this IRuleBuilderInitial<T, string> rule)
        => rule
            .Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("El nombre de la categoría es obligatorio")
            .MaximumLength(CustomTagCategory.MaxNameLength)
                .WithMessage($"El nombre de la categoría admite hasta {CustomTagCategory.MaxNameLength} caracteres")
            .Must(name => !BuiltInTags.CollidesWithBuiltInCategory(name.Trim()))
                .WithMessage("Ese nombre ya es el de una categoría predefinida");
}
