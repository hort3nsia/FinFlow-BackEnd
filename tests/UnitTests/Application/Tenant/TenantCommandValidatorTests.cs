using FinFlow.Application.Tenant.Commands.ApproveTenant;
using FinFlow.Application.Tenant.Commands.CreateIsolatedTenant;
using FinFlow.Application.Tenant.Commands.CreateSharedTenant;
using FinFlow.Application.Tenant.Commands.RejectTenant;
using FinFlow.Application.Tenant.Validators;

namespace FinFlow.UnitTests.Application.Tenant;

public sealed class TenantCommandValidatorTests
{
    [Fact]
    public void CreateSharedTenantCommandValidator_ReturnsErrors_ForMissingFields()
    {
        var validator = new CreateSharedTenantCommandValidator();
        var command = new CreateSharedTenantCommand(Guid.Empty, null, "", "", "");

        var result = validator.Validate(command);

        Assert.Contains(result.Errors, x => x.PropertyName == nameof(CreateSharedTenantCommand.AccountId));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(CreateSharedTenantCommand.Name));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(CreateSharedTenantCommand.TenantCode));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(CreateSharedTenantCommand.Currency));
    }

    [Fact]
    public void CreateIsolatedTenantCommandValidator_ReturnsErrors_ForMissingCompanyInfo()
    {
        var validator = new CreateIsolatedTenantCommandValidator();
        var command = new CreateIsolatedTenantCommand(Guid.Empty, null, "", "", "", null!);

        var result = validator.Validate(command);

        Assert.Contains(result.Errors, x => x.PropertyName == nameof(CreateIsolatedTenantCommand.AccountId));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(CreateIsolatedTenantCommand.Name));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(CreateIsolatedTenantCommand.TenantCode));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(CreateIsolatedTenantCommand.Currency));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(CreateIsolatedTenantCommand.CompanyInfo));
    }

    [Fact]
    public void CreateIsolatedTenantCommandValidator_ReturnsErrors_ForBlankCompanyInfoFields()
    {
        var validator = new CreateIsolatedTenantCommandValidator();
        var command = new CreateIsolatedTenantCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Tenant A",
            "tenant-a",
            "VND",
            new CompanyInfoModel("", ""));

        var result = validator.Validate(command);

        Assert.Contains(result.Errors, x => x.PropertyName == "CompanyInfo.CompanyName");
        Assert.Contains(result.Errors, x => x.PropertyName == "CompanyInfo.TaxCode");
    }

    [Fact]
    public void ApproveAndRejectTenantValidators_ReturnErrors_ForInvalidInputs()
    {
        var approveValidator = new ApproveTenantCommandValidator();
        var rejectValidator = new RejectTenantCommandValidator();

        var approveResult = approveValidator.Validate(new ApproveTenantCommand(Guid.Empty));
        var rejectResult = rejectValidator.Validate(new RejectTenantCommand(Guid.Empty, ""));

        Assert.Contains(approveResult.Errors, x => x.PropertyName == nameof(ApproveTenantCommand.RequestId));
        Assert.Contains(rejectResult.Errors, x => x.PropertyName == nameof(RejectTenantCommand.RequestId));
        Assert.Contains(rejectResult.Errors, x => x.PropertyName == nameof(RejectTenantCommand.Reason));
    }
}
