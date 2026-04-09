namespace FinFlow.Application.Tenant.DTOs.Requests;

public record RejectTenantRequest(Guid RequestId, string Reason);
