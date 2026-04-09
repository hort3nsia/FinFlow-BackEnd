using FinFlow.Application.Tenant.DTOs.Responses;
using HotChocolate.Authorization;
using HotChocolate.Types;
using MediatR;
using FinFlow.Application.Tenant.Queries.GetPendingTenantRequests;

namespace FinFlow.Api.GraphQL.Auth;

public record PendingTenantApprovalPayload(
    Guid RequestId,
    string TenantCode,
    string Name,
    string CompanyName,
    string TaxCode,
    string? RequestedByEmail,
    int? EmployeeCount,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    string Status
);

[ExtendObjectType(typeof(global::Query))]
public sealed class AuthQueries
{
    [Authorize]
    public async Task<IReadOnlyList<PendingTenantApprovalPayload>> PendingTenantRequestsAsync(
        [Service] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetPendingTenantRequestsQuery(), cancellationToken);
        if (result.IsFailure)
            throw new GraphQLException(new HotChocolate.Error(result.Error.Description, result.Error.Code));

        return result.Value
            .Select(x => new PendingTenantApprovalPayload(
                x.RequestId,
                x.TenantCode,
                x.Name,
                x.CompanyName,
                x.TaxCode,
                x.RequestedByEmail,
                x.EmployeeCount,
                x.CreatedAt,
                x.ExpiresAt,
                x.Status.ToString()))
            .ToList();
    }
}
