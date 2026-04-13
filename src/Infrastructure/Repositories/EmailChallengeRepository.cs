using FinFlow.Domain.EmailChallenges;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Infrastructure.Repositories;

internal sealed class EmailChallengeRepository : IEmailChallengeRepository
{
    private readonly ApplicationDbContext _dbContext;

    public EmailChallengeRepository(ApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<EmailChallenge?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.Set<EmailChallenge>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<EmailChallenge?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbContext.Set<EmailChallenge>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<EmailChallenge?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        await _dbContext.Set<EmailChallenge>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public async Task<EmailChallenge?> GetByTokenHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        await _dbContext.Set<EmailChallenge>()
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public async Task<EmailChallenge?> GetLatestByAccountIdAndPurposeAsync(Guid accountId, EmailChallengePurpose purpose, CancellationToken cancellationToken = default) =>
        await _dbContext.Set<EmailChallenge>()
            .AsNoTracking()
            .Where(x => x.AccountId == accountId && x.Purpose == purpose)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<EmailChallenge?> GetLatestByAccountIdAndPurposeForUpdateAsync(Guid accountId, EmailChallengePurpose purpose, CancellationToken cancellationToken = default) =>
        await _dbContext.Set<EmailChallenge>()
            .Where(x => x.AccountId == accountId && x.Purpose == purpose)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(EmailChallenge emailChallenge) => _dbContext.Set<EmailChallenge>().Add(emailChallenge);

    public void Update(EmailChallenge emailChallenge) => _dbContext.Set<EmailChallenge>().Update(emailChallenge);
}
