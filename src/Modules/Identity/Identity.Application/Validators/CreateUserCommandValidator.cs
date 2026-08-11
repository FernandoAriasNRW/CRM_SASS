using FluentValidation;
using Identity.Application.Commands;

namespace Identity.Application.Validators;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nombre es requerido")
            .MaximumLength(120).WithMessage("Nombre debe tener max 120 caracteres");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email es requerido")
            .EmailAddress().WithMessage("Formato de email invalido");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Contrasena es requerida")
            .MinimumLength(6).WithMessage("Contrasena debe tener al menos 6 caracteres");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Rol es requerido")
            .Must(r => r == "Admin" || r == "Member")
            .WithMessage("Rol debe ser Admin o Member");
    }
}
