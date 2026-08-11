using FluentValidation;
using Identity.Application.Commands;

namespace Identity.Application.Validators;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId es requerido");

        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Contrasena actual es requerida");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Nueva contrasena es requerida")
            .MinimumLength(6).WithMessage("Nueva contrasena debe tener al menos 6 caracteres");
    }
}
