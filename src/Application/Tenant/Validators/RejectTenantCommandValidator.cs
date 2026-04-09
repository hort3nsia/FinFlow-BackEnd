using FinFlow.Application.Tenant.Commands.RejectTenant;
using FluentValidation;

namespace FinFlow.Application.Tenant.Validators;

public sealed class RejectTenantCommandValidator : AbstractValidator<RejectTenantCommand>
{
    public RejectTenantCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty();
    }
}
