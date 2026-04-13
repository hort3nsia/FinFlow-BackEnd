using FinFlow.Application.Auth.DTOs.Requests;
using FinFlow.Application.Common;
using FinFlow.Domain.Abstractions;

namespace FinFlow.Application.Auth.Commands.VerifyEmailByToken;

public record VerifyEmailByTokenCommand(VerifyEmailByTokenRequest Request)
    : ICommand<Result>;
