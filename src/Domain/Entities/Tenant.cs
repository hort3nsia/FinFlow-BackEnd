using FinFlow.Domain.Abstractions;
using FinFlow.Domain.Enums;
using FinFlow.Domain.Events;

namespace FinFlow.Domain.Entities;

public sealed class Tenant : Entity
{
    private Tenant(Guid id, string name, string tenantCode, TenancyModel tenancyModel, string currency)
    {
        Id = id;
        Name = name;
        TenantCode = tenantCode;
        TenancyModel = tenancyModel;
        Currency = currency;
        CreatedAt = DateTime.UtcNow;
    }

    private Tenant() { }

    public string Name { get; private set; } = null!;
    public string TenantCode { get; private set; } = null!;
    public TenancyModel TenancyModel { get; private set; }
    public string? ConnectionString { get; private set; }
    public string Currency { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static Result<Tenant> Create(string name, string tenantCode, TenancyModel tenancyModel = TenancyModel.Shared, string currency = "VND")
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Tenant>(TenantErrors.NameRequired);
        if (name.Length > 150)
            return Result.Failure<Tenant>(TenantErrors.NameTooLong);
        if (string.IsNullOrWhiteSpace(tenantCode))
            return Result.Failure<Tenant>(TenantErrors.CodeRequired);
        if (!System.Text.RegularExpressions.Regex.IsMatch(tenantCode, @"^[a-z0-9-]{3,50}$"))
            return Result.Failure<Tenant>(TenantErrors.InvalidCodeFormat);

        var tenant = new Tenant(Guid.NewGuid(), name, tenantCode, tenancyModel, currency);
        tenant.RaiseDomainEvent(new TenantCreatedDomainEvent(tenant.Id, tenant.TenantCode, tenant.Name));
        return tenant;
    }

    public Result ChangeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 150)
            return Result.Failure(TenantErrors.NameRequired);
        Name = name;
        return Result.Success();
    }

    public Result ChangeCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            return Result.Failure(TenantErrors.InvalidCurrency);
        Currency = currency.ToUpperInvariant();
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (!IsActive) return Result.Failure(TenantErrors.AlreadyDeactivated);
        IsActive = false;
        RaiseDomainEvent(new TenantDeactivatedDomainEvent(Id));
        return Result.Success();
    }

    public Result Activate()
    {
        if (IsActive) return Result.Failure(TenantErrors.AlreadyActive);
        IsActive = true;
        RaiseDomainEvent(new TenantActivatedDomainEvent(Id));
        return Result.Success();
    }
}