using FinFlow.Application.Tenant.Commands.CreateIsolatedTenant;
using FinFlow.Application.Tenant.DTOs.Requests;
using FluentValidation;

namespace FinFlow.Application.Tenant.Validators;

public sealed class CreateIsolatedTenantCommandValidator : AbstractValidator<CreateIsolatedTenantCommand>
{
    public CreateIsolatedTenantCommandValidator()
    {
        RuleFor(x => x.Request.AccountId).NotEmpty();
        RuleFor(x => x.Request.Name).NotEmpty();
        RuleFor(x => x.Request.TenantCode).NotEmpty();
        RuleFor(x => x.Request.Currency).NotEmpty();
        RuleFor(x => x.Request.CompanyInfo).NotNull();

        When(x => x.Request.CompanyInfo is not null, () =>
        {
            RuleFor(x => x.Request.CompanyInfo!.CompanyName).NotEmpty();
            RuleFor(x => x.Request.CompanyInfo!.TaxCode).NotEmpty();
        });
    }
}
