namespace FinFlow.Application.Tenant.Commands.CreateIsolatedTenant;

public record CompanyInfoModel(
    string CompanyName,
    string TaxCode,
    string? Address = null,
    string? Phone = null,
    string? ContactPerson = null,
    string? BusinessType = null,
    int? EmployeeCount = null
);
