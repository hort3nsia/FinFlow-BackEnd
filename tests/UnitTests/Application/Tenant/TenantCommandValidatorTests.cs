using FinFlow.Application.Tenant.Commands.ApproveTenant;
using FinFlow.Application.Tenant.Commands.CreateIsolatedTenant;
using FinFlow.Application.Tenant.Commands.CreateSharedTenant;
using FinFlow.Application.Tenant.Commands.RejectTenant;
using FinFlow.Application.Tenant.DTOs.Requests;
using FinFlow.Application.Tenant.Validators;

namespace FinFlow.UnitTests.Application.Tenant;

public sealed class TenantCommandValidatorTests
{
    [Fact]
    public void CreateSharedTenantCommandValidator_ReturnsErrors_ForMissingFields()
    {
        var validator = new CreateSharedTenantCommandValidator();
        var command = new CreateSharedTenantCommand(new CreateSharedTenantRequest(Guid.Empty, null, "", "", ""));

        var result = validator.Validate(command);

        Assert.Contains(result.Errors, x => x.PropertyName == "Request.AccountId");
        Assert.Contains(result.Errors, x => x.PropertyName == "Request.Name");
        Assert.Contains(result.Errors, x => x.PropertyName == "Request.TenantCode");
        Assert.Contains(result.Errors, x => x.PropertyName == "Request.Currency");
    }

    [Fact]
    public void CreateIsolatedTenantCommandValidator_ReturnsErrors_ForMissingCompanyInfo()
    {
        var validator = new CreateIsolatedTenantCommandValidator();
        var command = new CreateIsolatedTenantCommand(new CreateIsolatedTenantRequest(Guid.Empty, null, "", "", "", null));

        var result = validator.Validate(command);

        Assert.Contains(result.Errors, x => x.PropertyName == "Request.AccountId");
        Assert.Contains(result.Errors, x => x.PropertyName == "Request.Name");
        Assert.Contains(result.Errors, x => x.PropertyName == "Request.TenantCode");
        Assert.Contains(result.Errors, x => x.PropertyName == "Request.Currency");
        Assert.Contains(result.Errors, x => x.PropertyName == "Request.CompanyInfo");
    }

    [Fact]
    public void CreateIsolatedTenantCommandValidator_ReturnsErrors_ForBlankCompanyInfoFields()
    {
        var validator = new CreateIsolatedTenantCommandValidator();
        var command = new CreateIsolatedTenantCommand(
            new CreateIsolatedTenantRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Tenant A",
                "tenant-a",
                "VND",
                new CompanyInfoRequest("", "")));

        var result = validator.Validate(command);

        Assert.Contains(result.Errors, x => x.PropertyName == "Request.CompanyInfo.CompanyName");
        Assert.Contains(result.Errors, x => x.PropertyName == "Request.CompanyInfo.TaxCode");
    }

    [Fact]
    public void ApproveAndRejectTenantValidators_ReturnErrors_ForInvalidInputs()
    {
        var approveValidator = new ApproveTenantCommandValidator();
        var rejectValidator = new RejectTenantCommandValidator();

        var approveResult = approveValidator.Validate(new ApproveTenantCommand(new ApproveTenantRequest(Guid.Empty)));
        var rejectResult = rejectValidator.Validate(new RejectTenantCommand(new RejectTenantRequest(Guid.Empty, "")));

        Assert.Contains(approveResult.Errors, x => x.PropertyName == "Request.RequestId");
        Assert.Contains(rejectResult.Errors, x => x.PropertyName == "Request.RequestId");
        Assert.Contains(rejectResult.Errors, x => x.PropertyName == "Request.Reason");
    }
}
