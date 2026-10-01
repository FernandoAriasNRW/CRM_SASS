using FluentValidation;

namespace Tags.Application.Commands;

public sealed class RenameTagCategoryCommandValidator : AbstractValidator<RenameTagCategoryCommand>
{
    public RenameTagCategoryCommandValidator()
    {
        RuleFor(x => x.Name).ValidCategoryName();
    }
}
