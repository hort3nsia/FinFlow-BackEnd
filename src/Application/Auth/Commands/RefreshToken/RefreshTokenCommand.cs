using FinFlow.Application.Auth.Responses;
using FinFlow.Application.Common;
using FinFlow.Domain.Abstractions;

namespace FinFlow.Application.Auth.Commands.RefreshToken;

public record RefreshTokenCommand(string RefreshToken) : ICommand<Result<AuthResponse>>;
