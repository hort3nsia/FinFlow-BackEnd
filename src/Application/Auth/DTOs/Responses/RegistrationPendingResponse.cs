namespace FinFlow.Application.Auth.DTOs.Responses;

public record RegistrationPendingResponse(
    Guid AccountId,
    string Email,
    bool RequiresEmailVerification,
    int CooldownSeconds);
