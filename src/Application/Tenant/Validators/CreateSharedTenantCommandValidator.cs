using FinFlow.Application.Tenant.Commands.CreateSharedTenant;
using FluentValidation;

namespace FinFlow.Application.Tenant.Validators;

public sealed class CreateSharedTenantCommandValidator : AbstractValidator<CreateSharedTenantCommand>
{
    public CreateSharedTenantCommandValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.TenantCode).NotEmpty();
        RuleFor(x => x.Currency).NotEmpty();
    }
}
