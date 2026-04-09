namespace FinFlow.Application.Tenant.DTOs.Requests;

public record CompanyInfoRequest(
    string CompanyName,
    string TaxCode,
    string? Address = null,
    string? Phone = null,
    string? ContactPerson = null,
    string? BusinessType = null,
    int? EmployeeCount = null);
