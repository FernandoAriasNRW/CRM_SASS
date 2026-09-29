using FluentValidation;
using Tags.Application.Abstractions.Repositories;
using Tags.Domain.ValueObjects;

namespace Tags.Application.Commands;

/// <summary>
/// Las reglas comunes al alta y a la edición. Los límites son los de las columnas
/// (<c>TagConfiguration</c>): pasarlos daría un 500 al guardar en lugar de un 400 que diga qué
/// campo sobra.
/// </summary>
public sealed class TagFieldsValidator : AbstractValidator<ITagFields>
{
    public TagFieldsValidator(ITagCategoryRepository categories)
    {
        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("El nombre de la etiqueta es obligatorio")
            .MaximumLength(100).WithMessage("El nombre de la etiqueta admite hasta 100 caracteres");

        RuleFor(x => x.ColorHex)
            .Matches("^#[0-9A-Fa-f]{6}$").WithMessage("El color tiene que ser hexadecimal, como #3B82F6")
            .When(x => !string.IsNullOrEmpty(x.ColorHex));

        RuleFor(x => x.Category)
            .Cascade(CascadeMode.Stop)
            .Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("La categoría es obligatoria")
            .Must(c => !TagCategory.IsAutomatic(c))
                .WithMessage("Las etiquetas de equipos y proyectos se crean solas al crear el equipo o el proyecto")
            .MustAsync(async (fields, category, ct) =>
                    TagCategory.IsBuiltIn(category) || await categories.ExistsAsync(fields.TenantId, category.Trim(), ct))
                .WithMessage(fields => $"No existe la categoría «{fields.Category}»");
    }
}
