namespace FinFlow.Application.Auth.DTOs.Responses;

public record ChallengeDispatchResponse(
    bool Accepted,
    int CooldownSeconds);
