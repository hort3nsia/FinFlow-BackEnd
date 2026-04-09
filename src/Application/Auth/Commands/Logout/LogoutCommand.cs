using FinFlow.Application.Common;
using FinFlow.Domain.Abstractions;

namespace FinFlow.Application.Auth.Commands.Logout;

public record LogoutCommand(string RefreshToken) : ICommand<Result>;
