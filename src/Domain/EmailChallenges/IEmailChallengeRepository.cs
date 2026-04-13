using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;

namespace FinFlow.Domain.EmailChallenges;

public interface IEmailChallengeRepository
{
    Task<EmailChallenge?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmailChallenge?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmailChallenge?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<EmailChallenge?> GetByTokenHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<EmailChallenge?> GetLatestByAccountIdAndPurposeAsync(Guid accountId, EmailChallengePurpose purpose, CancellationToken cancellationToken = default);
    Task<EmailChallenge?> GetLatestByAccountIdAndPurposeForUpdateAsync(Guid accountId, EmailChallengePurpose purpose, CancellationToken cancellationToken = default);

    void Add(EmailChallenge emailChallenge);
    void Update(EmailChallenge emailChallenge);
}
