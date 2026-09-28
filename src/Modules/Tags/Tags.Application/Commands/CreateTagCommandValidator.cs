using FluentValidation;

namespace Tags.Application.Commands;

/// <summary>
/// Los límites son los de las columnas (<c>TagConfiguration</c>): pasarlos daría un 500 al guardar
/// en lugar de un 400 que diga qué campo sobra.
/// </summary>
public sealed class CreateTagCommandValidator : AbstractValidator<CreateTagCommand>
{
    public CreateTagCommandValidator()
    {
        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("El nombre de la etiqueta es obligatorio")
            .MaximumLength(100).WithMessage("El nombre de la etiqueta admite hasta 100 caracteres");

        RuleFor(x => x.ColorHex)
            .Matches("^#[0-9A-Fa-f]{6}$").WithMessage("El color tiene que ser hexadecimal, como #3B82F6")
            .When(x => !string.IsNullOrEmpty(x.ColorHex));

        // La categoría no se limita a TagCategory.All: las etiquetas sembradas ya usan otras
        // («Priority», «Tech»…), y rechazarlas aquí dejaría la lista con valores que no se pueden
        // volver a crear.
        RuleFor(x => x.Category)
            .MaximumLength(50).WithMessage("La categoría admite hasta 50 caracteres");
    }
}
