using FluentValidation;

namespace Tags.Application.Commands;

public sealed class CreateTagCategoryCommandValidator : AbstractValidator<CreateTagCategoryCommand>
{
    public CreateTagCategoryCommandValidator()
    {
        RuleFor(x => x.Name).ValidCategoryName();
    }
}
