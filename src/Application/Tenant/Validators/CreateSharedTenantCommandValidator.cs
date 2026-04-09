using FinFlow.Application.Tenant.Commands.CreateSharedTenant;
using FinFlow.Application.Tenant.DTOs.Requests;
using FluentValidation;

namespace FinFlow.Application.Tenant.Validators;

public sealed class CreateSharedTenantCommandValidator : AbstractValidator<CreateSharedTenantCommand>
{
    public CreateSharedTenantCommandValidator()
    {
        RuleFor(x => x.Request.AccountId).NotEmpty();
        RuleFor(x => x.Request.Name).NotEmpty();
        RuleFor(x => x.Request.TenantCode).NotEmpty();
        RuleFor(x => x.Request.Currency).NotEmpty();
    }
}
