using FinFlow.Application.Auth.DTOs.Requests;
using FinFlow.Application.Auth.DTOs.Responses;
using FinFlow.Application.Common;
using FinFlow.Domain.Abstractions;

namespace FinFlow.Application.Auth.Commands.ResendEmailVerification;

public record ResendEmailVerificationCommand(ResendEmailVerificationRequest Request)
    : ICommand<Result<ChallengeDispatchResponse>>;
