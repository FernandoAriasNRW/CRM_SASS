using FluentValidation;
using Tags.Application.Abstractions.Repositories;

namespace Tags.Application.Commands;

public sealed class UpdateTagCommandValidator : AbstractValidator<UpdateTagCommand>
{
    public UpdateTagCommandValidator(ITagCategoryRepository categories)
    {
        Include(new TagFieldsValidator(categories));
    }
}
