using FluentValidation;
using Identity.Application.Commands;

namespace Identity.Application.Validators;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email es requerido")
            .EmailAddress().WithMessage("Formato de email invalido");
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Contrasena es requerida")
            .MinimumLength(6).WithMessage("Contrasena debe tener al menos 6 caracteres");
    }
}
