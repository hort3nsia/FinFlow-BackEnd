using FinFlow.Application.Tenant.Commands.CreateIsolatedTenant;
using FluentValidation;

namespace FinFlow.Application.Tenant.Validators;

public sealed class CreateIsolatedTenantCommandValidator : AbstractValidator<CreateIsolatedTenantCommand>
{
    public CreateIsolatedTenantCommandValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.TenantCode).NotEmpty();
        RuleFor(x => x.Currency).NotEmpty();
        RuleFor(x => x.CompanyInfo).NotNull();

        When(x => x.CompanyInfo is not null, () =>
        {
            RuleFor(x => x.CompanyInfo.CompanyName).NotEmpty();
            RuleFor(x => x.CompanyInfo.TaxCode).NotEmpty();
        });
    }
}
