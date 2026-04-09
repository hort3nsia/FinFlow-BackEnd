using FinFlow.Application.Auth.Commands.Register;
using FluentValidation;

namespace FinFlow.Application.Auth.Validators;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.TenantCode).NotEmpty();
        RuleFor(x => x.DepartmentName).NotEmpty();
    }
}
