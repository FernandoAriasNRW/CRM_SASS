using FluentValidation;
using Tags.Application.BuiltIn;
using Tags.Domain.Entities;

namespace Tags.Application.Commands;

public sealed class CreateTagCategoryCommandValidator : AbstractValidator<CreateTagCategoryCommand>
{
    public CreateTagCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("El nombre de la categoría es obligatorio")
            .MaximumLength(CustomTagCategory.MaxNameLength)
                .WithMessage($"El nombre de la categoría admite hasta {CustomTagCategory.MaxNameLength} caracteres")
            .Must(name => !BuiltInTags.CollidesWithBuiltInCategory(name.Trim()))
                .WithMessage(x => $"«{x.Name.Trim()}» ya es una categoría predefinida");
    }
}
