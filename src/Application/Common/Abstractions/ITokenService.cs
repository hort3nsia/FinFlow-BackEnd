namespace FinFlow.Application.Common.Abstractions;

public interface ITokenService
{
    int RefreshTokenExpirationDays { get; }
    string GenerateAccountAccessToken(Guid id, string email);
    string GenerateAccessToken(Guid id, string email, string role, Guid idTenant, Guid membershipId);
    string GenerateRefreshToken();
}
