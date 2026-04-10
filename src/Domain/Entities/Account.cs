using FinFlow.Domain.Abstractions;
using FinFlow.Domain.Events;

namespace FinFlow.Domain.Entities;

public sealed class Account : Entity
{
    private Account(Guid id, string email, string passwordHash)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    private Account() { }

    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public bool IsActive { get; private set; }

    public static Result<Account> Create(string email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure<Account>(AccountErrors.EmailRequired);
        if (!System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            return Result.Failure<Account>(AccountErrors.InvalidEmailFormat);
        if (string.IsNullOrWhiteSpace(passwordHash))
            return Result.Failure<Account>(AccountErrors.PasswordRequired);

        var account = new Account(Guid.NewGuid(), email.ToLowerInvariant(), passwordHash);
        account.RaiseDomainEvent(new AccountCreatedDomainEvent(account.Id, account.Email));
        return account;
    }

    public Result ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            return Result.Failure(AccountErrors.PasswordRequired);

        PasswordHash = newPasswordHash;
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (!IsActive) return Result.Failure(AccountErrors.AlreadyDeactivated);
        IsActive = false;
        RaiseDomainEvent(new AccountDeactivatedDomainEvent(Id));
        return Result.Success();
    }

    public Result Activate()
    {
        if (IsActive) return Result.Failure(AccountErrors.AlreadyActive);
        IsActive = true;
        RaiseDomainEvent(new AccountActivatedDomainEvent(Id));
        return Result.Success();
    }
}
