using FluentValidation;
using Identity.Application.Commands;

namespace Identity.Application.Validators;

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId es requerido");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Formato de email invalido")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Role)
            .Must(r => r == "Admin" || r == "Member" || string.IsNullOrEmpty(r))
            .WithMessage("Rol debe ser Admin o Member")
            .When(x => !string.IsNullOrEmpty(x.Role));
    }
}
