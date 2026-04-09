using FinFlow.Application.Tenant.Commands.ApproveTenant;
using FluentValidation;

namespace FinFlow.Application.Tenant.Validators;

public sealed class ApproveTenantCommandValidator : AbstractValidator<ApproveTenantCommand>
{
    public ApproveTenantCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
    }
}
